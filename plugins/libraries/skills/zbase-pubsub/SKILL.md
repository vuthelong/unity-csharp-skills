---
name: zbase-pubsub
description: Sets up and reviews Zitga-Tech ZBase.Foundation.PubSub (com.zbase.foundation.pubsub, namespace ZBase.Foundation.PubSub), a DI-free, UniTask-based messenger for Unity. Covers install via OpenUPM and its dependencies (UniTask, com.zbase.foundation, com.zbase.collections.pooled, System.Runtime.CompilerServices.Unsafe), the Messenger object with MessagePublisher / MessageSubscriber / AnonPublisher / AnonSubscriber, scopes (Global(), Scope<TScope>(), UnityScope(obj), GlobalScope), IMessage and ZBASE_FOUNDATION_PUBSUB_RELAX_MODE, sync and async (Func<..., UniTask>) handlers, handler order, Publish vs PublishAsync, PublishWithContext / PublishingContext / CallerInfo, CachedPublisher, WithState, ISubscription and unsubscribeToken, Compress, and DISABLE_ZBASE_PUBSUB_DEBUG. Use when the user mentions ZBase PubSub, Messenger.MessagePublisher, MessageSubscriber.Global(), ISubscription, IMessage constraint errors, "Found no subscription for", or compares it with MessagePipe.
license: MIT
metadata:
  category: libraries
  sources: "https://github.com/Zitga-Tech/ZBase.Foundation.PubSub"
  unity: "6000.0+"
---

# ZBase.Foundation.PubSub

A plain-object messenger: create a `Messenger`, publish typed messages into scopes, subscribe sync or UniTask handlers. No DI container is involved. Verified against the repo HEAD (package.json 1.5.1, June 2024; OpenUPM serves 1.5.2), runtime under `Packages/ZBase.Foundation.PubSub/ZBase.Foundation.PubSub`, samples under `Samples~`.

Reference file, read when needed:

- `references/api.md` - read when you need every `Subscribe` overload (stateless, stateful, contextual), the publisher method list, `CachedPublisher`, or the scripting define symbols.

Related skills: `messagepipe` (DI-first alternative, compared below), `unitask` (handler and publish signatures are UniTask), `r3` (stream operators), `vcontainer` (to share a `Messenger` instance through DI).

## Install

The package depends on OpenUPM and Unity NuGet packages, so install through OpenUPM so the dependencies resolve:

```
openupm add com.zbase.foundation.pubsub
```

This pulls `com.cysharp.unitask`, `com.zbase.collections.pooled`, `com.zbase.foundation` and `org.nuget.system.runtime.compilerservices.unsafe`. Without openupm-cli, add a scoped registry `https://package.openupm.com` with scopes `com.zbase`, `com.cysharp`, `org.nuget` to `Packages/manifest.json`, then add `"com.zbase.foundation.pubsub": "1.5.2"`.

A git URL install (`https://github.com/Zitga-Tech/ZBase.Foundation.PubSub.git?path=Packages/ZBase.Foundation.PubSub`) works only if that scoped registry is already present, because UPM cannot resolve the dependencies from git. The repo's own README documents only the OpenUPM dependency list.

Reference asmdef `ZBase.Foundation.PubSub` (it references `UniTask`, `ZBase.Foundation`, `ZBase.Collections.Pooled`). Import the "Samples" sample from Package Manager for `BasicUsage` and `SpecificScopeUsage`.

## Message model

- Messages implement `IMessage` (strict mode, default). Prefer `struct` messages. Add the define `ZBASE_FOUNDATION_PUBSUB_RELAX_MODE` (Player > Scripting Define Symbols) to allow any type.
- `Publish<TMessage>()` without an argument requires `new()` and publishes `new TMessage()`.
- `AnonPublisher` / `AnonSubscriber` publish and handle a payload-less signal (internally `AnonMessage`). Use them for "something happened" events with no data.
- No source generators and no attributes are involved.

## Scopes

A scope partitions subscribers; publishing in one scope never reaches another. Obtain a publisher/subscriber view (cheap readonly structs) from the `Messenger` properties each time, or cache it in a field:

| Call | Scope |
|---|---|
| `.Global()` | `GlobalScope` (all instances equal) |
| `.Scope<TScope>()` | `default(TScope)`, `TScope : struct` |
| `.Scope(scopeValue)` | any value, compared with `Equals`/`GetHashCode`, so implement `IEquatable<T>` on custom scope structs |
| `.UnityScope(unityObject)` | a `UnityObjectRef<T>` keyed by the object's instance id |

## Workflow

1. Define messages: `public struct EnemyKilled : IMessage { public int Score; }`.
2. Own one `Messenger` per lifetime you care about (one for the app, or one per feature). It is `IDisposable`; disposing it drops every broker and subscription.
3. Subscribe and keep the `ISubscription` (or pass an `unsubscribeToken`):

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using ZBase.Foundation.PubSub;

public struct EnemyKilled : IMessage
{
    public int Score;
}

public sealed class ScoreBoard : MonoBehaviour
{
    [SerializeField] private int _score;

    private void Start()
    {
        var subscriber = GameMessenger.Instance.MessageSubscriber.Global();
        subscriber.Subscribe<EnemyKilled>(OnEnemyKilled, destroyCancellationToken);
        subscriber.Subscribe<EnemyKilled>(PlayKillFxAsync, destroyCancellationToken, order: 10);
    }

    private void OnEnemyKilled(EnemyKilled message) => _score += message.Score;

    private async UniTask PlayKillFxAsync(EnemyKilled message, CancellationToken token)
    {
        await UniTask.Delay(200, cancellationToken: token);
    }
}

public static class GameMessenger
{
    public static readonly Messenger Instance = new();
}
```

4. Publish:
   - `messenger.MessagePublisher.Global().Publish(new EnemyKilled { Score = 10 });` fire-and-forget.
   - `await messenger.MessagePublisher.Global().PublishAsync(new EnemyKilled { Score = 10 }, token);` waits for every handler, including async ones.
   - Hot path: `var cached = messenger.MessagePublisher.GlobalCache<EnemyKilled>();` once, then `cached.Publish(msg)`; `Dispose()` the `CachedPublisher` when done.
5. Unsubscribe with `subscription.Dispose()` / `subscription.Unsubscribe()`, or by cancelling the token passed as `unsubscribeToken` (those overloads return `void`).
6. After many unsubscribes, call `subscriber.Compress<TMessage>()` to drop empty order groups (no-op while a `CachedPublisher` for that scope is alive).

## Dispatch semantics (from `MessageBroker<TMessage>`)

- Handlers are grouped by the `order` argument. Groups run from the highest `order` value to the lowest; each group is awaited before the next starts. Within a group, all handlers are started together and awaited with `UniTask.WhenAll`, so their relative order is not guaranteed.
- Sync handlers (`Action<T>`) run inline. `Publish` (non-async) calls `PublishAsync(...).Forget()`: it runs synchronously until the first group containing a handler that does not complete immediately, then the remaining groups continue later. Do not rely on lower-order handlers having run when `Publish` returns if any higher-order handler is async.
- The `CancellationToken` passed to `Publish`/`PublishAsync` is checked between groups and passed into token-aware handlers.
- With no subscriber for the scope/type, editor and `DEBUG` builds log "Found no subscription for `T` in scope `S`" as a warning; release builds do nothing.
- Error handling differs per build. In the editor and `DEBUG` builds (unless `DISABLE_ZBASE_PUBSUB_DEBUG` is defined), each handler call is wrapped in try/catch and exceptions are logged through `ILogger` (default: `UnityEngine.Debug`). In release builds there is no try/catch: an exception stops the remaining handlers and surfaces as an unobserved UniTask exception (`UniTaskScheduler.UnobservedTaskException`) for `Publish`, or is thrown from the awaited `PublishAsync`.
- Handler identity is derived from the delegate (`HandlerId`: method handle + delegate hash + state hash). Subscribing an identical delegate twice in the same scope and order is ignored; the second call returns a no-op subscription.

## Rules

- Keep the `Messenger` reachable for as long as anyone publishes or subscribes. Two `new Messenger()` instances never see each other's messages.
- Always tie subscriptions to a lifetime: store the `ISubscription` and dispose it in `OnDestroy`/`Dispose`, or pass `destroyCancellationToken` as `unsubscribeToken`. Handlers that capture `this` keep MonoBehaviours alive and run on destroyed objects otherwise.
- Do not assume `UnityScope(obj)` cleans up when `obj` is destroyed; nothing listens for destruction. Unsubscribe explicitly.
- Prefer `subscriber.WithState(state)` overloads with `static` lambdas to avoid closure allocation: `subscriber.WithState(this).Subscribe<EnemyKilled>(static (self, msg) => self.OnEnemyKilled(msg));`.
- Use `PublishWithContext` only when handlers need the caller location (`PublishingContext.Caller` with `CallerMemberName`, `CallerFilePath`, `CallerLineNumber`); it captures caller info via compiler attributes.

## Pitfalls

- **Unity 6000.5+ compile error**: `UnityObjectRef<T>` calls `Object.GetInstanceID()` and `Resources.InstanceIDToObject`. Per repo guidance `GetInstanceID()` becomes CS0619 on 6000.5+ (use `GetEntityId()`). The package has no fix at HEAD; embed it (copy into `Packages/`) and patch `UnityObjectRef<T>` with `#if UNITY_6000_4_OR_NEWER` branches, or avoid `UnityScope` and use your own scope struct.
- **Samples do not compile as-is**: `BasicUsage` refers to `PublishContext` (the type is `PublishingContext`), uses `Input.GetKeyUp` (fails when only the Input System is enabled), and `TMPro`. Treat samples as reading material.
- **"must be retrieved via MessageSubscriber.Scope API"**: a `default(Subscriber<T>)` struct was used. Always obtain views from `messenger.MessageSubscriber` / `MessagePublisher`.
- **Constraint errors (CS0315/CS0311)**: the message type lacks `IMessage`, or `Publish<T>()` without argument is used on a type without a parameterless constructor.
- **Domain reload disabled**: static `Messenger` fields survive Enter Play Mode; reset them with `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` or dispose them on exit.
- **Thread safety**: brokers lock internally, but handlers touching Unity APIs must still run on the main thread.

## ZBase PubSub vs MessagePipe

| | ZBase.Foundation.PubSub | MessagePipe (`messagepipe`) |
|---|---|---|
| Wiring | `new Messenger()`, no DI | DI container (VContainer, Zenject, builtin) and `GlobalMessagePipe` |
| Registration | none; brokers created lazily on first subscribe | `RegisterMessageBroker<T>` per type (unless VContainer open generics) |
| Routing | scope objects (`Global`, value scopes, Unity object scopes) | keyless or keyed `IPublisher<TKey, T>` |
| Handlers | one API for sync and `UniTask` handlers, priority `order` groups | separate sync and async interfaces; async uses `AsyncPublishStrategy` |
| Message constraint | `IMessage` (unless relax mode) | any type |
| Extras | `PublishingContext` caller info, `AnonPublisher`, `CachedPublisher` | buffered pub/sub, request/response, filters, diagnostics window, analyzer |
| Errors | logged per handler in editor/DEBUG, unguarded in release | propagate to publisher unless a filter catches them |

Pick ZBase PubSub for small projects or modules without a DI container that want ordered async handlers. Pick MessagePipe when the project already uses VContainer/Zenject, needs request/response, filters, or the leak diagnostics window.
