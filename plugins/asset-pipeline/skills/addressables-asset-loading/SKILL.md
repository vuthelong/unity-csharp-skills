---
name: addressables-asset-loading
description: Sets up, loads, releases and ships content with Unity Addressables (com.unity.addressables 2.x-4.x) in Unity 6. Covers groups and schemas, packing modes, labels, addresses vs AssetReference / AssetReferenceT<T> / AssetLabelReference, LoadAssetAsync, LoadAssetsAsync, InstantiateAsync, Release / ReleaseInstance, AsyncOperationHandle reference counting, awaiting with UniTask ToUniTask, Task, Awaitable and 4.x ToAwaitable, LoadSceneAsync / UnloadSceneAsync, profiles, local vs remote content, catalogs, content update builds, CCD, Analyze duplicate dependencies, Play Mode Scripts, building with the Player, the Addressables Report and Profiler module (Event Viewer removed in 2.0), and pitfalls (leaked or double-released handles, sub-assets, ScriptableObjects duplicating assets, Resources mixed with Addressables). Use when the user mentions Addressables, AssetReference, AsyncOperationHandle, labels, remote content, catalog updates, "memory not freed after Release", or duplicated assets in bundles.
license: MIT
metadata:
  category: asset-pipeline
  sources: "com.unity.addressables@2.11.2 and @4.1.1 package source and Documentation~ (packages.unity.com), github.com/Cysharp/UniTask/src/UniTask/Assets/Plugins/UniTask/Runtime/External/Addressables, docs.unity3d.com/Packages/com.unity.addressables@2.7/manual"
  unity: "6000.0+"
---

# Addressables asset loading

Verified against the package sources of `com.unity.addressables` 2.11.2 and 4.1.1 (both declare `"unity": "6000.0"`). The runtime API below is the same in 2.x and 4.x unless marked. Use the version Package Manager recommends for your editor; upgrade majors deliberately.

| Version | Notable for Unity 6 |
|---|---|
| 2.x | Event Viewer removed (since 2.0), replaced by the Profiler module and Addressables Report. Binary catalogs. Auto Group Generator (2.10) |
| 3.x | Warning when Addressables is used outside Play Mode |
| 4.x | `await handle` directly (throws `AsyncOperationHandleException`), `handle.ToAwaitable(CancellationToken)` / `ToAwaitable(MonoBehaviour)` that release on cancel, public `AsyncOperationHandle.ReferenceCount`, folder keys (`LoadAssetsAsync<T>(folderAddress)`), `AddressableAssetGroup.IncludeInBuild`, Content Directory schema on Unity 6000.6+ (local content only) |

## Workflow

1. **Install** `com.unity.addressables`, open Window > Asset Management > Addressables > Groups, click Create Addressables Settings. Commit `Assets/AddressableAssetsData`.
2. **Organize groups** by load/unload lifetime, not by asset type. Things loaded and released together go together. Details: [references/groups-and-builds.md](references/groups-and-builds.md).
3. **Reference content** from code with `AssetReferenceT<T>` fields (designer-assigned, refactor-safe), addresses (strings, from data tables) or labels (sets). See the table below.
4. **Load and release** with exactly one owner per handle. Patterns and full examples: [references/loading-patterns.md](references/loading-patterns.md).
5. **Choose the Play Mode Script**: `Use Asset Database` for iteration, `Use Existing Build` before every release to catch missing dependencies, bundle duplication and shader stripping that the Asset Database mode hides.
6. **Analyze** (Window > Asset Management > Addressables > Analyze): fix duplicate dependencies, then check the Addressables Report.
7. **Build** content with the Player (Build Addressables on Player Build) or as a separate step in CI. Remote content and updates: [references/groups-and-builds.md](references/groups-and-builds.md).
8. **Profile memory** with the Addressables Profiler module and Memory Profiler: [references/memory-and-debugging.md](references/memory-and-debugging.md).

## Keys: address, AssetReference or label

| Key | Type | Use when |
|---|---|---|
| Address | `string` (`"enemies/goblin"`) | IDs come from data (CSV rows, server config). Renaming the address breaks callers; keep addresses stable and generated |
| `AssetReference` | serialized GUID | Generic reference set in the Inspector |
| `AssetReferenceT<T>`, `AssetReferenceGameObject`, `AssetReferenceTexture2D`, `AssetReferenceSprite`, `AssetReferenceAtlasedSprite` | serialized GUID, type-filtered | Default for designer-wired references. Survives renames and moves |
| `AssetLabelReference` | serialized label string | Load a set (`"preload"`, `"level1"`) with `LoadAssetsAsync` |
| `IResourceLocation` | from `LoadResourceLocationsAsync` | Check a key exists, or load many specific locations without failing on missing keys |
| Folder address (4.x) | `string` | Load all assets of an addressable folder (Include Folder Keys in Catalog) |
| Sub-asset | `"sheet[spriteName]"` or `AssetReferenceAtlasedSprite` | Single sprite from a sheet or SpriteAtlas |

Restrict a generic `AssetReference` in the Inspector with `[AssetReferenceUILabelRestriction("label")]`.

## Core API

| Call | Returns | Release with |
|---|---|---|
| `Addressables.LoadAssetAsync<T>(key)` | `AsyncOperationHandle<T>` | `Addressables.Release(handle)` (or `Release(result)`) |
| `Addressables.LoadAssetsAsync<T>(key or keys, callback, MergeMode, releaseDependenciesOnFailure)` | `AsyncOperationHandle<IList<T>>` | One `Release(handle)` for the whole list |
| `Addressables.InstantiateAsync(key, parent / position, rotation)` | `AsyncOperationHandle<GameObject>` | `Addressables.ReleaseInstance(instance)` or `Release(handle)`. Not `Destroy` alone |
| `assetReference.LoadAssetAsync<T>()` | handle stored in `assetReference.OperationHandle` | `assetReference.ReleaseAsset()`. Only one load per reference instance at a time |
| `assetReference.InstantiateAsync(...)` | instance handle | `assetReference.ReleaseInstance(go)` |
| `Addressables.LoadSceneAsync(key, LoadSceneMode, activateOnLoad, priority)` | `AsyncOperationHandle<SceneInstance>` | `Addressables.UnloadSceneAsync(handle or sceneInstance)` |
| `Addressables.LoadResourceLocationsAsync(key, type)` | `AsyncOperationHandle<IList<IResourceLocation>>` | `Release(handle)` |
| `Addressables.GetDownloadSizeAsync(key)` / `DownloadDependenciesAsync(key, autoReleaseHandle)` | size / download op | `Release` unless auto-released |
| `Addressables.CheckForCatalogUpdates(autoReleaseHandle)` / `UpdateCatalogs(...)` | changed catalog ids / locators | auto-release by default |
| `handle.WaitForCompletion()` | `T` | Synchronous; stalls the main thread, unsupported on WebGL |

`MergeMode` for key lists: `Union` (any key), `Intersection` (all keys, for example label "enemy" AND label "forest"), `UseFirst`.

## Handle lifetime rules

1. **Every load or instantiate must be released exactly once** by the code that owns it. Each `LoadAssetAsync` call increments a reference count on the asset and its bundles; the bundle unloads only when every count it depends on reaches zero.
2. **Track handles, not results**, when results can be shared. `Addressables.Release(object)` looks up the handle by the exact result instance; releasing a copy (for example a new `List` built from the result) does nothing and logs an error.
3. **Check `handle.IsValid()`** before releasing in teardown paths that can run twice (`OnDisable` then `OnDestroy`, pool clear plus scene unload). A released handle is invalid; releasing it again throws "Attempting to use an invalid operation handle".
4. **Release failed handles too.** A failed load still holds a reference until released. With 4.x `await handle`, catch `AsyncOperationHandleException` and `e.Handle.Release()` in the catch.
5. **Instantiated objects**: `InstantiateAsync` instances must go through `ReleaseInstance`. If you `Destroy` them yourself, their handle leaks unless `trackHandle` was true and the scene unloads. Prefer loading the prefab once and using `Object.Instantiate` or a pool (see `zbase-pooling`), keeping a single prefab handle.
6. **Scene-scoped content**: `LoadSceneMode.Single` releases Addressables scenes but not assets you loaded separately. Those need explicit `Release`.
7. **Release order**: release instances before the prefab handle, and ScriptableObject data before the assets it references.

## Awaiting

| Option | Code | Notes |
|---|---|---|
| UniTask (`com.cysharp.unitask`, auto-enables `UNITASK_ADDRESSABLE_SUPPORT`) | `await handle.ToUniTask(cancellationToken: token)` | Throws on failure. `autoReleaseWhenCanceled: true` releases on cancel; then do not release again elsewhere. Preferred if the project uses UniTask (see `unitask`) |
| 4.x Awaitable | `await handle.ToAwaitable(token)` or `ToAwaitable(this)` | Cancel releases the handle. With 4.x and UniTask both installed, `await handle` binds to the package's instance `GetAwaiter`, so call `.ToUniTask()` explicitly when you want UniTask semantics |
| `Task` | `await handle.Task` | Allocates; never throws, returns `default` on failure, so check `handle.Status` |
| 2.x without UniTask | `while (!handle.IsDone) await Awaitable.NextFrameAsync(token);` | Then read `handle.Status` / `Result` |
| Callback | `handle.Completed += OnLoaded` | Unsubscribe if the owner dies first |

Pass a lifetime token (`destroyCancellationToken`, or a source canceled in `OnDisable`) to every await that can outlive its owner. Cancelling the await does not cancel the load or release the handle, except with `autoReleaseWhenCanceled` / `ToAwaitable(token)`.

## Pitfalls

| Symptom | Cause / fix |
|---|---|
| Memory grows every level | Leaked handles: loads without matching `Release`, or `Destroy` on `InstantiateAsync` objects. Check the Profiler module's Handles column |
| Exception "Attempting to use an invalid operation handle" | Released twice, or used after release. Guard with `IsValid()`, single owner |
| "Attempting to load AssetReference that has already been loaded" | `assetReference.LoadAssetAsync()` called twice on the same field. Use `Addressables.LoadAssetAsync<T>(assetReference)`, which allows multiple handles |
| Asset still in memory after Release | Its bundle stays loaded while any other asset from it is in use (Released status in the Profiler). Split groups by lifetime or call `Resources.UnloadUnusedAssets` at transitions |
| Same texture in memory twice | Implicit dependency duplicated into several bundles, or also referenced from a scene in Build Profiles / `Resources`. Run Analyze; make the shared asset Addressable in a shared group |
| ScriptableObject config pulls whole asset sets into its bundle | Direct references from an Addressable SO include those assets as implicit dependencies of the SO's bundle. Reference them with `AssetReferenceT<T>` (lazy) or make them Addressable in their own groups |
| Sprite from a sheet loads as `Texture2D` | Load `Sprite` with `"sheet[name]"`, `LoadAssetAsync<IList<Sprite>>`, or `AssetReferenceAtlasedSprite` for SpriteAtlas (see `manage-sprite-atlas`) |
| Works in Editor, missing in build | Play Mode Script was `Use Asset Database`; test with `Use Existing Build`. Or content was not rebuilt after changes |
| Pink materials only in Addressables | Shader variants stripped or shader in a different bundle than expected; include shaders/variant collections in a shared group |
| `Resources.Load` and Addressables both used for the same asset | Two copies shipped and loaded. Move it out of `Resources`; Analyze "Check Resources to Addressable Duplicate Dependencies" |
| Remote content fails after Player update | Catalog/bundles built with a different Unity version or with serialized type changes; rebuild content for each Player version |
| `MissingReferenceException` after scene unload | Objects instantiated into an Addressables scene and moved elsewhere are unloaded with that scene's bundle |

## Related skills

- `unitask`: `ToUniTask`, cancellation, `UniTaskVoid`.
- `zbase-pooling`: Addressables-backed GameObject pools (`AddressGameObjectPool`, `AssetRefGameObjectPool`).
- `game-data-pipeline`: CSV game data baked to ScriptableObjects and shipped as Addressables.
- `zbase-csv-reader`: the CSV baker used by that pipeline.
- `manage-sprite-atlas`: SpriteAtlas setup before making atlases Addressable.
- `unity-texture-import`: texture memory, which dominates most bundles.
- `spine-unity` and `blender-to-unity-pipeline`: preparing the content you ship.
- `csharp-unity`: code style for the examples.
