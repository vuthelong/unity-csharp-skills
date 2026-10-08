---
name: optimize-3d-rendering
description: Diagnoses and reduces 3D rendering cost in Unity 6 URP (URP 17+) - CPU vs GPU triage with the Frame Debugger, Rendering Debugger, Profiler GPU module, Stats and Render Graph Viewer; batching (SRP Batcher UnityPerMaterial CBUFFER rules, MaterialPropertyBlock, GPU Resident Drawer with Forward+ and BatchRendererGroup Variants Keep All, GPU occlusion culling, static batching, GPU instancing, dynamic batching); culling (LODGroup, LOD crossfade, baked occlusion culling, far plane, layerCullDistances); lighting cost (Forward vs Forward+ vs Deferred+, additional light limits, shadow cascades, distance and resolution, baked vs mixed); mesh and texture budgets, mipmap streaming, shader variant stripping, URP Asset quality tiers, and mobile render scale, MSAA vs FXAA, HDR, depth and opaque texture and post-processing cost. Use when the user reports low FPS, high draw calls or SetPass calls, GPU-bound frames, overheating phones, long builds from shader variants, or asks to optimize a 3D URP scene.
license: MIT
metadata:
  category: rendering-vfx
  sources: "github.com/vuthelong/unity-csharp-skills (original), docs.unity3d.com/6000.0/Documentation/Manual/SRPBatcher.html, docs.unity3d.com/6000.0/Documentation/Manual/urp/gpu-resident-drawer.html, docs.unity3d.com/6000.0/Documentation/Manual/urp/gpu-culling.html, docs.unity3d.com/6000.0/Documentation/Manual/static-batching.html, docs.unity3d.com/6000.0/Documentation/Manual/GPUInstancing.html, docs.unity3d.com/6000.0/Documentation/Manual/frame-debugger-window.html, docs.unity3d.com/6000.0/Documentation/Manual/urp/features/rendering-debugger.html, docs.unity3d.com/6000.0/Documentation/Manual/urp/render-graph-view.html, docs.unity3d.com/6000.0/Documentation/Manual/RenderingStatistics.html, docs.unity3d.com/6000.0/Documentation/Manual/OcclusionCulling.html, docs.unity3d.com/6000.0/Documentation/Manual/LevelOfDetail.html, docs.unity3d.com/6000.0/Documentation/ScriptReference/Camera-layerCullDistances.html, docs.unity3d.com/6000.0/Documentation/Manual/urp/rendering-paths-comparison.html, docs.unity3d.com/6000.0/Documentation/Manual/urp/shadows-in-urp.html, docs.unity3d.com/6000.0/Documentation/Manual/TextureStreaming.html, docs.unity3d.com/6000.0/Documentation/Manual/shader-variant-stripping.html, docs.unity3d.com/6000.0/Documentation/Manual/urp/configure-for-better-performance.html"
  unity: "6000.0+"
---

# Optimize 3D Rendering (URP, Unity 6)

## Workflow

1. **Measure on the target device** in a Development Build. Record CPU main, CPU render and GPU frame times (Profiler, FrameTimingManager, Rendering Debugger Display Stats). Full procedure: [references/diagnostics.md](references/diagnostics.md).
2. **Classify the bottleneck**:
   - CPU render-bound (render thread, culling, many SetPass calls): go to batching and culling.
   - GPU-bound and scales with resolution: go to render scale, post-processing, overdraw, MSAA/HDR, depth/opaque copies.
   - GPU-bound, not resolution-dependent: triangles, shadow passes, lights, vertex cost.
   - Memory or build size: textures, meshes, shader variants.
3. **Inspect the frame**: Frame Debugger for draw order and batch-break reasons; Render Graph Viewer for passes, copies and native render pass merges; Rendering Debugger overlays for overdraw and light complexity.
4. **Apply one change at a time** from the prioritized checklist below and re-measure.
5. **Set per-platform tiers** so the fixes ship: URP Asset per quality level, stripping matched to the features used.

## Prioritized checklist

Work top-down; earlier items are cheaper to try and usually pay more.

1. Confirm CPU vs GPU bound on device. Do not optimize Editor numbers.
2. Remove what you do not need: extra cameras, unused renderer features, post effects on overlay/UI cameras, Depth/Opaque Texture on the URP Asset when only one camera or effect needs them.
3. Shadows: turn off Cast Shadows on small props and foliage; cut shadow distance and cascade count per tier; lower shadow resolution and soft-shadow quality on mobile.
4. Lighting: bake static lighting (Baked or Mixed Subtractive/Shadowmask on mobile); keep one realtime directional light; additional lights Per Vertex or Disabled on low tiers; shrink light ranges.
5. Resolution: Render Scale 0.7-0.85 on mobile with an upscaling filter; HDR 32-bit precision or off on low tiers; MSAA only when no pass forces a resolve, else FXAA.
6. SRP Batcher: every shader compatible (Inspector), no MaterialPropertyBlock on common renderers, fewer keyword combinations across materials.
7. GPU Resident Drawer for scenes with many repeated MeshRenderers: Forward+, Instanced Drawing, BatchRendererGroup Variants Keep All, then try GPU Occlusion. A/B against static batching.
8. Culling: LODGroups with a culling last LOD, LOD Bias / Max LOD per tier, `layerCullDistances` for small props, lower far plane with fog, baked occlusion culling for dense interiors.
9. Post-processing: Bloom downscaled with fewer iterations; no DoF, motion blur or SSAO on low mobile.
10. Assets: mipmaps on, ASTC/BC compression, Max Size overrides, Read/Write off, mipmap streaming with a budget; mesh LODs and vertex channel stripping.
11. Shader variants: disable unused features on every included URP Asset, Graphics stripping settings, `shader_feature_local`, an `IPreprocessShaders` stripper for remaining waste; prewarm what is left.
12. Re-verify every tier in a player build: pink materials or missing effects mean over-stripping.

## Rules

- Measure SetPass calls, not Batches, to judge SRP Batcher efficiency.
- The SRP Batcher batches by shader variant; material count is not the problem, keyword divergence is.
- `MaterialPropertyBlock` opts a renderer out of the SRP Batcher and the GPU Resident Drawer. Use material instances or instanced data instead.
- GPU Resident Drawer silently does nothing in builds unless BatchRendererGroup Variants is **Keep All**, the renderer uses Forward+ (or Deferred+), and the platform supports compute (not GLES).
- GPU occlusion culling only culls GRD-drawn renderers.
- Static batching trades memory for draw calls; do not static-batch many copies of one dense mesh.
- Dynamic batching stays off in URP unless measured otherwise on old GLES hardware.
- Every pass that samples camera color or depth (opaque texture, depth copy, some renderer features) breaks tile-memory flow on mobile and makes MSAA expensive.
- Every URP Asset reachable from the build platform's quality levels adds shader variants.
- Flag 6000.x minor-specific features as "verify in your editor version": Deferred+ (6000.1+), Mesh LOD (6000.2+), PSO prewarming via `GraphicsStateCollection`, Mipmap Streaming renames, Render Graph Compatibility Mode removal.

## References

- [references/diagnostics.md](references/diagnostics.md): read first; Profiler, Stats, Frame Debugger, Rendering Debugger, GPU module, Render Graph Viewer.
- [references/batching.md](references/batching.md): read when SetPass calls or render-thread time are high, or before enabling the GPU Resident Drawer.
- [references/culling-lod.md](references/culling-lod.md): read when triangle counts or culling time are high; includes a `layerCullDistances` script.
- [references/lighting-cost.md](references/lighting-cost.md): read when lights, shadows or the rendering path dominate GPU time.
- [references/assets-and-variants.md](references/assets-and-variants.md): read for mesh/texture budgets, mipmap streaming, and shader variant stripping (includes an `IPreprocessShaders` example).
- [references/quality-tiers-mobile.md](references/quality-tiers-mobile.md): read when setting up URP quality tiers or targeting mobile (render scale, MSAA vs FXAA, HDR, depth/opaque texture, post cost).

## Pitfalls

| Symptom | Likely cause |
|---|---|
| Frame Debugger shows "SRP Batcher: node is not compatible" | Shader CBUFFER layout, or a MaterialPropertyBlock on the renderer |
| GRD works in Editor, not in the build | BatchRendererGroup Variants stripped, or GLES/unsupported API |
| Draw calls fine, GPU still slow | Fill rate: resolution, overdraw, post-processing, shadows sampling, MSAA resolve |
| MSAA suddenly expensive on mobile | Opaque/depth texture or a custom pass forces a resolve and store |
| Shadow pass larger than the main pass | Too many shadow casters, too many cascades, too long distance |
| Point lights tank performance | Point light shadows render 6 maps each; Forward per-object limits cause popping |
| Build takes hours, huge shader memory | Features enabled on an unused URP Asset or tier, `multi_compile` instead of `shader_feature` |
| Pink materials only in player | Over-stripping (Graphics stripping or a custom IPreprocessShaders) |
| Blurry textures after enabling streaming | Memory budget below non-streaming texture memory |
| Hitch the first time an effect appears | Shader/PSO compilation; prewarm variants |

## Related skills

- `urp-postprocessing`: Volume setup and per-effect settings.
- `unity-lighting`: light setup, baking, probes and APV.
- `unity-texture-import`: compression formats and import settings.
- `level-geometry-authoring`: LODGroup, terrain and ProBuilder authoring.
- `memory-snapshot-profiling`: GPU and texture memory captures.
- `validate-urp-render-graph-renderer-feature`: custom renderer feature correctness and pass merging.
