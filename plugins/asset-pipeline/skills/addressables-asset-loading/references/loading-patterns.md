# Addressables loading patterns

Read when writing code that loads, instantiates, preloads by label, loads scenes, downloads remote content or updates catalogs. All examples follow the `csharp-unity` house style. UniTask examples need `com.cysharp.unitask` (the `UniTask.Addressables` assembly is auto-referenced and enabled by `UNITASK_ADDRESSABLE_SUPPORT`).

## 1. One prefab, many instances (preferred for gameplay spawns)

Load the prefab once, instantiate with `Object.Instantiate` or a pool, release the single handle when the owner dies. Cheaper than `InstantiateAsync` per spawn, and pooling works normally.

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public sealed class EnemySpawner : MonoBehaviour
{
    #region Fields
    [SerializeField] private AssetReferenceGameObject enemyPrefab;
    [SerializeField] private Transform spawnPoint;

    private AsyncOperationHandle<GameObject> _prefabHandle;
    private GameObject _prefab;
    #endregion

    #region Properties
    public bool IsReady => this._prefab != null;
    #endregion

    #region Unity Lifecycle
    private void Start() => LoadAsync(destroyCancellationToken).Forget();

    private void OnDestroy()
    {
        if (this._prefabHandle.IsValid()) Addressables.Release(this._prefabHandle);
    }
    #endregion

    #region Public Methods
    public GameObject Spawn()
    {
        if (!IsReady) return null;

        return Instantiate(this._prefab, this.spawnPoint.position, this.spawnPoint.rotation);
    }
    #endregion

    #region Private Methods
    private async UniTaskVoid LoadAsync(CancellationToken token)
    {
        this._prefabHandle = Addressables.LoadAssetAsync<GameObject>(this.enemyPrefab);
        this._prefab = await this._prefabHandle.ToUniTask(cancellationToken: token);
    }
    #endregion
}
```

- `Addressables.LoadAssetAsync<GameObject>(this.enemyPrefab)` uses the reference as a key, so other components can load the same reference without "already been loaded" errors.
- The handle has one owner (`OnDestroy`). If the object dies mid-load, `OnDestroy` releases, and the await throws `OperationCanceledException`, which `UniTaskVoid` ignores. Do not also pass `autoReleaseWhenCanceled: true`, or the handle is released twice.
- If loading fails, `ToUniTask` throws; the handle is still valid and `OnDestroy` releases it.
- Instances made with `Instantiate` are ordinary objects: `Destroy` them or return them to a pool. They must be gone before the prefab handle is released, or their meshes and materials can unload under them.

## 2. Handle scope for screens and levels

A plain owner that collects handles and releases them together when a screen closes or a level unloads.

```csharp
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public sealed class AddressableScope : System.IDisposable
{
    #region Fields
    private readonly List<AsyncOperationHandle> _handles = new();
    #endregion

    #region Public Methods
    public AsyncOperationHandle<T> Load<T>(object key)
    {
        var handle = Addressables.LoadAssetAsync<T>(key);
        this._handles.Add(handle);
        return handle;
    }

    public AsyncOperationHandle<IList<T>> LoadAll<T>(object key)
    {
        var handle = Addressables.LoadAssetsAsync<T>(key, null);
        this._handles.Add(handle);
        return handle;
    }

    public void Dispose()
    {
        for (var i = this._handles.Count - 1; i >= 0; i--)
        {
            var handle = this._handles[i];
            if (handle.IsValid()) Addressables.Release(handle);
        }

        this._handles.Clear();
    }
    #endregion
}
```

`AsyncOperationHandle<T>` converts implicitly to the non-generic `AsyncOperationHandle`. Usage: `var sprite = await this._scope.Load<Sprite>("icons/sword").ToUniTask(cancellationToken: token);`, then `this._scope.Dispose()` in `OnDestroy` or on screen close.

## 3. Preload by label into a lookup

```csharp
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public sealed class ItemIconCatalog : System.IDisposable
{
    #region Fields
    private readonly Dictionary<string, Sprite> _byName = new();
    private AsyncOperationHandle<IList<Sprite>> _handle;
    #endregion

    #region Public Methods
    public async UniTask LoadAsync(AssetLabelReference label, CancellationToken token)
    {
        this._handle = Addressables.LoadAssetsAsync<Sprite>(label, null, true);
        var sprites = await this._handle.ToUniTask(cancellationToken: token);

        this._byName.Clear();
        for (var i = 0; i < sprites.Count; i++)
        {
            var sprite = sprites[i];
            this._byName[sprite.name] = sprite;
        }
    }

    public bool TryGet(string iconName, out Sprite sprite) => this._byName.TryGetValue(iconName, out sprite);

    public void Dispose()
    {
        this._byName.Clear();
        if (this._handle.IsValid()) Addressables.Release(this._handle);
    }
    #endregion
}
```

- One handle covers the whole list; never release the individual sprites.
- `releaseDependenciesOnFailure: true` (the last argument) makes a single missing asset fail and release everything. Pass `false` to keep partial results, then release the handle anyway.
- Intersection of labels: `Addressables.LoadAssetsAsync<GameObject>(new List<object> { "enemy", "forest" }, null, Addressables.MergeMode.Intersection)`.
- To skip keys that may not exist, first `LoadResourceLocationsAsync(key, typeof(T))`, check `Result.Count`, then load by locations and release the locations handle.

## 4. Additive scenes

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

public sealed class LevelLoader : MonoBehaviour
{
    #region Fields
    private AsyncOperationHandle<SceneInstance> _sceneHandle;
    #endregion

    #region Public Methods
    public async UniTask LoadAsync(AssetReference scene, CancellationToken token)
    {
        await UnloadAsync();

        this._sceneHandle = Addressables.LoadSceneAsync(scene, LoadSceneMode.Additive);
        var instance = await this._sceneHandle.ToUniTask(cancellationToken: token);
        SceneManager.SetActiveScene(instance.Scene);
    }

    public async UniTask UnloadAsync()
    {
        if (!this._sceneHandle.IsValid()) return;

        var unload = Addressables.UnloadSceneAsync(this._sceneHandle);
        this._sceneHandle = default;
        await unload.ToUniTask();
    }
    #endregion
}
```

- `UnloadSceneAsync(handle)` auto-releases the load handle (`autoReleaseHandle` defaults to true). Do not also call `Release` on it.
- `activateOnLoad: false` gives you `SceneInstance.ActivateAsync()` for a controlled reveal, but Unity's scene activation queue blocks other `AsyncOperation`s (including other scene loads and some asset loads) until you activate. Activate promptly.
- `LoadSceneMode.Single` unloads every other scene, releasing other Addressables scene handles; separately loaded assets stay loaded.
- Assets loaded for a scene should be released after the scene unloads, not before, or objects in the scene lose their meshes and textures.

## 5. Without UniTask

Addressables 4.x:

```csharp
private async void Start()
{
    try
    {
        this._prefab = await Addressables.LoadAssetAsync<GameObject>(this.enemyPrefab).ToAwaitable(this);
    }
    catch (System.OperationCanceledException)
    {
    }
    catch (AsyncOperationHandleException<GameObject> e)
    {
        Debug.LogError($"{nameof(EnemySpawner)} failed: {e.Message}", this);
        e.Handle.Release();
    }
}
```

`ToAwaitable(this)` ties the handle to `destroyCancellationToken`: destroying the object releases it, before or after completion. Use `ToAwaitable(token)` with a source created in `OnEnable` and cancelled in `OnDisable` for repeatable loads. `AsyncOperationHandleException` lives in `UnityEngine.ResourceManagement.Exceptions`. `async void` is acceptable here only because `Start` must be `void` and the body catches everything.

Addressables 2.x, `Awaitable` only:

```csharp
private async Awaitable<GameObject> LoadPrefabAsync(object key, CancellationToken token)
{
    this._prefabHandle = Addressables.LoadAssetAsync<GameObject>(key);
    while (!this._prefabHandle.IsDone) await Awaitable.NextFrameAsync(token);

    if (this._prefabHandle.Status == AsyncOperationStatus.Succeeded) return this._prefabHandle.Result;

    Debug.LogError($"Load failed: {this._prefabHandle.OperationException}", this);
    return null;
}
```

`await handle.Task` also works in 2.x but allocates and never throws; check `Status` afterwards.

## 6. Remote content: size, download, catalog update

```csharp
public async UniTask<bool> EnsureDownloadedAsync(object key, System.IProgress<float> progress, CancellationToken token)
{
    var sizeHandle = Addressables.GetDownloadSizeAsync(key);
    var size = await sizeHandle.ToUniTask(cancellationToken: token);
    Addressables.Release(sizeHandle);
    if (size == 0) return true;

    var download = Addressables.DownloadDependenciesAsync(key);
    try
    {
        await download.ToUniTask(progress, cancellationToken: token);
        return download.Status == AsyncOperationStatus.Succeeded;
    }
    finally
    {
        Addressables.Release(download);
    }
}

public async UniTask UpdateCatalogsAsync(CancellationToken token)
{
    var check = Addressables.CheckForCatalogUpdates(false);
    var changed = await check.ToUniTask(cancellationToken: token);
    var hasUpdates = changed != null && changed.Count > 0;

    if (hasUpdates)
    {
        var update = Addressables.UpdateCatalogs(true, changed, false);
        await update.ToUniTask(cancellationToken: token);
        Addressables.Release(update);
    }

    Addressables.Release(check);
}
```

- Show `size` to the player before large downloads; `download.GetDownloadStatus()` gives bytes and percent for custom progress UI.
- Update catalogs before loading anything that may have changed, typically on the title screen. Assets already loaded from old bundles keep using them until released.
- `UpdateCatalogs(true, ...)` (`autoCleanBundleCache`) removes cached bundles that no catalog references anymore.
- Enable **Only update catalogs manually** in the Addressables settings if you call this yourself, so startup does not also check.
- In a cancel or failure path, the `finally` and the trailing releases above must still run; wrap them in `try/finally` if cancellation is likely.

## 7. Synchronous loads

`handle.WaitForCompletion()` blocks until done. Acceptable for tiny local assets during a loading screen. It stalls on remote downloads, and WebGL does not support it. Never call it in gameplay frames.
