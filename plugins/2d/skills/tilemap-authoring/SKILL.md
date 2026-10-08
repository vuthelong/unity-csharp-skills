---
name: tilemap-authoring
description: Builds 2D tile levels with Unity Tilemap (com.unity.2d.tilemap) and Rule Tiles (com.unity.2d.tilemap.extras) — creates Grid/Tilemap/TilemapRenderer hierarchies, Tile and RuleTile/IsometricRuleTile/HexagonalRuleTile assets, Tile Palettes (rectangular, hexagonal, isometric, isometric Z-as-Y) via GridPaletteUtility.CreateNewPalette, paints and fills cells from C# (SetTile, SetTiles, SetTilesBlock), sets up TilemapCollider2D with CompositeCollider2D, and generates auto-tiling TilingRules from terrain sprites by segmenting each sprite into a 3x3 color grid. Use when the user wants a tilemap level, a tile palette, auto-tiling walls/roads/terrain, neighbor rules for tiles, an empty RuleTile template, or isometric/hex tile sorting fixes.
license: Unity Companion License (see licenses/UNITY-COMPANION-LICENSE.md)
metadata:
  category: 2d
  sources: "Unity-Technologies/skills/skills/tilemap-palette-create, Unity-Technologies/skills/skills/tilemap-ruletile-createempty, Unity-Technologies/skills/skills/tilemap-ruletile-createfromsegment, Unity-Technologies/skills/skills/sprite-segment-3x3grid, AlexeyPerov/Unity-Open-MCP/skills/extensions/tilemap"
  unity: "6000.0+"
---

# Tilemap Authoring

## Vocabulary

- **Grid**: parent GameObject that defines cell layout (`Rectangle`, `Hexagon`, `Isometric`, `IsometricZAsY`) and cell size.
- **Tilemap**: child GameObject with `Tilemap` (cell data) + `TilemapRenderer`. Several Tilemaps share one Grid as layers (Ground, Walls, Decor).
- **Tile**: `ScriptableObject` asset (`TileBase` subclass). `Tile` = sprite + color + collider type. `RuleTile` (extras) picks its sprite from its neighbors.
- **Tile Palette**: a prefab with a `Grid` and a `GridPalette` sub-asset; the painting source in the Tile Palette window.
- Cells are `Vector3Int` (`x`, `y` cell; `z` layer, usually 0).

## Preconditions

- `com.unity.2d.tilemap` (built into the 2D feature set). Rule Tiles need `com.unity.2d.tilemap.extras` 4.x or newer. Install with `unity-package-management`.
- Editor scripts (`UnityEditor.Tilemaps`, `AssetDatabase`) go in an `Editor` folder. If code lives in an asmdef, reference `Unity.2D.Tilemap.Extras` for `RuleTile`.
- Tile sprites need a consistent PPU equal to the cell size in pixels. Slice sheets with `sprite-editor`.

## Pick the workflow

| User wants | Do |
|---|---|
| A level layer, painted from code | Workflow A |
| A Tile Palette asset | Workflow B |
| An empty RuleTile to fill in later (no sprites given) | Workflow C |
| Auto-tiling from existing terrain/edge sprites | Workflow D |

### A. Grid, Tilemap, tiles, painting

1. Create the hierarchy (reuse an existing Grid for extra layers): `Grid` GameObject -> child with `Tilemap` + `TilemapRenderer`.
2. Create one `Tile` asset per distinct visual (`ScriptableObject.CreateInstance<Tile>()`, set `sprite`, `AssetDatabase.CreateAsset`). Save assets before painting with them.
3. Paint: `SetTile` for single cells, `SetTiles` / `SetTilesBlock` for batches (one refresh instead of N). Prefer `SetTilesBlock(BoundsInt, TileBase[])` for rectangles; `BoxFill` has flood-fill semantics that surprise people.
4. Collisions: `TilemapCollider2D` + `CompositeCollider2D` (adds a `Rigidbody2D`; set it Static). On Unity 6 set `tilemapCollider.compositeOperation = Collider2D.CompositeOperation.Merge`; `usedByComposite` is obsolete.
5. Call `tilemap.CompressBounds()` after large erases, and `EditorSceneManager.MarkSceneDirty` when editing a scene from an editor script.

Code for every step: [references/tilemap-api.md](references/tilemap-api.md).

### B. Tile Palette

Gather name, grid type, optional cell size, and (isometric) sort axis; ask if missing. Create with `GridPaletteUtility.CreateNewPalette` using [scripts/CreatePaletteTemplate.cs](scripts/CreatePaletteTemplate.cs). Defaults:

| Grid | Cell sizing | Cell size | Sort mode / axis |
|---|---|---|---|
| Rectangular | Automatic | (1, 1, 0) | Default |
| Hexagonal (point top) | Manual | (0.8659766, 1, 1) | Default |
| Isometric | Manual | (1, 0.5, 1) | CustomAxis (0, 1, 0) |
| Isometric Z as Y | Manual | (1, 0.5, 1) | CustomAxis (0, 1, -0.26) |

Verify the created prefab has a `GridPalette` sub-asset. Details and sorting rules: [references/palettes-and-sorting.md](references/palettes-and-sorting.md).

### C. Empty RuleTile

Only when the user supplied no sprites; otherwise use D.
1. Choose the type: `RuleTile` (rectangular), `IsometricRuleTile` (same 3x3 neighborhood in cell space), or `HexagonalRuleTile`.
2. Rectangular/isometric: `RuleTileTemplates.CreateEmpty<RuleTile>(path, useRotatedTemplate)` in [scripts/RuleTileTemplates.cs](scripts/RuleTileTemplates.cs) creates the 47-rule Fixed or 15-rule Rotated template with one `null` sprite per rule.
3. Hexagonal: build the 38-rule template from [references/hexagonal-ruletile-patterns.md](references/hexagonal-ruletile-patterns.md).

### D. RuleTile from sprites

1. Make sure the sheet is sliced into one sprite per tile configuration.
2. Segment every sprite into a 3x3 grid against its center's majority color ([scripts/SpriteSegment3x3Grid.cs](scripts/SpriteSegment3x3Grid.cs)), producing patterns like `X X X / X * X / . . .`.
3. Convert patterns to TilingRules, drop unknown patterns (unless the user opts out), dedupe (first sprite wins), sort by specificity, set the default sprite: [scripts/TilemapRuleTileCreateFromSegment.cs](scripts/TilemapRuleTileCreateFromSegment.cs).
4. One-click editor path: `RuleTileGenerator.Generate(texture)` in [scripts/RuleTileGenerator.cs](scripts/RuleTileGenerator.cs) (temporarily enables Read/Write, writes `<texture>_RuleTile.asset`). Step-by-step control: [scripts/ManualWorkflowExample.cs](scripts/ManualWorkflowExample.cs).
5. Report the rule count and any skipped sprites with their patterns.

Algorithm, symbol mapping, thresholds, and the pattern table: [references/rule-tiles.md](references/rule-tiles.md) and [references/ruletile-patterns.md](references/ruletile-patterns.md).

## Rules and pitfalls

- `.` = `Neighbor.This` (1). `X` = don't care: leave the position out of `m_Neighbors` / `m_NeighborPositions`. Use `Neighbor.NotThis` (2) only when the user explicitly asks to forbid a neighbor.
- Rules evaluate top to bottom, first match wins. Keep the most specific rules first and the 0-neighbor rule last.
- Mark generated assets dirty (`EditorUtility.SetDirty`) and `AssetDatabase.SaveAssets()`; RuleTile edits made from code are otherwise lost on reload.
- Seams between tiles are a texture/camera problem, not a tilemap one: see `2d-pixel-perfect` and pack tiles with `manage-sprite-atlas` (padding >= 4, tight packing off).
- Prefer `Object.FindFirstObjectByType<Grid>()` / `FindAnyObjectByType`; `FindObjectOfType` is obsolete on 6000.x.
- Never hand-edit `.prefab`/`.asset` YAML for palettes or rule tiles; use the APIs.
