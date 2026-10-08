# Tilemap Scripting Recipes

Namespaces: `UnityEngine.Tilemaps` (runtime), `UnityEditor` / `UnityEditor.SceneManagement` (editor). Snippets are editor-side unless noted.

## Create Grid + Tilemap layer

```csharp
public static Tilemap CreateLayer(string gridName, string layerName, int sortingOrder)
{
    var gridObject = GameObject.Find(gridName);
    Grid grid = null;
    if (gridObject == null || !gridObject.TryGetComponent(out grid))
    {
        var gridGo = new GameObject(gridName, typeof(Grid));
        Undo.RegisterCreatedObjectUndo(gridGo, "Create Grid");
        grid = gridGo.GetComponent<Grid>();
        grid.cellLayout = GridLayout.CellLayout.Rectangle;
        grid.cellSize = new Vector3(1f, 1f, 0f);
    }

    var layerGo = new GameObject(layerName, typeof(Tilemap), typeof(TilemapRenderer));
    Undo.RegisterCreatedObjectUndo(layerGo, "Create Tilemap");
    layerGo.transform.SetParent(grid.transform, false);
    layerGo.GetComponent<TilemapRenderer>().sortingOrder = sortingOrder;

    EditorSceneManager.MarkSceneDirty(layerGo.scene);
    return layerGo.GetComponent<Tilemap>();
}
```

For hexagonal grids set `cellLayout = Hexagon` and `cellSwizzle` (`XYZ` point-top, `YXZ` flat-top). For isometric use `Isometric` or `IsometricZAsY` with cell size `(1, 0.5, 1)`.

## Create a Tile asset

```csharp
public static Tile CreateTile(string assetPath, Sprite sprite, Tile.ColliderType collider = Tile.ColliderType.Sprite)
{
    var tile = ScriptableObject.CreateInstance<Tile>();
    tile.sprite = sprite;
    tile.colliderType = collider;
    AssetDatabase.CreateAsset(tile, AssetDatabase.GenerateUniqueAssetPath(assetPath));
    AssetDatabase.SaveAssets();
    return tile;
}
```

Load sprites from a sliced sheet with `AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()`. The Tile Palette window's drag-and-drop creates the same assets in bulk.

## Paint

```csharp
tilemap.SetTile(new Vector3Int(3, 2, 0), groundTile);

var bounds = new BoundsInt(-10, -10, 0, 21, 21, 1);
var block = new TileBase[bounds.size.x * bounds.size.y * bounds.size.z];
System.Array.Fill(block, groundTile);
tilemap.SetTilesBlock(bounds, block);

var positions = new Vector3Int[] { new(0, 0, 0), new(1, 0, 0) };
var tiles = new TileBase[] { wallTile, wallTile };
tilemap.SetTiles(positions, tiles);
```

- `SetTilesBlock` and `SetTiles` refresh once; looping `SetTile` refreshes per call.
- `SetTiles(TileChangeData[], bool ignoreLockFlags)` sets tile, color, and transform in one call.
- `BoxFill(position, tile, startX, startY, endX, endY)` floods from `position` within the box; use `SetTilesBlock` for a plain rectangle.
- Erase with `SetTile(pos, null)` or `ClearAllTiles()`. Run `CompressBounds()` afterwards so `cellBounds` shrinks.
- Undo for editor tools: `Undo.RegisterCompleteObjectUndo(tilemap, "Paint")` before painting.

## Read

```csharp
var occupied = new List<Vector3Int>();
foreach (var pos in tilemap.cellBounds.allPositionsWithin)
{
    if (tilemap.HasTile(pos))
        occupied.Add(pos);
}
Vector3Int cell = tilemap.WorldToCell(worldPos);
Vector3 center = tilemap.GetCellCenterWorld(cell);
```

`GetTilesBlock(bounds)` returns a flat array in x, then y, then z order. `GetUsedTilesCount` / `GetUsedTilesNonAlloc` list distinct tiles.

## Colliders

```csharp
var tilemapCollider = tilemap.gameObject.AddComponent<TilemapCollider2D>();
var body = tilemap.gameObject.AddComponent<Rigidbody2D>();
body.bodyType = RigidbodyType2D.Static;
tilemap.gameObject.AddComponent<CompositeCollider2D>();
tilemapCollider.compositeOperation = Collider2D.CompositeOperation.Merge;
```

`Collider2D.usedByComposite` is obsolete in Unity 6; use `compositeOperation`. Per-tile shape comes from `Tile.colliderType` (`None`, `Sprite` = sprite Physics Shape, `Grid` = full cell). Edit physics shapes with `sprite-editor` (`ISpritePhysicsOutlineDataProvider`).

## Renderer modes

`TilemapRenderer.mode`:
- `Chunk`: batches by chunk; fastest for static tiles that share one texture/atlas. Sorting is per chunk, so tiles cannot interleave with sprites.
- `Individual`: sorts each tile with other renderers; use for isometric levels where characters walk behind tiles.
- `SRPBatch` (Unity 6): SRP Batcher-friendly per-tile rendering in URP.

Keep a layer's tiles in one atlas (`manage-sprite-atlas`) so Chunk mode batches.

## RuleTile at runtime

`RuleTile` assets paint like any tile. After changing neighbors from code, the tilemap refreshes affected cells automatically; call `tilemap.RefreshAllTiles()` after swapping a RuleTile's rules.
