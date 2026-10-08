# MessagePipe recipes (Unity)

## VContainer root scope

```csharp
using MessagePipe;
using VContainer;
using VContainer.Unity;

public readonly struct ScoreChanged
{
    public readonly int Value;
    public ScoreChanged(int value) => Value = value;
}

public readonly struct DamageTaken
{
    public readonly float Amount;
    public DamageTaken(float amount) => Amount = amount;
}

public sealed class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        var options = builder.RegisterMessagePipe(o =>
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            o.EnableCaptureStackTrace = true;
#endif
        });

        builder.RegisterBuildCallback(c => GlobalMessagePipe.SetProvider(c.AsServiceProvider()));

        builder.RegisterMessageBroker<ScoreChanged>(options);
        builder.RegisterMessageBroker<int, DamageTaken>(options);
        builder.RegisterRequestHandler<PingRequest, PingResponse, PingHandler>(options);

        builder.RegisterEntryPoint<ScorePresenter>();
    }
}
```

Keyed publish/subscribe (`int` = entity id):

```csharp
public sealed class HealthSystem : System.IDisposable
{
    private readonly System.IDisposable _subscription;

    public HealthSystem(ISubscriber<int, DamageTaken> damage)
    {
        _subscription = damage.Subscribe(42, OnDamage);
    }

    private void OnDamage(DamageTaken message) { }

    public void Dispose() => _subscription.Dispose();
}
```

## Plain C# entry point that owns subscriptions

```csharp
using System;
using MessagePipe;
using VContainer.Unity;

public sealed class ScorePresenter : IStartable, IDisposable
{
    private readonly ISubscriber<ScoreChanged> _scoreChanged;
    private IDisposable _subscriptions;

    public ScorePresenter(ISubscriber<ScoreChanged> scoreChanged) => _scoreChanged = scoreChanged;

    public void Start()
    {
        var bag = DisposableBag.CreateBuilder();
        _scoreChanged.Subscribe(OnScoreChanged).AddTo(bag);
        _subscriptions = bag.Build();
    }

    private void OnScoreChanged(ScoreChanged message) { }

    public void Dispose() => _subscriptions?.Dispose();
}
```

## Zenject installer

```csharp
using MessagePipe;
using Zenject;

public sealed class MessagingInstaller : MonoInstaller
{
    public override void InstallBindings()
    {
        var options = Container.BindMessagePipe();
        Container.BindMessageBroker<ScoreChanged>(options);
        GlobalMessagePipe.SetProvider(Container.AsServiceProvider());
    }
}
```

## No DI container

```csharp
using MessagePipe;
using UnityEngine;

public static class MessagingBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        var builder = new BuiltinContainerBuilder();
        builder.AddMessagePipe();
        builder.AddMessageBroker<ScoreChanged>();
        GlobalMessagePipe.SetProvider(builder.BuildServiceProvider());
    }
}
```

Consumers call `GlobalMessagePipe.GetPublisher<ScoreChanged>()` / `GetSubscriber<ScoreChanged>()`. Fast enter-play-mode (domain reload off) re-runs `BeforeSceneLoad`, which replaces the provider, so stale subscriptions on the old provider simply stop receiving.

## Async publish and await all handlers

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using MessagePipe;

public sealed class LevelFlow
{
    private readonly IAsyncPublisher<LevelLoaded> _levelLoaded;

    public LevelFlow(IAsyncPublisher<LevelLoaded> levelLoaded) => _levelLoaded = levelLoaded;

    public UniTask NotifyAsync(CancellationToken ct) =>
        _levelLoaded.PublishAsync(new LevelLoaded(), AsyncPublishStrategy.Sequential, ct);
}
```

Subscriber side: `asyncSubscriber.Subscribe(async (msg, ct) => { await UniTask.Delay(100, cancellationToken: ct); })`.

## Request/response

```csharp
using MessagePipe;

public readonly struct PingRequest { }
public readonly struct PingResponse { }

public sealed class PingHandler : IRequestHandler<PingRequest, PingResponse>
{
    public PingResponse Invoke(PingRequest request) => new PingResponse();
}
```

Inject `IRequestHandler<PingRequest, PingResponse>` and call `Invoke(new PingRequest())`. Register additional handlers for the same pair and inject `IRequestAllHandler<PingRequest, PingResponse>` to call `InvokeAll`. With `RequestHandlerLifetime = Scoped` (default), handlers resolved from a child scope are new per scope.

## EventFactory (instance-owned events)

```csharp
using System;
using MessagePipe;

public sealed class Timer : IDisposable
{
    private readonly IDisposablePublisher<int> _tick;
    public ISubscriber<int> OnTick { get; }

    public Timer(EventFactory events) => (_tick, OnTick) = events.CreateEvent<int>();

    public void Raise(int value) => _tick.Publish(value);

    public void Dispose() => _tick.Dispose();
}
```

Disposing the publisher unsubscribes every listener. Outside DI use `GlobalMessagePipe.CreateEvent<int>()`.

## Error-isolating filter

```csharp
using System;
using MessagePipe;
using UnityEngine;

public sealed class LogErrorFilter<T> : MessageHandlerFilter<T>
{
    public override void Handle(T message, Action<T> next)
    {
        try
        {
            next(message);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }
}
```

Apply globally per type (Unity has no open-generic global filters): `options.AddGlobalMessageHandlerFilter<LogErrorFilter<ScoreChanged>>(-1000);`. A helper that wraps `RegisterMessageBroker<T>` and the filter registration keeps this in one place.

## Hunting subscription leaks

1. Enable `EnableCaptureStackTrace` (option or the toggle in the diagnostics window).
2. Open `Window > MessagePipe Diagnostics`, enable auto reload.
3. Load and unload the suspect scene a few times; the count should return to baseline.
4. Rows whose count grows show the subscribe call site; that site discarded or never disposed its `IDisposable`.
5. In code, `MessagePipeDiagnosticsInfo.SubscribeCount` can be asserted in a PlayMode test after unloading.
