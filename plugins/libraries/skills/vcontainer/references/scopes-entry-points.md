# VContainer scopes, entry points, integrations and testing (1.19.0)

## LifetimeScope members (`VContainer.Unity`)

| Member | Notes |
|---|---|
| `protected virtual void Configure(IContainerBuilder builder)` | registrations |
| `public ParentReference parentReference` (serialized) | inspector "Parent": pick a parent scope type (`TypeName`) or set `parentReference.Object` in code |
| `public bool autoRun = true` | build in `Awake`; set false to call `Build()` yourself |
| `protected List<GameObject> autoInjectGameObjects` | inspector "Auto Inject Game Objects"; `InjectGameObject` runs on each after build |
| `IObjectResolver Container`, `LifetimeScope Parent`, `bool IsRoot` | |
| `void Build()` | builds the container (also builds a not-yet-built root parent) |
| `CreateChild(IInstaller installer = null, string childScopeName = null)`, `CreateChild(Action<IContainerBuilder> installation, string childScopeName = null)`, generic `CreateChild<TScope>(...)` | new child GameObject under this scope's transform |
| `CreateChildFromPrefab<TScope>(TScope prefab, IInstaller installer = null)` / `(TScope prefab, Action<IContainerBuilder>)` | instantiates a scope prefab as child |
| `void Dispose()` | disposes the container and destroys the scope GameObject |
| `protected virtual LifetimeScope FindParent()` | override to choose the parent in code |
| `static LifetimeScope Create(IInstaller installer = null, string name = null)` / `Create(Action<IContainerBuilder>, string name = null)` | creates a scope GameObject from code (tests, bootstrap) |
| `static ParentOverrideScope EnqueueParent(LifetimeScope parent)` | scopes built while the returned disposable is alive use `parent` |
| `static ExtraInstallationScope Enqueue(Action<IContainerBuilder>)` / `Enqueue(IInstaller)` | extra registrations for scopes built inside the block (`PushParent`/`Push` are obsolete aliases) |
| `static LifetimeScope Find<T>()`, `Find<T>(Scene scene)` | locate a scope instance |

`LifetimeScope` runs at `[DefaultExecutionOrder(-5000)]`.

### Parent resolution order (`GetRuntimeParent`)

1. The scope is the configured root (VContainerSettings) - no parent.
2. `parentReference.Object` (set by `CreateChild*`, or by code).
3. `FindParent()` override.
4. `parentReference.Type` from the inspector: searched with `FindAnyObjectByType`. If not built yet, the child waits and builds when the parent builds; if never found, `VContainerParentTypeReferenceNotFound` is thrown.
5. The innermost active `EnqueueParent` block.
6. The root scope from `VContainerSettings.RootLifetimeScope` (instantiated on demand, marked DontDestroyOnLoad by the settings).

So an inspector parent type wins over `EnqueueParent`.

### Additive scene as child

```csharp
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using VContainer.Unity;

public sealed class LevelLoader
{
    private readonly LifetimeScope _parent;

    public LevelLoader(LifetimeScope parent) => _parent = parent;

    public async UniTask LoadAsync(LevelConfig config)
    {
        using (LifetimeScope.EnqueueParent(_parent))
        using (LifetimeScope.Enqueue(builder => builder.RegisterInstance(config)))
        {
            await SceneManager.LoadSceneAsync("Level", LoadSceneMode.Additive);
        }
    }
}
```

### Runtime child scope

```csharp
var child = _scope.CreateChild(builder =>
{
    builder.RegisterInstance(extraAsset);
    builder.RegisterEntryPoint<BattleLoop>();
}, "Battle");

child.Dispose();
```

Entry points registered in the child start right after it is created. Keep the reference and `Dispose()` it to stop ticking and dispose its services.

### Project root scope

`Assets > Create > VContainer > VContainer Settings` creates the settings asset and adds it to Preloaded Assets. Assign a scope prefab to "Root Lifetime Scope". Every scope without another parent becomes its child. If settings do not load in a build, check Project Settings > Player > Preloaded Assets.

`VContainerSettings` fields: `RootLifetimeScope`, `EnableDiagnostics`, `DisableScriptModifier`, `RemoveClonePostfix`.

### Async / background build

Set `autoRun = false`, load the scene, then `await UniTask.Run(() => scope.Build());`. Unity-dependent registrations such as `RegisterComponentInHierarchy` must not run on a worker thread; collect scene references in an overridden `Awake` (call `base.Awake()`) and use `RegisterInstance` in `Configure`. The `VCONTAINER_PARALLEL_CONTAINER_BUILD` define builds registrations in parallel (only worth it for many registrations).

## Entry points (`VContainer.Unity`)

| Interface | Timing |
|---|---|
| `IInitializable.Initialize()` | right after the container is built |
| `IPostInitializable.PostInitialize()` | after all `Initialize` |
| `IStartable.Start()` | near `MonoBehaviour.Start` |
| `IAsyncStartable.StartAsync(CancellationToken)` | same timing; token cancels when the scope is destroyed |
| `IPostStartable.PostStart()` | after `Start` |
| `IFixedTickable.FixedTick()` / `IPostFixedTickable.PostFixedTick()` | around `FixedUpdate` |
| `ITickable.Tick()` / `IPostTickable.PostTick()` | around `Update` |
| `ILateTickable.LateTick()` / `IPostLateTickable.PostLateTick()` | around `LateUpdate` |
| `IDisposable.Dispose()` | when the container is disposed (Singleton/Scoped) |

`IAsyncStartable.StartAsync` returns `UniTask` when `com.cysharp.unitask` is installed (`VCONTAINER_UNITASK_INTEGRATION`), `UnityEngine.Awaitable` on Unity 2023.1+ otherwise. All `StartAsync` calls start together; later PlayerLoop phases do not wait for them. Exceptions in entry points are logged with `Debug.LogException` unless the scope registers `RegisterEntryPointExceptionHandler(ex => ...)`, which replaces the default logging. For async entry points UniTask's `UniTaskScheduler.UnobservedTaskException` also applies.

Entry points run on VContainer's own PlayerLoop systems, so they keep ticking regardless of any MonoBehaviour being enabled, and they work even when the scope is built late.

## Integrations

- **UniTask** (`unitask`): automatic `VCONTAINER_UNITASK_INTEGRATION` define; `IAsyncStartable` returns `UniTask`. Use `UniTask.Yield(PlayerLoopTiming.X)` inside `StartAsync` to pick a phase.
- **MessagePipe** (`messagepipe`): install `com.cysharp.messagepipe.vcontainer`, then in `Configure`:

```csharp
var options = builder.RegisterMessagePipe();
builder.RegisterBuildCallback(c => MessagePipe.GlobalMessagePipe.SetProvider(c.AsServiceProvider()));
builder.RegisterMessageBroker<ScoreChanged>(options);
```

With VContainer 1.14+ on Unity 2022.1+ MessagePipe resolves `IPublisher<T>` / `ISubscriber<T>` without `RegisterMessageBroker`, but request handlers still need `RegisterRequestHandler`.
- **R3 / UniRx** (`r3`): subscribe in `IStartable.Start`, collect disposables, dispose in `IDisposable.Dispose` of the entry point.
- **ECS (beta)**: with `com.unity.entities` installed (`VCONTAINER_ECS_INTEGRATION`): `RegisterSystemFromDefaultWorld<T>()` / `UseDefaultWorld(systems => systems.Add<T>())` for auto-bootstrapped systems (method injection only), `RegisterNewWorld(name, lifetime)` + `RegisterSystemIntoWorld<T>(name)` (`.IntoGroup<TGroup>()`) or `UseNewWorld(name, lifetime, systems => ...)` for custom worlds with constructor injection, plus `RegisterUnmanagedSystemFromDefaultWorld<T>()` / `RegisterUnmanagedSystemIntoWorld<T>(name)`. Resolve `World` or `IEnumerable<World>`.

## Diagnostics window

1. Create VContainerSettings and tick **Enable Diagnostics**.
2. `Window > VContainer Diagnostics` shows the dependency tree per scope, where each registration was made, and the resolved instance's fields (`ToString()`).
3. In code: `DiagnositcsContext.GetDiagnosticsInfos()` (class name spelled this way in 1.19.0 source; the docs write `DiagnosticsContext`) or `resolver.Diagnostics?.GetDiagnosticsInfos()`.

Diagnostics allocate heavily; disable for profiling and release builds.

## Source generator and stripping

- Default: reflection at build time, cached per type.
- Source generator (Unity 2021.3+): download `VContainer.SourceGenerator.dll` from the release matching your VContainer version, put it under `Assets/`, disable all platforms in its import settings, and give it the `RoslynAnalyzer` asset label. Assemblies referencing `VContainer` get generated injectors for types that have `[Inject]` or are used as a `Register*` type argument, excluding `[InjectIgnore]`. Not generated (falls back to reflection): nested classes, structs, private types.
- Generated types are `[Preserve]`d. For reflection-only types, mark the constructor `[Inject]` (`InjectAttribute` derives from `PreserveAttribute`) or add a `link.xml`.
- The old IL weaving (`VContainer.EnableCodeGen`) is deprecated.

## Testing

Edit Mode, no Unity objects:

```csharp
using NUnit.Framework;
using VContainer;

public sealed class ScoreServiceTests
{
    [Test]
    public void ResolvesWithFakeDependency()
    {
        var builder = new ContainerBuilder();
        builder.Register<IClock, FakeClock>(Lifetime.Singleton);
        builder.Register<ScoreService>(Lifetime.Singleton);

        using var container = builder.Build();
        var service = container.Resolve<ScoreService>();

        Assert.That(service, Is.Not.Null);
    }
}
```

Play Mode, with entry points:

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

public sealed class GameLoopTests
{
    [UnityTest]
    public IEnumerator StartsAndDisposes()
    {
        var scope = LifetimeScope.Create(builder =>
        {
            builder.RegisterEntryPoint<ScoreTicker>().AsSelf();
            builder.Register<ScoreService>(Lifetime.Singleton);
        });

        yield return null;

        var ticker = scope.Container.Resolve<ScoreTicker>();
        Assert.That(ticker.Started, Is.True);

        scope.Dispose();
    }
}
```

`ScoreTicker` is an `IStartable` that depends on `ScoreService` and sets `Started` in `Start()`. One frame (`yield return null`) is enough for `Initialize`/`Start`/`PostStart` to run.

Constructor-injected classes can also be tested with plain `new` and fakes, without any container.
