# VContainer registration and injection reference (1.19.0)

All `Register*` methods are extension methods on `IContainerBuilder` and return a `RegistrationBuilder` (or `ComponentRegistrationBuilder`) for chaining.

## Plain C# types (`VContainer.ContainerBuilderExtensions`)

| Call | Notes |
|---|---|
| `Register<T>(Lifetime lifetime)` | concrete type, resolvable as `T` only |
| `Register<TInterface, TImplement>(Lifetime lifetime)` | resolvable as `TInterface` only |
| `Register<TInterface1, TInterface2, TImplement>(Lifetime)` / three-interface version | |
| `Register(Type type, Lifetime lifetime)` | also accepts an open generic definition: `Register(typeof(Repository<>), Lifetime.Singleton)`; confirmed for IL2CPP on Unity 2022.1+ |
| `Register(Type interfaceType, Type implementationType, Lifetime)` | |
| `Register<TInterface>(Func<IObjectResolver, TInterface> implementationConfiguration, Lifetime lifetime)` | delegate runs once per lifetime unit; the container owns and disposes the result |
| `RegisterInstance<TInterface>(TInterface instance)` (+ 2 and 3 interface generic versions, and `RegisterInstance(object, Type)`) | always Singleton, not disposed, no method injection |
| `RegisterFactory<T>(Func<T>)` up to `RegisterFactory<TP1, TP2, TP3, TP4, T>(Func<TP1, ..., T>)` | resolves as `Func<..., T>`; Singleton |
| `RegisterFactory<T>(Func<IObjectResolver, Func<T>> factoryFactory, Lifetime lifetime)` (and parameterized versions) | outer func runs once per lifetime, inner func per call |
| `RegisterBuildCallback(Action<IObjectResolver>)` | after the container is built |
| `RegisterDisposeCallback(Action<IObjectResolver>)` | when the container is disposed |

Objects returned from factory functions are not tracked; dispose them yourself.

## RegistrationBuilder chain

| Method | Effect |
|---|---|
| `As<TInterface>()` (up to four type args), `As(Type)`, `As(params Type[])` | resolvable as these types (replaces the default self-type) |
| `AsSelf()` | also resolvable as the implementation type |
| `AsImplementedInterfaces()` | resolvable as every implemented interface |
| `WithParameter<TParam>(TParam value)`, `WithParameter<TParam>(Func<IObjectResolver, TParam>)`, `WithParameter<TParam>(Func<TParam>)` | inject this value for constructor/method parameters of that type, for this registration only |
| `WithParameter(string name, object value)`, `WithParameter(Type type, object value)` and `Func<IObjectResolver, object>` versions | match by parameter name or type |
| `Keyed(object key)` | register under a key; inject with `[Key(key)]` or resolve with `Resolve<T>(key)` |

## Components (`VContainer.Unity.ContainerBuilderUnityExtensions`)

| Call | Lifetime | Notes |
|---|---|---|
| `RegisterComponent<TInterface>(TInterface component)` | Singleton | existing instance (e.g. a `[SerializeField]` reference); injected at build even if never resolved |
| `RegisterComponentInHierarchy<T>()` / `RegisterComponentInHierarchy(Type)` | Scoped | first `GetComponentInChildren(type, includeInactive: true)` over the root objects of the scope's scene, or under `UnderTransform(parent)` |
| `RegisterComponentOnNewGameObject<T>(Lifetime lifetime, string newGameObjectName = null)` | given | creates a GameObject with `T` on resolve |
| `RegisterComponentInNewPrefab<T>(T prefab, Lifetime lifetime)` | given | instantiates the prefab on resolve and injects it |
| `RegisterComponentInNewPrefab<T>(Func<IObjectResolver, T> prefab, Lifetime)` / `<TInterface, TImplement>(Func<IObjectResolver, TImplement>, Lifetime)` | given | prefab chosen at resolve time |

`ComponentRegistrationBuilder` adds `UnderTransform(Transform)`, `UnderTransform(Func<Transform>)`, `UnderTransform(Func<IObjectResolver, Transform>)` and `DontDestroyOnLoad()` (new GameObject / new prefab registrations).

Grouping helpers:

```csharp
builder.UseComponents(parentTransform, components =>
{
    components.AddInstance(hudView);
    components.AddInHierarchy<PlayerController>();
    components.AddInNewPrefab(enemySpawnerPrefab, Lifetime.Scoped);
    components.AddOnNewGameObject<AudioHub>(Lifetime.Scoped, "AudioHub");
});

builder.UseEntryPoints(Lifetime.Singleton, entryPoints =>
{
    entryPoints.Add<GameLoop>();
    entryPoints.Add<InputPump>().AsSelf();
    entryPoints.OnException(UnityEngine.Debug.LogException);
});
```

## Entry points

| Call | Notes |
|---|---|
| `RegisterEntryPoint<T>(Lifetime lifetime = Lifetime.Singleton)` | `Register<T>(lifetime).AsImplementedInterfaces()` plus PlayerLoop dispatch. Add `.AsSelf()` to also resolve it as `T`. |
| `RegisterEntryPoint<TInterface>(Func<IObjectResolver, TInterface>, Lifetime)` | delegate-created entry point |
| `RegisterEntryPointExceptionHandler(Action<Exception>)` | per scope; replaces the default `Debug.LogException` |

## ScriptableObjects

Expose the asset as a `[SerializeField]` on the scope and `RegisterInstance` it (or its serializable sub-objects). Do not `Register<MySettings>(...)`; the container would try to `new` a ScriptableObject.

## Collections

Registering several implementations of the same interface lets consumers inject `IEnumerable<T>` or `IReadOnlyList<T>`:

```csharp
builder.Register<IDamageModifier, ArmorModifier>(Lifetime.Singleton);
builder.Register<IDamageModifier, BuffModifier>(Lifetime.Singleton);

public sealed class DamageCalculator
{
    public DamageCalculator(IReadOnlyList<IDamageModifier> modifiers) { }
}
```

Resolving a single `IDamageModifier` in that case returns the last registration.

## Injection rules

| Target | How |
|---|---|
| Constructor | automatic for a single public constructor; with several constructors exactly one must have `[Inject]` (otherwise `Type found multiple [Inject] marked constructors` / `does not found injectable constructor`) |
| Method | `[Inject]` on any method, any name and accessibility; runs after construction (or on `Inject`/`InjectGameObject`) |
| Field / property | `[Inject]` on the member; with keys, both `[Inject]` and `[Key(...)]` are required |
| Keys | `[Key(WeaponType.Primary)]` on parameters, fields, properties; any key type |
| Opt out | `[InjectIgnore]` on a type excludes it from source generation |

Optional dependencies are not supported in constructors; use `IObjectResolver.TryResolve<T>(out var value, key)` or `ResolveOrDefault<T>(defaultValue, key)` in a factory instead.

## IObjectResolver (extensions in `VContainer` and `VContainer.Unity`)

| Member | Notes |
|---|---|
| `Resolve<T>(object key = null)`, `Resolve(Type, object key = null)` | throws `VContainerException` when missing |
| `TryResolve<T>(out T resolved, object key = null)`, `ResolveOrDefault<T>(T defaultValue = default, object key = null)` | non-throwing |
| `Inject(object instance)` | re-runs `[Inject]` members (overwrites) |
| `InjectGameObject(GameObject)` | every MonoBehaviour on the object and its children, active or not |
| `Instantiate(prefab)`, `Instantiate(prefab, Transform parent, bool worldPositionStays = false)`, `Instantiate(prefab, Vector3 position, Quaternion rotation)`, `Instantiate(prefab, position, rotation, Transform parent)` | for `GameObject` and any `Component` type; injects before the instance's `Awake` |
| `CreateScope(Action<IContainerBuilder>)` | non-Unity child container (`IScopedObjectResolver`) |
| `Diagnostics` | non-null when diagnostics are enabled |

`LifetimeScope` registers itself (`RegisterInstance<LifetimeScope>(this).AsSelf()`) and `IObjectResolver` is always resolvable, so both can be injected.
