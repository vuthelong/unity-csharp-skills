---
name: sprite-editor
description: Edits sprite metadata (rects, names, pivots, 9-slice borders, outlines, physics shapes) and slices sprite sheets (automatic, grid, isometric) through ISpriteEditorDataProvider, run in the Editor via unity command eval or as a generated Editor script. Use when the user asks to slice a sprite sheet, set pivots or borders, rename sprites, set custom outlines, or extract a sprite to PNG. Not for atlas packing or texture compression.
license: Unity Companion License (see licenses/UNITY-COMPANION-LICENSE.md)
metadata:
  category: 2d
  sources: "https://github.com/Unity-Technologies/skills/tree/main/skills/sprite-editor"
  unity: "6000.0+"
---

# Sprite Editor

Sprite metadata lives inside the asset importer, not in a file you can safely edit. Reach it by running C# in a live Editor.

## Workflow

1. **Prefer a connected Editor with `eval`.** Follow `unity-cli` to connect and confirm `eval` is in the command catalog. If `eval` is unavailable (no Editor reachable, no Pipeline package, or a Pipeline version that does not compile on this Editor), use the fallback below instead of stopping.
2. **Build the snippet from the Safe Core Pattern** in [references/templates.md](references/templates.md): get the data provider, check edit capabilities, modify, `Apply()`, `SaveAndReimport()`.
3. **Abort on a failed capability check.** Never bypass it, even if you think the call would work; that is how importer data gets corrupted.
4. **Verify**: re-read `GetSpriteRects()` (or the Console / Project window) and report what changed.

**Fallback without `eval`:** write the same code as an Editor script under an `Editor` folder with a `[MenuItem]` entry point, and tell the user which menu item to run and what to check afterwards. Never hand-edit `.meta` files to change sprite data.

## Passing C# to `eval`

Eval syntax and prerequisites: see the `unity-cli` skill (unity-editor-tooling plugin) → Eval syntax.

The files in `scripts/` are written as normal C# with usings. When you inline them into `eval`, qualify every type (`UnityEditor.U2D.Sprites.SpriteRect`, `UnityEngine.Object`); when you save them into the project, put them in an `Editor` folder.

## Operations

| Task | Capability required | Reference |
|---|---|---|
| Rename, move/resize rect, set border | `EditSpriteName`, `EditSpriteRect`, `EditBorder` | Safe Core Pattern |
| Set pivot / alignment | `EditPivot` | [scripts/SetPivotExample.cs](scripts/SetPivotExample.cs) |
| Add, delete, or slice sprites | `CreateAndDeleteSprite` | slicing scripts below |
| Custom outline / physics shape | none beyond provider presence | `ISpriteOutlineDataProvider`, `ISpritePhysicsOutlineDataProvider` |

Slicing (all go through [scripts/GenerateNewSpriteRects.cs](scripts/GenerateNewSpriteRects.cs), which handles names, overlap with existing sprites, and file ID bookkeeping):
- [scripts/AutomaticSliceTexture.cs](scripts/AutomaticSliceTexture.cs): detect opaque islands.
- [scripts/GridSliceTexture.cs](scripts/GridSliceTexture.cs): fixed cell size, offset, padding.
- [scripts/IsometricSliceTexture.cs](scripts/IsometricSliceTexture.cs): diamond tiles with diamond outlines.
- [scripts/GetTextureToSlice.cs](scripts/GetTextureToSlice.cs): readable texture at original image size (required before slicing).
- [scripts/GetTextureSourceImageSize.cs](scripts/GetTextureSourceImageSize.cs): original source image width/height (the space sprite rects use) without making a readable copy.
- [scripts/SpriteToPng.cs](scripts/SpriteToPng.cs): render one sprite's mesh to PNG bytes.

Read [references/scripts-guide.md](references/scripts-guide.md) when slicing or combining scripts (modes, parameters, name generators). Read [references/api-reference.md](references/api-reference.md) for every data provider interface, `SpriteRect` fields, and coordinate-space rules.

## Rules

- All sprite data (rects, borders, pivots, outlines) is in **original source image pixels**, not the imported texture size. A 4096 PNG imported at Max Size 2048 still has rect `(0,0,4096,4096)`. Slice with `GetTextureToSlice`, never with `sprite.texture` directly.
- When adding or removing sprites, keep `ISpriteNameFileIdDataProvider` in sync (`SpriteEditorUtility.ApplySpriteRects` does this). Otherwise new sprites get unstable file IDs and references to existing sprites can break.
- For a `TextureImporter`, `textureType` must be `Sprite` and `spriteImportMode` `Multiple` before slicing into several sprites.
- Use enum values, never magic numbers: `SpriteAlignment.Custom`, not `9`; cast with `(int)SpriteAlignment.Center` only where an API takes `int`.
- Prefer standalone `eval` snippets. Do not wrap the work in an `AssetPostprocessor`; a `[MenuItem]` wrapper is fine for the no-`eval` fallback.
- Do not use the legacy `TextureImporter.spritesheet` / `SpriteMetaData` path; it bypasses the data provider and does not work for PSD or custom importers.
- `UnityEditorInternal.InternalSpriteUtility` (used for auto and grid detection) is internal API; it works in 6000.x but can change between minors. If it fails to compile, fall back to computing rects yourself.
