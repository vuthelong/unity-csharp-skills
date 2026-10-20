---
name: manage-sprite-atlas
description: Creates, configures, and loads Sprite Atlas V2 assets from editor C#, by default through a prebuild generator - packables, packing and per-platform texture settings, HD/SD variants, late binding with Addressables, and runtime GetSprite caching. Use when the user wants to create or optimize sprite atlases, cut draw calls or texture memory for 2D/UI sprites, fix sprites missing at runtime because of atlases, or load atlases on demand.
license: Unity Companion License
metadata:
  category: 2d
  sources: "https://github.com/Unity-Technologies/skills/tree/main/skills/manage-sprite-atlas, https://github.com/AlexeyPerov/Unity-Open-MCP/tree/master/skills/extensions/spriteatlas"
  unity: "6000.0+"
---

# Sprite Atlas V2

V2 splits authoring from runtime:

| Context | Type | Use for |
|---|---|---|
| Editor authoring | `SpriteAtlasAsset` (`UnityEditor.U2D`) | `Load`, `Save`, `Add`/`Remove` packables, variant/master links, custom packer |
| Editor settings | `SpriteAtlasImporter` (`UnityEditor.U2D`) | Texture, packing, platform settings, `includeInBuild`, `variantScale`; persist with `SaveAndReimport()` |
| Editor packing | `SpriteAtlasUtility` | Optional preview packing; builds pack automatically |
| Runtime | `SpriteAtlas` (`UnityEngine.U2D`) | `GetSprite`, `GetSprites`, `CanBindTo`, `spriteCount` |
| Runtime loading | `SpriteAtlasManager` | `atlasRequested` / `atlasRegistered` for late binding |

Never create or edit a `SpriteAtlas` in editor code. The one editor use is reading packables with the `SpriteAtlas.GetPackables()` extension. Settings set on a `SpriteAtlasAsset` in memory are **not** persisted by `SpriteAtlasAsset.Save`; always configure through `SpriteAtlasImporter`.

## Before writing code

1. **Look for earlier generated scripts.** Search for `// [UNITY-SKILL:SPRITEATLAS]`. If found, list them and ask: update in place, create new ones with different names, or abort (make no changes).
2. **Ask for the delivery mechanism.**
   - **Built-in** (`includeInBuild = true`): core UI, always-needed gameplay sprites. Simple, instant.
   - **Addressables late binding** (`includeInBuild = false`): DLC, per-level art, skins, localized sprites. Needs `com.unity.addressables`; read [references/addressables-delivery.md](references/addressables-delivery.md) and generate everything it lists.
3. **Read the scripts you will adapt**: [scripts/SpriteAtlasAuthoring.cs](scripts/SpriteAtlasAuthoring.cs) (helpers), [scripts/SpriteAtlasPrebuildGenerator.cs](scripts/SpriteAtlasPrebuildGenerator.cs) (default entry point), and [references/pitfalls.md](references/pitfalls.md). Writing atlas code from memory is the main source of atlases that import fine and then do nothing.

## Workflow

1. **Enable Sprite Packer Mode and read it back.** `SpriteAtlasAuthoring.EnsureSpritePackerEnabled()` sets `EditorSettings.spritePackerMode = SpriteAtlasV2` and returns the value actually stored. `Disabled` is the default zero value; an atlas created while packing is disabled still imports and looks finished but never packs (Inspector: "Sprite Atlas packing is disabled"). Report the value you read. Use `SpriteAtlasV2Build` only if the user wants packing for builds only.
2. **Generate atlases in a prebuild step (default).** Adapt `SpriteAtlasPrebuildGenerator.GenerateAll()`: one `Generate(...)` call per atlas, choosing a categorization strategy:
   - Folder: `CollectFolderPackable("Assets/Art/UI")` adds the folder itself, so new sprites join automatically.
   - Naming: `CollectSpriteTextures("Assets/Art", n => n.StartsWith("icon_"))`.
   - Labels or scene usage: filter `AssetDatabase.FindAssets("l:Label t:Texture2D", new[] { "Assets" })` or scene dependencies.
   `GenerateAll()` is static, so the same code also runs from CI or an existing editor tool; do not add a `[MenuItem]` unless the user asks for one.
3. **Pick a preset** (`AtlasPreset`): `UI` (no rotation, no tight packing: UI `Image` cannot render rotated or tight-packed sprites), `World` (tight packing), `PixelArt` (point filter, uncompressed, no tight packing; see `2d-pixel-perfect`).
4. **Variants** (HD/SD): `CreateVariant(masterPath, variantPath, scale)`. Only one of master/variant may be included in the build; the helper excludes the master when the variant ships.
5. **Verify**: re-read `EditorSettings.spritePackerMode`, open the atlas Inspector or call `SpriteAtlasAuthoring.PackForPreview(paths)` and check `SpriteAtlas.spriteCount`, then enter Play Mode and confirm sprites render (Frame Debugger shows one texture per batch).

Manual one-off authoring (atlas assets committed to the repo, created once) is fine when the user asks for it: call `CreateOrUpdateAtlas` + `ConfigureImporter` directly instead of from the build hook.

## Rules

- Only add packables from `Assets/`. Built-in resources cannot be packed and package assets are read-only.
- V2 atlas files use `.spriteatlasv2`.
- Assign enums directly: `platform.format = TextureImporterFormat.ASTC_6x6` (no casts).
- Scope `AssetDatabase.FindAssets` to `new[] { "Assets" }` or a subfolder; unscoped searches reach into `Packages/`.
- `SpriteAtlasImporter` settings are structs: copy, modify, assign back, then `SaveAndReimport()`.
- `SpriteAtlas.GetSprite` / `GetSprites` return a new clone on every call; cache and reuse them ([scripts/AtlasSpriteCache.cs](scripts/AtlasSpriteCache.cs)) and `Dispose()` the cache to destroy the clones when done.
- Do not hand-edit `.spriteatlasv2` or `.meta` files.

## References

- [references/api.md](references/api.md): full V2 API (members, settings structs, platform names and formats). Read when writing any atlas code.
- [references/pitfalls.md](references/pitfalls.md): invalid patterns, runtime gotchas, V1 to V2 migration, checklists. Read before generating code and when an atlas "does nothing".
- [references/addressables-delivery.md](references/addressables-delivery.md): late binding, defines, asmdefs, Addressables build integration.
- [scripts/SpriteAtlasLateBinding.cs](scripts/SpriteAtlasLateBinding.cs): ready-made `SpriteAtlasManager.atlasRequested` handler for atlases not included in the build (Addressables when `UNITY_ADDRESSABLES` is defined).
- [references/custom-packing.md](references/custom-packing.md): `ScriptablePacker` custom packing algorithms.

Related: `sprite-editor` (slicing before packing), `tilemap-authoring` (tile atlases), `unity-texture-import` (source texture settings), `ui-ugui` (UI Image constraints).
