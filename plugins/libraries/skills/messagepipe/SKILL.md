---
name: messagepipe
description: Sets up and reviews Cysharp MessagePipe (com.cysharp.messagepipe) for DI-first pub/sub in Unity. Covers install via UPM git URLs (core, MessagePipe.VContainer, MessagePipe.Zenject) plus UniTask, RegisterMessagePipe / RegisterMessageBroker / RegisterRequestHandler (VContainer), BindMessagePipe / BindMessageBroker (Zenject), BuiltinContainerBuilder, GlobalMessagePipe.SetProvider, MessagePipeOptions, IPublisher / ISubscriber, keyed IPublisher<TKey, TMessage>, IBufferedPublisher, IAsyncPublisher / IAsyncSubscriber with AsyncPublishStrategy, IRequestHandler / IAsyncRequestHandler / IRequestAllHandler, EventFactory, MessageHandlerFilter, DisposableBag, AddTo(destroyCancellationToken), the MessagePipe Diagnostics window and EnableCaptureStackTrace. Use when the user mentions MessagePipe, IPublisher<T>, ISubscriber<T>, GlobalMessagePipe, RegisterMessageBroker, "No such registration of type: MessagePipe.IPublisher", subscription leaks, or event aggregator/mediator patterns.
license: MIT
metadata:
  category: libraries
  sources: "https://github.com/Cysharp/MessagePipe"
  unity: "6000.0+"
---

# MessagePipe

Typed, DI-managed pub/sub and mediator for Unity. Verified against MessagePipe 1.8.2 (`src/MessagePipe.Unity/Assets/Plugins/MessagePipe*`). The Unity build replaces every `ValueTask` in the .NET README with `UniTask`, has no open-generic auto registration (except through VContainer 1.14+, below), and needs every message type registered.

Reference files, read when needed:

- `references/api.md` - read when you need exact interface members, subscribe extension overloads, filter base classes, `MessagePipeOptions` defaults, or the full list of DI registration methods per container.
- `references/recipes.md` - read when writing a concrete setup: VContainer scope, Zenject installer, no-DI bootstrap, keyed/buffered/async pub-sub, request handlers, `EventFactory`, filters, or leak hunting.

Related skills: `vcontainer` (container and scopes), `unitask` (all async signatures here are UniTask), `r3` (reactive streams), `zbase-pubsub` (a DI-free alternative).

## Install

MessagePipe for Unity ships as UPM packages inside the repo. The NuGet package `MessagePipe` is for .NET servers only; do not import it into Unity.

1. Install UniTask first (see `unitask`). The `MessagePipe` asmdef references `UniTask` and `UniTask.Linq`, so it does not compile without it.
2. Add the core package from git URL, pinned to a release tag (tags are plain `x.y.z`):
   `https://github.com/Cysharp/MessagePipe.git?path=src/MessagePipe.Unity/Assets/Plugins/MessagePipe#1.8.2`
3. Add exactly one container integration:
   - VContainer: `https://github.com/Cysharp/MessagePipe.git?path=src/MessagePipe.Unity/Assets/Plugins/MessagePipe.VContainer#1.8.2`
   - Zenject/Extenject: `https://github.com/Cysharp/MessagePipe.git?path=src/MessagePipe.Unity/Assets/Plugins/MessagePipe.Zenject#1.8.2`
   - Neither: use the built-in `BuiltinContainerBuilder` from the core package.
4. OpenUPM also mirrors all three: `openupm add com.cysharp.messagepipe com.cysharp.messagepipe.vcontainer` (or `com.cysharp.messagepipe.zenject`). The README only documents git URLs and unitypackages.
5. Alternatively import `MessagePipe.1.8.2.unitypackage` (+ `MessagePipe.VContainer` / `MessagePipe.Zenject` unitypackages) from the GitHub releases page.
6. Optional: `MessagePipe.Analyzer.dll` from the releases page, labelled `RoslynAnalyzer`, flags `Subscribe` calls whose `IDisposable` is discarded.

Package ids: `com.cysharp.messagepipe`, `com.cysharp.messagepipe.vcontainer`, `com.cysharp.messagepipe.zenject`. Runtime asmdefs: `MessagePipe`, `MessagePipe.VContainer`, `MessagePipe.Zenject`.

## Workflow

1. Pick the container (VContainer recommended; see `vcontainer`).
2. In the composition root, call the setup method and keep the returned `MessagePipeOptions`:
   - VContainer: `var options = builder.RegisterMessagePipe(o => { ... });`
   - Zenject: `var options = Container.BindMessagePipe(o => { ... });`
   - No DI: `var b = new BuiltinContainerBuilder(); b.AddMessagePipe(o => { ... });`
3. Register every message type you publish:
   - VContainer: `builder.RegisterMessageBroker<TMessage>(options)` (registers sync, async, buffered and buffered-async for keyless) and `builder.RegisterMessageBroker<TKey, TMessage>(options)` (keyed sync + async).
   - Zenject: `BindMessageBroker<TMessage>(options)` / `BindMessageBroker<TKey, TMessage>(options)`.
   - Builtin: `AddMessageBroker<TMessage>()` / `AddMessageBroker<TKey, TMessage>()` (no options argument, despite the README sample).
   - Unity 2022.1+ (so all of Unity 6) with VContainer 1.14+: the `MESSAGEPIPE_OPENGENERICS_SUPPORT` define is set automatically and `IPublisher<>` / `ISubscriber<>` (and the `ISingleton*` / `IScoped*` variants) resolve without `RegisterMessageBroker`. Request handlers still need explicit registration.
4. Register handlers and filters: `RegisterRequestHandler<TReq, TRes, THandler>(options)`, `RegisterAsyncRequestHandler<...>(options)`, `RegisterMessageHandlerFilter<TFilter>()` (Zenject: `Bind*`; builtin: `Add*`).
5. Call `GlobalMessagePipe.SetProvider(...)` once after the container is built. It is required for the diagnostics window and for `GlobalMessagePipe.Get*` / `CreateEvent`:
   - VContainer: `builder.RegisterBuildCallback(c => GlobalMessagePipe.SetProvider(c.AsServiceProvider()));`
   - Zenject: `GlobalMessagePipe.SetProvider(Container.AsServiceProvider());`
   - Builtin: `GlobalMessagePipe.SetProvider(b.BuildServiceProvider());`
6. Inject `IPublisher<T>` / `ISubscriber<T>` (or async, keyed, buffered variants) into consumers. Store every `Subscribe` result and dispose it with the owner.
7. During development, open `Window > MessagePipe Diagnostics`, toggle "Enable CaptureStackTrace", exercise the scene, and verify the subscribe count returns to baseline after unloading.

## Choosing the interface

| Need | Inject |
|---|---|
| Fire event, sync handlers | `IPublisher<T>` / `ISubscriber<T>` |
| Per-entity/topic channel | `IPublisher<TKey, T>` / `ISubscriber<TKey, T>` |
| Late subscribers get the latest value (BehaviorSubject-like) | `IBufferedPublisher<T>` / `IBufferedSubscriber<T>` (keyless only; a null class value is not replayed) |
| Await all handlers | `IAsyncPublisher<T>.PublishAsync(msg, AsyncPublishStrategy.Sequential or Parallel, ct)` |
| One handler returns a result (mediator) | `IRequestHandler<TReq, TRes>` / `IAsyncRequestHandler<TReq, TRes>` |
| All handlers return results | `IRequestAllHandler<TReq, TRes>` / `IAsyncRequestAllHandler<TReq, TRes>` |
| Instance-owned event, like a C# `event` | `EventFactory.CreateEvent<T>()` returning `(IDisposablePublisher<T>, ISubscriber<T>)` |

`IAsyncPublisher.Publish` (non-async) is fire-and-forget. `PublishAsync` without a strategy uses `MessagePipeOptions.DefaultAsyncPublishStrategy` (default `Parallel`, implemented with WhenAll). There is no `OnCompleted`/`OnError`: publish a separate message to signal completion.

## Subscription lifetime

`Subscribe` returns `IDisposable`; a discarded result is a leak. MessagePipe has no weak references. Pick one owner per subscription:

```csharp
using System;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using VContainer;

public sealed class ScoreView : MonoBehaviour
{
    [Inject]
    private void Construct(ISubscriber<ScoreChanged> scoreChanged, IAsyncSubscriber<LevelLoaded> levelLoaded)
    {
        var bag = DisposableBag.CreateBuilder();
        scoreChanged.Subscribe(OnScoreChanged).AddTo(bag);
        levelLoaded.Subscribe(OnLevelLoadedAsync).AddTo(bag);
        bag.Build().AddTo(destroyCancellationToken);
    }

    private void OnScoreChanged(ScoreChanged message) { }

    private UniTask OnLevelLoadedAsync(LevelLoaded message, System.Threading.CancellationToken ct) => UniTask.CompletedTask;
}
```

- `DisposableBag.CreateBuilder()` + `.AddTo(bag)` + `bag.Build()` composes many subscriptions; `DisposableBag.Create(d1, d2, ...)` does the same for a fixed set.
- `.AddTo(CancellationToken)` is UniTask's extension (`Cysharp.Threading.Tasks.CancellationTokenExtensions.AddTo`), not MessagePipe's. With `MonoBehaviour.destroyCancellationToken` it disposes on `OnDestroy`.
- `DisposableBag.CreateSingleAssignment()` + `.SetTo(d)` lets a handler dispose its own subscription (one-shot). For "first value only", prefer `await subscriber.FirstAsync(ct)`.
- Plain C# services resolved by VContainer: implement `IDisposable` and dispose the bag there; the container disposes Singleton/Scoped instances with the scope.
- With `InstanceLifetime.Scoped`, disposing the DI scope unsubscribes everything that broker owns.

## Rules

- Register every message type for Zenject and the builtin container, and for VContainer when not relying on open generics. Missing registration throws at resolve time ("VContainerException: No such registration of type: MessagePipe.IPublisher`1[...]"), not at compile time.
- Keep the options object from `RegisterMessagePipe` / `BindMessagePipe` and pass the same instance to every `Register*`/`Bind*` call. Global filters must be added to it via `options.AddGlobalMessageHandlerFilter<MyFilter<T>>()` per closed type; Unity cannot register open-generic global filters.
- Zenject: `InstanceLifetime.Singleton` is not supported; the lifetime is always Scoped.
- `BuiltinContainerBuilder` is always singleton, has no scopes and no `IRequestAllHandler` / `IAsyncRequestAllHandler`. Use it through `GlobalMessagePipe`.
- An exception in a sync handler propagates to the publisher and stops later handlers. Wrap with a filter (`MessageHandlerFilter<T>` with try/catch) if handlers must be isolated.
- Prefer `readonly struct` messages to avoid allocation per publish.
- Enable `EnableCaptureStackTrace` only in development builds; it captures a `StackTrace` per subscribe.
- `EnableAutoRegistration` and `SetAutoRegistrationSearch*` are .NET-only; they are compiled out on Unity.

## Pitfalls

- **Analyzer not running**: Unity's Roslyn analyzer support needs the DLL labelled `RoslynAnalyzer`; the README also points to Cysharp/CsprojModifier for IDE-side analysis.
- **Diagnostics window empty**: `GlobalMessagePipe.SetProvider` was never called, or was called with a different container than the one that resolved the publishers.
- **IL2CPP / AOT**: MessagePipe resolves closed generic broker types at runtime. Explicit `RegisterMessageBroker<T>` per type keeps the generic instantiations visible to IL2CPP. With VContainer open-generic resolution on Unity 6 (IL2CPP full generic sharing) it works, but keep the explicit registrations if a target still fails with `ExecutionEngineException` or a missing-method error, and keep handler/filter types from being stripped (they are resolved by reflection; VContainer's `[Inject]` constructors are preserved, plain ones may need `link.xml` or `[UnityEngine.Scripting.Preserve]`).
- **Name clash with R3**: both libraries define `DisposableBag` (MessagePipe: static class with `CreateBuilder`; R3: a struct used as `AddTo(ref bag)`). In files that import both namespaces, alias one (`using MpBag = MessagePipe.DisposableBag;`).
- **Subscribing after scope disposal** returns an empty disposable silently (`HandlingSubscribeDisposedPolicy.Ignore`). Set `Throw` while debugging lifetime bugs.
- **`FirstAsync` without a token** cannot be cancelled; always pass a token (`destroyCancellationToken` or a timeout via UniTask's `CancelAfterSlim`).

## MessagePipe vs R3 vs UniTask

- MessagePipe: decoupled, type-keyed broadcast between systems that do not know each other, owned by the DI container. Use for game-wide events, commands and request/response.
- R3 (`r3`): composition of event streams (throttle, combine, switch), UI binding, `ReactiveProperty`. `ISubscriber<T>.AsObservable()` returns `System.IObservable<T>`, which R3 consumes via its `ToObservable()` conversion; use that bridge when a MessagePipe event needs operators.
- UniTask (`unitask`): one-shot async flow. MessagePipe's async handlers, `FirstAsync`, and `AsAsyncEnumerable` (returns `IUniTaskAsyncEnumerable<T>`) are UniTask-based, so `await foreach` style consumption uses UniTask.Linq.
