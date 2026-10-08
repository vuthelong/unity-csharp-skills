# UniTask API tables

Signatures checked against UniTask 2.5.11. Optional trailing parameters are abbreviated: most PlayerLoop methods end with `CancellationToken cancellationToken = default, bool cancelImmediately = false`.

## Waiting and timing (`UniTask.*`)

| Method | Signature (abridged) | Returns |
|---|---|---|
| `Yield` | `()`, `(PlayerLoopTiming)` | `YieldAwaitable` (cheapest, no token) |
| `Yield` | `(CancellationToken, bool cancelImmediately = false)`, `(PlayerLoopTiming, CancellationToken, bool)` | `UniTask` |
| `NextFrame` | `()`, `(PlayerLoopTiming)`, `(CancellationToken, bool)`, `(PlayerLoopTiming, CancellationToken, bool)` | `UniTask` |
| `Delay` | `(int ms \| TimeSpan, bool ignoreTimeScale = false, PlayerLoopTiming delayTiming = Update, ct, cancelImmediately)` | `UniTask` |
| `Delay` | `(int ms \| TimeSpan, DelayType delayType, PlayerLoopTiming delayTiming = Update, ct, cancelImmediately)` | `UniTask` |
| `WaitForSeconds` | `(float \| int duration, bool ignoreTimeScale = false, PlayerLoopTiming delayTiming = Update, ct, cancelImmediately)` | `UniTask` |
| `DelayFrame` | `(int delayFrameCount, PlayerLoopTiming delayTiming = Update, ct, cancelImmediately)` | `UniTask` |
| `WaitForEndOfFrame` | `(CancellationToken = default)` on 2023.1+; `(MonoBehaviour coroutineRunner, ...)` on any version | `UniTask` |
| `WaitForFixedUpdate` | `()` / `(CancellationToken, bool)` | `YieldAwaitable` / `UniTask` |
| `WaitUntil` | `(Func<bool>, PlayerLoopTiming = Update, ct, cancelImmediately)`, `(T state, Func<T,bool>, ...)` | `UniTask` |
| `WaitWhile` | same shapes as `WaitUntil` | `UniTask` |
| `WaitUntilCanceled` | `(CancellationToken, PlayerLoopTiming = Update, bool completeImmediately = false)` | `UniTask` |
| `WaitUntilValueChanged` | `<T,U>(T target, Func<T,U> monitorFunction, PlayerLoopTiming = Update, IEqualityComparer<U> = null, ct, cancelImmediately)` | `UniTask<U>` |
| `Never` | `(CancellationToken)` / `<T>(CancellationToken)` | completes only on cancel |

`DelayType`: `DeltaTime` (scaled), `UnscaledDeltaTime`, `Realtime` (Stopwatch).

## Factories and helpers

| Method | Purpose |
|---|---|
| `UniTask.CompletedTask`, `FromResult<T>(v)`, `FromException(ex)`, `FromCanceled(ct)` | pre-completed tasks |
| `UniTask.Create(Func<UniTask>)`, `Create(Func<CancellationToken,UniTask>, ct)`, `Create<T>(state, Func<T,UniTask>)` | start async work from a lambda |
| `UniTask.Defer(Func<UniTask>)` | create lazily on first await |
| `UniTask.Lazy(Func<UniTask>)` / `Lazy<T>` | `AsyncLazy` / `AsyncLazy<T>`; awaitable many times, runs once |
| `UniTask.Void(Func<UniTaskVoid>)` | run fire-and-forget immediately |
| `UniTask.Action(Func<UniTaskVoid>)` | build an `Action` for `+=` events |
| `UniTask.UnityAction(Func<UniTaskVoid>)` (+ `<T>` ... `<T0..T3>` and token overloads) | build a `UnityAction` for uGUI events |
| `UniTask.Post(Action, PlayerLoopTiming = Update)` | queue an action onto the main-thread loop |
| `UniTask.ToCoroutine(Func<UniTask>)` | IEnumerator for `[UnityTest]` / `StartCoroutine` |

## Threading

| Method | Notes |
|---|---|
| `UniTask.SwitchToThreadPool()` | continue on a thread-pool thread |
| `UniTask.SwitchToMainThread(ct)` / `(PlayerLoopTiming, ct)` | return to the main thread |
| `UniTask.ReturnToMainThread(ct)` | `await using` scope that switches back on dispose |
| `UniTask.SwitchToSynchronizationContext(ctx, ct)` | continue on a given context |
| `UniTask.RunOnThreadPool(Action \| Func<UniTask> \| Func<T> \| Func<UniTask<T>>, bool configureAwait = true, ct)` | `Task.Run` equivalent; `configureAwait: true` returns to main thread |
| `UniTask.Run(...)` | `[Obsolete]`, use `RunOnThreadPool` |
| `PlayerLoopHelper.IsMainThread`, `PlayerLoopHelper.MainThreadId` | thread checks |

## Instance members on `UniTask` / `UniTask<T>`

| Member | Notes |
|---|---|
| `Status` | `UniTaskStatus`; `Status.IsCompleted()` etc. |
| `Preserve()` | cache the result so the task can be awaited many times |
| `SuppressCancellationThrow()` | `UniTask<bool>` / `UniTask<(bool IsCanceled, T Result)>` |
| `GetAwaiter()` | |

## Extension methods (`UniTaskExtensions` etc.)

| Method | Notes |
|---|---|
| `Forget()`, `Forget(Action<Exception>, bool handleExceptionOnMainThread = true)` | fire-and-forget a `UniTask` / `UniTask<T>` |
| `AttachExternalCancellation(ct)` | stop awaiting when `ct` fires; does not stop the inner work |
| `Timeout(TimeSpan, DelayType = DeltaTime, PlayerLoopTiming timeoutCheckTiming = Update, CancellationTokenSource taskCancellationTokenSource = null)` | throws `TimeoutException`; prefer tokens |
| `TimeoutWithoutException(...)` | `UniTask<bool>` / `UniTask<(bool IsTimeout, T Result)>` |
| `ContinueWith(...)` | chained continuation |
| `ToCoroutine(resultHandler, exceptionHandler)` | UniTask to IEnumerator |
| `AsUniTask()` on `Task`, `Task<T>`, `ValueTask`, `ValueTask<T>`, `Awaitable`, `Awaitable<T>` (2023.1+), `UniTask<T>` | conversions |
| `AsTask()` | UniTask to `Task` |
| `AsAsyncUnitUniTask()` | `UniTask` to `UniTask<AsyncUnit>` |
| `ToCancellationToken()` / `ToCancellationToken(linkToken)` | token that fires when the task completes |
| `cancellationToken.WaitUntilCanceled()` | awaitable |
| `cancellationToken.ToUniTask()` | `(UniTask, CancellationTokenRegistration)` |
| `cancellationToken.RegisterWithoutCaptureExecutionContext(Action)` | cheaper `Register` |
| `disposable.AddTo(cancellationToken)` | dispose on cancel |
| `cts.CancelAfterSlim(TimeSpan \| int ms, DelayType = DeltaTime, PlayerLoopTiming = Update)` | PlayerLoop timer; returns `IDisposable` |
| `cts.RegisterRaiseCancelOnDestroy(Component \| GameObject)` | cancel the CTS when the object is destroyed |
| `GetCancellationTokenOnDestroy()` on `MonoBehaviour`, `Component`, `GameObject` | 2022.2+: MonoBehaviour returns `destroyCancellationToken`; GameObject adds an `AsyncDestroyTrigger` |

## Combinators

| Method | Returns |
|---|---|
| `UniTask.WhenAll(params UniTask[])` / `(IEnumerable<UniTask>)` | `UniTask` |
| `UniTask.WhenAll<T>(params UniTask<T>[])` | `UniTask<T[]>` |
| `UniTask.WhenAll(UniTask<T1>, UniTask<T2>, ...)` | `UniTask<(T1, T2, ...)>`; also `await (t1, t2)` |
| `UniTask.WhenAny(params UniTask[])` | `UniTask<int>` winner index |
| `UniTask.WhenAny<T>(params UniTask<T>[])` | `UniTask<(int winArgumentIndex, T result)>` |
| `UniTask.WhenAny<T>(UniTask<T>, UniTask)` | `UniTask<(bool hasResultLeft, T result)>` |
| `UniTask.WhenEach<T>(params UniTask<T>[])` / `(IEnumerable<UniTask<T>>)` | `IUniTaskAsyncEnumerable<WhenEachResult<T>>` |

## Awaiting Unity types

| Type | `await` result | Extra |
|---|---|---|
| `AsyncOperation` | none | `WithCancellation(ct[, cancelImmediately])`, `ToUniTask(IProgress<float>, PlayerLoopTiming, ct, cancelImmediately)` |
| `ResourceRequest` | `UnityEngine.Object` | same |
| `AssetBundleRequest` | `UnityEngine.Object` (`asset`) | `AwaitForAllAssets()` for `allAssets` |
| `AssetBundleCreateRequest` | `AssetBundle` | same |
| `UnityWebRequestAsyncOperation` | `UnityWebRequest` | throws `UnityWebRequestException` on error |
| `AsyncGPUReadbackRequest` | `AsyncGPUReadbackRequest` | `WithCancellation`, `ToUniTask(timing, ct)` |
| `AsyncInstantiateOperation` / `<T>` | `Object[]` / `T[]` | `WithCancellation`, `ToUniTask` |
| `JobHandle` | none | `ToUniTask(PlayerLoopTiming waitTiming)` |
| `IEnumerator` | none | `ToUniTask(PlayerLoopTiming, ct)`, `ToUniTask(MonoBehaviour coroutineRunner)` |
| `AsyncOperationHandle` / `<T>` (Addressables) | none / `T` | `WithCancellation`, `ToUniTask(progress, timing, ct)`; `UNITASK_ADDRESSABLE_SUPPORT` |
| DOTween `Tween` | none | `WithCancellation`, `ToUniTask(TweenCancelBehaviour, ct)`, `AwaitForComplete/Pause/Play/Rewind/StepComplete`; `UNITASK_DOTWEEN_SUPPORT` |

`TweenCancelBehaviour`: `Kill` (default), `KillWithCompleteCallback`, `Complete`, `CompleteWithSequenceCallback`, `CancelAwait`, and `...AndCancelAwait` variants (`KillAndCancelAwait`, `KillWithCompleteCallbackAndCancelAwait`, `CompleteAndCancelAwait`, `CompleteWithSequenceCallbackAndCancelAwait`). On cancellation the plain variants kill or complete the tween and the await finishes normally; `CancelAwait` leaves the tween running and throws `OperationCanceledException`; the `...AndCancelAwait` variants act on the tween and throw.

## Completion sources

| Type | Notes |
|---|---|
| `UniTaskCompletionSource` / `<T>` | `Task`, `TrySetResult`, `TrySetException`, `TrySetCanceled`; awaitable by many callers |
| `AutoResetUniTaskCompletionSource` / `<T>` | pooled; `Create()`, `CreateFromCanceled`, `CreateFromException`, `CreateCompleted` / `CreateFromResult`; returns to pool after one await, so hand its `Task` to exactly one awaiter |
| `UniTaskCompletionSourceCore<T>` | building block for custom `IUniTaskSource` |

## Diagnostics and config

| API | Notes |
|---|---|
| `UniTaskScheduler.UnobservedTaskException` | `event Action<Exception>` |
| `UniTaskScheduler.UnobservedExceptionWriteLogType` | `LogType`, default `Exception`; used only when no handler is subscribed |
| `UniTaskScheduler.PropagateOperationCanceledException` | default `false` |
| `UniTaskScheduler.DispatchUnityMainThread` | default `true` |
| `PlayerLoopHelper.Initialize(ref PlayerLoopSystem, InjectPlayerLoopTimings = All)` | `InjectPlayerLoopTimings.All`, `Standard`, `Minimum`, or flags |
| `PlayerLoopHelper.IsInjectedUniTaskPlayerLoop()`, `DumpCurrentPlayerLoop()` | diagnostics |
| `PlayerLoopHelper.AddAction(timing, IPlayerLoopItem)`, `AddContinuation(timing, Action)` | custom loop work |
| `TaskPool.SetMaxPoolSize(int)`, `TaskPool.GetCacheSizeInfo()` | pool tuning |
| `Window > UniTask Tracker` | Enable Tracking (low cost), Enable StackTrace (high cost), Reload, GC.Collect |
| `UniTaskSynchronizationContext` | optional replacement for `UnitySynchronizationContext` (affects `async Task` only) |

## .NET Task mapping

| .NET | UniTask |
|---|---|
| `Task` / `ValueTask` | `UniTask` |
| `Task<T>` / `ValueTask<T>` | `UniTask<T>` |
| `async void` | `async UniTaskVoid` |
| `TaskCompletionSource<T>` | `UniTaskCompletionSource<T>` / `AutoResetUniTaskCompletionSource<T>` |
| `new Progress<T>` | `Progress.Create<T>` / `Progress.CreateOnlyValueChanged<T>` |
| `CancellationTokenSource.CancelAfter` | `CancelAfterSlim` |
| `Task.Run` | `UniTask.RunOnThreadPool` |
| `Task.Delay` / `Task.Yield` | `UniTask.Delay` / `UniTask.Yield` |
| `Task.WhenAll` / `WhenAny` / `WhenEach` | `UniTask.WhenAll` / `WhenAny` / `WhenEach` |
| `IAsyncEnumerable<T>` | `IUniTaskAsyncEnumerable<T>` |
| `TaskScheduler.UnobservedTaskException` | `UniTaskScheduler.UnobservedTaskException` |
