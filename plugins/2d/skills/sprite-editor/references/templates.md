# Sprite Editor Code Templates

## Safe Core Pattern (mandatory)

Fully qualified because it runs through `eval`. As a saved `.cs` file, add `using UnityEditor.U2D.Sprites;` and shorten.

```csharp
var assetPath = "Assets/Art/Characters/hero_sheet.png";
var importer = UnityEditor.AssetImporter.GetAtPath(assetPath);
if (importer == null)
    throw new System.Exception($"No importer at {assetPath}. Operation aborted.");

var factory = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
factory.Init();
var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
if (dataProvider == null)
    throw new System.Exception("Importer does not support sprite editing. Operation aborted.");
dataProvider.InitSpriteEditorDataProvider();

var editCapability = dataProvider.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteFrameEditCapability>();
if (editCapability == null)
    throw new System.Exception("Importer exposes no edit capability. Operation aborted.");

var capability = editCapability.GetEditCapability();
if (!capability.HasCapability(UnityEditor.U2D.Sprites.EEditCapability.EditPivot))
    throw new System.Exception("Importer does not allow the requested edit. Operation aborted.");

var spriteRects = dataProvider.GetSpriteRects();
foreach (var rect in spriteRects)
{
    rect.alignment = UnityEngine.SpriteAlignment.BottomCenter;
}

dataProvider.SetSpriteRects(spriteRects);
dataProvider.Apply();
((UnityEditor.AssetImporter)dataProvider.targetObject).SaveAndReimport();
```

Throwing ends the snippet; do not add code after a `throw` in the same branch.

## Capabilities (`EEditCapability`)

Check the one that matches the task; check several with `&&` when the task touches several.

| Flag | Allows |
|---|---|
| `EditSpriteName` | Renaming sprites |
| `EditSpriteRect` | Moving/resizing rects |
| `EditBorder` | 9-slice borders |
| `EditPivot` | Pivot and alignment |
| `CreateAndDeleteSprite` | Adding, deleting, or slicing |

## Selecting sprites

- By name: `spriteRects.First(r => r.name == "hero_idle_0")` (add `System.Linq` qualification under `eval`: `System.Linq.Enumerable.First(spriteRects, r => ...)`).
- By sprite asset: compare `rect.spriteID` with the GUID from the editor `GetSpriteID()` extension on the `Sprite`.
- From the selection: `UnityEditor.Selection.activeObject as UnityEngine.Texture2D`, then `AssetDatabase.GetAssetPath`.

## Setting outlines

```csharp
var outlineProvider = dataProvider.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteOutlineDataProvider>();
var outline = new System.Collections.Generic.List<UnityEngine.Vector2[]>
{
    new[] { new UnityEngine.Vector2(-8, -8), new UnityEngine.Vector2(8, -8), new UnityEngine.Vector2(8, 8), new UnityEngine.Vector2(-8, 8) }
};
outlineProvider.SetOutlines(spriteRects[0].spriteID, outline);
dataProvider.Apply();
```

Outline points are in pixels relative to the sprite rect center.
