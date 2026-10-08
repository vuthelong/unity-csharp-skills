---
name: unitask
description: Writes and reviews allocation-free async/await code in Unity with Cysharp UniTask (com.cysharp.unitask, namespace Cysharp.Threading.Tasks). Covers install via UPM git URL or OpenUPM, UniTask / UniTask<T> / UniTaskVoid and Forget(), PlayerLoopTiming, Delay / DelayFrame / Yield / NextFrame / WaitUntil / WaitWhile / WaitUntilValueChanged, cancellation (GetCancellationTokenOnDestroy, destroyCancellationToken, linked tokens, SuppressCancellationThrow, AttachExternalCancellation, CancelAfterSlim, TimeoutController), WhenAll / WhenAny / WhenEach, awaiting UnityWebRequest, Addressables and DOTween, async LINQ (UniTaskAsyncEnumerable), AsyncReactiveProperty, Channel, async triggers, UniTaskCompletionSource, thread switching, UniTaskTracker, Preserve(), and UniTask vs Unity 6 Awaitable. Use when the user writes async code in Unity, replaces coroutines, mentions UniTask, UniTaskVoid, Forget, CancellationToken in MonoBehaviours, or hits "UniTask awaited twice", leaked tasks, or unobserved exceptions.
license: MIT
metadata:
  category: libraries
  sources: "https://github.com/Cysharp/UniTask"
  unity: "6000.0+"
---

# UniTask

Struct-based, pooled async/await for Unity that runs on the PlayerLoop (no threads unless you ask). Verified against UniTask 2.5.11 (`src/UniTask/Assets/Plugins/UniTask`).

Reference files, read when needed:

- `references/api-tables.md` - read when you need exact signatures: factory methods, awaiting Unity types, combinators, conversions, Task-to-UniTask mapping.
- `references/cancellation-recipes.md` - read when wiring lifetimes, timeouts, linked tokens, completion sources, exception handling, or tests.
- `references/streams-and-events.md` - read when using async LINQ, `await foreach`, uGUI/MonoBehaviour async events, `AsyncReactiveProperty`, or `Channel`.

## Workflow

1. Install (pick one):
   - Package Manager > Add package from git URL: `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask` (pin with `#2.5.11`; release tags are plain `x.y.z`).
   - `Packages/manifest.json`: `"com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11"`.
   - OpenUPM: `openupm add com.cysharp.unitask`.
2. Reference the `UniTask` asmdef from your asmdef. Add `UniTask.Linq` for async LINQ, `UniTask.Addressables` / `UniTask.DOTween` / `UniTask.TextMeshPro` for integrations.
3. `using Cysharp.Threading.Tasks;` (plus `Cysharp.Threading.Tasks.Linq` and `Cysharp.Threading.Tasks.Triggers` as needed).
4. Pick the return type (table below), accept `CancellationToken cancellationToken` as the last parameter, and pass a lifetime token from the root caller down.
5. Before shipping, open `Window > UniTask Tracker`, enable tracking + stack traces, exercise the feature, and confirm no tasks are left pending.

## Return types

| Type | Use for | Notes |
|---|---|---|
| `UniTask` | awaitable work with no result | struct; replaces `Task` / `ValueTask` |
| `UniTask<T>` | awaitable work with a result | struct; `AsUniTask()` drops the result for free |
| `UniTaskVoid` | fire-and-forget entry points (`async UniTaskVoid Start()`, button handlers) | not awaitable; exceptions go straight to `UniTaskScheduler.UnobservedTaskException`; call `.Forget()` at the call site to silence the warning |
| `async void` | never | runs on the .NET task system, not UniTask; use `UniTask.Action` / `UniTask.UnityAction` / `UniTask.Void` for event lambdas |

`async UniTaskVoid Foo()` + `Foo().Forget()` is cheaper than `async UniTask Foo()` + `Foo().Forget()` when nobody ever awaits it. Use `Forget()` on a `UniTask` when the method is sometimes awaited and sometimes not. `Forget(Action<Exception> exceptionHandler, bool handleExceptionOnMainThread = true)` routes errors to your handler instead of the global one.

## Core rules

- **Await once.** A `UniTask` is backed by a pooled source; awaiting it twice throws (or worse, reads a recycled source). For multiple awaiters use `.Preserve()`, `UniTask.Lazy(...)` / `AsyncLazy` for cached fields, or a `UniTaskCompletionSource` (which supports many awaiters).
- **Always pass a token.** Unity does not stop async methods when a GameObject dies. Use `destroyCancellationToken` (MonoBehaviour, Unity 2022.2+) or `this.GetCancellationTokenOnDestroy()`; on 2022.2+ the MonoBehaviour overload simply returns `destroyCancellationToken`. `Application.exitCancellationToken` covers app quit.
- **Dispose what you create.** Every `new CancellationTokenSource()` and `CreateLinkedTokenSource(...)` must be `Cancel()`ed and `Dispose()`d (usually in `OnDisable` / `OnDestroy`, or with `using`).
- **Use `CancelAfterSlim`, not `CancelAfter`.** `CancelAfter` uses a threading timer; `CancelAfterSlim(TimeSpan)` runs on the PlayerLoop and returns an `IDisposable` to stop it.
- **Prefer tokens over `.Timeout()`.** `.Timeout` / `.TimeoutWithoutException` only stop waiting; the underlying work keeps running. Pass a token from `TimeoutController.Timeout(...)` instead.
- **Cancellation is checked on the PlayerLoop.** PlayerLoop-based methods notice a cancelled token on their next tick. Pass `cancelImmediately: true` for immediate completion (costs a `CancellationToken.Register`).
- **Let `OperationCanceledException` propagate.** It is silently ignored by the unobserved handler. Filter it out of broad catches: `catch (Exception ex) when (ex is not OperationCanceledException)`.
- **`SuppressCancellationThrow()` only at the source.** It returns `(bool IsCanceled, T Result)` / `UniTask<bool>` without throwing, but only if called directly on the leaf operation; wrapping a method that already threw just converts the result.

## Timing (PlayerLoopTiming)

`Initialization`, `LastInitialization`, `EarlyUpdate`, `LastEarlyUpdate`, `FixedUpdate`, `LastFixedUpdate`, `PreUpdate`, `LastPreUpdate`, `Update`, `LastUpdate`, `PreLateUpdate`, `LastPreLateUpdate`, `PostLateUpdate`, `LastPostLateUpdate`, `TimeUpdate`, `LastTimeUpdate`. Default for most methods is `Update`, which runs before `MonoBehaviour.Update`.

Coroutine equivalents:

| Coroutine | UniTask |
|---|---|
| `yield return null` | `await UniTask.NextFrame()` (guaranteed next frame). `UniTask.Yield()` returns at the next run of the timing, which can be the same frame. |
| `WaitForSeconds(s)` | `await UniTask.Delay(TimeSpan.FromSeconds(s), cancellationToken: ct)` or `UniTask.WaitForSeconds(s, ...)` |
| `WaitForSecondsRealtime(s)` | `UniTask.Delay(..., ignoreTimeScale: true)` or `DelayType.UnscaledDeltaTime` / `DelayType.Realtime` |
| N frames | `UniTask.DelayFrame(n, cancellationToken: ct)` |
| `WaitForFixedUpdate` | `UniTask.WaitForFixedUpdate()` / `UniTask.Yield(PlayerLoopTiming.FixedUpdate)` |
| `WaitForEndOfFrame` | `UniTask.WaitForEndOfFrame(ct)` (Unity 2023.1+ uses `Awaitable.EndOfFrameAsync`). `LastPostLateUpdate` is NOT end of frame; `ReadPixels` / `ScreenCapture` need the real one. |
| `WaitUntil` / `WaitWhile` | `UniTask.WaitUntil(() => cond, cancellationToken: ct)` / `WaitWhile`; state overloads `WaitUntil(state, s => ...)` avoid closure allocs |
| poll a value | `await UniTask.WaitUntilValueChanged(target, t => t.Value, cancellationToken: ct)` |
| `StartCoroutine(IEnumerator)` | `await enumerator.ToUniTask(this)` for full compatibility; plain `await enumerator` does not support `WaitForEndOfFrame` / `WaitForFixedUpdate` / nested `Coroutine` |

## Awaiting Unity operations

`await` works directly on `AsyncOperation`, `ResourceRequest`, `AssetBundleRequest` (`AwaitForAllAssets()` for `allAssets`), `AssetBundleCreateRequest`, `UnityWebRequestAsyncOperation`, `AsyncGPUReadbackRequest`, `AsyncInstantiateOperation`, `JobHandle`, `IEnumerator`.

- `.WithCancellation(ct)` or `.ToUniTask(progress, timing, ct)` add cancellation / progress. Direct `await` resumes at the native completion point; `WithCancellation` / `ToUniTask` resume at the chosen `PlayerLoopTiming`. Do not use `LoadSceneAsync(...).ToUniTask()`: the continuation then runs after the new scene's `Start`.
- `UnityWebRequest` failures throw `UnityWebRequestException` (`ResponseCode`, `Error`, `Text`, `IsNetworkError`, `IsHttpError`). Wrap requests in `using`.
- Use `Progress.Create<float>(...)` or `Progress.CreateOnlyValueChanged<float>(...)`, or implement `IProgress<float>` on the caller; avoid `new System.Progress<T>`.
- Addressables: `AsyncOperationHandle` / `AsyncOperationHandle<T>` become awaitable automatically when `com.unity.addressables` is installed (`UNITASK_ADDRESSABLE_SUPPORT`).
- DOTween: `Tween` becomes awaitable when `com.demigiant.dotween` is in the package list (`UNITASK_DOTWEEN_SUPPORT` via asmdef versionDefines). If DOTween lives in `Assets/` as a plain asset, add `UNITASK_DOTWEEN_SUPPORT` to Scripting Define Symbols and make sure the `DOTween.Modules` asmdef exists (DOTween Utility Panel > Create ASMDEF), because `UniTask.DOTween` references it. Default await completes when the tween is killed; with `SetAutoKill(false)` use `AwaitForComplete`, `AwaitForPause`, `AwaitForPlay`, `AwaitForRewind`, `AwaitForStepComplete`. `TweenCancelBehaviour` controls what cancellation does to the tween.
- TextMeshPro: enabled when `com.unity.textmeshpro` or `com.unity.ugui` 2.0+ (Unity 6) is present.

## Concurrency

- `await UniTask.WhenAll(a, b, c)` returns a tuple for mixed `UniTask<T>` types; `await (a, b, c)` is shorthand. Array overloads return `T[]`.
- `UniTask.WhenAny(tasks)` returns `(int winArgumentIndex, T result)`; losers keep running, so cancel them via a shared linked token.
- `await foreach (var r in UniTask.WhenEach(tasks))` yields `WhenEachResult<T>` as each finishes (`IsCompletedSuccessfully`, `IsFaulted`, `Result`, `Exception`, `GetResult()`).

## Threads and WebGL

- `await UniTask.SwitchToThreadPool();` then `await UniTask.SwitchToMainThread(ct);` to hop back. `UniTask.RunOnThreadPool(func, configureAwait: true, ct)` is the `Task.Run` equivalent; `UniTask.Run` is `[Obsolete]`.
- No Unity API off the main thread. Check with `PlayerLoopHelper.IsMainThread`.
- WebGL has no thread pool: `SwitchToThreadPool` / `RunOnThreadPool` do not give parallelism there. Everything else in UniTask is PlayerLoop-based and works on WebGL.
- Use `UniTask.Create(...)`, `UniTask.Defer(...)` or `UniTask.Void(...)` when you only need to start async work, not change threads.

## UniTask vs Unity 6 Awaitable

| Need | Choose |
|---|---|
| App/game code with `WhenAll` / `WhenAny`, frame delays, explicit `PlayerLoopTiming`, async LINQ, tracker window | UniTask |
| A library or package that must not depend on a third-party package | `Awaitable` in the public API |
| Coroutine-level waits only (`NextFrameAsync`, `WaitForSecondsAsync`, `EndOfFrameAsync`, `FixedUpdateAsync`, `MainThreadAsync`, `BackgroundThreadAsync`) | either; Awaitable is built in |

Both are pooled and both must be awaited only once. Bridge with `awaitable.AsUniTask()` (compiled under `UNITY_2023_1_OR_NEWER`). In files that use both, keep `using UnityEngine;` so `await SceneManager.LoadSceneAsync(...)` and similar resolve without ambiguity between Unity's `AsyncOperation` awaiter and UniTask's.

## PlayerLoop setup

- UniTask injects its loop at `RuntimeInitializeLoadType.BeforeSceneLoad`. To use UniTask from your own `BeforeSceneLoad` code, initialize earlier at `AfterAssembliesLoaded`: `var loop = PlayerLoop.GetCurrentPlayerLoop(); PlayerLoopHelper.Initialize(ref loop);`.
- Entities (ECS) can overwrite the player loop. Re-run `PlayerLoopHelper.Initialize(ref loop)` after ECS world setup. Diagnose with `PlayerLoopHelper.IsInjectedUniTaskPlayerLoop()` and `PlayerLoopHelper.DumpCurrentPlayerLoop()`.
- `PlayerLoopHelper.Initialize(ref loop, InjectPlayerLoopTimings.Minimum)` (`Update | FixedUpdate | LastPostLateUpdate`) trims overhead; awaiting a non-injected timing then never completes.

## Pitfalls

- Unobserved exceptions: errors in `UniTaskVoid` / `Forget()` go to `UniTaskScheduler.UnobservedTaskException` (an event). With no subscriber they are logged at `UniTaskScheduler.UnobservedExceptionWriteLogType` (default `LogType.Exception`); once you subscribe, logging is your job. Subscribe at startup to report them to telemetry; `OperationCanceledException` is ignored unless `UniTaskScheduler.PropagateOperationCanceledException = true`.
- A `UniTask` stored in a field and awaited later from two places: use `Preserve()` or `AsyncLazy`.
- Forgetting a token on `Delay` / `WaitUntil` in a destroyed object leaks the task and keeps lambdas (and the object) alive. Check the tracker.
- A `WaitUntil` / `WaitWhile` predicate that touches Unity objects (`transform`, components) throws `MissingReferenceException` once the object is destroyed unless the wait was cancelled first.
- Profiler shows `AsyncStateMachine` allocations in Debug code optimization only (the compiler emits classes in Debug, structs in Release). Switch the Editor to Release before judging GC.
- `TaskPool.SetMaxPoolSize(n)` caps pooled promise objects per type; `TaskPool.GetCacheSizeInfo()` lists current pools.
- Edit Mode: all timings run on `EditorApplication.update`; `Delay` with `DeltaTime` switches to `Realtime`. `-batchmode -quit` exits before tasks finish; quit with `EditorApplication.Exit(0)` instead.
- Tests: `[UnityTest] public IEnumerator T() => UniTask.ToCoroutine(async () => { ... });`.

## Related skills

- `csharp-unity` for general C# and MonoBehaviour standards.
- `review-unity-csharp` for leak-focused review of async code.
- `animation-sequencer` and `zbase-csv-reader` for libraries that are often awaited with UniTask.
