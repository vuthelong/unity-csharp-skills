# ZBase.Foundation.Pooling type catalog (2.3.3)

## Interfaces (`ZBase.Foundation.Pooling`)

| Interface | Members |
|---|---|
| `IPool` | marker |
| `IShareable` | marker required by `SharedPool.Of<T>()` |
| `IRentable<T>` | `T Rent()` |
| `IAsyncRentable<T>` : `IRentable<UniTask<T>>` | `UniTask<T> Rent(CancellationToken cancelToken)` |
| `IReturnable<T>` | `void Return(T instance)` |
| `IReleaseInstances<T>` | `void ReleaseInstances(int keep, Action<T> onReleased = null)` |
| `ICountable` | `int Count()` (idle instances) |
| `IPool<T>` | `IPool, IRentable<T>, IReturnable<T>, IReleaseInstances<T>, ICountable` |
| `IAsyncPool<T>` | `IPool, IAsyncRentable<T>, IReturnable<T>, IReleaseInstances<T>, ICountable` |
| `IInstantiable<T>` | `T Instantiate()` |
| `IAsyncInstantiable<T>` | `UniTask<T> Instantiate(CancellationToken cancelToken)` |

## C# object pools

| Type | Notes |
|---|---|
| `Pool<T, TInstantiator>` (`T : class`, `TInstantiator : IInstantiable<T>`) | ctors: `()`, `(UniqueQueue<T>)`, `(TInstantiator)`, `(TInstantiator, UniqueQueue<T>)`. Virtual hooks: `RentPostprocess(T)`, `ReturnPreprocess(T)`, `Dispose()`. Implements `IPool<T>`, `IShareable`, `IDisposable`. |
| `Pool<T>` | `Pool<T, ActivatorInstantiator<T>>` |
| `AsyncPool<T, TInstantiator>` (`TInstantiator : IAsyncInstantiable<T>`) | hooks: `UniTask RentPostprocess(T, CancellationToken)`, `ReturnPreprocess(T)` |
| `AsyncPool<T>` | `AsyncPool<T, AsyncActivatorInstantiator<T>>` |
| `ActivatorInstantiator<T>` / `AsyncActivatorInstantiator<T>` | `Activator.CreateInstance` |
| `DefaultConstructorInstantiator<T>` / `AsyncDefaultConstructorInstantiator<T>` | `where T : class, new()` |
| `SharedPool.Of<T>()` | `where T : IPool, IShareable, new()`; one static instance per closed type |

`Return(null)` is ignored. Returning an instance already idle in the pool is ignored (`UniqueQueue<T>`).

Batch extensions (`Pool_T_Rents`, `Pool_T_Returns`, `AsyncPool_T_Rents`): `pool.Rent(T[] output)`, `Rent(T[] output, int count)`, `Rent(in Span<T> output[, count])`, `Rent<T, TOutput>(TOutput output[, count])` for `ICollection<T>`-like outputs; `pool.Return(T[])`, `Return(in Span<T>)`, `Return(in ReadOnlySpan<T>)`, `Return<T, TInstances>(TInstances)` for `IEnumerable<T>`, plus generated multi-argument `Return(pool, a, b, c, ...)` overloads.

## Disposable contexts

| API | Returns |
|---|---|
| `pool.DisposableContext()` on `IPool<T>` | `DisposableContext<T>`; `Rent()` returns `Disposable<T>` |
| `pool.DisposableContext()` on `IAsyncPool<T>` | `AsyncDisposableContext<T>`; `Rent()` / `Rent(ct)` return `UniTask<Disposable<T>>` |
| `Disposable<T>` (readonly struct) | `Instance` field, `Dispose()` returns to the pool, explicit cast to `T` |

```csharp
var context = SharedPool.Of<Pool<PathRequest>>().DisposableContext();
using (var request = context.Rent())
{
    request.Instance.Clear();
}
```

## Collection pools

Both `System.Collections.Generic.Pooling` (BCL collections) and `ZBase.Collections.Pooled.Generic.Pooling` (ZBase pooled collections) provide `ListPool<T>`, `HashSetPool<T>`, `QueuePool<T>`, `StackPool<T>`, `DictionaryPool<TKey, TValue>`; the ZBase variant adds `ArrayDictionaryPool<TKey, TValue>` and `ArrayHashSetPool<T>`. All clear the collection on return and work with `SharedPool.Of<...>()`.

## Unity object pools (`ZBase.Foundation.Pooling.UnityPools`)

| Type | Notes |
|---|---|
| `IPrefab` | `int PrepoolAmount { get; set; }` |
| `IPrefab<T>` | `IPrefab, IAsyncInstantiable<T>, IReleasable<T>, IHasParent` (`Transform Parent`) |
| `IPrefab<T, TSource>` | adds `TSource Source` |
| `IUnityPool<T>` / `IUnityPool<T, TPrefab>` | `IAsyncPool<T>` + `TPrefab Prefab` |
| `IPrepoolable` | `UniTask Prepool(CancellationToken)` |
| `IPrepooler<T, TPrefab, TPool>` / `UnityPrepooler<T, TPrefab, TPool>` | instantiates `PrepoolAmount` items and `Return`s them; uses `defaultParent` when `Parent` is unset |
| `UnityPool<T, TPrefab>` (`[Serializable]`) | ctors: `()`, `(TPrefab)`, `(UniqueQueue<int, T>)`, `(UniqueQueue<int, T>, TPrefab)`. `Prefab`, `Prepool(ct)`, `Rent()`, `Rent(ct)`, `Return(T)`, `ReleaseInstances(keep, onReleased)` (defaults to `Prefab.Release`), `Count()`, `Dispose()`. Hooks: `UniTask RentPostprocess(T, CancellationToken)`, `ReturnPreprocess(T)`. |
| `GameObjectPool<TPrefab>` / `GameObjectPool` | return: `SetActive(false)` and reparent to `Prefab.Parent` unless `DontApplyPrefabParentOnReturn` |
| `ComponentPool<T, TPrefab>` / `ComponentPool<T>` | same behaviour on `component.gameObject` |
| `UnityPrefab<T, TSource>` (abstract, `[Serializable]`) | serialized `Source`, `Parent`, `PrepoolAmount`; override `protected abstract UniTask<T> Instantiate(TSource source, Transform parent, CancellationToken cancelToken = default)` and `public abstract void Release(T instance)` |
| `GameObjectPrefab` | `Object.Instantiate(source, parent, true)` (world position stays) or without parent; `Release` destroys |
| `ComponentPrefab<T>` | `Object.Instantiate(source, parent)`; `Release` destroys the GameObject |

### Behaviours

| Type | Usable directly |
|---|---|
| `PoolBehaviour<T, TPool>` (abstract) | no; serialized `TPool`, forwards `IAsyncPool<T>`, disposes in `OnDestroy` |
| `UnityPoolBehaviour<T, TPrefab, TPool>` (abstract) | no; `PrepoolOnStart`, sets `Prefab.Parent` to itself in `Awake`, `OnAwake()` / `OnStart()` virtuals (`Awake` and `Start` are not virtual) |
| `GameObjectPoolBehaviour` | yes |
| `ComponentPoolBehaviour<T>` | declare a non-generic subclass (`public class BulletPool : ComponentPoolBehaviour<Bullet> {}`) so Unity can serialize it |

Custom prefab example:

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using ZBase.Foundation.Pooling.UnityPools;

[Serializable]
public sealed class InjectedEnemyPrefab : UnityPrefab<GameObject, GameObject>
{
    protected override UniTask<GameObject> Instantiate(GameObject source, Transform parent, CancellationToken cancelToken = default)
    {
        var instance = UnityEngine.Object.Instantiate(source, parent, false);
        return UniTask.FromResult(instance);
    }

    public override void Release(GameObject instance)
    {
        if (instance) UnityEngine.Object.Destroy(instance);
    }
}

[Serializable]
public sealed class InjectedEnemyPool : GameObjectPool<InjectedEnemyPrefab> { }
```

Replace `Object.Instantiate` with `IObjectResolver.Instantiate` to inject pooled objects with VContainer (see `vcontainer`).

## Scriptable pools (`ZBase.Foundation.Pooling.ScriptablePools`)

| Type | Menu |
|---|---|
| `ScriptablePool<T>` (ScriptableObject, `IUnityPool<T, ScriptablePrefab>`) | base |
| `ScriptableGameObjectPool` | `Pooling/Scriptables/Pools/GameObject` |
| `ScriptableComponentPool` | `Pooling/Scriptables/Pools/Component` |
| `ScriptableSource`, `ScriptableSource<T>` | abstract sources with `Instantiate(Transform parent, CancellationToken)` and `Release(Object)` |
| `ScriptableGameObjectSource` | `Pooling/Scriptables/Sources/GameObject` |
| `ScriptableComponentSource<T>` | subclass per component type |
| `ScriptablePrefab` | serialized `Source` and `PrepoolAmount`; `Parent` is runtime-only |

`ScriptablePool<T>.Parent` must be assigned at runtime (setter throws on null). Its `PrepoolOnStart` flag is serialized but nothing reads it; call `Prepool(ct)` yourself.

## Addressables (`ZBase.Foundation.Pooling.AddressableAssets`)

| Address (string) | AssetReference |
|---|---|
| `AddressGameObjectPool`, `AddressGameObjectPool<TPrefab>` | `AssetRefGameObjectPool` |
| `AddressComponentPool<T>`, `AddressComponentPool<T, TPrefab>` | `AssetRefComponentPool<T>` |
| `AddressPrefab<T>` (abstract, `Source` is `string`), `AddressGameObjectPrefab`, `AddressComponentPrefab<T>` | `AssetRefPrefab<T, TAssetRef>`, `AssetRefGameObjectPrefab`, `AssetRefComponentPrefab<T>` (`AssetReferenceGameObject`) |
| `AddressGameObjectPoolBehaviour`, `AddressGameObjectPoolBehaviour<TPrefab, TPool>`, `AddressComponentPoolBehaviour<T>`, `AddressComponentPoolBehaviour<T, TPrefab, TPool>` | `AssetRefGameObjectPoolBehaviour`, `AssetRefComponentPoolBehaviour<T>` |

Instantiation uses `Addressables.InstantiateAsync(address, parent)` / `assetReference.InstantiateAsync(parent, true)` awaited with the cancel token; release uses `Addressables.ReleaseInstance` / `AssetReference.ReleaseInstance`. Destroying an Addressables instance with `Object.Destroy` instead leaks its handle, so always trim with `ReleaseInstances` before dropping the pool.

Scriptable Addressables sources (`ScriptableAddressGameObjectSource`, `ScriptableAddressComponentSource<T>`, `ScriptableAssetRefGameObjectSource`, `ScriptableAssetRefComponentSource<T>`) live in the legacy namespace `Unity.Pooling.Scriptables.AddressableAssets`.
