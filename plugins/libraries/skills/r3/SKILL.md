---
name: r3
description: Writes and reviews reactive code in Unity with Cysharp R3 (NuGet R3 + UPM com.cysharp.r3), the successor to UniRx. Covers install through NuGetForUnity plus the R3.Unity git URL, Observable / Subject / BehaviorSubject / ReplaySubject / ReactiveProperty / ReadOnlyReactiveProperty / SerializableReactiveProperty, Subscribe with OnErrorResume and OnCompleted(Result), subscription disposal (AddTo(this), AddTo(ref DisposableBuilder), DisposableBag, CompositeDisposable, Disposable.Combine, RegisterTo(destroyCancellationToken)), UnityFrameProvider / UnityTimeProvider, EveryUpdate / EveryValueChanged / Interval / Timer, operators (ThrottleFirst, Debounce, Chunk, CombineLatest, Merge, Switch), SubscribeAwait with AwaitOperation, FirstAsync / LastAsync, uGUI OnClickAsObservable / SubscribeToText, MonoBehaviour triggers, Observable Tracker, and UniRx migration. Use when the user mentions R3, UniRx, Rx, Observable<T>, ReactiveProperty, Subject, AddTo, subscription leaks, or reactive UI binding in Unity.
license: MIT
metadata:
  category: libraries
  sources: "https://github.com/Cysharp/R3"
  unity: "6000.0+"
---

# R3 (Reactive Extensions for Unity)

Verified against R3 core (`src/R3`) and R3.Unity 1.3.1 (`src/R3.Unity/Assets/R3.Unity`). R3 needs Unity 2021.3 or newer, so Unity 6 is fine.

Reference files, read when needed:

- `references/operators.md` - read when choosing a factory or operator, or checking an exact overload (time, frame and async variants).
- `references/unirx-migration.md` - read when porting UniRx code or explaining how R3 differs from System.Reactive.

## Install

R3 ships in two parts: the core is a NuGet DLL, and the Unity layer is a UPM package.

1. Install NuGetForUnity, for example from the git URL `https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity` or from OpenUPM.
2. Open **NuGet > Manage NuGet Packages**, search for `R3`, and install it. This also brings in `Microsoft.Bcl.TimeProvider` and `Microsoft.Bcl.AsyncInterfaces`.
3. If Unity reports assembly version conflicts, clear **Project Settings > Player > Other Settings > Assembly Version Validation**.
4. In Package Manager, choose **Add package from git URL**: `https://github.com/Cysharp/R3.git?path=src/R3.Unity/Assets/R3.Unity`. Pin a release with `#1.3.1`. Tags use plain `x.y.z`.
5. The `R3.Unity` asmdef is auto-referenced and precompiles against `R3.dll`. Add `using R3;`, plus `using R3.Triggers;` for MonoBehaviour message observables.

What gets enabled is controlled by asmdef versionDefines. uGUI helpers need `com.unity.ugui` (`R3_UGUI_SUPPORT`). Physics triggers need the physics modules. TextMeshPro helpers (`SubscribeToText(TMP_Text)`, `TMP_InputField` and `TMP_Dropdown` observables) live under `Runtime/External/TextMeshPro`.

At `AfterAssembliesLoaded`, `UnityProviderInitializer` sets `ObservableSystem.DefaultTimeProvider = UnityTimeProvider.Update` and `ObservableSystem.DefaultFrameProvider = UnityFrameProvider.Update`. It also routes unhandled errors to `Debug.LogException`.

## Core model

```csharp
public abstract class Observable<T> { IDisposable Subscribe(Observer<T> observer); }
public abstract class Observer<T> : IDisposable
{
    void OnNext(T value);
    void OnErrorResume(Exception error);
    void OnCompleted(Result result);
}
```

- `Observable<T>` is an abstract class, not `IObservable<T>`. Convert with `ToObservable()` and `AsSystemObservable()`.
- **Errors do not end the stream.** An exception inside an operator goes to `OnErrorResume`, and the subscription stays alive. Errors that reach a `Subscribe` with no `onErrorResume` go to `ObservableSystem.GetUnhandledExceptionHandler()`. To change that handler, call `ObservableSystem.RegisterUnhandledExceptionHandler(...)`.
- **Completion carries the error.** `OnCompleted(Result result)` replaces `OnError` and `OnCompleted`. Check `result.IsSuccess` or `result.IsFailure`, and read `result.Exception`. Build results with `Result.Success` or `Result.Failure(ex)`.
- To get Rx-style "stop on first error", add `.OnErrorResumeAsFailure()`. `Catch` handles only failure completions, not `OnErrorResume`, so it needs this conversion first.

Subscribe overloads:

```csharp
source.Subscribe(x => ...);
source.Subscribe(x => ..., result => ...);
source.Subscribe(x => ..., ex => ..., result => ...);
source.Subscribe(state, static (x, s) => ...);
```

## Subjects and properties

| Type | Behaviour |
|---|---|
| `Subject<T>` | Plain event. `OnNext` is **not** thread-safe. |
| `BehaviorSubject<T>(initial)` | Replays the latest value. Thread-safe. |
| `ReplaySubject<T>(bufferSize / window / timeProvider)` | Replays a buffer. Thread-safe. |
| `ReplayFrameSubject<T>` | Replay window measured in frames. |
| `ReactiveProperty<T>(value[, equalityComparer])` | Holds a value and replays it on subscribe. Skips the notification when the new value equals the current one. Not thread-safe. |
| `SynchronizedReactiveProperty<T>` | Thread-safe `ReactiveProperty`. |
| `ReadOnlyReactiveProperty<T>` | Read-only view with `CurrentValue`. Expose it from a private `ReactiveProperty`, or build one with `source.ToReadOnlyReactiveProperty(initialValue)`. |
| `SerializableReactiveProperty<T>` (R3.Unity) | Use instead of `ReactiveProperty` in `[SerializeField]` / public fields. Inspector edits call `ForceNotify()`. |
| `ReactiveCommand<T>` / `ReactiveCommand` | Command with an `Observable<bool>` can-execute source. |

```csharp
public sealed class PlayerModel : IDisposable
{
    private readonly ReactiveProperty<int> _hp = new(100);
    public ReadOnlyReactiveProperty<int> Hp => _hp;
    public ReadOnlyReactiveProperty<bool> IsDead { get; }

    public PlayerModel() => IsDead = _hp.Select(x => x <= 0).ToReadOnlyReactiveProperty();

    public void Damage(int amount) => _hp.Value -= amount;

    public void Dispose() => Disposable.Dispose(_hp, IsDead);
}
```

- Use `Value` to get or set on a mutable property. A read-only property exposes `CurrentValue` instead.
- Setting `Value` to an equal value does nothing. To notify anyway, call `ForceNotify()` or `OnNext(value)`, for example after mutating a reference type in place.
- To customise equality, pass an `IEqualityComparer<T>` to the constructor. Passing `null` disables the check.
- To clamp or validate, override `OnValueChanging(ref T value)`.
- Disposing any subject or property sends `OnCompleted` to every subscriber, which unsubscribes them. `Dispose(false)` skips that.

## Disposal (most important rule)

Every `Subscribe` returns an `IDisposable`. Tie each one to a lifetime, or it leaks.

| Pattern | When |
|---|---|
| `.AddTo(this)` (`Component` or `GameObject`) | Simplest option in MonoBehaviours. Adds an `ObservableDestroyTrigger`, and also works for objects that were never activated. |
| `var d = Disposable.CreateBuilder(); ... .AddTo(ref d); d.RegisterTo(destroyCancellationToken);` | Fastest option for a fixed set built in `Awake`/`Start`. `RegisterTo` builds and registers in one call. |
| `Disposable.Combine(d1, d2, d3)` | Known count. Up to 8 are stored as fields. |
| `DisposableBag` field + `.AddTo(ref _bag)` | Subscriptions added dynamically. It is a struct, so never copy it. Not thread-safe. |
| `CompositeDisposable` + `.AddTo(_composite)` | You need `Remove` or thread safety. This is the slowest option. |
| `subscription.RegisterTo(cancellationToken)` | Dispose when a token fires, such as `destroyCancellationToken`. |
| Factory `cancellationToken` arguments (`EveryUpdate(ct)`, `Interval(period, ct)`, `UnityEvent.AsObservable(ct)`) | The source sends `OnCompleted` when the token is cancelled, which ends the whole chain. |

`Disposable.Create(action)`, `SerialDisposable`, and `SingleAssignmentDisposable` are also available.

## Unity providers

- `UnityFrameProvider` timings: `Initialization`, `EarlyUpdate`, `FixedUpdate`, `PreUpdate`, `Update`, `PreLateUpdate`, `PostLateUpdate`, `TimeUpdate`, `PostFixedUpdate`.
- `UnityTimeProvider` has the same timings, each in three forms: scaled (`Update`), `...IgnoreTimeScale` (`UpdateIgnoreTimeScale`), and `...Realtime` (`UpdateRealtime`).

```csharp
Observable.EveryUpdate(UnityFrameProvider.FixedUpdate, destroyCancellationToken).Subscribe(_ => Step());
Observable.Interval(TimeSpan.FromSeconds(1), UnityTimeProvider.UpdateIgnoreTimeScale).Subscribe(_ => Tick()).AddTo(this);
Observable.EveryValueChanged(transform, t => t.position, destroyCancellationToken).Subscribe(OnMoved);
Observable.Timer(TimeSpan.FromSeconds(3)).Subscribe(_ => Explode()).AddTo(this);
```

Time-based operators that take no provider use `ObservableSystem.DefaultTimeProvider`, which defaults to scaled `Update`. They stall while `Time.timeScale = 0`, so pass an `IgnoreTimeScale` provider for pause menus. Frame variants (`IntervalFrame`, `DelayFrame`, `DebounceFrame`, `ThrottleFirstFrame`, `ChunkFrame`, `TakeFrame`) use `DefaultFrameProvider`.

`ObserveOnMainThread()` and `SubscribeOnMainThread()` move work back onto the Unity main thread. `ObserveOn(UnityFrameProvider.PostLateUpdate)` picks a specific timing.

## uGUI, UnityEvent, triggers

```csharp
button.OnClickAsObservable().ThrottleFirst(TimeSpan.FromSeconds(0.5)).Subscribe(_ => Fire()).AddTo(this);
model.Hp.SubscribeToText(hpLabel).AddTo(this);
model.Hp.Select(x => x > 0).SubscribeToInteractable(attackButton).AddTo(this);
slider.OnValueChangedAsObservable().Subscribe(v => volume.Value = v).AddTo(this);
myUnityEvent.AsObservable(destroyCancellationToken).Subscribe(_ => ...);
this.OnTriggerEnterAsObservable().Where(c => c.CompareTag("Player")).Subscribe(_ => Pickup()).AddTo(this);
```

The `OnValueChangedAsObservable` methods emit the current value on subscribe and complete when the component is destroyed. Triggers (`UpdateAsObservable`, `FixedUpdateAsObservable`, `OnCollisionEnterAsObservable`, `OnEnableAsObservable`, `OnDestroyAsObservable`, `OnBecameVisibleAsObservable`, ...) add hidden trigger components. Prefer `Observable.EveryUpdate` over `UpdateAsObservable` when you don't need a per-object update.

## Async integration

- `SubscribeAwait`, `SelectAwait`, and `WhereAwait` take `Func<T, CancellationToken, ValueTask>`. Choose an `AwaitOperation` for values that arrive while the previous call is still running:
  - `Sequential` (default): queue them.
  - `Drop`: ignore them, which blocks double-clicks.
  - `Switch`: cancel the previous call.
  - `Parallel` and `SequentialParallel`: run concurrently, limited by `maxConcurrent`.
  - `ThrottleFirstLast`: run the first and the last.
- `cancelOnCompleted` controls whether the running call is cancelled when the source completes.
- Single-value results are `Task`-returning methods: `FirstAsync`, `LastAsync`, `FirstOrDefaultAsync`, `ToArrayAsync`, `WaitAsync`, `ForEachAsync`, `CountAsync`, and others. There is no `ToTask()`. UniRx's `ToTask()` and `ToUniTask()` map to `FirstAsync()` and `LastAsync()`.
- `OnErrorResume` errors fault these tasks the same way failure completions do.
- `Observable.FromAsync(ct => ...)` wraps one async call. `ToAsyncEnumerable()` lets you use `await foreach`.
- UniTask interop (see `unitask`): R3 does not depend on UniTask. A `ValueTask` lambda can `await` UniTask APIs, for example `SubscribeAwait(async (x, ct) => await UniTask.Delay(500, cancellationToken: ct), AwaitOperation.Drop)`. To convert the other way, call `.AsUniTask()` on the `Task` returned by `FirstAsync`.

```csharp
button.OnClickAsObservable()
    .SubscribeAwait(async (_, ct) =>
    {
        using var req = UnityWebRequest.Get(url);
        await req.SendWebRequest().WithCancellation(ct);
        label.text = req.downloadHandler.text;
    }, AwaitOperation.Drop)
    .AddTo(this);
```

## Leak detection

Open **Window > Observable Tracker**. Turn on **Enable Tracking** (low cost) and **Enable StackTrace** (high cost), run the scenario, and confirm that subscriptions disappear after their owners are destroyed. Turn both off when you are done. In code, set `ObservableTracker.EnableTracking` and `ObservableTracker.EnableStackTrace`, then iterate with `ObservableTracker.ForEachActiveTask(state => ...)`.

## Pitfalls

- **Missing `AddTo`.** A chain built from `EveryUpdate`, `Interval`, or a long-lived subject outlives the MonoBehaviour and keeps calling into a destroyed object, which throws `MissingReferenceException`. Every subscription needs an owner.
- **Subscribing in `OnEnable` with `AddTo(this)`.** These subscriptions pile up on each enable. Dispose them in `OnDisable` with a field `DisposableBag` and `_bag.Clear()`, which disposes the items and keeps the bag usable. Don't use `Dispose()` here: after it, every later `Add` disposes the new item immediately.
- **Assuming one error kills the stream.** One bad element no longer unsubscribes in R3. Retry-style logic belongs inside a `SelectAwait` body, because R3 has no `Retry` operator.
- **Expecting an equal value to re-fire.** Setting a `ReactiveProperty` to an equal value is silent. This often bites when you mutate a list or class in place. Call `ForceNotify()`.
- **Thread safety.** Operators expect `OnNext` to come from a single thread. Use `Synchronize()` on multi-thread sources. `Subject<T>.OnNext` and `ReactiveProperty` are not thread-safe. Use `SynchronizedReactiveProperty`, or a `lock` around `Subject.OnNext`. Unity APIs must run on the main thread, so add `ObserveOnMainThread()` after thread-pool work.
- **Sources that never complete.** `ReactiveProperty` and `Subject` don't complete until disposed. `FirstAsync` and `LastAsync` on them can wait forever unless you pass a `CancellationToken`.
- **WebGL.** The default Unity providers run on the PlayerLoop and work on WebGL. Avoid `ObserveOnThreadPool` and `SubscribeOnThreadPool` there.
- **Inspector edits.** `SerializableReactiveProperty` only notifies from the inspector through its property drawer. Setting the serialized `value` field from code bypasses notification, so always set `.Value`.

## Related skills

- `unitask` for async work inside `SubscribeAwait` and for awaiting results.
- `ui-ugui` for the Canvas and components being bound.
- `csharp-unity` for general coding standards.
