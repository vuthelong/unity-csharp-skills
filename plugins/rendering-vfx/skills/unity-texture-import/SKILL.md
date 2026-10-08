---
name: unity-texture-import
description: Configures and audits Unity 6 texture import settings through TextureImporter, platform overrides, Presets, and AssetPostprocessor rules. Covers texture types (Default, Normal map, Sprite, Single Channel, Cookie, Lightmap), sRGB vs linear data, compression formats per platform (ASTC, BC7, BC5, BC6H, ETC2), max size, mipmaps, mipmap streaming and mipmap limit groups, Read/Write cost, wrap/filter/aniso, and textures for VFX, flipbooks, LUTs, HDRIs, and masks. Use when the user mentions TextureImporter, import settings, texture compression, texture memory, ASTC, BC7, crunch, mipmaps, "normal map looks wrong", "texture is blurry", "texture uses too much memory", or wants import rules enforced automatically by folder.
license: MIT
metadata:
  category: rendering-vfx
  sources: "AlexeyPerov/Unity-Open-MCP/skills/extensions/texture"
  unity: "6000.0+"
---

# Unity Texture Import

Import settings live in the texture's `.meta` file and are applied by `TextureImporter`. Two different things are worth reading:

- **Importer settings** (`TextureImporter`): what the texture *will be* after import (type, compression, max size, per-platform overrides).
- **Imported result** (`Texture2D`): what it *is* now (`width`, `height`, `format`, `mipmapCount`, `graphicsFormat`). Use this to confirm the importer did what you expected; the format differs per active build target.

## Workflow

1. **Classify the texture by content**, not by file name: color (albedo, UI, sprites), data (normal, mask, roughness, flow, noise, LUT), or HDR (skies, emissive HDR).
2. **Set type and color space**: color -> sRGB on; data -> sRGB off; normal maps -> type Normal map.
3. **Set max size** to the largest size it will ever be displayed at, not the source size.
4. **Set mipmaps**: on for anything in 3D; off for UI and pixel-perfect 2D.
5. **Set compression per platform** (default + overrides for Android, iOS, Standalone, WebGL).
6. **Apply and reimport** (`importer.SaveAndReimport()`), then **verify** the imported `Texture2D.format` and memory (Inspector footer, Memory Profiler).
7. **Automate** the rule with an `AssetPostprocessor` or Presets so new assets follow it (see [references/import-automation.md](references/import-automation.md)).

## Type and color space

| Content | Texture Type | sRGB | Alpha | Notes |
|---|---|---|---|---|
| Albedo / base color, UI, sprites | Default or Sprite | On | As needed | |
| Normal map | Normal map | (forced off) | n/a | Never import as Default; tangent normals break |
| Mask maps (metallic/AO/smoothness packed) | Default | **Off** | Smoothness in A | sRGB on corrupts values |
| Roughness / height / single mask | Single Channel (R or A) | Off | | Quarter of the memory of RGBA |
| Flow maps, vector fields, noise | Default | Off | | Disable compression if artifacts distort motion |
| Color grading LUT | Default | Off | | No mips, no compression, Clamp |
| HDRI sky | Default, Shape Cube | n/a (HDR) | | BC6H on desktop, ASTC HDR on mobile |
| Light cookie | Cookie | Off | | Clamp for spot, Repeat only if tiling |
| VFX flipbook | Default | On (color) / Off (data) | Usually | Mips on, Clamp, keep frames on power-of-two grid |

`alphaIsTransparency` dilates color into transparent pixels to avoid dark fringes on alpha-blended sprites and particles.

## Compression by platform (Unity 6 defaults and good picks)

| Target | Color | Color + alpha | Normal | Single channel | HDR |
|---|---|---|---|---|---|
| Desktop / console (Standalone) | BC7 (or DXT1/BC1 for size) | BC7 | BC5 | BC4 | BC6H |
| Android | ASTC 6x6 (4x4 for hero, 8x8 for background) | ASTC 6x6 | ASTC 4x4-5x5 | ASTC / ETC2 R | ASTC HDR |
| iOS / visionOS | ASTC | ASTC | ASTC 4x4-5x5 | ASTC | ASTC HDR |
| WebGL / Web | DXT (desktop browsers) or ASTC/ETC2 (mobile browsers); see `optimize-web` | | | | |

- Unity 6 Android defaults to ASTC; ETC2 is only needed for very old GLES 3.0 devices.
- Crunch compression shrinks download size, not GPU memory; it lowers quality and slows import. Use for download-bound projects only.
- `Compressed` / `CompressedHQ` / `CompressedLQ` on the default platform pick a format per target automatically. Use explicit overrides when you need predictability.
- Uncompressed RGBA32 at 2048x2048 with mips is ~21 MB; BC7 is ~5.3 MB; ASTC 6x6 is ~2.4 MB.

## Size, mips, and memory

- **Max Size** caps import resolution. Downscaling at import is free at runtime; oversize textures waste memory and bandwidth.
- **Non power of two**: compressed formats need multiples of the block size; ASTC/BC handle NPOT, but old ETC paths and some features prefer POT. `npotScale` rescales on import.
- **Mipmaps**: required for 3D to avoid shimmering and cache misses; +33% memory. Off for UI, cursor, and pixel-perfect sprites. Use *Mip Map Filtering* Kaiser for detail textures that get blurry too fast.
- **Mipmap Streaming** (`streamingMipmaps`): loads only needed mips; enable in Quality settings with a memory budget. Good for large open worlds.
- **Mipmap Limit Groups** (`mipmapLimitGroupName`): per-group quality-level mip bias, so "Low" quality drops environment detail without touching UI or characters. Set `ignoreMipmapLimit` for textures that must stay sharp.
- **Read/Write** (`isReadable`) keeps a CPU copy: doubles memory. Enable only when scripts call `GetPixels`/`ReadPixels` style APIs or for runtime mesh/texture generation.
- **Filter**: Point for pixel art, Bilinear default, Trilinear smooths mip transitions. **Aniso** 2-8 for ground/road textures seen at grazing angles; 0-1 for everything else.
- **Wrap**: Repeat for tiling, Clamp for UI, decals, cookies, LUTs, flipbooks (prevents edge bleed).

## Scripting the importer

```csharp
using UnityEditor;

public static class TextureImportTools
{
    public static void ConfigureMaskMap(string assetPath)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Compressed;

        var android = importer.GetPlatformTextureSettings("Android");
        android.overridden = true;
        android.maxTextureSize = 1024;
        android.format = TextureImporterFormat.ASTC_6x6;
        importer.SetPlatformTextureSettings(android);

        importer.SaveAndReimport();
    }
}
```

- Platform names for overrides: `"Standalone"`, `"Android"`, `"iPhone"`, `"tvOS"`, `"WebGL"`, plus console names. `GetDefaultPlatformTextureSettings()` returns the default.
- Batch many edits inside `AssetDatabase.StartAssetEditing()` / `StopAssetEditing()` (in `try/finally`) so Unity reimports once.
- Changing import settings rewrites `.meta` files; commit them.
- Force a reimport without changing settings: `AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate)`.

## Auditing

Read [references/import-automation.md](references/import-automation.md) for the folder-rule `AssetPostprocessor`, Presets, and an audit script that lists oversize, uncompressed, readable, or sRGB-mismatched textures.

## Related skills

- `optimize-web` for WebGL/WebGPU texture formats and download size.
- `unity-vfx-graph`, `unity-particle-system` for flipbook and noise usage.
- `unity-lighting` for cookies, HDRIs, and lightmap memory.
