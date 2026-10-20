---
name: vcontainer
description: "Sets up and reviews hadashiA VContainer dependency injection in Unity: LifetimeScope hierarchies, lifetimes, interface and keyed registration, factories, component registration, entry points (IStartable, ITickable, IAsyncStartable), scene and prefab scopes, IL2CPP stripping. Use when composing game services with DI or debugging 'No such registration of type', 'Circular dependency detected', null [Inject] fields in Awake, or parent scope not found when loading scenes."
license: MIT
metadata:
  category: libraries
  sources: "https://github.com/hadashiA/VContainer"
  unity: "6000.0+"
---

# VContainer

Constructor-injection DI container with Unity scene/prefab scopes and PlayerLoop entry points. Verified against VContainer 1.19.0 (`VContainer/Assets/VContainer`) and the docs in `website/docs`.

Reference files, read when needed:

- `references/registration.md` - read when you need an exact `Register*` overload, component registration options, factories, keys, parameters, open generics, collections, or injection rules.
- `references/scopes-entry-points.md` - read when wiring parent/child scopes, additive scenes, runtime child scopes, entry point timing, async startup, exception handlers, integrations (UniTask, MessagePipe, ECS), diagnostics, source generator, or tests.

Related skills: `messagepipe` (pub/sub on top of VContainer), `unitask` (`IAsyncStartable` returns UniTask when installed), `r3` (dispose subscriptions in entry points), `zlogger` (register `ILoggerFactory` / `ILogger<T>`), `csharp-unity` (general architecture and code standards).

## Install

- Git URL (pin a release tag; tags are plain `x.y.z`): add to `Packages/manifest.json`
  `"jp.hadashikick.vcontainer": "https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer#1.19.0"`
- OpenUPM: `openupm add jp.hadashikick.vcontainer` (or scoped registry `https://package.openupm.com` with scope `jp.hadashikick.vcontainer`).
- Or the `.unitypackage` from the releases page.

Reference asmdef `VContainer`. `using VContainer;` for `IContainerBuilder`, `Lifetime`, `[Inject]`, `[Key]`, `IObjectResolver`; `using VContainer.Unity;` for `LifetimeScope`, entry point interfaces and Unity extensions.

## Workflow

1. Create a scope: `public class GameLifetimeScope : LifetimeScope { protected override void Configure(IContainerBuilder builder) { ... } }`. Add it to a GameObject in the scene.
2. Register services in `Configure`:

```csharp
using UnityEngine;
using VContainer;
using VContainer.Unity;

public sealed class GameLifetimeScope : LifetimeScope
{
    [SerializeField] private GameSettings _settings;
    [SerializeField] private HudView _hud;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterInstance(_settings);
        builder.Register<ScoreService>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
        builder.Register<IEnemyFactory, EnemyFactory>(Lifetime.Scoped);
        builder.RegisterComponent(_hud);
        builder.RegisterComponentInHierarchy<PlayerController>();
        builder.RegisterEntryPoint<GameLoop>();
    }
}
```

3. Write consumers with constructor injection (plain C#) or an `[Inject]` method (MonoBehaviours):

```csharp
using System;
using VContainer;
using VContainer.Unity;

public sealed class GameLoop : IStartable, ITickable, IDisposable
{
    private readonly ScoreService _score;
    private readonly HudView _hud;

    public GameLoop(ScoreService score, HudView hud)
    {
        _score = score;
        _hud = hud;
    }

    public void Start() => _hud.Show(_score.Current);
    public void Tick() { }
    public void Dispose() { }
}

public sealed class PlayerController : UnityEngine.MonoBehaviour
{
    private ScoreService _score;

    [Inject]
    private void Construct(ScoreService score) => _score = score;
}
```

4. Make sure every MonoBehaviour that needs injection is reached by one of: `RegisterComponent*`, the scope's `autoInjectGameObjects` list (inspector "Auto Inject Game Objects"), or `IObjectResolver.Instantiate` / `InjectGameObject` at runtime. `[Inject]` alone does nothing.
5. Enter Play Mode. Registration errors surface when the scope builds in `Awake` (missing dependency, multiple `[Inject]` constructors, circular dependency).
6. For multi-scene games, create a project root scope (`Assets > Create > VContainer > VContainer Settings`, set "Root Lifetime Scope" prefab) and parent scene scopes to it.

## Lifetimes

| Lifetime | Behaviour |
|---|---|
| `Lifetime.Singleton` | one instance for the scope that registered it and all children; a child that re-registers the type gets its own. Same type cannot be registered twice in one container. |
| `Lifetime.Scoped` | one instance per `LifetimeScope`. A child resolving a Scoped registration from its parent creates its own instance. Disposed with the scope. |
| `Lifetime.Transient` | new instance per resolve; never disposed by the container. |

`RegisterInstance` is always Singleton and not owned (no `Dispose`, no method injection). `RegisterComponentInHierarchy` is always Scoped. `Register<T>` / `Register<TInterface, TImplement>` require an explicit `Lifetime` argument (the docs' `Register<IServiceA, ServiceA>()` sample omits it but the source has no default); `RegisterEntryPoint<T>` defaults to Singleton.

## Rules

- Prefer constructor injection with `readonly` fields for plain C#. Mark exactly one constructor `[Inject]` if a class has several; zero or one constructor needs no attribute.
- MonoBehaviours cannot use constructor injection; use one `[Inject]` method (any name, any accessibility) or `[Inject]` fields/properties.
- Use `RegisterEntryPoint<T>()` (not `Register<T>().AsImplementedInterfaces()`) for anything implementing `IStartable`/`ITickable`/etc.; only entry points are scheduled on the PlayerLoop.
- Instantiate prefabs that need injection with `resolver.Instantiate(prefab, parent)` and never with `Object.Instantiate`. For objects created elsewhere (Addressables, pools), call `resolver.InjectGameObject(go)`.
- Inject `IObjectResolver` only into factories and composition code. Prefer `Func<...>` factories via `RegisterFactory` over service-locator calls.
- Keep `Configure` free of side effects; use `RegisterBuildCallback(container => ...)` for work that needs the built container (e.g. `GlobalMessagePipe.SetProvider`).
- Register settings ScriptableObjects (or their serializable sub-objects) with `RegisterInstance`.

## Pitfalls

- **Circular dependencies**: the builder throws `VContainerException: Circular dependency detected!` with the path. Break the cycle with an event/message (see `messagepipe`), a `Func<T>` factory, or by splitting the type.
- **"No such registration of type: X"**: the type, or the exact interface it is resolved as, was not registered in this scope or any parent. `Register<Foo>` does not register `IFoo`; add `.As<IFoo>()` or `.AsImplementedInterfaces()`. Keyed registrations resolve only with `[Key(...)]` or `Resolve<T>(key)`.
- **Scoped object resolved from a parent**: a Scoped type registered in the parent but resolved from a child is constructed again in the child, with the child's dependencies. If you need one shared instance, register it Singleton (or resolve it from the parent scope).
- **Resolving before the build**: `LifetimeScope` has `[DefaultExecutionOrder(-5000)]` and builds in its `Awake` when `autoRun` is true, injecting registered components during that build. So injection normally finishes before other components' `Awake`, but not always: a component with an execution order earlier than -5000, or one awakened before the scope exists (another scene, a parent scope still loading, a runtime-instantiated prefab), sees null fields. Do not use injected fields in `Awake`/`OnEnable`; use `Start` or an entry point. With `autoRun = false`, nothing is injected until you call `Build()`.
- **Constructor injection on MonoBehaviour**: not supported; Unity creates MonoBehaviours. Use `[Inject]` methods. The same applies to auto-created ECS systems.
- **Scene loading order**: a scene scope whose parent lives in another scene must load after the parent is built. Wrap the load in `using (LifetimeScope.EnqueueParent(parentScope)) { await SceneManager.LoadSceneAsync(...); }`, or set the parent type in the inspector (`parentReference`). An inspector parent type takes priority over `EnqueueParent`; if the type is not found when the scene finishes loading, VContainer throws (`VContainerParentTypeReferenceNotFound`). Use `LifetimeScope.Enqueue(builder => ...)` (there is no `EnqueueExtraInstaller`) to add registrations to the next scope built inside the `using` block.
- **Disposing scopes**: `scope.Dispose()` disposes the container (calls `IDisposable` on Singleton/Scoped instances it created) and destroys the scope's GameObject. Children created with `CreateChild` are parented under the scope transform and go with it. Scoped MonoBehaviours elsewhere in the scene are not destroyed; parent them under the scope or dispose them yourself.
- **IL2CPP stripping**: reflection-only constructors can be stripped. `[Inject]` derives from `PreserveAttribute`, so marking the constructor `[Inject]` keeps it; otherwise use `link.xml`. The source generator also preserves generated types.
- **Unity 6 notes**: VContainer 1.19 already uses `FindAnyObjectByType` (2022.1+) and switches its diagnostics scope name to `GetEntityId()` behind its own `#if UNITY_6000_4_OR_NEWER` gate (newer Unity 6 releases move to `EntityId` and deprecate `GetInstanceID`; check your version's docs), and `IAsyncStartable` returns `UnityEngine.Awaitable` on 2023.1+ when UniTask is not installed.
- **Diagnostics left on**: `VContainerSettings.EnableDiagnostics` adds heavy GC allocation; keep it off in builds.
