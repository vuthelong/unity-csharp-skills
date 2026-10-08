# Groups, schemas, content builds and updates

Read when organizing Addressables groups, choosing schema settings, setting up local vs remote content and profiles, building in CI, shipping content updates, using Cloud Content Delivery (CCD), or fixing duplicate dependencies.

## Content build systems

| | AssetBundles (Content Packing & Loading schema) | Content Directories (Content Directory schema) |
|---|---|---|
| Availability | All Addressables versions | Addressables 4.x on Unity 6000.6+ (`ENABLE_CONTENT_DIRECTORIES`) |
| Unit of loading/unloading | AssetBundle (whole bundle stays while any asset in it is in use) | Per asset with its direct dependencies |
| Duplication | Non-Addressable shared dependencies are copied into every bundle that uses them | Deduplicated automatically |
| Remote content and updates | Yes | No (local only) |
| Profiler module support | Yes | No |

For a Unity 6.0-6.5 project, or any project with remote content, use AssetBundles. Do not mix the two for the same assets; shared dependencies get built twice. The rest of this file is about AssetBundle builds.

## Group strategy

- **Group by lifetime**: `Boot` (loaded at start, never released), `UI_Common`, `Level_01`, `Characters_Hero`, `Shared_Materials`. Everything in a bundle loads and unloads together in practice.
- **Shared dependencies get their own group**: textures, materials and shaders used by several groups go into a `Shared_*` group, otherwise they are duplicated (see Analyze).
- **Packing mode** (`Bundle Mode`):
  - `Pack Together`: one bundle per group. Fewer files, worse unloading granularity.
  - `Pack Separately`: one bundle per entry (folder entries pack together). Fine-grained unloading, more bundles and catalog entries.
  - `Pack Together By Label`: one bundle per label combination.
- Aim for bundles that are neither tiny (per-bundle overhead, file handles, catalog size) nor huge (cannot unload partially, large downloads). Several MB to tens of MB of compressed data is a common target for local content; smaller for remote.
- **Scenes** go in their own groups. AssetBundles cannot mix scenes and other assets, so Addressables already splits a group's scenes into separate bundles; dedicated groups keep that visible and let scene content unload independently.
- Keep a minimal bootstrap scene in Build Profiles > Scene List, and load everything else through Addressables, so scene dependencies are not duplicated in the Player data.

## Content Packing & Loading schema (`BundledAssetGroupSchema`)

| Setting | Recommendation |
|---|---|
| Build & Load Paths | `Local` for content shipped with the Player, `Remote` for downloadable content |
| Include in Build | Off to exclude a group temporarily (4.x: `AddressableAssetGroup.IncludeInBuild`) |
| Asset Bundle Compression | `LZ4` for local (fast, random access). `LZMA` only for remote downloads that are recompressed to LZ4 in the cache. `Uncompressed` rarely |
| Asset Bundle CRC | Enabled for remote, Disabled for local (CPU cost on load) |
| Use Asset Bundle Cache | On for remote |
| Bundle Naming Mode | `Append Hash to Filename` for remote (cache busting), any for local |
| Internal Asset Naming Mode | `GUID` (or `Dynamic` for smaller catalogs); avoid `Full Path` in release (larger, leaks paths) |
| Include Addresses / GUIDs / Labels in Catalog | Turn off what you never load by (GUIDs off if you never load by GUID) to shrink the catalog |
| Bundle Mode | See Group strategy |

## Content Update Restriction schema (`ContentUpdateGroupSchema`)

- **Prevent Updates** on (static content): changed assets are moved to a new remote group by "Check for Content Update Restrictions", so players download only the changes. Use for all local groups and for large remote groups.
- **Prevent Updates** off (dynamic content): any change rebuilds the whole bundle and players re-download it. Use for small, frequently changing remote groups (events, config).
- Do not change this setting between a release build and its update builds.

## Profiles and paths

Window > Asset Management > Addressables > Profiles. Default values:

| Variable | Default |
|---|---|
| Local.BuildPath | `[UnityEngine.AddressableAssets.Addressables.BuildPath]/[BuildTarget]` (under `Library`) |
| Local.LoadPath | `{UnityEngine.AddressableAssets.Addressables.RuntimePath}/[BuildTarget]` (StreamingAssets) |
| Remote.BuildPath | `ServerData/[BuildTarget]` |
| Remote.LoadPath | undefined: set your CDN URL, for example `https://cdn.example.com/game/[BuildTarget]` |

`[...]` is evaluated at build time, `{...}` at runtime. Keep one profile per environment (Dev, Staging, Release). Changing the remote load path requires a full content rebuild. Local build artifacts are copied into StreamingAssets during the Player build automatically; custom local paths are not.

Settings asset > Catalog: **Build Remote Catalog** on (with Remote paths) is required for content updates. **Only update catalogs manually** stops the automatic startup check if you call `CheckForCatalogUpdates` yourself. Binary catalogs (default in 2.x) load faster than JSON.

## Building

| Way | When |
|---|---|
| Groups window > Build > New Build > Default Build Script | Full content build; writes `addressables_content_state.bin` under `Assets/AddressableAssetsData/<Platform>` |
| Build Addressables on Player Build (Settings > Build, or Preferences) | Builds content during every Player build. Convenient, slower. Since 2.9.1 a failed Addressables build fails the Player build |
| Script / CI | `AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result)`; success when `string.IsNullOrEmpty(result.Error)` |
| Groups window > Build > Update a Previous Build | Content update against a saved content state |
| Clear Build Cache | When builds behave inconsistently; forces a full rebuild |

CI sketch (Editor script, run with `-executeMethod`):

```csharp
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public static class AddressablesBuild
{
    #region Public Methods
    public static void BuildContent()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("Addressables settings not found.");
            EditorApplication.Exit(1);
            return;
        }

        var profileId = settings.profileSettings.GetProfileId("Release");
        if (!string.IsNullOrEmpty(profileId)) settings.activeProfileId = profileId;

        AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
        if (string.IsNullOrEmpty(result.Error)) return;

        Debug.LogError($"Addressables build failed: {result.Error}");
        EditorApplication.Exit(1);
    }

    public static void BuildUpdate()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var statePath = ContentUpdateScript.GetContentStateDataPath(false);
        var result = ContentUpdateScript.BuildContentUpdate(settings, statePath);
        if (result != null && string.IsNullOrEmpty(result.Error)) return;

        Debug.LogError($"Addressables update failed: {result?.Error}");
        EditorApplication.Exit(1);
    }
    #endregion
}
```

Scripting defines and the development flag are passed from the Player build to the content build only when the define `ADDRESSABLES_ADD_DEFINES` is set (2.9.1+).

## Content update workflow (remote content)

1. Ship a Player with a **full build**. Archive `addressables_content_state.bin` for that release (commit it or store it with the build artifacts).
2. Make content changes (assets and serialized data only; code cannot be updated through Addressables).
3. Create a version-control branch, then run **Tools > Check for Content Update Restrictions** with the archived state file. Changed assets from Prevent Updates groups move to a new remote group.
4. **Build > Update a Previous Build** with the same state file. Upload the new bundles and the new catalog (`catalog_*.bin/.json` + `.hash`) to the remote load path.
5. Clients call `CheckForCatalogUpdates` / `UpdateCatalogs` (or rely on the startup check) and download changed bundles on demand or via `DownloadDependenciesAsync`.
6. The next Player release uses a new full build and a new state file. Keep using the same state file for every update to the same Player release.

Script changes that alter serialized layout, MonoScript identity (class rename, namespace, assembly move) or the Unity version require a new Player and full content build. **MonoScript Bundle Naming Prefix** separates MonoScript data so data-only changes stay small.

## Cloud Content Delivery (CCD)

- Remote load path for a badge: `https://[ProjectID].client-api.unity3dusercontent.com/client_api/v1/environments/[EnvironmentName]/buckets/[BucketID]/release_by_badge/[BadgeName]/entry_by_path/content/?path=`
- Or pin a release: `.../releases/[ReleaseID]/entry_by_path/content/?path=`
- With **Enable CCD Features** (Settings) and `com.unity.services.ccd.management` installed, profiles can target a bucket and badge directly and the build can upload. Content Directory groups are never uploaded to CCD.
- Use separate buckets per platform and environment; promote by moving badges, not by rebuilding.
- Use one badge per Player version (for example `v1_2`) so old clients keep loading content compatible with their catalog.

Any static HTTP host works the same way (S3 + CloudFront, GCS, your CDN). Test locally with Hosting Services or `npx http-server` on the remote build folder.

## Analyze and duplicate dependencies

Window > Asset Management > Addressables > Analyze:

| Rule | Type | Action |
|---|---|---|
| Check Duplicate Bundle Dependencies | Auto Fix | Finds non-Addressable assets pulled into several bundles. "Fix" moves them into a new `Duplicate Asset Isolation` group; better to move them into a meaningful `Shared_*` group yourself |
| Check Scene to Addressable Duplicate Dependencies | Manual | Assets used by both Scene List scenes and Addressables: shipped twice. Move scenes to Addressables or the assets out |
| Check Resources to Addressable Duplicate Dependencies | Manual | Assets in `Resources` that Addressables also depends on. Move them out of `Resources` and make them Addressable |
| Bundle Layout Preview | Manual | Shows which implicit assets each bundle will contain |

The duplicate-bundle rule runs a full build layout pass, so it is slow on large projects. Run it in CI and fail on new duplicates. Custom rules: the "Custom Analyze Rules" sample in the package.

Common duplication sources: a material or shader used by prefabs in different groups; a ScriptableObject that directly references many sprites or prefabs (all become implicit dependencies of its bundle, see the SKILL pitfalls); fonts and TMP font assets; the same texture inside a SpriteAtlas and also referenced directly.
