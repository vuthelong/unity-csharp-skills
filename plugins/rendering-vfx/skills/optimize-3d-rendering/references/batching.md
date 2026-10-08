# Batching in URP (Unity 6)

Read when SetPass calls or render-thread time are high, or before enabling the GPU Resident Drawer.

## Which path renders what

| Path | Reduces | Works with | Notes |
|---|---|---|---|
| SRP Batcher | SetPass / state setup cost (not draw count) | MeshRenderer, SkinnedMeshRenderer with SRP Batcher compatible shaders | On by default. Batches by shader variant, not by material. |
| GPU Resident Drawer (GRD) | Draw calls and CPU submission, via BatchRendererGroup instancing | MeshRenderer only | Opt-in. Biggest win for many instances of the same mesh. |
| GPU occlusion culling | GPU work for hidden objects | GRD-drawn renderers only | Opt-in, requires GRD. |
| Static batching | Draw calls for static geometry sharing a material | Batching Static MeshRenderers | Build-time combined vertex buffers, extra memory. |
| GPU instancing | Draw calls for identical mesh + material | Shaders with instancing support, when the SRP Batcher is not used for that object | Also `Graphics.RenderMeshInstanced` / `RenderMeshIndirect` for procedural drawing. |
| Dynamic batching | Draw calls for tiny meshes | Meshes under ~300 vertices | Off by default in URP; usually slower than the SRP Batcher. |

Priority when several apply: GRD (if enabled and the object qualifies) > static batching > SRP Batcher > GPU instancing. An object rendered by the SRP Batcher is not GPU-instanced, even with "Enable GPU Instancing" ticked on the material. Verify exact precedence between GRD and static batching in your editor version.

## SRP Batcher

The SRP Batcher keeps material data persistent in GPU memory and only uploads per-object data, so many materials using the **same shader variant** draw with minimal CPU cost.

Enable: URP Asset > Rendering > SRP Batcher (shown under Advanced properties), or `GraphicsSettings.useScriptableRenderPipelineBatching = true`.

### Shader compatibility

- All built-in engine per-object properties (`unity_ObjectToWorld`, `unity_WorldToObject`, `unity_LODFade`, lightmap ST, SH, etc.) are declared in one constant buffer named `UnityPerDraw`. URP's include files do this.
- All material properties are declared in a single constant buffer named `UnityPerMaterial`, with the **same layout in every pass** of the shader (include the `UnityPerMaterial` block in every pass, even ones that do not use all properties).
- Textures and samplers are outside the CBUFFER.

```hlsl
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half _Smoothness;
CBUFFER_END
```

- Select the shader asset: the Inspector shows **SRP Batcher: compatible** or the reason it is not.
- Shader Graph and all URP shaders are compatible. Hand-written shaders and ported Built-in shaders are the usual offenders (see `migrate-birp-to-urp`).
- A property declared in `Properties {}` but missing from `UnityPerMaterial` (or vice versa, or different per pass) breaks compatibility.

### What breaks batching per object

- **MaterialPropertyBlock**: a renderer with a property block is drawn outside the SRP Batcher. Prefer one material instance per variation (material instances of the same shader variant still batch), or per-instance data through instancing / GRD-compatible paths.
- Different shader **variants** (keywords) start a new batch. Material keyword sprawl (each material toggling different features) fragments batches; standardize features across materials.
- Renderer types other than Mesh/SkinnedMesh (particles, lines, trails) use other paths.
- `Renderer.material` getter creates a new material instance; that is fine for the SRP Batcher but leaks if not destroyed (see `csharp-unity`).

Disable the SRP Batcher temporarily (script toggle above) and compare render-thread time to confirm it is doing its job.

## GPU Resident Drawer

Uses BatchRendererGroup to instance MeshRenderers on the GPU, cutting CPU draw-submission cost. Introduced in Unity 6.0.

### Requirements

1. URP Asset > Rendering > **GPU Resident Drawer: Instanced Drawing**.
2. Rendering path **Forward+** on the URP Renderer (Deferred+ also qualifies in 6000.1+; plain Forward and Deferred do not). Verify the Deferred+ rule in your editor version.
3. Project Settings > Graphics > Shader Stripping > **BatchRendererGroup Variants: Keep All**. With the default stripping the DOTS instancing variants are removed from builds and GRD silently does nothing in the player.
4. SRP Batcher enabled.
5. A platform with compute shader support and a graphics API that supports BRG (DX11/12, Vulkan, Metal, modern consoles). Not OpenGL ES; check WebGPU support per version.
6. Shaders that support DOTS instancing (`#pragma multi_compile _ DOTS_INSTANCING_ON`). URP Lit, Simple Lit, Unlit and Shader Graph do.

### Renderers it takes and skips

- Takes: `MeshRenderer` with a DOTS-instancing shader, static or dynamic.
- Skips (falls back to the SRP Batcher): `SkinnedMeshRenderer`, particle and other non-MeshRenderer renderers, renderers using a `MaterialPropertyBlock`, objects with a script that uses a per-instance render callback such as `OnRenderObject`, and objects whose shader lacks DOTS instancing. The exclusion list changes between versions; confirm what GRD picked up with the Rendering Debugger GPU Resident Drawer panel and the Frame Debugger (BRG draws versus SRP Batch draws).

### Static vs dynamic

- Static and dynamic MeshRenderers both work. Moving objects pay a per-frame upload of their transform data; tens of thousands of moving instances can make that upload the new bottleneck.
- Static batching combines meshes into a few large buffers, which removes the per-mesh identity GRD instances on. With GRD on, A/B test with Player Settings > Static Batching off; many projects see better results with GRD alone. Verify in your editor version.
- Enabling/disabling or instantiating many renderers in one frame causes a spike while GRD registers them; pool and keep objects resident instead of toggling.

### Pitfalls

- Build size and build time increase with Keep All BRG variants.
- Benefit is small for scenes of mostly unique meshes; it shines for repeated props, foliage, modular kits.
- Editor Scene view behavior can differ from the player; profile GRD in a Development Build.
- Light Probe Proxy Volumes, some legacy lighting features and custom shaders without DOTS instancing fall back silently. Watch for visual differences between Editor and player.

## GPU occlusion culling

- URP Asset > Rendering > **GPU Occlusion** (requires GRD).
- Uses a depth pyramid from the previous frame plus the current one to cull GRD instances on the GPU. Good for dense scenes with large occluders and dynamic content; costs some GPU time for the depth pyramid.
- Only GRD-drawn renderers are occlusion-culled this way. Skinned meshes and other fallbacks are not.
- Independent of baked (Umbra) occlusion culling; you can use both, but measure: in dense static interiors baked occlusion is still cheaper on CPU.
- Debug: Rendering Debugger > GPU Resident Drawer > occlusion test overlay / culling stats.

## Static batching

- Player Settings > Other Settings > Static Batching, plus **Batching Static** flag on GameObjects.
- Combines meshes that share a material into large vertex/index buffers at build time (or scene load). Draw calls drop, memory rises (each instance's vertices are duplicated in the combined buffer).
- Objects cannot move, scale or rotate afterward.
- Runtime: `StaticBatchingUtility.Combine(root)` for procedurally placed static content; source meshes must be Read/Write enabled.
- Avoid for many instances of one high-poly mesh (memory explosion); use instancing or GRD instead.

## GPU instancing vs the SRP Batcher

- The SRP Batcher wins whenever a shader is compatible, so the material "Enable GPU Instancing" checkbox is ignored for those objects.
- Use instancing explicitly for: thousands of identical meshes driven from code (`Graphics.RenderMeshInstanced`, `RenderMeshIndirect` with a compute-filled args buffer), grass, debris. Per-instance data goes through instanced properties or structured buffers, not MaterialPropertyBlock-per-renderer.
- On Unity 6, GRD replaces most manual instancing for scene-placed MeshRenderers.

## Dynamic batching

- URP Asset > Rendering > Dynamic Batching (Advanced), off by default; may be hidden or deprecated in later URP versions.
- Transforms vertices on the CPU every frame for meshes under ~300 vertices / 900 attributes. On modern CPUs the SRP Batcher is faster and keeps the vertex work on the GPU.
- Consider it only for very old GLES devices with many tiny meshes that cannot use the SRP Batcher, and measure.
