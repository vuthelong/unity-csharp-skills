# Tile Palettes and Isometric Sorting

## `GridPaletteUtility.CreateNewPalette` (`UnityEditor.Tilemaps`)

```csharp
GameObject CreateNewPalette(
    string folderPath,
    string name,
    GridLayout.CellLayout layout,
    GridPalette.CellSizing cellSizing,
    Vector3 cellSize,
    GridLayout.CellSwizzle swizzle,
    TransparencySortMode sortMode,
    Vector3 sortAxis)
```

Returns the palette prefab root (or `null`). `folderPath` is project-relative (`Assets/Palettes`) and must exist. The prefab contains a `Grid` and a `GridPalette` sub-asset; confirm it with `AssetDatabase.LoadAllAssetsAtPath(path).OfType<GridPalette>().Any()`.

Parameters:
- `CellLayout`: `Rectangle`, `Hexagon`, `Isometric`, `IsometricZAsY`.
- `CellSizing`: `Automatic` (derived from sprites) or `Manual`.
- `CellSwizzle`: `XYZ` normally; `YXZ` for flat-top hexagons.
- `sortMode` / `sortAxis`: how the palette preview sorts tiles; match the scene's sorting.

The ready-made helpers live in `scripts/CreatePaletteTemplate.cs` (`CreateRectangularPalette`, `CreateHexagonalPalette`, `CreateIsometricPalette`, `CreateIsometricZAsYPalette`, `CreateCustomPalette`).

## Adding tiles to a palette

Drag tiles or sprites into the Tile Palette window (sprites create Tile assets on the fly). From code, load the palette prefab with `PrefabUtility.LoadPrefabContents`, paint the child `Tilemap` with `SetTile`, then `PrefabUtility.SaveAsPrefabAsset` and `UnloadPrefabContents`.

## Isometric sorting (scene)

Isometric tiles need sorting along Y so tiles further up draw first:

| Grid | Transparency Sort Mode | Axis |
|---|---|---|
| Isometric | Custom Axis | (0, 1, 0) |
| Isometric Z as Y | Custom Axis | (0, 1, -0.26) |

- **URP (2D Renderer)**: set it on the 2D Renderer Data asset (Transparency Sort Mode / Axis). The Graphics settings fields are ignored.
- **Built-in**: Project Settings > Graphics > Camera Settings.
- Set `TilemapRenderer.mode = Individual` when characters must interleave with tiles.
- For Z as Y, tile height comes from cell Z; the Tile Palette brush exposes Z position (`-` / `=` keys) for raised tiles.

Hexagonal grids use default sorting.
