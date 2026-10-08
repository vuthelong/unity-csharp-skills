---
name: sprite-editor
description: Edits sprite metadata (names, rects, pivots, 9-slice borders, outlines, physics shapes) and slices sprite sheets (automatic, grid, isometric) by generating editor C# against ISpriteEditorDataProvider and SpriteDataProviderFactories, run in the open Editor via `unity command eval`. Works for TextureImporter, PSDImporter, and custom importers. Use when the user asks to slice a sprite sheet, set pivots or borders, rename sprites, set custom outlines, extract a sprite to PNG, or otherwise edit what the Sprite Editor window edits. Not for atlas packing (manage-sprite-atlas) or texture compression settings (unity-texture-import).
license: Unity Companion License (see licenses/UNITY-COMPANION-LICENSE.md)
metadata:
  category: 2d
  sources: "Unity-Technologies/skills/skills/sprite-editor"
  unity: "6000.0+"
---

# Sprite Editor

Sprite metadata lives inside the asset importer, not in a file you can safely edit. Reach it by running C# in a live Editor.

## Workflow

1. **Get a connected Editor with `eval`.** Follow `unity-cli` to install the CLI, connect to the Editor, add `com.unity.pipeline`, and tell a missing Editor apart from one stuck in Safe Mode. Then confirm `eval` is in the command catalog (`unity command --format json`); its presence depends on the Pipeline package version. Use `unity command eval --code '<snippet>'`. Only use `eval_file` if the catalog lists it. Default timeout is 30 s.
2. **Build the snippet from the Safe Core Pattern** in [references/templates.md](references/templates.md): get the data provider, check edit capabilities, modify, `Apply()`, `SaveAndReimport()`.
3. **Abort on a failed capability check.** Never bypass it, even if you think the call would work; that is how importer data gets corrupted.
4. **Verify**: re-read `GetSpriteRects()` (or the Console / Project window) and report what changed.

If no Editor is reachable, stop and say so. Never hand-edit `.meta` files to change sprite data.

## Passing C# to `eval`

`eval` compiles a statement block, not a file:
- No `using` directives (`CS0210`: parsed as a disposal statement).
- Fully qualify types: `UnityEditor.AssetDatabase`, `UnityEditor.U2D.Sprites.SpriteRect`, `UnityEngine.Object` (bare `Object` is ambiguous with `object`, `CS0104`).

The files in `scripts/` are written as normal C# with usings. When you inline them into `eval`, qualify every type; when you save them into the project, put them in an `Editor` folder.

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
- [scripts/SpriteToPng.cs](scripts/SpriteToPng.cs): render one sprite's mesh to PNG bytes.

Read [references/scripts-guide.md](references/scripts-guide.md) when slicing or combining scripts (modes, parameters, name generators). Read [references/api-reference.md](references/api-reference.md) for every data provider interface, `SpriteRect` fields, and coordinate-space rules.

## Rules

- All sprite data (rects, borders, pivots, outlines) is in **original source image pixels**, not the imported texture size. A 4096 PNG imported at Max Size 2048 still has rect `(0,0,4096,4096)`. Slice with `GetTextureToSlice`, never with `sprite.texture` directly.
- When adding or removing sprites, keep `ISpriteNameFileIdDataProvider` in sync (`SpriteEditorUtility.ApplySpriteRects` does this). Otherwise new sprites get unstable file IDs and references to existing sprites can break.
- For a `TextureImporter`, `textureType` must be `Sprite` and `spriteImportMode` `Multiple` before slicing into several sprites.
- Use enum values, never magic numbers: `SpriteAlignment.Custom`, not `9`; cast with `(int)SpriteAlignment.Center` only where an API takes `int`.
- Generate standalone snippets. Do not wrap the work in an `AssetPostprocessor` or `[MenuItem]`.
- Do not use the legacy `TextureImporter.spritesheet` / `SpriteMetaData` path; it bypasses the data provider and does not work for PSD or custom importers.
- `UnityEditorInternal.InternalSpriteUtility` (used for auto and grid detection) is internal API; it works in 6000.x but can change between minors. If it fails to compile, fall back to computing rects yourself.
