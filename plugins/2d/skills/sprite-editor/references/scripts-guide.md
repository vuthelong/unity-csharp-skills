# Sprite Editor Scripts Guide

All scripts are `static partial class SpriteEditorUtility` (plus `IsometricSliceUtility`) in namespace `SpriteEditorTools`. Save them together in an `Editor` folder, or inline the needed methods into an `eval` snippet with fully qualified types.

## Files

| File | Method | Use when |
|---|---|---|
| `GetTextureSourceImageSize.cs` | `GetTextureSourceImageSize(Texture2D, out w, out h)` | You need the true source size |
| `GetTextureToSlice.cs` | `GetTextureToSlice(ITextureDataProvider)` | Before any slicing; returns a readable texture at original size |
| `GenerateNewSpriteRects.cs` | `GenerateNewSpriteRects(...)`, `ApplySpriteRects(...)` | Turning `Rect`s into `SpriteRect`s and writing them with file IDs in sync |
| `AutomaticSliceTexture.cs` | `AutomaticSliceTexture(...)` | Sprites separated by transparency |
| `GridSliceTexture.cs` | `GridSliceTexture(...)` | Evenly spaced cells (animation strips, tilesets) |
| `IsometricSliceTexture.cs` | `IsometricSliceTexture(...)` | Isometric diamond tilesets |
| `SetPivotExample.cs` | `SetCustomPivot`, `SetPivot` | Pivot changes |
| `SpriteToPng.cs` | `SpriteToPng(Sprite)` | Export one sprite (respects tight mesh) |

## Slicing pattern

```csharp
var texture = SpriteEditorUtility.GetTextureToSlice(textureProvider);
IEnumerable<Rect> rects = /* algorithm */;
var newRects = SpriteEditorUtility.GenerateNewSpriteRects(spriteDataProvider, rects, SpriteEditorUtility.AddNewSpriteMethod.DeleteAll, nameGenerator);
SpriteEditorUtility.ApplySpriteRects(spriteDataProvider, newRects);
spriteDataProvider.Apply();
((AssetImporter)spriteDataProvider.targetObject).SaveAndReimport();
```

The slice helpers already call `ApplySpriteRects`; you still call `Apply()` and `SaveAndReimport()`.

## `AddNewSpriteMethod`

| Mode | Behaviour |
|---|---|
| `DeleteAll` | Replace all sprites. Names that already existed keep their file IDs, so references survive a re-slice with the same naming |
| `Smart` | Reuse an overlapping existing sprite (keeps its name/ID, updates its rect); create new ones elsewhere. Existing sprites with no overlap are dropped |
| `Safe` | Keep every existing sprite; add only rects that overlap nothing |

`bestFit` picks the closest overlapping sprite by area ratio instead of the first hit; `kBestFitTolerance` rejects poor matches.

## Name generators

```csharp
string path = AssetDatabase.GetAssetPath(spriteDataProvider.targetObject);
string baseName = string.IsNullOrEmpty(path) ? "sprite" : System.IO.Path.GetFileNameWithoutExtension(path);
Func<int, string> nameGenerator = i => $"{baseName}_{i}";
```

Unity's own convention is `<texture>_<index>`; keep it unless the user wants otherwise so re-slices map to the same file IDs.

## Grid slice parameters

- `offset`: pixels from the top-left corner.
- `size`: cell size in pixels.
- `padding`: gap between cells.
- `keepEmptyRects`: keep fully transparent cells (false by default).

## Isometric slice parameters

- `size`: diamond cell size (e.g. 64x32).
- `isAlternate`: start with the half-cell row offset.
- Writes a diamond outline per sprite through `ISpriteOutlineDataProvider` so tilemap colliders and meshes follow the diamond.
