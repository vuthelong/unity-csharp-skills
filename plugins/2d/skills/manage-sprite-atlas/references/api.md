# Sprite Atlas V2 API Reference

Namespaces: editor `UnityEditor`, `UnityEditor.U2D`; runtime `UnityEngine.U2D`.

## SpriteAtlasAsset (editor authoring)

```csharp
var asset = new SpriteAtlasAsset();
asset.Add(Object[] objects);          // Sprite, Texture2D, or folder (DefaultAsset)
asset.Remove(Object[] objects);
asset.SetIsVariant(bool value);
asset.SetMasterAtlas(SpriteAtlas master); // runtime SpriteAtlas loaded via AssetDatabase.LoadAssetAtPath<SpriteAtlas>
asset.SetScriptablePacker(ScriptablePacker packer);
bool isVariant = asset.isVariant;
SpriteAtlas master = asset.GetMasterAtlas();

SpriteAtlasAsset.Save(asset, "Assets/Atlases/UI.spriteatlasv2");
SpriteAtlasAsset loaded = SpriteAtlasAsset.Load("Assets/Atlases/UI.spriteatlasv2");
```

After `Save`, call `AssetDatabase.ImportAsset(path)` before touching the importer.

Obsolete on `SpriteAtlasAsset` (use the importer): `SetIncludeInBuild`, `IsIncludeInBuild`, `SetVariantScale`, `SetTextureSettings`, `SetPackingSettings`, `SetPlatformSettings`. Values set through them are not saved to disk.

There is no `SpriteAtlasAsset.GetPackables()`. Read packables from the imported runtime atlas: `AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path).GetPackables()` (editor extension in `UnityEditor.U2D.SpriteAtlasExtensions`).

## SpriteAtlasImporter (editor settings)

```csharp
var importer = AssetImporter.GetAtPath(path) as SpriteAtlasImporter;

importer.includeInBuild = true;
importer.variantScale = 0.5f;            // variants only, 0.1..1

var tex = importer.textureSettings;      // SpriteAtlasTextureSettings (struct)
tex.filterMode = FilterMode.Bilinear;
tex.generateMipMaps = false;
tex.readable = false;
tex.sRGB = true;
tex.anisoLevel = 1;
importer.textureSettings = tex;

var pack = importer.packingSettings;     // SpriteAtlasPackingSettings (struct)
pack.padding = 4;                        // 2, 4, or 8 in the Inspector
pack.enableRotation = false;
pack.enableTightPacking = false;
pack.enableAlphaDilation = true;
pack.blockOffset = 1;
importer.packingSettings = pack;

var android = importer.GetPlatformSettings("Android"); // TextureImporterPlatformSettings
android.overridden = true;
android.maxTextureSize = 2048;
android.format = TextureImporterFormat.ASTC_6x6;
android.compressionQuality = 50;
importer.SetPlatformSettings(android);

importer.SaveAndReimport();
```

`AssetImporter.GetAtPath` takes one argument; cast the result.

### Platform names and typical formats

| Platform string | Typical sprite format |
|---|---|
| `"Standalone"` | `BC7` (quality) or `DXT5` |
| `"Android"` | `ASTC_6x6` (`ASTC_4x4` for sharper UI), `ETC2_RGBA8` for very old GPUs |
| `"iOS"` / `"tvOS"` | `ASTC_6x6` / `ASTC_4x4` |
| `"WebGL"` | `DXT5` / `BC7` for desktop browsers, `ASTC` for mobile web (see `optimize-web`) |
| `"WindowsStoreApps"` | `BC7` / `DXT5` |

PVRTC is deprecated in Unity 6; do not use it for atlases. Pixel art: `RGBA32` (uncompressed). `maxTextureSize`: 32 to 16384; 2048 is a safe mobile ceiling.

## SpriteAtlasUtility (editor packing)

```csharp
SpriteAtlasUtility.PackAllAtlases(BuildTarget target, bool canCancel = true);
SpriteAtlasUtility.PackAtlases(SpriteAtlas[] atlases, BuildTarget target, bool canCancel = true);
```

Takes runtime `SpriteAtlas[]`, not `SpriteAtlasAsset[]`. Optional: player builds pack atlases automatically. Use for preview or to validate `spriteCount` in CI.

## EditorSettings.spritePackerMode

| Value | Meaning |
|---|---|
| `Disabled` | Default zero value. Nothing packs |
| `SpriteAtlasV2` | Packs in Editor, Play Mode, and builds (recommended) |
| `SpriteAtlasV2Build` | Packs for builds only; Play Mode uses loose sprites |
| `BuildTimeOnlyAtlas`, `AlwaysOnAtlas` | Sprite Atlas V1 (legacy) |

## SpriteAtlas (runtime)

| Member | Notes |
|---|---|
| `int spriteCount` | |
| `string tag` | Atlas name; the key passed to `atlasRequested` |
| `bool isVariant` | |
| `Sprite GetSprite(string name)` | Returns a clone each call; cache it |
| `int GetSprites(Sprite[] sprites)` / `GetSprites(Sprite[], string name)` | Clones; size the array with `spriteCount` |
| `bool CanBindTo(Sprite sprite)` | Whether the sprite belongs to this atlas |

## SpriteAtlasManager (runtime)

```csharp
static event Action<string, Action<SpriteAtlas>> atlasRequested; // fired for atlases not in the build
static event Action<SpriteAtlas> atlasRegistered;
```

Register before the first scene loads (`RuntimeInitializeOnLoadMethod(BeforeSceneLoad)`); requests raised before a handler exists are not repeated. Call the callback with the loaded atlas; until then the sprites render invisible.
