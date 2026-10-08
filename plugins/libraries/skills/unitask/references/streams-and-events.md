# Async streams, events, reactive properties, channels

Requires the `UniTask.Linq` asmdef reference and `using Cysharp.Threading.Tasks.Linq;` for LINQ operators. MonoBehaviour triggers need `using Cysharp.Threading.Tasks.Triggers;`.

## UniTaskAsyncEnumerable generators

| Generator | Signature (abridged) |
|---|---|
| `EveryUpdate` | `(PlayerLoopTiming updateTiming = Update, bool cancelImmediately = false)` yields `AsyncUnit` |
| `EveryValueChanged` | `<TTarget,TProperty>(TTarget target, Func<TTarget,TProperty> propertySelector, PlayerLoopTiming monitorTiming = Update, IEqualityComparer<TProperty> = null, bool cancelImmediately = false)` |
| `Timer` | `(TimeSpan dueTime[, TimeSpan period], PlayerLoopTiming = Update, bool ignoreTimeScale = false, bool cancelImmediately = false)` |
| `Interval` | `(TimeSpan period, PlayerLoopTiming = Update, bool ignoreTimeScale = false, ...)` |
| `TimerFrame` / `IntervalFrame` | frame-count versions |
| `Create<T>` | `(Func<IAsyncWriter<T>, CancellationToken, UniTask> create)`; call `await writer.YieldAsync(value)` |
| `Return`, `Range`, `Repeat`, `Empty`, `Never`, `Throw` | standard |

`IEnumerable<T>.ToUniTaskAsyncEnumerable()`, `IObservable<T>.ToUniTaskAsyncEnumerable()`, `ToObservable()` convert in and out.

## Operators

Standard LINQ: `Where`, `Select`, `SelectMany`, `Take`, `TakeWhile`, `Skip`, `SkipWhile`, `First`, `Last`, `Single`, `Count`, `Any`, `All`, `Aggregate`, `Sum`, `Min`, `Max`, `Average`, `Distinct`, `GroupBy`, `OrderBy`, `Zip`, `Concat`, `ToArray`, `ToList`, `ToDictionary`, `ToHashSet`, `ToLookup`.

UniTask extras: `Append`, `Prepend`, `DistinctUntilChanged`, `Buffer`, `CombineLatest`, `Merge`, `Do`, `Pairwise`, `Publish`, `Queue`, `SkipLast`, `TakeLast`, `SkipUntil`, `TakeUntil`, `SkipUntilCanceled`, `TakeUntilCanceled`, `ForEachAsync`, `Subscribe`.

Operators that take a `Func` also have `...Await` (`Func<T, UniTask<TR>>`) and `...AwaitWithCancellation` (`Func<T, CancellationToken, UniTask<TR>>`) forms, for example `SelectAwait`, `WhereAwait`, `ForEachAwaitAsync`, `ForEachAwaitWithCancellationAsync`.

## Consuming

```csharp
await foreach (var _ in UniTaskAsyncEnumerable.EveryUpdate().WithCancellation(destroyCancellationToken))
{
    Tick();
}

await UniTaskAsyncEnumerable.Interval(TimeSpan.FromSeconds(1))
    .ForEachAsync(_ => Debug.Log("tick"), destroyCancellationToken);
```

Do not write `async IAsyncEnumerable<T>` iterators for Unity loops; they are not driven by UniTask. Use `UniTaskAsyncEnumerable.Create`.

## Pull semantics

`IUniTaskAsyncEnumerable` is pull-based. While a `ForEachAwaitAsync` body is awaiting, new push events (button clicks) are dropped. That is a free double-click guard. To keep them, insert `.Queue()` before the consumer. `Subscribe(async x => ...)` runs each handler fire-and-forget.

## uGUI events

| Component | Single await | Stream | Reusable handler |
|---|---|---|---|
| `Button` | `OnClickAsync([ct])` | `OnClickAsAsyncEnumerable([ct])` | `GetAsyncClickEventHandler([ct])` |
| `Toggle` | `OnValueChangedAsync` | `OnValueChangedAsAsyncEnumerable` | `GetAsyncValueChangedEventHandler` |
| `Slider`, `Scrollbar`, `ScrollRect`, `Dropdown`, `InputField` | `OnValueChangedAsync` | `OnValueChangedAsAsyncEnumerable` | `GetAsyncValueChangedEventHandler` |
| `InputField` | `OnEndEditAsync` | `OnEndEditAsAsyncEnumerable` | `GetAsyncEndEditEventHandler` |
| `UnityEvent` / `UnityEvent<T>` | `OnInvokeAsync` | `OnInvokeAsAsyncEnumerable` | `GetAsyncEventHandler` |

Without a token these default to the component's destroy token. Dispose a reusable handler (`using var h = button.GetAsyncClickEventHandler(ct);`). TMP equivalents for `TMP_InputField` live in `UniTask.TextMeshPro`.

```csharp
await okButton.OnClickAsAsyncEnumerable(destroyCancellationToken)
    .Where((_, i) => i % 2 == 0)
    .ForEachAsync(_ => Confirm(), destroyCancellationToken);
```

## MonoBehaviour async triggers

`GetAsync<Message>Trigger()` on any `GameObject` or `Component` adds a hidden trigger component. Each trigger is an `IUniTaskAsyncEnumerable` and has `<Message>Async([ct])` plus `Get<Message>AsyncHandler([ct])`.

Examples verified in source: `GetAsyncAwakeTrigger`, `GetAsyncStartTrigger`, `GetAsyncDestroyTrigger`, `GetAsyncEnableTrigger`, `GetAsyncDisableTrigger`, `GetAsyncUpdateTrigger` (`UpdateAsync()`, `GetUpdateAsyncHandler()`), `GetAsyncFixedUpdateTrigger`, `GetAsyncLateUpdateTrigger`, `GetAsyncCollisionEnterTrigger` (`OnCollisionEnterAsync()`, `GetOnCollisionEnterAsyncHandler()`), `GetAsyncTriggerEnterTrigger`, `GetAsyncBecameVisibleTrigger`, `GetAsyncApplicationPauseTrigger`, `GetAsyncApplicationFocusTrigger`, `GetAsyncPointerClickTrigger`, `GetAsyncBeginDragTrigger`, `GetAsyncDragTrigger`, `GetAsyncMoveTrigger`.

```csharp
var hit = await this.GetAsyncCollisionEnterTrigger().OnCollisionEnterAsync(destroyCancellationToken);

using var handler = this.GetAsyncCollisionEnterTrigger().GetOnCollisionEnterAsyncHandler(destroyCancellationToken);
var first = await handler.OnCollisionEnterAsync();
var second = await handler.OnCollisionEnterAsync();
```

The handler form does not miss events between awaits and allocates less than repeated `OnCollisionEnterAsync()` calls.

Avoid `GetAsyncUpdateTrigger` as a general update loop; `UniTaskAsyncEnumerable.EveryUpdate()` or a normal `Update` is cheaper.

## AsyncReactiveProperty

```csharp
private readonly AsyncReactiveProperty<int> _hp = new(100);

private void Start()
{
    _hp.WithoutCurrent().BindTo(_hpLabel);
    _hp.Where(v => v <= 0).ForEachAsync(_ => Die(), destroyCancellationToken).Forget();
}

private void OnDestroy() => _hp.Dispose();

public async UniTask WaitForNextChangeAsync(CancellationToken ct) => await _hp.WaitAsync(ct);
```

- `Value` setter pushes to every subscriber; the enumerable replays the current value first unless you use `WithoutCurrent()`.
- `ToReadOnlyAsyncReactiveProperty(ct)` / `(initialValue, ct)` turns any stream into a `ReadOnlyAsyncReactiveProperty<T>`.
- `BindTo` targets `UnityEngine.UI.Text`, `Selectable` (bool to `interactable`) and, with TMP support, `TMP_Text`.
- Dispose the property to complete all consumers.

## Channel

```csharp
public sealed class AsyncMessageBroker<T> : IDisposable
{
    private readonly Channel<T> _channel = Channel.CreateSingleConsumerUnbounded<T>();
    private readonly IConnectableUniTaskAsyncEnumerable<T> _multicast;
    private readonly IDisposable _connection;

    public AsyncMessageBroker()
    {
        _multicast = _channel.Reader.ReadAllAsync().Publish();
        _connection = _multicast.Connect();
    }

    public void Publish(T value) => _channel.Writer.TryWrite(value);

    public IUniTaskAsyncEnumerable<T> Messages => _multicast;

    public void Dispose()
    {
        _channel.Writer.TryComplete();
        _connection.Dispose();
    }
}
```

- Only `CreateSingleConsumerUnbounded<T>()` exists: many writers, one reader. Use `.Publish()` + `Connect()` to fan out.
- Writer: `TryWrite`, `TryComplete(Exception error = null)`, `Complete(error)`.
- Reader: `TryRead`, `WaitToReadAsync(ct)`, `ReadAsync(ct)`, `ReadAllAsync(ct)`, `Completion`.
- Writing after completion fails (`TryWrite` returns false); reading a completed, empty channel throws `ChannelClosedException` from `ReadAsync`.
