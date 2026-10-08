# Mesh and texture budgets, mipmap streaming, shader variant stripping

Read when triangle counts, GPU memory, texture bandwidth, build size or shader compile/load times are the problem.

## Mesh budgets

Rough per-frame visible triangle budgets (including shadow passes); calibrate on your lowest target device:

| Tier | Visible triangles | Hero character | Typical prop |
|---|---|---|---|
| Low-end mobile | 100k-300k | 5k-10k | 100-1k |
| Mid/high mobile | 300k-1M | 10k-30k | 500-3k |
| Desktop / console | 2M-10M+ | 30k-100k | 1k-20k |

Import and player settings:

- **Read/Write Enabled** off unless scripts read the mesh at runtime (it keeps a CPU copy, doubling memory).
- **Mesh Compression** (importer) shrinks disk size only; **Vertex Compression** (Player Settings) reduces runtime vertex format precision per channel.
- **Optimize Mesh Data** (Player Settings) strips vertex channels unused by any shader in the build. Breaks meshes whose channels are only used by runtime-assigned shaders.
- **Index Format**: 16-bit unless a mesh exceeds 65k vertices.
- Remove unused channels at export (extra UV sets, vertex colors, tangents on unlit meshes). See `blender-to-unity-pipeline` for export settings.
- Skinned meshes: Quality > Skin Weights 2 bones on mobile, 4 on desktop. GPU Skinning (Player Settings) moves skinning off the CPU; compute/batched skinning options vary by version.
- Overdraw from alpha-tested or transparent foliage often costs more than triangles. Use the Rendering Debugger Overdraw overlay; trim transparent cards to the opaque silhouette.

## Texture budgets

Full import guidance is in `unity-texture-import`. Rendering-relevant rules:

- Mipmaps **on** for all 3D textures: without them distant surfaces sample the full-resolution texture (cache thrash, shimmer).
- Compression: ASTC (4x4 to 8x8 by importance) on mobile, BC7 / BC5 (normals) / BC4 (single-channel masks) / DXT1 on desktop. Uncompressed RGBA32 is 4-8x the memory.
- **Max Size** per platform override: 1024 for most mobile assets, 2048 for hero assets.
- Read/Write off.
- Pack channels: metallic / AO / detail / smoothness in one mask map instead of separate textures.
- Atlases or texture arrays help batching only for non-SRP-Batcher paths; with the SRP Batcher, material count matters less than shader variant count.
- Render textures: size and format are bandwidth. Avoid R16G16B16A16 where R11G11B10 or RGBA32 suffice. Release temporary RTs (`RenderTexture.Release`) and destroy created ones.

## Mipmap streaming

Loads only the mip levels needed for the current view, within a memory budget.

1. Quality settings: enable **Mipmap Streaming** (named **Texture Streaming** in earlier versions; verify the label in your editor version). Set **Memory Budget**, **Renderers Per Frame**, **Max Level Reduction**, **Max IO Requests**. Enable **Add All Cameras** or add `StreamingController` to cameras.
2. Per texture: importer **Mipmap Streaming** / **Stream Mipmap Levels** on, with a **Priority**.
3. Runtime API (property names may differ by version):
   - `QualitySettings.streamingMipmapsActive`, `streamingMipmapsMemoryBudget`, `streamingMipmapsMaxLevelReduction`.
   - `Texture.currentTextureMemory`, `desiredTextureMemory`, `targetTextureMemory`, `totalTextureMemory`, `nonStreamingTextureMemory` to watch the budget.
   - `Texture2D.requestedMipmapLevel` to force mips for UI or scripted textures.
4. Debug: Scene view draw mode **Texture Mipmap Streaming** (or the Rendering Debugger equivalent): green = fewer mips loaded than ideal, red = more.

Pitfalls:

- Budget lower than `nonStreamingTextureMemory` means streaming textures drop to their max reduction everywhere (blurry world).
- Textures used by custom shaders that sample with explicit LOD or by compute shaders do not report their usage; set `requestedMipmapLevel` manually or exclude them.
- Lightmaps and textures in AssetBundles / Addressables need the streaming flag at build time.
- Teleports cause a blurry frame while mips load; prefetch by placing a camera or using `StreamingController.SetPreloading`.

## Shader variant stripping

Variant count drives build time, build size, runtime shader memory and first-use hitches.

### Find the variants

- Build log: `Compiled shader 'X' pass 'Y' ... Full variant space / After settings filtering / After built-in stripping / After scriptable stripping`.
- URP Global Settings > **Shader Variant Log Level** (Disabled / Only SRP Shaders / All Shaders) and **Export Shader Variants** for a JSON report. Verify the exact setting names in your editor version.
- Memory Profiler: shader memory per shader (`memory-snapshot-profiling`).

### Strip by configuration (cheapest, safest)

- Every URP Asset included in the build contributes its enabled features. A feature enabled on any included asset (any quality level for that platform) keeps its variants. Disable unused features on **all** included URP Assets: additional light shadows, soft shadows, SSAO, decals, light cookies, reflection probe blending/box projection, LOD crossfade, Forward+ if unused, HDR/alpha output, rendering layers.
- Remove unused URP Assets from Quality levels for the build platform; unused assets referenced anywhere still add variants.
- Project Settings > Graphics > Shader Stripping: Lightmap Modes and Fog Modes to **Custom** with only the modes you use; **Instancing Variants** Strip Unused; **BatchRendererGroup Variants** Strip All if you do not use GRD (Keep All if you do).
- URP Global Settings (Project Settings > Graphics > URP): post-processing, debug, unused-variant and screen coord override stripping toggles. Names and grouping move between URP versions; verify in your editor version.
- In your own shaders: `shader_feature_local` for material toggles (only used combinations ship), `multi_compile_local` only for runtime-switched keywords, and `_fragment` / `_vertex` suffixes to scope keywords to one stage.

### Strip by script

```csharp
using System.Collections.Generic;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

public class StripDebugKeywordVariants : IPreprocessShaders
{
    #region Fields

    private static readonly ShaderKeyword DebugDisplayKeyword = new("DEBUG_DISPLAY");

    #endregion

    #region Properties

    public int callbackOrder => 100;

    #endregion

    #region Public Methods

    public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
    {
        if (UnityEditor.EditorUserBuildSettings.development) return;

        for (var i = data.Count - 1; i >= 0; i--)
        {
            if (!data[i].shaderKeywordSet.IsEnabled(DebugDisplayKeyword)) continue;
            data.RemoveAt(i);
        }
    }

    #endregion
}
```

- Place in an `Editor` folder or Editor-only asmdef.
- Iterate backwards when removing. No LINQ (the callback runs for every shader pass; allocations add minutes to builds).
- Use `IPreprocessComputeShaders` for compute shaders.
- Over-stripping produces pink or wrong materials only in builds. Test every quality tier in a player build after changing stripping.
- Local keywords: construct `ShaderKeyword(shader, "NAME")` for `_local` keywords; the global constructor does not match them.

### Warm-up and PSO hitches

- First use of a variant compiles/creates the pipeline state on the GPU driver: a hitch on Vulkan, Metal and DX12.
- `ShaderVariantCollection.WarmUp()` covers older APIs. Unity 6 adds PSO tracing and prewarming through `GraphicsStateCollection`; the introducing minor is 6000.1 or later, verify in your editor version.
