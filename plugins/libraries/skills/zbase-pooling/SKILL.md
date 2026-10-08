---
name: zbase-pooling
description: Sets up and reviews Zitga-Tech ZBase.Foundation.Pooling (com.zbase.foundation.pooling) for pooling C# objects, collections, GameObjects and Components in Unity, and chooses between it and UnityEngine.Pool. Covers install via OpenUPM or git URL, IPool<T> / IAsyncPool<T>, Pool<T> / AsyncPool<T> and instantiators, SharedPool.Of<T>, DisposableContext, ListPool / DictionaryPool, GameObjectPool / ComponentPool<T> / UnityPool<T, TPrefab> with GameObjectPrefab / ComponentPrefab<T>, prepooling (PrepoolAmount, Prepool, UnityPrepooler), GameObjectPoolBehaviour / ComponentPoolBehaviour<T>, ScriptableGameObjectPool, Addressables pools (AddressGameObjectPool, AssetRefGameObjectPool), UniTask Rent, ReleaseInstances, Dispose, and pitfalls (double return, pooled objects destroyed with their scene, inactive rented objects, state reset). Use when the user mentions ZBase pooling, GameObjectPool, SharedPool.Of, Rent/Return, prepool, or asks which object pool to use in Unity.
license: MIT
metadata:
  category: libraries
  sources: "https://github.com/Zitga-Tech/ZBase.Foundation.Pooling"
  unity: "6000.0+"
---

# ZBase.Foundation.Pooling

Interface-driven pools: sync pools for C# objects and collections, UniTask-based async pools for Unity objects, optional Addressables pools. Verified against package.json 2.3.3 (repo HEAD, Feb 2024; same as OpenUPM latest). Runtime: `Packages/ZBase.Foundation.Pooling/ZBase.Foundation.Pooling`; Addressables module: `.../ZBase.Foundation.Pooling.Addressables`.

Reference file, read when needed:

- `references/types.md` - read when you need the full type catalog (pools, prefabs, behaviours, scriptable pools, Addressables variants, collection pools), constructor overloads, or the override hooks for custom pools and prefabs.

Related skills: `unitask` (all Unity-object rents return `UniTask<T>`), `csharp-unity` (GC and pooling guidance), `zbase-pubsub`.

## Install

- OpenUPM (resolves dependencies): `openupm add com.zbase.foundation.pooling`.
- Git URL (README): first add the Unity NuGet scoped registry (`name: Unity NuGet`, `url: https://unitynuget-registry.azurewebsites.net`, `scopes: ["org.nuget"]`), then add `https://github.com/Zitga-Tech/ZBase.Foundation.Pooling.git?path=Packages/ZBase.Foundation.Pooling`. `com.cysharp.unitask` and `com.zbase.collections.pooled` must also resolve, so an OpenUPM scoped registry (`com.cysharp`, `com.zbase`) is still needed.
- Dependencies: `org.nuget.system.runtime.compilerservices.unsafe` 6.0.0, `com.cysharp.unitask`, `com.zbase.collections.pooled`.
- Addressables support compiles automatically when `com.unity.addressables` is installed (define `ZBASE_FOUNDATION_POOLING_ADDRESSABLES`, asmdef `ZBase.Foundation.Pooling.Addressables`, also needs `UniTask.Addressables`).

Asmdef to reference: `ZBase.Foundation.Pooling`. Namespaces:

| Namespace | Contents |
|---|---|
| `ZBase.Foundation.Pooling` | `IPool<T>`, `IAsyncPool<T>`, `Pool<T>`, `AsyncPool<T>`, instantiators, `SharedPool`, `DisposableContext` |
| `ZBase.Foundation.Pooling.UnityPools` | `GameObjectPool`, `ComponentPool<T>`, prefabs, prepooler, pool behaviours |
| `ZBase.Foundation.Pooling.ScriptablePools` | `ScriptableGameObjectPool`, `ScriptableComponentPool`, sources |
| `ZBase.Foundation.Pooling.AddressableAssets` | `Address*` and `AssetRef*` pools, prefabs, behaviours |
| `System.Collections.Generic.Pooling` | `ListPool<T>`, `HashSetPool<T>`, `QueuePool<T>`, `StackPool<T>`, `DictionaryPool<TKey, TValue>` for BCL collections |
| `ZBase.Collections.Pooled.Generic.Pooling` | same pools for `ZBase.Collections.Pooled` collections, plus `ArrayDictionaryPool`, `ArrayHashSetPool` |

## Core contracts

- `IPool<T>`: `T Rent()`, `void Return(T)`, `void ReleaseInstances(int keep, Action<T> onReleased = null)`, `int Count()`.
- `IAsyncPool<T>`: `UniTask<T> Rent()`, `UniTask<T> Rent(CancellationToken)`, plus the same `Return`, `ReleaseInstances`, `Count`.
- `Count()` is the number of idle instances in the pool, not the number rented.
- There is no max size and no rented-object tracking. The pool grows on demand; trim it with `ReleaseInstances(keep)`.

## Workflow: C# objects and collections

1. Pick a pool: `new Pool<MyClass>()` (creates with `Activator.CreateInstance`), `new Pool<MyClass, DefaultConstructorInstantiator<MyClass>>()` (needs `new()`), or a custom `IInstantiable<T>` struct.
2. For app-wide shared pools use `SharedPool.Of<Pool<MyClass>>()` or `SharedPool.Of<ListPool<int>>()` (type must implement `IPool`, `IShareable`, `new()`).
3. Rent and return, or use the disposable context for scoped borrowing:

```csharp
using System.Collections.Generic;
using System.Collections.Generic.Pooling;
using ZBase.Foundation.Pooling;

public static class Targeting
{
    public static int CountNear(IReadOnlyList<int> ids)
    {
        var pool = SharedPool.Of<ListPool<int>>();
        using var rented = pool.DisposableContext().Rent();
        var buffer = rented.Instance;
        for (var i = 0; i < ids.Count; i++)
        {
            if (ids[i] > 0) buffer.Add(ids[i]);
        }
        return buffer.Count;
    }
}
```

The collection pools call `Clear()` on return. `Pool<T>` itself does not reset anything: subclass `Pool<T, TInstantiator>` and override `ReturnPreprocess(T)` / `RentPostprocess(T)` to reset state.

## Workflow: GameObjects and Components

1. Create a pool with a prefab:

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using ZBase.Foundation.Pooling.UnityPools;

public sealed class BulletSpawner : MonoBehaviour
{
    [SerializeField] private ComponentPool<Bullet> _pool = new();

    private void Start() => PrepoolAsync().Forget();

    private async UniTaskVoid PrepoolAsync()
    {
        _pool.Prefab.Parent = transform;
        await _pool.Prepool(destroyCancellationToken);
    }

    public async UniTask<Bullet> SpawnAsync(Vector3 position, CancellationToken token)
    {
        var bullet = await _pool.Rent(token);
        bullet.transform.SetPositionAndRotation(position, Quaternion.identity);
        bullet.ResetState();
        bullet.gameObject.SetActive(true);
        return bullet;
    }

    public void Despawn(Bullet bullet) => _pool.Return(bullet);

    private void OnDestroy()
    {
        _pool.ReleaseInstances(0);
        _pool.Dispose();
    }
}
```

`ComponentPool<T>`, `GameObjectPool` and `UnityPool<T, TPrefab>` are `[Serializable]`, so the prefab (`Source`), `Parent`, `PrepoolAmount` and `DontApplyPrefabParentOnReturn` are set in the inspector; `Prepool` instantiates `PrepoolAmount` instances and returns them to the pool. `Bullet` here is your component with a `ResetState()` method.

2. Or use a component wrapper: add `GameObjectPoolBehaviour` (ready to use) or a non-generic subclass of `ComponentPoolBehaviour<T>` / `UnityPoolBehaviour<T, TPrefab, TPool>`. These set the prefab parent to their own transform in `Awake`, prepool in `Start` when `PrepoolOnStart` is ticked, and dispose the pool in `OnDestroy`.
3. Asset-based pools: `Assets > Create > Pooling > Scriptables > Pools > GameObject` (`ScriptableGameObjectPool`) plus a source asset (`Pooling > Scriptables > Sources > GameObject`). Set `Parent` at runtime before renting.
4. Addressables: `AddressGameObjectPool` / `AddressComponentPool<T>` (string address) or `AssetRefGameObjectPool` / `AssetRefComponentPool<T>` (`AssetReference`). Instances come from `Addressables.InstantiateAsync` and are released with `Addressables.ReleaseInstance`.

## Lifetime and cleanup

- `Return` deactivates the GameObject (`SetActive(false)`) and reparents it to `Prefab.Parent` unless `DontApplyPrefabParentOnReturn` is set. `Rent` does not reactivate it: instances taken from the queue are inactive, freshly instantiated ones are active (if the prefab is). Always `SetActive(true)` after configuring the rented object.
- `Dispose()` only clears the internal queue. It does not destroy pooled instances. Call `ReleaseInstances(0)` first; with no callback it calls `Prefab.Release(instance)` (`Object.Destroy` for `GameObjectPrefab`/`ComponentPrefab<T>`, `Addressables.ReleaseInstance` for Addressables prefabs). Plain `Pool<T>.ReleaseInstances` just drops references unless you pass `onReleased`.
- `PoolBehaviour.OnDestroy` disposes the pool; idle instances parented under the behaviour are destroyed with it, but rented instances parented elsewhere are not.
- `ScriptablePool<T>` lives in an asset: its queue survives scene loads, while the pooled scene objects do not.

## Pitfalls

- **Returning twice**: the queue ignores an instance that is already idle (`UniqueQueue` keyed by instance id or reference), so an immediate double return is harmless. Returning after someone else re-rented it is not: both holders now share one object. Null your reference after `Return`, or wrap rentals in `DisposableContext` so ownership ends with `using`.
- **Rented or pooled objects outliving their scene**: pooled GameObjects parented under scene objects are destroyed on scene unload, but the pool still holds the (now destroyed) references and will hand them out from `Rent`, causing `MissingReferenceException`. Either scope the pool to the scene (behaviour in the scene, `ReleaseInstances(0)` + `Dispose` on unload), or keep the pool and its parent in `DontDestroyOnLoad`. `SharedPool.Of<T>()` and scriptable pools are global by design; do not put scene objects in them.
- **State leaks**: nothing resets components. Reset transforms, rigidbody velocity (`linearVelocity` on Unity 6), particle systems, trails, animator state and event subscriptions in a `RentPostprocess`/`ReturnPreprocess` override or in your spawn code.
- **Unity 6000.5+ compile error**: `UnityPool<T, TPrefab>.Return` keys the queue with `instance.GetInstanceID()`, which repo guidance says becomes CS0619 on 6000.5+ (use `GetEntityId()`). The package is unmaintained since 2024; embed it in `Packages/` and patch that line under `#if UNITY_6000_4_OR_NEWER` if you target 6000.5+.
- **Name clashes**: `ListPool<T>`, `HashSetPool<T>`, `DictionaryPool<TKey, TValue>` exist in `UnityEngine.Pool`, `System.Collections.Generic.Pooling` and `ZBase.Collections.Pooled.Generic.Pooling`. Import one namespace per file or alias.
- **Async rent races**: two concurrent `Rent()` calls on an empty pool both instantiate. That is fine, but prepool before gameplay to avoid instantiation spikes and async frame delays (Addressables instantiation is truly async).
- **Unbounded growth**: no `maxSize`; call `ReleaseInstances(keep)` after spikes (wave end, scene transition).

## ZBase pooling vs UnityEngine.Pool

| | `UnityEngine.Pool` (built-in) | ZBase.Foundation.Pooling |
|---|---|---|
| Types | `ObjectPool<T>` (stack), `LinkedPool<T>` (linked list, no up-front array), `GenericPool<T>`, `UnsafeGenericPool<T>`, `ListPool<T>`, `HashSetPool<T>`, `DictionaryPool<TKey, TValue>`, `CollectionPool<TCollection, TItem>` | sync `Pool<T>`, async `UnityPool<T, TPrefab>`, `GameObjectPool`, `ComponentPool<T>`, scriptable and Addressables pools, collection pools |
| Creation | sync `createFunc` delegate | sync instantiators or async `IPrefab<T>.Instantiate` returning `UniTask<T>` |
| Hooks | `actionOnGet`, `actionOnRelease`, `actionOnDestroy` delegates | subclass overrides (`RentPostprocess`, `ReturnPreprocess`) and `IPrefab.Release` |
| Limits and safety | `maxSize` (excess destroyed via `actionOnDestroy`), `collectionCheck` throws on double release in the editor | no max, duplicate idle returns ignored silently |
| Scoped borrow | `Get(out T)` returns a `PooledObject<T>` for `using` | `pool.DisposableContext().Rent()` returns `Disposable<T>` |
| GameObject support | none built in; write the delegates yourself | prefab, parent, prepooling, deactivate-on-return, inspector-serializable |
| Dependencies | none (engine) | UniTask, ZBase.Collections.Pooled, Unsafe |

Use `UnityEngine.Pool` by default: synchronous creation, temporary collections (`ListPool<T>.Get(out var list)`), bounded pools with editor double-release checks, and no extra packages. Use ZBase pooling when the project already depends on ZBase/UniTask and you want async instantiation (Addressables), inspector-configured prefab pools with prepooling, or `SharedPool.Of<T>()` singletons. Do not mix both for the same object type.
