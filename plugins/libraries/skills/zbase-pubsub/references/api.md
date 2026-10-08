# ZBase.Foundation.PubSub API reference

Namespace `ZBase.Foundation.PubSub`. `T` below is the message type (`where T : IMessage` unless `ZBASE_FOUNDATION_PUBSUB_RELAX_MODE`).

## Messenger

```csharp
public sealed class Messenger : IDisposable
{
    public MessageSubscriber MessageSubscriber { get; }
    public MessagePublisher MessagePublisher { get; }
    public AnonSubscriber AnonSubscriber { get; }
    public AnonPublisher AnonPublisher { get; }
    public void Dispose();
}
```

## Getting scoped views

| `MessagePublisher` | `MessageSubscriber` | Returns |
|---|---|---|
| `Global()` | `Global()` | `Publisher<GlobalScope>` / `Subscriber<GlobalScope>` |
| `Scope<TScope>()` (`TScope : struct`) | `Scope<TScope>()` | view for `default(TScope)` |
| `Scope<TScope>(TScope scope)` | `Scope<TScope>(TScope scope)` | view for that scope value |
| `UnityScope<TScope>(TScope obj)` (`TScope : UnityEngine.Object`) | `UnityScope<TScope>(TScope obj)` | `UnityPublisher<TScope>` / `UnitySubscriber<TScope>` keyed by `UnityObjectRef<TScope>` |
| `GlobalCache<T>(ILogger = null)` | | `CachedPublisher<T>` for the global scope |
| `Cache<TScope, T>(ILogger = null)` / `Cache<TScope, T>(TScope scope, ILogger = null)` | | `CachedPublisher<T>` |
| `UnityCache<TScope, T>(TScope obj, ILogger = null)` | | `CachedPublisher<T>` |

`AnonPublisher` and `AnonSubscriber` expose the same scope methods (`Global`, `Scope`, `UnityScope`, plus `GlobalCache`, `Cache`, `UnityCache` on the publisher) with no `T`.

Each view has `IsValid` and `Scope`. Views are readonly structs; `default` views are invalid.

## Publisher<TScope> / UnityPublisher<TScope> methods

| Method | Notes |
|---|---|
| `void Publish<T>(CancellationToken token = default, ILogger logger = null)` | publishes `new T()`; needs `T : new()` |
| `void Publish<T>(T message, CancellationToken token = default, ILogger logger = null)` | fire-and-forget (`PublishAsync(...).Forget()`) |
| `UniTask PublishAsync<T>(CancellationToken token = default, ILogger logger = null)` | |
| `UniTask PublishAsync<T>(T message, CancellationToken token = default, ILogger logger = null)` | completes when all handler groups finish |
| `void PublishWithContext<T>(...)` / `UniTask PublishWithContextAsync<T>(...)` | same shapes plus trailing `[CallerLineNumber]`, `[CallerMemberName]`, `[CallerFilePath]` parameters; handlers receive `PublishingContext` |
| `CachedPublisher<T> Cache<T>(ILogger logger = null)` | |

`AnonPublisher.Publisher<TScope>` has `Publish(token, logger)`, `PublishAsync(token, logger)`, `PublishWithContext(...)`, `PublishWithContextAsync(...)`, `Cache(logger)`.

## CachedPublisher<T> (struct, IDisposable)

Holds the broker for one scope so publishing skips the lookup. Members: `IsValid`, `Publish(...)`, `PublishAsync(...)`, `PublishWithContext(...)`, `PublishWithContextAsync(...)` (same parameter shapes as above, without `T` type argument), `Dispose()` (releases the cache reference so `Compress` can remove the broker). It is a mutable struct: store it in a field and do not copy it around before disposing.

## Subscriber<TScope> / UnitySubscriber<TScope> handler overloads

Every overload takes `int order = 0, ILogger logger = null` after the handler. Two families:

- returns `ISubscription`: `Subscribe<T>(handler, int order = 0, ILogger logger = null)`
- returns `void`: `Subscribe<T>(handler, CancellationToken unsubscribeToken, int order = 0, ILogger logger = null)`; cancelling the token unsubscribes.

Accepted handler types:

| Stateless | Contextual |
|---|---|
| `Action` | `Action<PublishingContext>` |
| `Action<T>` | `Action<T, PublishingContext>` |
| `Func<UniTask>` | `Func<PublishingContext, UniTask>` |
| `Func<T, UniTask>` | `Func<T, PublishingContext, UniTask>` |
| `Func<CancellationToken, UniTask>` | `Func<PublishingContext, CancellationToken, UniTask>` |
| `Func<T, CancellationToken, UniTask>` | `Func<T, PublishingContext, CancellationToken, UniTask>` |

Parameterless handlers still need the type argument: `subscriber.Subscribe<LevelStarted>(OnLevelStarted)`.

`Compress<T>(ILogger logger = null)` removes empty order groups and drops the scope's broker when empty and not cached.

### Stateful subscribers

`MessageSubscriberExtensions.WithState<TScope, TState>(this MessageSubscriber.Subscriber<TScope>, TState state) where TState : class` returns `Subscriber<TScope, TState>` whose overloads prepend `TState` to each handler type above (`Action<TState>`, `Action<TState, T>`, `Func<TState, T, CancellationToken, UniTask>`, `Action<TState, T, PublishingContext>`, ...). Use with `static` lambdas to avoid captures. `AnonSubscriber` and the Unity subscribers have the same `WithState` pattern.

### AnonSubscriber.Subscriber<TScope>

Same as above without `T`: `Subscribe(Action handler, ...)`, `Subscribe(Func<CancellationToken, UniTask> handler, ...)`, contextual variants, `Compress(ILogger)`.

## ISubscription

```csharp
public interface ISubscription : IDisposable
{
    void Unsubscribe() => Dispose();
}
```

Disposing is idempotent. A failed subscribe (invalid view, duplicate delegate) returns an inert subscription.

## Context types

- `PublishingContext` (readonly struct): `CallerInfo Caller`.
- `CallerInfo` (readonly struct): `CallerMemberName`, `CallerFilePath`, `CallerLineNumber`, `IsCreated` (false when the message was published without context).
- `GlobalScope` (readonly struct): all instances are equal.
- `UnityObjectRef<T>`: `IsCreated`, `InstanceId`, `ToObject()`; implicit conversion from `UnityEngine.Object`.

## ILogger

`ZBase.Foundation.PubSub.ILogger` has default interface methods `LogWarning(string)`, `LogError(string)`, `LogException(Exception)` that forward to `UnityEngine.Debug` in the editor and `DEBUG` builds. Pass a custom implementation per call to route logs elsewhere. Its name collides with `Microsoft.Extensions.Logging.ILogger` and `UnityEngine.ILogger`; alias it when both namespaces are imported.

## Scripting define symbols

| Symbol | Effect |
|---|---|
| `ZBASE_FOUNDATION_PUBSUB_RELAX_MODE` | drops the `IMessage` constraint |
| `DISABLE_ZBASE_PUBSUB_DEBUG` | removes validation, warnings and per-handler try/catch even in the editor/DEBUG |

Validation (null checks, "Found no subscription" warnings, exception logging) is active when `UNITY_EDITOR || DEBUG` and `DISABLE_ZBASE_PUBSUB_DEBUG` is not defined.
