# Sprite Atlas Pitfalls, Migration, and Checklists

## Invalid patterns

| Wrong | Right |
|---|---|
| `new SpriteAtlas()` / `ScriptableObject.CreateInstance<SpriteAtlas>()` in editor code | `new SpriteAtlasAsset()` + `SpriteAtlasAsset.Save` |
| `spriteAtlas.Add(...)`, `spriteAtlas.SetPackingSettings(...)` (V1 extensions) | `SpriteAtlasAsset.Add`, `SpriteAtlasImporter.packingSettings` |
| `AssetDatabase.LoadAssetAtPath<SpriteAtlasAsset>(path)` | `SpriteAtlasAsset.Load(path)` |
| `atlasAsset.SetIncludeInBuild(true)` and other setters on the asset | Same property on `SpriteAtlasImporter`, then `SaveAndReimport()` |
| `variant.SetMasterAtlas(path)` / `SetMasterAtlasPath(path)` | `SetMasterAtlas(AssetDatabase.LoadAssetAtPath<SpriteAtlas>(masterPath))` |
| `atlasAsset.GetPackables()` | `AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path).GetPackables()` |
| `format = (int)TextureImporterFormat.ASTC_6x6` | `format = TextureImporterFormat.ASTC_6x6` |
| `SpriteAtlasUtility.PackAtlases(atlasAssets, ...)` with `SpriteAtlasAsset[]` | Load `SpriteAtlas[]` first |
| `AssetDatabase.FindAssets("t:SpriteAtlas", SearchMode.AllAssets)` | `FindAssets("t:SpriteAtlas", new[] { "Assets" })` (two overloads only) |
| `AssetImporter.GetAtPath(path, typeof(SpriteAtlasImporter))` | `AssetImporter.GetAtPath(path) as SpriteAtlasImporter` |
| `.spriteatlas` extension for new V2 atlases | `.spriteatlasv2` |
| Changing importer properties without `SaveAndReimport()` | Always finish with `SaveAndReimport()` |
| Modifying the importer struct in place (`importer.packingSettings.padding = 4`) | Copy, change, assign back |

## Why an atlas "does nothing"

1. Sprite Packer Mode is `Disabled` (read it back).
2. `includeInBuild = false` and no `atlasRequested` handler: sprites render invisible in the player.
3. Master and variant both `includeInBuild = true`: Unity picks one non-deterministically and logs a warning. Ship exactly one.
4. The same sprite is in two atlases: Unity warns and picks one; draw calls do not drop as expected.
5. Play Mode with `SpriteAtlasV2Build`: atlases pack only in builds.
6. Packables outside `Assets/` were silently ignored.
7. The atlas was generated but the scene references sprites from a different texture (re-sliced sheet with new file IDs; see `sprite-editor`).

## Settings guidance

- **UI atlases**: rotation off, tight packing off. UI `Image` draws the rect, so tight-packed neighbors bleed in and rotated sprites render sideways.
- **World sprites**: tight packing on saves space; rotation is safe for `SpriteRenderer` but gains little.
- **Pixel art / tiles**: point filter, no mipmaps, uncompressed, padding >= 4, tight packing off (prevents seams; see `2d-pixel-perfect`).
- **Alpha dilation** fills transparent padding with edge colors to stop dark fringes under bilinear filtering; leave off for point-filtered art.
- **Mipmaps** off for UI and 2D cameras that do not zoom out; on for world sprites seen at many scales.
- **Readable** off unless code reads pixels; it doubles memory.
- Group atlases by what is on screen together (one per screen/level/character), not by type; a 4096 atlas loaded for one icon wastes memory.
- Keep atlases at or below 2048 on mobile; let multiple pages form rather than forcing 4096.

## Runtime

- Register `atlasRequested` before the first scene loads; cache loaded atlases; never load the same address twice concurrently (track the in-flight handle).
- Release Addressables handles when the content is gone; atlases are reference-counted through the handle.
- `GetSprite` clones; cache by name and destroy clones you created when done.
- Do not call `SpriteAtlasUtility` or any `UnityEditor` API from runtime code; wrap editor scripts in `#if UNITY_EDITOR` or an Editor folder.
- `Resources.Load<SpriteAtlas>` works but forces the atlas into the Resources bundle; prefer Addressables for on-demand atlases.

## Build-time behaviour

- Generating atlases in `IPreprocessBuildWithReport` is supported: assets created and reimported there are included. Keep `callbackOrder` low (the shipped generator uses -100) so it runs before other processors, including Addressables.
- Do not pack or modify atlases in `IPostprocessBuildWithReport`; the player is already built.
- `PackAtlases` in a prebuild hook is unnecessary; the build packs.

## V1 to V2 migration

1. Project Settings > Editor > Sprite Atlas Mode: Sprite Atlas V2 - Enabled. Unity offers to migrate V1 `.spriteatlas` assets to `.spriteatlasv2` (one-way; commit first).
2. Replace V1 editor scripting:
   - `ScriptableObject.CreateInstance<SpriteAtlas>()` + `AssetDatabase.CreateAsset` -> `new SpriteAtlasAsset()` + `SpriteAtlasAsset.Save`.
   - `SpriteAtlasExtensions.Add/Remove` on `SpriteAtlas` -> `SpriteAtlasAsset.Add/Remove`.
   - `SpriteAtlasExtensions.SetPackingSettings/SetTextureSettings/SetPlatformSettings/SetIncludeInBuild/SetIsVariant/SetMasterAtlas` -> `SpriteAtlasImporter` properties (and `SpriteAtlasAsset.SetIsVariant/SetMasterAtlas` for variants).
3. Update build scripts that searched for `.spriteatlas` paths or `t:SpriteAtlas` and then cast to V1 types.
4. Runtime code (`SpriteAtlas`, `SpriteAtlasManager`) does not change.

## Checklists

**Create**: packer mode on and read back -> collect `Assets/` packables -> `SpriteAtlasAsset.Save` -> `ImportAsset` -> importer texture/packing/platform settings -> `includeInBuild` -> `SaveAndReimport` -> verify `spriteCount`.

**Ship**: one of master/variant included; no sprite in two atlases; late-binding handler present for every excluded atlas; Addressables content built with the player.
