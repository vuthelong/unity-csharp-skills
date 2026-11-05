# Packing rules

## What a prefab must satisfy

`ParticleAtlasOptimizer.Scan` / `PlanRepack` reject a prefab (row `error`) when:

| Rejection | Why | Fix |
|---|---|---|
| Not under `vfxRoot` or not a `.prefab` | Scope guard | Move it or widen `vfxRoot` |
| Nested or variant prefab | Rewriting would lose overrides | Pack the inner prefab; outer prefabs inherit it |
| Missing script | Saving would drop data | Restore the script |
| Invalid sorting layer on an enabled renderer | Output sorting would be undefined | Fix the layer |
| Animator or Animation anywhere | It may animate renderer properties the snapshot freezes | Replace sprite-swap animators with a Grid flipbook; otherwise keep native |
| Unsupported shader | The atlas shader only reproduces known blend math | Preparation, or add a `packableShaders` entry if the math matches |
| SpriteMask interaction | Mask stencil is per renderer | Keep native |
| `groups + native > budget` | Packing would not reduce draws enough | Merge sorting layers or GameObject layers; budget max 3 |
| Batch material outside `materialFolder` or not `M_<key>_AB_Add.mat` | Ownership unclear | Restore the original material |
| Already belongs to another key | One prefab, one atlas | Keep its key or restore first |
| Output name occupied by assets not owned by the group | Refuses to overwrite | Pick another key |
| A required consumer of an existing atlas fails | The atlas is shared; partial rebuild would break it | Fix that consumer first |

Warnings (row `warning`) do not block: runtime scripts on the prefab, deleted packed sources (dropped on repack), and the standing reminder to review transparency order and half-resolution detail.

## Renderer budget

`groups` = distinct `(sortingLayerID, gameObject.layer)` pairs among packable sources; one `Combined Atlas <layer>` output per pair. `native` = every other enabled renderer (non-particle renderers, particles with unsupported shaders are rejected instead, mesh-mode particles with `≥ keepNativeMeshVertexCount` vertices). A dense mesh copied every frame costs more CPU than its own draw call, which is why it stays native.

## Packable shaders

Default `packableShaders` (shader, blend, tint property, scale property, factor):

| Shader | Blend | Tint | Factor |
|---|---|---|---|
| `ParticleVfx/Legacy Additive` | Additive | `_TintColor` | 2 |
| `ParticleVfx/Legacy Alpha Blended` | Alpha | `_TintColor` | 2 |
| `Legacy Shaders/Particles/Additive` | Additive | `_TintColor` | 2 |
| `Legacy Shaders/Particles/Alpha Blended` | AlphaClamped | `_TintColor` | 2 |
| `Mobile/Particles/Alpha Blended` | Alpha | `_MainColor` × `_Scale` if present | 1 |

The atlas shader computes `vertexColor × (tint × factor) × atlasTexel`, then blends premultiplied with `One OneMinusSrcAlpha`: additive sources write alpha 0, alpha sources write their alpha, so one material draws both in a single batch. Only `_MainTex` and its tiling/offset are packed; anything else the original shader does (masks, dissolve, UV panning, custom vertex streams, HDR intensity, soft particles) is lost. Add an entry only when the shader's output is exactly that formula. URP `Particles/Unlit` and Shader Graph particle shaders are not packable as-is.

`Mobile/Particles/Additive` is not packable (no tint, 1× math with additive blend): preparation converts it to `ParticleVfx/Legacy Additive` with `_TintColor = 0.5 grey`, which reproduces the same output.

## Color space

Packing was validated in **Gamma** color space with anisotropic filtering disabled; `requireGammaColorSpace: true` makes `Scan` and `BuildGroup` refuse otherwise. With it set to false in Linear projects, the atlas is imported as sRGB so sampling matches sRGB sources; non-color (linear) source textures will then be decoded wrongly. Render-compare every group before shipping in Linear.

## Atlas build

- Textures are shelf-packed (best short-side fit) into a square page in 64 px steps, 4 px padding replicated with each texture's wrap mode, up to 4096 (8192 fallback) before downsampling.
- `halfResolution` box-filters the whole page once; any page above the output limit (2048, or the existing atlas's `maxTextureSize` when repacking) is halved again. Regions keep their normalized UVs.
- Import: uncompressed RGBA32 on default, `platformFormat` (ASTC 8x8) on `compressedPlatforms`, point filter, clamp, no mipmaps. The shader samples with explicit point or linear samplers per source, so the atlas filter mode does not matter.
- `ParticleAtlasTexturePolicy` (`texturePolicyPath`, JSON `{"textures":[{"guid":"…","name":"…","maxPackedSize":128}]}`) caps individual textures before packing. Limits are absolute, so rebuilding never halves a texture twice.
- Sprite sheets (Multiple import mode, `sheet` / `seq_` names, or a Texture Sheet Animation with more than one frame) are reduced last when a page must shrink, because frame detail matters more.

## Prefab rewrite

`ConvertPrefab` loads the prefab contents, disables packed source renderers, creates or reuses `Combined Atlas <layer>` children, writes `sources`, saves, and then re-applies only the changed YAML documents onto the original text (`PreserveSourceDocuments`). File IDs never change, so references to the prefab and its children survive, and the particle system documents stay byte-identical (Optimize verifies this). Retired outputs are disabled and emptied, not deleted, for the same reason.
