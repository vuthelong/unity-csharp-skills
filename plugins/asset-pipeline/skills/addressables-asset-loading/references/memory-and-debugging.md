# Addressables memory, profiling and debugging

Read when memory does not drop after releasing, when finding leaked handles, when inspecting what a build contains, or when diagnosing load failures. Covers the Unity 6 tooling that replaced the Event Viewer.

## Tools in Unity 6

| Tool | Open | Shows | Notes |
|---|---|---|---|
| Addressables Profiler module | Window > Analysis > Profiler > Profiler Modules > Addressable Assets | Per frame: catalogs, bundles, assets, objects, **Handles** (reference count), Status (Queued, Downloading, Loading, Active, Released), Source (Local, Cache, Download), Refs By / Refs To | Requires Preferences > Addressables > **Debug Build Layout**, a content build, and Play Mode Script **Use Existing Build** (not Use Asset Database). Works on device builds if the build report for that build is in `Library/com.unity.addressables/buildReports`. Content Directory groups are not shown |
| Addressables Report | Window > Asset Management > Addressables > Addressables Report (Unity 6.6 and earlier) | Build contents: bundles, sizes, explicit/implicit assets, duplicates, references | Needs Debug Build Layout. Opens after each build unless disabled |
| Build Analysis window | Opens after builds on Unity 6.7+ | Same Addressables report, inside the engine's build analysis | Preference renamed "Open Build Analysis after build" |
| Memory Profiler (`com.unity.memoryprofiler`) | Window > Analysis > Memory Profiler | Actual native memory: textures, meshes, AssetBundle objects, duplicates by name | Compare two snapshots around a level transition. See `memory-snapshot-profiling` |
| Analyze window | Window > Asset Management > Addressables > Analyze | Duplicate dependencies before building | See groups-and-builds.md |

The **Event Viewer** and its API were removed in Addressables 2.0. Advice to "enable Send Profiler Events and open the Event Viewer" is outdated; use the Profiler module.

Reading the Profiler module:

- **Handles > 0 on an asset you think you released**: something still owns a handle. Search its handle count over frames to find when it was taken.
- **Asset Released but bundle Active**: another asset from the same bundle is still in use, so the bundle (and the released asset's memory) stays.
- **Assets not loaded** view: assets inside a loaded bundle that were never requested, which shows how much of the bundle you pay for without using.

## Why memory does not drop after Release

1. **Bundle granularity.** An AssetBundle unloads only when every asset loaded from it is released, and when no other loaded bundle depends on it. Released assets inside a still-loaded bundle stay in memory. Fix: split groups by lifetime, use Pack Separately for large independent assets, or call `Resources.UnloadUnusedAssets()` at a loading screen to free released assets inside still-loaded bundles.
2. **Bundle dependencies are per bundle, not per asset.** If any asset in bundle A references bundle B, loading any asset from A loads B. Keep cross-group references simple; check Refs To in the report.
3. **Still referenced.** Static fields, event subscriptions, caches, UI images, materials on pooled objects. Addressables releasing a handle does not destroy objects other code still references. In the Memory Profiler, follow the references to the root.
4. **Leaked handles.** `InstantiateAsync` objects destroyed with `Destroy` instead of `ReleaseInstance`, `AssetReference.LoadAssetAsync` without `ReleaseAsset`, `LoadResourceLocationsAsync` and `GetDownloadSizeAsync` handles never released, failed loads never released.
5. **Sub-assets load their container.** Loading one sprite from a sprite sheet, or a sprite packed in a SpriteAtlas, loads the whole texture or atlas page. Pack atlases by usage (see `manage-sprite-atlas`).
6. **Duplicates.** The same texture in two bundles, or in a bundle and in `Resources`/a Scene List scene, exists twice in memory. The Memory Profiler shows two objects with the same name; Analyze shows why.

## Unload behavior to know

- `LoadSceneMode.Single` triggers `Resources.UnloadUnusedAssets` and releases other Addressables scenes. To keep a scene's content alive across it, keep its load handle and `Addressables.ResourceManager.Acquire(handle)` before the switch; `DontDestroyOnLoad` and `HideFlags.DontUnloadUnusedAsset` do not protect Addressables content.
- Unloading an Addressables scene unloads its bundle, including objects that were moved from that scene into another scene. Instantiate long-lived objects from their own loaded prefab instead of moving them out of a scene.
- Releasing a prefab handle while instances of it exist leaves the instances pointing at unloaded meshes, materials and textures (invisible or pink objects). Destroy or pool-clear instances first.
- With domain reload disabled (Enter Play Mode Options), static caches of handles survive between play sessions while Addressables reinitializes. Reset them in a `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` method (see `csharp-unity`).
- Addressables 3.x+ warns when you load outside Play Mode (editor tools), because those loads can be forcibly unloaded when the play mode state changes. Editor tools should use `AssetDatabase` instead.

## ScriptableObjects and bundle layout

An Addressable ScriptableObject that directly references assets (`[SerializeField] private Sprite icon;`) drags every referenced asset into its bundle as implicit dependencies, or adds bundle dependencies if those assets are Addressable elsewhere. A single "ItemDatabase" SO with direct references to every icon and prefab therefore loads everything.

- Use `AssetReferenceT<T>` / `AssetReferenceSprite` fields in data assets and load on demand.
- Or store addresses (strings or ids) and resolve through a loader, which is what `game-data-pipeline` does with CSV-baked tables.
- Keep data SOs in their own small group; they change often and should not invalidate big asset bundles in content updates.

## Resources and Addressables together

- `Resources` content is always loaded into the Player's built-in data and indexed at startup; it cannot be unloaded per asset as cleanly or updated remotely.
- An asset in `Resources` that is also a dependency of Addressable content is shipped and loaded twice. Run "Check Resources to Addressable Duplicate Dependencies".
- Migration: move the folder out of `Resources`, mark it Addressable with addresses equal to the old `Resources` paths, and replace `Resources.Load<T>(path)` with `Addressables.LoadAssetAsync<T>(path)` plus release. Addressables has a built-in convert option in the Groups window for `Resources` folders.

## Diagnosing failures

| Symptom | Check |
|---|---|
| `InvalidKeyException` | Key typo, asset not marked Addressable, wrong type for the key (`LoadAssetAsync<Sprite>` on a `Texture2D` entry), or the catalog in use is older than the content. `LoadResourceLocationsAsync(key, typeof(T))` returns 0 locations for invalid combinations |
| Remote load fails only on device | Remote load path wrong for that profile, HTTP blocked (iOS ATS / Android cleartext), CRC mismatch after re-upload, or CDN caching an old catalog `.hash` |
| Stuck at 0% | Activation-paused scene load (`activateOnLoad: false`) blocking the async queue |
| Error details missing | Enable **Log Runtime Exceptions** in the settings; read `handle.OperationException`. Set `ResourceManager.ExceptionHandler` to route errors to your logger |
| Need handle counts in code | 4.x: `handle.ReferenceCount` (0 when invalid). 2.x: internal, use the Profiler module |
| Shader or material broken only in Addressables builds | Shader variants stripped because no Player scene uses them; put shaders and a ShaderVariantCollection in a shared Addressable group |
