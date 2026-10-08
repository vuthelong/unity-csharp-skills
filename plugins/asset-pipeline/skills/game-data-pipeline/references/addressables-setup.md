# Marking generated data as Addressable

Read when installing `scripts/Editor/GameDataAddressables.cs`, changing the GameData group, or chasing catalog churn. APIs from `com.unity.addressables` 2.x (Unity 6); they are unchanged from 1.21.

## What the script does

`GameDataAddressables.SyncAll()` (menu **Tools > Game Data > Sync Addressables**, also run by `GameDataAddressablesPostprocessor` whenever an asset under `Generated/` is imported, moved or deleted):

1. `AddressableAssetSettingsDefaultObject.GetSettings(true)`: gets the settings, creating `Assets/AddressableAssetsData` if the project has none. Use `AddressableAssetSettingsDefaultObject.Settings` (no creation) in postprocessors; the postprocessor does that and only warns.
2. Finds or creates the `GameData` group with `settings.CreateGroup(name, setAsDefaultGroup: false, readOnly: false, postEvent: true, schemasToCopy: null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema))`. Schemas are configured only when the group is created (or from **Reset GameData Group Schema**), so manual tweaks in the Groups window survive.
3. For every asset under `Generated/` whose main type derives from `GameDataTable`:
   - stamps `schemaVersion` from `CodeSchemaVersion`;
   - `settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: false)` when the entry is missing or in another group;
   - `entry.SetAddress("gamedata/<ClassName>")`, from the file name with the namespace stripped (`Game.Data.HeroTable.asset` -> `gamedata/HeroTable`, folder mode `Game.Data.StageTable01.asset` -> `gamedata/StageTable01`);
   - `entry.SetLabel("gamedata", true, force: true)` (`force` also adds the label to the settings).
4. Removes entries in the group whose asset is gone or lives outside `Generated/`.
5. Raises one `settings.SetDirty(ModificationEvent.EntryMoved, changedEntries, postEvent: true, settingsModified: true)` only when something changed, then `AssetDatabase.SaveAssets()`.

Every step compares before writing, so re-running it on unchanged data does not modify any file.

Edit the constants at the top for your project: `GeneratedFolder`, `GroupName`, `Label`, `AddressPrefix`, `UseRemoteGroup`.

## Group schema settings

| Setting | Local data (default) | Remote balance data (`UseRemoteGroup = true`) |
|---|---|---|
| `BundledAssetGroupSchema.BundleMode` | `PackTogether`: one small bundle, one file open | `PackSeparately`: a balance change re-downloads only the tables that changed |
| `Compression` | `LZ4` | `LZMA` for download size (cached bundles are recompressed to LZ4 when `Caching.compressionEnabled`) |
| `BuildPath` / `LoadPath` | `Local.BuildPath` / `Local.LoadPath` | `Remote.BuildPath` / `Remote.LoadPath` |
| `BundleNaming` | `AppendHash` | `AppendHash` (required so old and new bundles never share a file name) |
| `UseAssetBundleCache` | on | on |
| `UseAssetBundleCrc` | off | on |
| `ContentUpdateGroupSchema.StaticContent` ("Prevent Updates") | on | off |
| `IncludeInBuild` | on | on |

Remote also sets `settings.BuildRemoteCatalog = true` and points `RemoteCatalogBuildPath` / `RemoteCatalogLoadPath` at the remote profile variables. Set the `Remote.LoadPath` profile value to your CDN, ideally including the app version (`https://cdn.example.com/gamedata/[BuildTarget]/[UnityEditor.PlayerSettings.bundleVersion]`) so each app version reads data built for its own code.

A hybrid is common: keep a `GameData` local group for tables that never change post-release (localization keys, level geometry ids) and a `GameDataRemote` group for balance tables. Put them in separate generated subfolders and run the sync once per folder/group pair.

Do not put CSVs, `CsvConfig`, `CsvData` reader configs or the service-account key in any group.

## Keeping GUIDs (and the catalog) stable

Addressables entries are keyed by asset GUID. A new GUID means a new entry, a different bundle content hash and, for remote groups, a content update every client downloads. Keep GUIDs stable:

- **Re-bake in place.** Upstream's `GetScriptableObject` loads the existing asset and only calls `CreateAsset` when none exists at `<scriptableObjectPath>/<Namespace.ClassName>.asset`. Never "clean" by deleting `Generated/` before a reimport.
- **Commit `.meta` files** for generated assets, and never generate them only in CI from a fresh checkout.
- **Do not rename the table class, its namespace or `scriptableObjectPath`** casually: the output path changes, a new asset with a new GUID appears and the old one is orphaned. If you must, move the old asset with `AssetDatabase.MoveAsset` to the new path first (the GUID moves with it), then reimport.
- **Folder mode with `separateScriptableObject`**: the asset name comes from the file name; renaming `stage_ch01.csv` creates a new asset.
- **Addresses come from names, not GUIDs**, so code keys (`gamedata/HeroTable`) survive a GUID change; labels make code independent of both.
- Run the same Unity and Addressables versions everywhere; a version bump can reserialize `AddressableAssetsData` once, which is expected.

Unchanged CSV content still rewrites the asset on reimport, but the YAML is identical, so git and the bundle hash see no change.

## Avoiding duplicated dependencies

A `GameData` bundle should contain nothing but the tables. Any `UnityEngine.Object` a table references (a `Sprite` set by a convert method, a prefab, a `ScriptableObject` that is not itself in a group) is pulled into the bundle implicitly, duplicated in every other bundle that also uses it, and re-downloaded with every balance update. Use string addresses or ids instead (see `layout-and-data-design.md`), and run the **Check Duplicate Bundle Dependencies** rule in the Addressables Analyze window (Window > Asset Management > Addressables > Analyze) after adding a table.
