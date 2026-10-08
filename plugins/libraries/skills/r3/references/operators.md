# R3 factories and operators

All factories are static methods on `Observable`. All operators are extension methods on `Observable<T>`. Signatures were checked in `src/R3/Factories` and `src/R3/Operators`. Time-based overloads without a provider use `ObservableSystem.DefaultTimeProvider`, and frame-based overloads without a provider use `DefaultFrameProvider`. In Unity both default to `Update`.

## Factories

| Factory | Overloads (abridged) | Emits |
|---|---|---|
| `EveryUpdate` | `()`, `(CancellationToken)`, `(FrameProvider)`, `(FrameProvider, CancellationToken)` | `Unit` every frame |
| `EveryValueChanged` | `(source, Func<TSource,TProperty> selector[, FrameProvider][, EqualityComparer<TProperty>], CancellationToken = default)` | the value whenever it changes, starting with the current value |
| `Interval` | `(TimeSpan period[, TimeProvider], CancellationToken = default)` | `Unit` per period |
| `Timer` | `(TimeSpan or DateTimeOffset dueTime[, TimeSpan period][, TimeProvider], CancellationToken = default)` | `Unit` once, or periodically |
| `IntervalFrame` | `(int periodFrame[, FrameProvider], CancellationToken = default)` | `Unit` every N frames |
| `TimerFrame` | `(int dueTimeFrame[, int periodFrame][, FrameProvider], CancellationToken = default)` | `Unit` after N frames |
| `NextFrame` | `([FrameProvider], CancellationToken = default)` | one `Unit` on the next frame |
| `Return`, `ReturnFrame`, `ReturnUnit`, `ReturnOnCompleted` | | single values |
| `Range`, `Repeat`, `Empty`, `Never`, `Throw`, `Defer`, `Create`, `CreateFrom` | | standard |
| `FromAsync` | `(Func<CancellationToken, ValueTask<T>>, bool configureAwait = true)` | the async result |
| `FromEvent`, `FromEventHandler` | | .NET events |
| `CombineLatest`, `Merge`, `Concat`, `Zip`, `ZipLatest`, `Race` | params / `IEnumerable` forms | combinators |

Unity-specific factories:

- `UnityEvent.AsObservable(CancellationToken = default)`, including the `UnityEvent<T>` through `UnityEvent<T0..T3>` variants.
- uGUI helpers: `OnClickAsObservable`, `OnValueChangedAsObservable` (`Toggle`, `Scrollbar`, `ScrollRect`, `Slider`, `InputField`, `Dropdown`), and `OnEndEditAsObservable` (`InputField`).
- TMP helpers: `OnEndEditAsObservable` and `OnValueChangedAsObservable` on `TMP_InputField`, and `OnValueChangedAsObservable` on `TMP_Dropdown`.
- `R3.Triggers` extensions on `Component` and `GameObject`:
  - Lifecycle and update: `UpdateAsObservable`, `FixedUpdateAsObservable`, `LateUpdateAsObservable`, `OnEnableAsObservable`, `OnDisableAsObservable`, `OnDestroyAsObservable`.
  - Collisions and triggers: `OnCollisionEnter/Stay/ExitAsObservable`, `OnTriggerEnter/Stay/ExitAsObservable`.
  - Mouse: `OnMouseDown/Drag/Enter/Exit/Over/Up/UpAsButtonAsObservable`.
  - Visibility: `OnBecameVisible/InvisibleAsObservable`.
  - Transform and UI layout: `OnTransformParentChangedAsObservable`, `OnTransformChildrenChangedAsObservable`, `OnRectTransformDimensionsChangeAsObservable`, `OnCanvasGroupChangedAsObservable`.
  - Particles and animation: `OnParticleCollisionAsObservable`, `OnParticleTriggerAsObservable`, `OnAnimatorMoveAsObservable`, `OnAnimatorIKAsObservable`.
  - Event-system triggers also exist (pointer, drag, select, submit and so on).

## Filtering and projection

| Operator | Notes |
|---|---|
| `Where(pred)`, `Where((x, i) => ...)`, `Where(state, (x, s) => ...)` | the state overloads avoid closure allocations |
| `WhereNotNull()`, `OfType<T>()`, `Cast<T>()` | |
| `Select(selector)`, `Select((x, i) => ...)`, `Select(state, ...)` | |
| `SelectMany` | |
| `Scan`, `Pairwise`, `Index`, `Timestamp`, `TimeInterval` | |
| `Distinct`, `DistinctBy`, `DistinctUntilChanged`, `DistinctUntilChangedBy` | `...By` replaces the selector overloads |
| `Skip`, `SkipLast`, `SkipWhile`, `SkipUntil`, `SkipFrame`, `SkipLastFrame` | |
| `Take`, `TakeLast`, `TakeWhile`, `TakeFrame`, `TakeLastFrame` | |
| `TakeUntil(Observable<TOther>)`, `TakeUntil(CancellationToken)`, `TakeUntil(Task)`, `TakeUntil(Func<T,CancellationToken,ValueTask>)`, `TakeUntil(Func<T,bool>)` | |
| `Prepend`, `Append`, `DefaultIfEmpty`, `IgnoreElements`, `AsUnitObservable` | |

## Rate limiting and buffering

| Operator | Time | Frame | Async / sampler |
|---|---|---|---|
| `ThrottleFirst` (first value, then mute) | `(TimeSpan[, TimeProvider])` | `ThrottleFirstFrame(int[, FrameProvider])` | `(Observable<TSample>)`, `(Func<T,CancellationToken,ValueTask>)` |
| `ThrottleLast` (UniRx `Sample`) | `(TimeSpan[, TimeProvider])` | `ThrottleLastFrame` | sampler and async forms |
| `ThrottleFirstLast` | `(TimeSpan[, TimeProvider])` | `ThrottleFirstLastFrame` | async form |
| `Debounce` (UniRx `Throttle`) | `(TimeSpan[, TimeProvider])` | `DebounceFrame(int[, FrameProvider])` | `(Func<T,CancellationToken,ValueTask>)` |
| `Chunk` (UniRx `Buffer`) | `(int count)`, `(int count, int skip)`, `(TimeSpan[, TimeProvider])`, `(TimeSpan, int count[, TimeProvider])` | `ChunkFrame()`, `ChunkFrame(int frameCount[, int count][, FrameProvider])` | `(Observable<TWindowBoundary>)`, `(Func<T,CancellationToken,ValueTask> asyncWindow)` |
| `Delay` | `(TimeSpan[, TimeProvider])` | `DelayFrame(int[, FrameProvider])` | |
| `DelaySubscription` | time | `DelaySubscriptionFrame` | |
| `Timeout` | time | `TimeoutFrame` | |

R3's throttle timers start only when a value arrives, not when you subscribe.

## Combining

| Operator | Notes |
|---|---|
| `CombineLatest(a, b, (x, y) => ...)` | up to 15 sources with a selector, or `Observable.CombineLatest(params Observable<T>[])` returning `T[]` |
| `Merge(a, b)`, `Observable.Merge(params ...)`, `Observable<Observable<T>>.Merge()` | |
| `Switch()` on `Observable<Observable<T>>` | subscribes to the latest inner observable only |
| `Concat`, `Zip`, `ZipLatest`, `WithLatestFrom`, `Race` (UniRx `Amb`) | |

## Error and lifecycle

| Operator | Notes |
|---|---|
| `OnErrorResumeAsFailure()` | turns `OnErrorResume` into `OnCompleted(Failure)`, which ends the stream |
| `Catch(Observable<T> second)`, `Catch<T, TException>(Func<TException, Observable<T>>)` | handles failure completion only |
| `IgnoreOnErrorResume()` | drops resumable errors |
| `Do(onNext:, onErrorResume:, onCompleted:, onDispose:, onSubscribe:)` | replaces `Finally` and the `Do***` methods |
| `DoCancelOnCompleted(CancellationTokenSource)` | |
| `Materialize`, `Dematerialize` | |

## Sharing

`Publish()`, `Publish(initialValue)`, `Replay(...)`, `ReplayFrame(...)`, `RefCount()`, `Share()`, `Multicast(subject)`. Use `Share()` when several subscribers must not each trigger their own upstream (for example, one `SelectAwait` web call).

## Scheduling

`ObserveOn(SynchronizationContext | TimeProvider | FrameProvider)`, `ObserveOnCurrentSynchronizationContext()`, `ObserveOnThreadPool()`, `SubscribeOn(...)`, `SubscribeOnThreadPool()`, `SubscribeOnSynchronize(gate)`, `Synchronize(gate)`, `Yield()`, `YieldFrame()`, `Trampoline()`. In R3.Unity: `ObserveOnMainThread()`, `SubscribeOnMainThread()`.

## Async

| Method | Signature (abridged) |
|---|---|
| `SubscribeAwait` | `(Func<T,CancellationToken,ValueTask> onNextAsync[, Action<Exception> onErrorResume][, Action<Result> onCompleted], AwaitOperation = Sequential, bool configureAwait = true, bool cancelOnCompleted = true, int maxConcurrent = -1)` |
| `SelectAwait` | `(Func<T,CancellationToken,ValueTask<TResult>>, AwaitOperation = Sequential, ...)` |
| `WhereAwait` | `(Func<T,CancellationToken,ValueTask<bool>>, AwaitOperation = Sequential, ...)` |
| `FirstAsync`, `FirstOrDefaultAsync`, `LastAsync`, `LastOrDefaultAsync`, `SingleAsync`, `SingleOrDefaultAsync`, `ElementAtAsync` | `Task<T>`, optional predicate, `CancellationToken` |
| `ToArrayAsync`, `ToListAsync`, `ToDictionaryAsync`, `ToHashSetAsync`, `ToLookupAsync` | `Task<...>` |
| `CountAsync`, `LongCountAsync`, `AnyAsync`, `AllAsync`, `ContainsAsync`, `IsEmptyAsync`, `SumAsync`, `MinAsync`, `MaxAsync`, `AverageAsync`, `AggregateAsync` | `Task<...>` |
| `ForEachAsync(Action<T>[, CancellationToken])`, `WaitAsync(CancellationToken)` | `Task` |
| `ToAsyncEnumerable(CancellationToken)` | `IAsyncEnumerable<T>` |
| `ToLiveList()` | `LiveList<T>`, for tests |

`AwaitOperation` values: `Sequential`, `Drop`, `Switch`, `Parallel`, `SequentialParallel`, `ThrottleFirstLast`.

## Testing

- `FakeFrameProvider` ships with R3: call `Advance()` / `Advance(n)` and read `GetFrameCount()`.
- For time, use `FakeTimeProvider` from Microsoft.Extensions.TimeProvider.Testing (install it separately through NuGetForUnity).
- Collect emitted values with `ToLiveList()`.
