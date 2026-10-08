# URP quality tiers per platform and mobile specifics

Read when setting up Low/Medium/High tiers, targeting mobile, or when GPU time scales with resolution.

## Quality tiers

1. Create one URP Asset (and Renderer) per tier: `URP-Low`, `URP-Medium`, `URP-High`.
2. Project Settings > Quality: one quality level per tier; assign each level's **Render Pipeline Asset**. Set the default level per platform in the quality matrix and untick levels a platform should never use.
3. Project Settings > Graphics: the **Default Render Pipeline** is used when a quality level has none assigned.
4. Switch at runtime: `QualitySettings.SetQualityLevel(index, applyExpensiveChanges: true)`. Changing the pipeline asset re-creates render targets: do it in a menu or loading screen, not mid-gameplay.
5. Pick the starting tier from device capability (`SystemInfo.graphicsMemorySize`, `SystemInfo.processorCount`, a GPU name allowlist, or a short benchmark) and let the player override it.

Every URP Asset reachable from the quality levels of the build platform contributes variants (see `assets-and-variants.md`). Keep the feature set consistent and disable features at the asset level rather than per camera when a tier never uses them.

### Example tier table

| Setting (URP Asset) | Low (mobile) | Medium | High (desktop) |
|---|---|---|---|
| Rendering path (Renderer) | Forward | Forward+ | Forward+ / Deferred+ |
| Render Scale | 0.7-0.8 | 0.85-1.0 | 1.0 (or STP upscaling) |
| Upscaling filter | Bilinear / FSR 1 | FSR 1 | STP |
| HDR | Off | On, 32-bit precision | On |
| MSAA | Off or 2x | 2x-4x | 4x or post AA |
| Depth Texture / Opaque Texture | Off | On only if needed | As needed |
| Main light shadows | 1 cascade, 1024, 25 m, hard | 2 cascades, 2048, 50 m, soft low | 4 cascades, 4096, 150 m, soft medium |
| Additional lights | Per Vertex or Disabled, no shadows | Per Pixel, limit 4 | Per Pixel, shadows |
| LOD Bias / Max LOD | 0.7 / 1 | 1.0 / 0 | 1.5 / 0 |
| Soft particles, SSAO, decals | Off | Selected | On |
| GPU Resident Drawer | Off (GLES) or Instanced if Vulkan/Metal capable | Instanced | Instanced |

Per-tier Volume overrides (post-processing quality) are covered in `urp-postprocessing`.

## Mobile specifics

### Render scale and upscaling

- Mobile GPUs are usually fill-rate and bandwidth bound. URP Asset > Quality > **Render Scale** 0.7-0.85 renders 3D at reduced resolution; UI (Screen Space Overlay) stays native.
- **Upscaling Filter**: Bilinear (cheapest), FSR 1.0 (spatial, sharper, extra pass), **STP** (Spatio-Temporal Post-processing, Unity 6, temporal; needs compute and motion vectors and is aimed at desktop/console and high-end mobile; verify platform support in your editor version).
- Dynamic resolution: enable **Allow Dynamic Resolution** on the camera and drive `ScalableBufferManager.ResizeBuffers` from frame timing; supported only on some graphics APIs.
- Native resolution on modern phones is huge (2.5-3x 1080p pixels). Consider capping the backbuffer with `Screen.SetResolution` on low tiers.

### MSAA vs FXAA

| | MSAA 2x/4x | FXAA | SMAA | TAA / STP |
|---|---|---|---|---|
| Where | Hardware, during rasterization | Fullscreen post pass | Multi-pass post | Temporal post |
| Cost on tile GPUs | Low when the resolve happens in tile memory | One fullscreen pass, scales with resolution | Higher | Highest, needs motion vectors |
| Quality | Geometry edges only; no shader/specular aliasing help | Blurs everything slightly | Better than FXAA | Best, ghosting risk |
| Breaks with | Opaque/depth texture copies and post-processing force a resolve and store | Needs post-processing on the camera | Same | Not recommended on low mobile |

- On Vulkan/Metal with Render Graph, 4x MSAA can be close to free if the MSAA attachments are memoryless and nothing samples the camera color or depth mid-frame. Check load/store actions in the Render Graph Viewer.
- Any pass that reads camera color or depth as a texture (opaque texture, depth texture copy, post-processing) forces the MSAA surface to be resolved and stored; MSAA cost then rises sharply. In that case FXAA can be cheaper.

### HDR

- HDR camera targets either keep LDR bandwidth (32-bit R11G11B10, no alpha) or double it (64-bit R16G16B16A16). URP Asset > Quality > **HDR Precision**: 32-bit is the mobile choice.
- Without HDR, bloom thresholds and tonemapping behave differently (no values above 1). Decide per tier and re-tune bloom for the LDR tier.

### Depth and opaque textures

- **Depth Texture** (URP Asset or per camera): required by soft particles, some decals, SSAO, depth-based water, DoF. Produced by a depth prepass or a depth copy after opaques; either breaks tile-memory flow on mobile.
- **Opaque Texture**: a copy (optionally downsampled: **Opaque Downsampling** 2x bilinear / 4x box) of the color after opaques, for refraction and distortion. A full-resolution copy per camera per frame.
- Enable them per camera (`UniversalAdditionalCameraData.requiresDepthTexture`, `requiresColorTexture`) only where needed, not globally on the asset.
- Renderer features that request depth or normals (SSAO, decals in DBuffer mode) silently enable a prepass; check the Frame Debugger for `DepthPrepass` / `DepthNormalPrepass`.

### Post-processing

Configuration is in `urp-postprocessing`. Cost rules:

- URP combines most effects into one Uber post pass, but Bloom (downsample/upsample chain), DoF, Motion Blur, SMAA, Panini and Lens Flare add their own passes.
- Bloom: enable **Downscale** to quarter resolution, lower **Max Iterations**, disable **High Quality Filtering** on mobile.
- Avoid on low mobile: Bokeh DoF, Motion Blur, SSAO, Screen Space Lens Flare, Film Grain at full resolution.
- Color grading: LDR or HDR grading LUT size 16 or 32 on mobile.
- Turn **Post Processing** off on cameras that do not need it (`UniversalAdditionalCameraData.renderPostProcessing`), especially overlay and UI cameras. Camera stacking multiplies costs.

### Other mobile levers

- Prefer Vulkan (Android) and Metal (iOS); GLES lacks GRD, compute-heavy features and some Render Graph optimizations.
- `Application.targetFrameRate = 30` or 60 explicitly; mobile defaults to 30. Lower frame targets on low tiers reduce thermal throttling.
- Adaptive Performance package (Samsung / Android) can scale resolution, LOD and shadows from thermal state.
- Watch overdraw: transparent UI over 3D, foliage cards and particle stacks. Use the Overdraw overlay.
- Avoid `Camera.Render` / extra cameras for minimaps and mirrors on low tiers; render at low resolution and low frequency.
