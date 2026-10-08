# Migrating from UniRx to R3

The rename list follows the upstream README section "Class/Method name changes from dotnet/reactive and neuecc/UniRx". Signatures were checked against source.

## Packages and namespaces

| UniRx | R3 |
|---|---|
| `using UniRx;` | `using R3;` |
| `using UniRx.Triggers;` | `using R3.Triggers;` |
| UniRx asset or UPM package | NuGet `R3` (through NuGetForUnity) plus UPM `com.cysharp.r3` (`R3.Unity`) |
| `IObservable<T>` / `IObserver<T>` | `Observable<T>` / `Observer<T>` (abstract classes); bridge with `ToObservable()` / `AsSystemObservable()` |
| `IScheduler`, `Scheduler.MainThreadIgnoreTimeScale` | `TimeProvider`, e.g. `UnityTimeProvider.UpdateIgnoreTimeScale` |
| `Scheduler.MainThreadFixedUpdate` and similar | `UnityFrameProvider.FixedUpdate` / `UnityTimeProvider.FixedUpdate` |

## Error model change

| UniRx / Rx | R3 |
|---|---|
| `OnError(ex)` ends the subscription | `OnErrorResume(ex)`: the subscription keeps running |
| `OnCompleted()` | `OnCompleted(Result result)`, where `result.IsFailure` / `result.Exception` describe failure |
| `Subscribe(onNext, onError, onCompleted)` | `Subscribe(onNext, onErrorResume, onCompleted: Action<Result>)` |
| Unhandled `OnError` rethrows | goes to `ObservableSystem` unhandled handler (`Debug.LogException` in Unity) |
| `Retry()` | none; retry inside `SelectAwait` / `SubscribeAwait` |
| `Catch` catches `OnError` | `Catch` only sees failure completions; insert `.OnErrorResumeAsFailure()` first to recover old semantics |
| `Finally(action)` | `Do(onDispose: action)` |

If the code relied on an error stopping a stream, add `.OnErrorResumeAsFailure()` right after the source.

## Renamed operators

| UniRx | R3 |
|---|---|
| `Amb` | `Race` |
| `Buffer` | `Chunk` |
| `BatchFrame` | `ChunkFrame` |
| `Throttle` | `Debounce` |
| `ThrottleFrame` | `DebounceFrame` |
| `Sample` | `ThrottleLast` |
| `SampleFrame` | `ThrottleLastFrame` |
| `StartWith` | `Prepend` |
| `x.ObserveEveryValueChanged(f)` | `Observable.EveryValueChanged(x, f)` |
| `Distinct(selector)` | `DistinctBy` |
| `DistinctUntilChanged(selector)` | `DistinctUntilChangedBy` |
| `DoOnCompleted`, `DoOnError`, `DoOnSubscribe`, `DoOnCancel` | `Do(onCompleted:, onErrorResume:, onSubscribe:, onDispose:)` |
| `First()`, `Last()`, `Single()` (observable) | `FirstAsync()`, `LastAsync()`, `SingleAsync()` returning `Task<T>`, or `Take(1)` / `TakeLast(1)` to stay observable |
| `ToTask()`, `ToUniTask()` | `FirstAsync()` / `LastAsync()` (then `.AsUniTask()` if needed) |
| `Observable.EveryUpdate()` | same name; optional `FrameProvider` and `CancellationToken` |
| `Observable.Timer`, `Interval`, `TimerFrame`, `IntervalFrame` | same names; `TimeProvider` / `FrameProvider` replace schedulers |
| `ObserveOnMainThread()` | same name (R3.Unity) |

## Subjects, properties, disposables

| UniRx | R3 |
|---|---|
| `AsyncSubject<T>` | `TaskCompletionSource<T>` (or UniTask's `UniTaskCompletionSource<T>`) |
| `IReadOnlyReactiveProperty<T>.Value` | `ReadOnlyReactiveProperty<T>.CurrentValue` |
| `ReactiveProperty.SkipLatestValueOnSubscribe()` | `.Skip(1)` |
| `ToReactiveProperty()` / `ToReadOnlyReactiveProperty()` | `ToReadOnlyReactiveProperty(initialValue)` |
| `ReactiveProperty` field in inspector, `IntReactiveProperty` etc. | `SerializableReactiveProperty<T>` |
| `ReactiveProperty.SetValueAndForceNotify(v)` | `rp.OnNext(v)` or set then `rp.ForceNotify()` |
| `ReactiveCollection` / `ReactiveDictionary` | `ObservableCollections.R3` (separate package) |
| `ReactiveCommand` | `ReactiveCommand<T>` / `ReactiveCommand` (constructed from `Observable<bool>` can-execute, or `ToReactiveCommand`) |
| `MessageBroker` / `AsyncMessageBroker` | removed; use MessagePipe or your own `Subject<T>` hub |
| `StableCompositeDisposable` | `Disposable.Combine(...)` |
| `CompositeDisposable` | still exists (`CompositeDisposable`); prefer `DisposableBag` / `Disposable.CreateBuilder()` for speed |
| `AddTo(this)` / `AddTo(gameObject)` | same, R3.Unity `MonoBehaviourExtensions.AddTo` |
| `AddTo(compositeDisposable)` | `AddTo(ICollection<IDisposable>)` (works with `CompositeDisposable`) |

## Unity-specific removals

| UniRx | R3 replacement |
|---|---|
| `Observable.FromCoroutine`, `ToYieldInstruction`, `MainThreadDispatcher.StartCoroutine` | async/await with UniTask or Unity 6 `Awaitable` |
| `MainThreadDispatcher.OnApplicationQuitAsObservable()` | `Application.exitCancellationToken` |
| `ObservableWWW` | `UnityWebRequest` + `SelectAwait` / `FromAsync` |
| `ObjectPool` (UniRx.Toolkit) | write your own (UniTask can help) or use `UnityEngine.Pool` |
| UniRx Logger | any logging library |
| `Subject` disposal did not complete subscribers | disposing any R3 subject or property sends `OnCompleted` (use `Dispose(false)` to suppress) |

## Behaviour differences to re-test

- `ReactiveProperty` deduplicates with `EqualityComparer<T>.Default`; same as UniRx, but `ForceNotify()` replaces `SetValueAndForceNotify`.
- `OnValueChangedAsObservable()` emits the current value immediately on subscribe and completes when the component is destroyed.
- `ThrottleFirst` / `ThrottleLast` timers are idle until the first value arrives (UniRx `Sample` ran continuously).
- `Subject<T>.OnNext` is not thread-safe in R3; add a lock or use `Synchronize`.
- Default time provider in Unity is scaled-time `Update`; schedulers that ignored time scale must be replaced with an `...IgnoreTimeScale` or `...Realtime` provider.
