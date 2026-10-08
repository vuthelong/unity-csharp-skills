# MessagePipe API reference (Unity, 1.8.2)

All types are in namespace `MessagePipe`. Async members return `Cysharp.Threading.Tasks.UniTask`.

## Pub/Sub interfaces

| Interface | Members |
|---|---|
| `IPublisher<TMessage>` | `void Publish(TMessage message)` |
| `ISubscriber<TMessage>` | `IDisposable Subscribe(IMessageHandler<TMessage> handler, params MessageHandlerFilter<TMessage>[] filters)` |
| `IAsyncPublisher<TMessage>` | `void Publish(TMessage, CancellationToken = default)` (fire-and-forget), `UniTask PublishAsync(TMessage, CancellationToken = default)`, `UniTask PublishAsync(TMessage, AsyncPublishStrategy, CancellationToken = default)` |
| `IAsyncSubscriber<TMessage>` | `IDisposable Subscribe(IAsyncMessageHandler<TMessage>, params AsyncMessageHandlerFilter<TMessage>[])` |
| `IPublisher<TKey, TMessage>` | `void Publish(TKey key, TMessage message)` |
| `ISubscriber<TKey, TMessage>` | `IDisposable Subscribe(TKey key, IMessageHandler<TMessage>, params MessageHandlerFilter<TMessage>[])` |
| `IAsyncPublisher<TKey, TMessage>` / `IAsyncSubscriber<TKey, TMessage>` | keyed versions of the async pair |
| `IBufferedPublisher<TMessage>` / `IBufferedSubscriber<TMessage>` | replays the latest value on subscribe (keyless only) |
| `IBufferedAsyncPublisher<TMessage>` / `IBufferedAsyncSubscriber<TMessage>` | `UniTask<IDisposable> SubscribeAsync(IAsyncMessageHandler<TMessage>, CancellationToken = default)` (and a filters overload) |
| `ISingletonPublisher<T>` / `IScopedPublisher<T>` (+ Subscriber, Async, keyed variants) | force a lifetime regardless of `InstanceLifetime`. Resolvable only through the open-generics path (VContainer 1.14+ on Unity 2022.1+). |
| `IMessageHandler<T>` | `void Handle(T message)` |
| `IAsyncMessageHandler<T>` | `UniTask HandleAsync(T message, CancellationToken cancellationToken)` |

## Request/response

| Interface | Members |
|---|---|
| `IRequestHandler<TReq, TRes>` | `TRes Invoke(TReq request)` |
| `IAsyncRequestHandler<TReq, TRes>` | `UniTask<TRes> InvokeAsync(TReq request, CancellationToken = default)` |
| `IRequestAllHandler<TReq, TRes>` | `TRes[] InvokeAll(TReq)`, `IEnumerable<TRes> InvokeAllLazy(TReq)` |
| `IAsyncRequestAllHandler<TReq, TRes>` | `InvokeAllAsync(TReq, CancellationToken)`, `InvokeAllAsync(TReq, AsyncPublishStrategy, CancellationToken)`, `InvokeAllLazyAsync` |

Implement `IRequestHandler<TReq, TRes>` on a class and register it with `RegisterRequestHandler<TReq, TRes, THandler>(options)`. Consumers inject `IRequestHandler<TReq, TRes>` (the first registered handler) or `IRequestAllHandler<TReq, TRes>` (all of them).

## Subscribe extensions (`SubscriberExtensions`)

| Extension | On |
|---|---|
| `Subscribe(Action<T> handler, params MessageHandlerFilter<T>[])` | `ISubscriber<T>`, `IBufferedSubscriber<T>` |
| `Subscribe(Action<T> handler, Func<T, bool> predicate, params ...)` | same; predicate runs first (`PredicateFilter`, Order `int.MinValue`) |
| `Subscribe(TKey key, Action<T> handler, ...)` | `ISubscriber<TKey, T>` |
| `Subscribe(Func<T, CancellationToken, UniTask> handler, ...)` | `IAsyncSubscriber<T>`, keyed overload takes `TKey key` first |
| `SubscribeAsync(Func<T, CancellationToken, UniTask>, CancellationToken)` | `IBufferedAsyncSubscriber<T>`, returns `UniTask<IDisposable>` |
| `FirstAsync(CancellationToken, [Func<T, bool> predicate], ...)` | sync, async, buffered, keyed (keyed takes `TKey key` first); returns `UniTask<T>` |
| `AsObservable(...)` | `ISubscriber<T>`, `IBufferedSubscriber<T>`, `ISubscriber<TKey, T>`; returns `System.IObservable<T>` |
| `AsAsyncEnumerable(...)` | `IAsyncSubscriber<T>`, `IBufferedAsyncSubscriber<T>`, `IAsyncSubscriber<TKey, T>`; returns `IUniTaskAsyncEnumerable<T>` |

## Filters

| Base class | Override | Attribute (per handler type) |
|---|---|---|
| `MessageHandlerFilter<T>` | `void Handle(T message, Action<T> next)` | `[MessageHandlerFilter(typeof(F<>), order)]` |
| `AsyncMessageHandlerFilter<T>` | `UniTask HandleAsync(T message, CancellationToken ct, Func<T, CancellationToken, UniTask> next)` | `[AsyncMessageHandlerFilter(type, order)]` |
| `RequestHandlerFilter<TReq, TRes>` | `TRes Invoke(TReq request, Func<TReq, TRes> next)` | `[RequestHandlerFilter(type, order)]` |
| `AsyncRequestHandlerFilter<TReq, TRes>` | `UniTask<TRes> InvokeAsync(TReq, CancellationToken, Func<TReq, CancellationToken, UniTask<TRes>> next)` | `[AsyncRequestHandlerFilter(type, order)]` |

Each filter has an `Order` property. Filters are applied from three places, sorted by order: global (`options.AddGlobal*Filter<T>(order)`), handler-type attribute, and per-subscription (`Subscribe(handler, new MyFilter<T> { Order = 10 })`). Filters are instantiated per subscription, so they may hold state. Register filter types with the container (`RegisterMessageHandlerFilter<T>()` etc.) when they have constructor dependencies.

## MessagePipeOptions

| Member | Default | Notes |
|---|---|---|
| `DefaultAsyncPublishStrategy` | `AsyncPublishStrategy.Parallel` | `Sequential` awaits handlers one by one |
| `InstanceLifetime` | `InstanceLifetime.Singleton` | `Scoped` gives one broker per DI scope; Zenject is always Scoped |
| `RequestHandlerLifetime` | `InstanceLifetime.Scoped` | `Singleton`, `Scoped`, `Transient` |
| `HandlingSubscribeDisposedPolicy` | `Ignore` | `Throw` raises when subscribing to a disposed broker |
| `EnableCaptureStackTrace` | `false` | Records a stack trace per subscribe for the diagnostics window |
| `EnableAutoRegistration`, `SetAutoRegistrationSearchAssemblies`, `SetAutoRegistrationSearchTypes` | n/a on Unity | compiled only for .NET |
| `AddGlobalMessageHandlerFilter<T>(int order = 0)` / `(Type, int)` | | also `AddGlobalAsyncMessageHandlerFilter`, `AddGlobalRequestHandlerFilter`, `AddGlobalAsyncRequestHandlerFilter` |

## DI registration per container

| Purpose | VContainer (`IContainerBuilder`) | Zenject (`DiContainer`) | Builtin (`BuiltinContainerBuilder`) |
|---|---|---|---|
| Core setup | `RegisterMessagePipe()` / `RegisterMessagePipe(Action<MessagePipeOptions>)` returns `MessagePipeOptions` | `BindMessagePipe(...)` returns `MessagePipeOptions` | `AddMessagePipe(...)` returns the builder |
| Keyless broker (sync, async, buffered, buffered async) | `RegisterMessageBroker<T>(options)` | `BindMessageBroker<T>(options)` | `AddMessageBroker<T>()` |
| Keyed broker (sync, async) | `RegisterMessageBroker<TKey, T>(options)` | `BindMessageBroker<TKey, T>(options)` | `AddMessageBroker<TKey, T>()` |
| Request handler | `RegisterRequestHandler<TReq, TRes, THandler>(options)` | `BindRequestHandler<...>(options)` | `AddRequestHandler<...>()` |
| Async request handler | `RegisterAsyncRequestHandler<TReq, TRes, THandler>(options)` | `BindAsyncRequestHandler<...>(options)` | `AddAsyncRequestHandler<...>()` |
| Filters | `RegisterMessageHandlerFilter<T>()`, `RegisterAsyncMessageHandlerFilter<T>()`, `RegisterRequestHandlerFilter<T>()`, `RegisterAsyncRequestHandlerFilter<T>()` | `Bind*Filter<T>()` | `Add*Filter<T>()` |
| Service provider for `GlobalMessagePipe` | `IObjectResolver.AsServiceProvider()` | `DiContainer.AsServiceProvider()` | `BuildServiceProvider()` |

`RegisterMessagePipe` also registers `MessagePipeDiagnosticsInfo`, `EventFactory` and `MessagePipeOptions` as singletons.

## GlobalMessagePipe (static)

`SetProvider(IServiceProvider)`, `IsInitialized`, `DiagnosticsInfo`, `GetPublisher<T>()`, `GetSubscriber<T>()`, `GetAsyncPublisher<T>()`, `GetAsyncSubscriber<T>()`, keyed `GetPublisher<TKey, T>()` etc., `GetRequestHandler<TReq, TRes>()`, `GetAsyncRequestHandler`, `GetRequestAllHandler`, `GetAsyncRequestAllHandler`, `GetBufferedPublisher<T>()`, `GetBufferedSubscriber<T>()`, `GetAsyncBufferedPublisher<T>()`, `GetAsyncBufferedSubscriber<T>()`, `CreateEvent<T>()`, `CreateAsyncEvent<T>()`, `CreateBufferedEvent<T>(T initialValue)`, `CreateBufferedAsyncEvent<T>(T initialValue)`.

## Disposables

| API | Use |
|---|---|
| `DisposableBag.Create(params IDisposable[])` | combine a fixed set |
| `DisposableBag.CreateBuilder()` / `CreateBuilder(int)` returning `DisposableBagBuilder` | `.AddTo(bag)` each subscription, then `bag.Build()`; `Add`, `Clear` |
| `DisposableBag.CreateSingleAssignment()` returning `SingleAssignmentDisposable` | assign with `.SetTo(d)` or `d.Disposable = ...` |
| `DisposableBag.CreateCancellation()` returning `CancellationTokenDisposable` | exposes `Token`, cancels on dispose |
| `DisposableBag.Empty` | no-op |

## Diagnostics

`MessagePipeDiagnosticsInfo` (injectable): `SubscribeCount`, `GetCapturedStackTraces(bool ascending = true)`, `GetGroupedByCaller(bool ascending = true)`. Editor window: `Window > MessagePipe Diagnostics` with toolbar toggles "Enable CaptureStackTrace", collapse, and auto reload.
