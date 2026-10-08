# Custom Packing with ScriptablePacker

`UnityEditor.U2D.ScriptablePacker` is an abstract `ScriptableObject` that replaces Unity's packing algorithm for a V2 atlas. It is a recent addition to Unity 6; confirm the type exists in the project's editor version (and inspect its declaration in the IDE) before generating code, because nested type members have shifted between releases.

## Use cases

Fixed grid layouts (for shaders that index cells), deterministic placement across builds, custom multi-page splitting, domain-specific ordering. For ordinary space efficiency, the built-in packer is better.

## Shape of the API

```csharp
public class GridSpritePacker : ScriptablePacker
{
    public int columns = 8;
    public int cellSize = 128;
    public int padding = 2;

    public override bool Pack(SpriteAtlasPackingSettings config, SpriteAtlasTextureSettings setting, PackerData input)
    {
        var sprites = input.spriteData;
        for (int i = 0; i < sprites.Length; i++)
        {
            var sprite = sprites[i];
            sprite.output.x = (i % columns) * (cellSize + padding) + padding;
            sprite.output.y = (i / columns) * (cellSize + padding) + padding;
            sprite.output.page = 0;
            sprite.output.rot = PackTransform.None;
            sprites[i] = sprite;
        }
        return true;
    }
}
```

Input (`PackerData`, owned by Unity; never dispose):
- `spriteData` (`NativeArray<SpriteData>`): per sprite `rect` (`RectInt` in its source texture), `texIndex`, mesh `indexOffset`/`indexCount`, `vertexOffset`/`vertexCount`, and the `output` you write.
- `textureData` (`NativeArray<TextureData>`): source texture `width`/`height` (and buffer offset into `colorData`).
- `colorData` (`NativeArray<Color32>`), `indexData` (`NativeArray<int>`), `vertexData` (`NativeArray<Vector2>`).

Output per sprite (`SpritePack`): `x`, `y` (pixels in the page), `page` (multi-page index), `rot` (`PackTransform`: `None`, `FlipHorizontal`, `FlipVertical`, `Rotate180`).

`SpriteData`, `SpritePack`, `TextureData`, `PackTransform`, and `PackerData` are nested in `ScriptablePacker`. Do not declare your own top-level types with these names; they shadow nothing and only confuse readers.

## Assigning a packer

```csharp
var packer = ScriptableObject.CreateInstance<GridSpritePacker>();
AssetDatabase.CreateAsset(packer, "Assets/Atlases/GridSpritePacker.asset");

var atlasAsset = SpriteAtlasAsset.Load(atlasPath);
atlasAsset.SetScriptablePacker(packer);
SpriteAtlasAsset.Save(atlasAsset, atlasPath);
AssetDatabase.ImportAsset(atlasPath);
```

Save the packer as an asset first; the atlas stores a reference, and an unsaved instance is lost on reload.

## Rules

- Keep every sprite inside the page: `x + rect.width + padding <= maxTextureSize` (and the same for y). Return `false` when sprites do not fit so Unity reports the failure.
- Respect `config.padding` unless you deliberately override it.
- Use `page` for overflow rather than exceeding the max texture size.
- Avoid allocations in `Pack`; it runs on every atlas import.
- Debug by logging per-sprite `rect` and `output`, then open the atlas Inspector preview after `SpriteAtlasUtility.PackAtlases`.
