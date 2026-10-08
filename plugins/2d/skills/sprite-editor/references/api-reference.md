# Sprite Editor Data Provider API (`UnityEditor.U2D.Sprites`)

Package: `com.unity.2d.sprite` (part of the 2D feature set). All types below are editor-only.

## Getting a provider

```csharp
var factory = new SpriteDataProviderFactories();
factory.Init();
ISpriteEditorDataProvider dp = factory.GetSpriteEditorDataProviderFromObject(importerOrTexture);
dp.InitSpriteEditorDataProvider();
```

`GetSpriteEditorDataProviderFromObject` accepts the importer or the imported asset. `null` means the importer does not support sprite editing.

## `ISpriteEditorDataProvider`

| Member | Notes |
|---|---|
| `SpriteImportMode spriteImportMode` | Single / Multiple / Polygon |
| `float pixelsPerUnit` | |
| `UnityEngine.Object targetObject` | The importer |
| `SpriteRect[] GetSpriteRects()` / `SetSpriteRects(SpriteRect[])` | Whole-array read/write |
| `void Apply()` | Writes changes to the importer; follow with `SaveAndReimport()` |
| `void InitSpriteEditorDataProvider()` | Call once after creation |
| `T GetDataProvider<T>()` / `bool HasDataProvider(Type)` | Optional providers below |
| `RegisterDataChangeCallback` / `UnregisterDataChangeCallback` | Change notifications |

## `SpriteRect`

| Field | Notes |
|---|---|
| `string name` | Unique within the texture |
| `GUID spriteID` | Stable ID; equals the sprite's `GetSpriteID()` |
| `Rect rect` | Original-image pixels |
| `Vector2 pivot` | Normalized 0..1 within the rect; used only when `alignment = Custom` |
| `SpriteAlignment alignment` | Preset or `Custom` |
| `Vector4 border` | 9-slice: left, bottom, right, top (pixels) |

## Optional providers

| Interface | Purpose | Key members |
|---|---|---|
| `ISpriteNameFileIdDataProvider` | Name to file ID map; keeps references stable when adding/removing sprites | `GetNameFileIdPairs()`, `SetNameFileIdPairs(IEnumerable<SpriteNameFileIdPair>)`; `new SpriteNameFileIdPair(name, guid)` |
| `ISpriteOutlineDataProvider` | Render mesh outline | `GetOutlines(guid)`, `SetOutlines(guid, List<Vector2[]>)`, `Get/SetTessellationDetail(guid, 0..1)` |
| `ISpritePhysicsOutlineDataProvider` | Physics Shape for `PolygonCollider2D` | same shape as outline provider |
| `ISpriteBoneDataProvider` | 2D Animation bones | `GetBones(guid)`, `SetBones(guid, List<SpriteBone>)` |
| `ISpriteMeshDataProvider` | Custom mesh | `Get/SetVertices` (`Vertex2DMetaData[]`), `Get/SetIndices`, `Get/SetEdges` |
| `ITextureDataProvider` | Source texture access | `texture`, `previewTexture`, `GetTextureActualWidthAndHeight(out w, out h)`, `GetReadableTexture2D()` |
| `ISecondaryTextureDataProvider` | Secondary textures (normal/mask maps) | `textures` (`SecondarySpriteTexture[]`) |
| `ISpriteFrameEditCapability` | What the importer allows | `GetEditCapability()`, `SetEditCapability(EditCapability)` |

## Coordinate space

Sprite data is always in **original source image** pixels. The imported `Texture2D` can be smaller (Max Size, platform overrides). Consequences:
- Rects, borders, pivots (via rect), and outlines all use original dimensions.
- Slicing must analyse a texture at original size: `ITextureDataProvider.GetTextureActualWidthAndHeight` gives the size; `GetTextureToSlice` upsamples the readable texture when needed.
- Unity scales internally when it builds the runtime sprite.

## Importer notes

- `TextureImporter`: needs `textureType = Sprite`; `spriteImportMode = Multiple` for several sprites. Set these through the importer and reimport before opening a data provider.
- `PSDImporter` (`com.unity.2d.psdimporter`) and custom `ScriptedImporter`s implement the same interface; never reach into importer-specific fields.
- Always use the data provider; it is the only path that works across importers and keeps file IDs consistent.
