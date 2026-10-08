---
name: validate-urp-render-graph-renderer-feature
description: Reviews and corrects Unity 6 URP ScriptableRendererFeature / ScriptableRenderPass code that uses the Render Graph API (RecordRenderGraph, ContextContainer, UniversalResourceData, AddRasterRenderPass, AddBlitPass, TextureHandle, PassData). Checks material and texture binding, read/write resource wiring, static render functions and stale PassData, texture descriptors, blit/copy simplification, global texture exposure, ConfigureInput declarations, pass culling, and leftover Compatibility Mode code (Execute, OnCameraSetup, RTHandle allocation). Use when the user wants a renderer feature reviewed, validated, debugged, ported from Execute() to Render Graph, or written correctly for URP 17, or reports a custom pass that renders black, does nothing, or errors in the Render Graph Viewer.
license: Unity Companion License (see licenses/UNITY-COMPANION-LICENSE.md)
metadata:
  category: rendering-vfx
  sources: "Unity-Technologies/skills/skills/validate-urp-render-graph-renderer-feature"
  unity: "6000.0+"
---

# Validate a URP Render Graph Renderer Feature

Review a Unity 6 URP `ScriptableRendererFeature` and its passes for correctness, then propose minimal fixes.

## Inputs

Ask for what is missing:

- Unity and URP version (Render Graph APIs shifted across 6000.0-6000.x).
- The feature and pass code.
- Intended behavior and expected inputs/outputs (which textures are read, what is written, at which `RenderPassEvent`).
- Constraints: required pass type, shader property names, project conventions.

## Output format

1. **Validation Summary**: short overall assessment.
2. **Confirmed Issues**: directly supported by the code.
3. **Likely Issues / Risky Assumptions**: depend on missing context.
4. **Recommended Fixes**: minimal, targeted, one per issue, with why it matters.
5. **Corrected Snippets**: small, only where useful.
6. **Missing Information**: what would raise confidence.

Distinguish confirmed from likely. Never invent APIs. Prefer targeted fixes over rewrites. Do not validate shader internals except where they affect binding.

For reference implementations of a correct blit feature, a raster pass with `PassData`, depth/normals input, and an Execute() -> Render Graph port, read [references/render-graph-patterns.md](references/render-graph-patterns.md).

## Checklist

### 0. API generation

- Render Graph is the default URP path in Unity 6. `Execute(ScriptableRenderContext, ref RenderingData)`, `OnCameraSetup`, `Configure`, and `cameraColorTargetHandle` belong to **Compatibility Mode**, which is deprecated in 6000.0 and is being removed in later 6000.x releases (hidden behind the `URP_COMPATIBILITY_MODE` scripting define from 6000.3). A pass that only overrides `Execute` does nothing when Render Graph is active. Flag as confirmed and port to `RecordRenderGraph`.
- `RenderingUtils.ReAllocateIfNeeded` is obsolete; in Render Graph code, transient textures come from `renderGraph.CreateTexture`, not `RTHandle` fields. Persistent `RTHandle`s (history buffers) must be imported with `renderGraph.ImportTexture`.
- Frame data comes from `ContextContainer`: `frameData.Get<UniversalResourceData>()`, `UniversalCameraData`, `UniversalRenderingData`, `UniversalLightData`. Flag use of `RenderingData` inside `RecordRenderGraph`.

### 1. Material binding

- Materials are declared, passed into the pass, null-checked, and actually used.
- **The primary input texture is bound explicitly** when the shader expects one. A common bug binds only a mask/noise texture and silently samples nothing as the main input. `Blitter.BlitTexture` / `AddBlitPass` bind the source as `_BlitTexture`; a shader expecting `_MainTex` gets nothing.
- Auxiliary textures and parameters are bound explicitly with names matching the shader's properties.
- Per-pass material changes do not leak across cameras (materials are shared assets).

### 2. Texture resource wiring

- Every texture sampled in the render function is declared with `builder.UseTexture(handle, AccessFlags.Read)`; every target with `builder.SetRenderAttachment(handle, index, AccessFlags.Write)` (and depth with `SetRenderAttachmentDepth`).
- A texture is not both sampled and used as an attachment in the same raster pass (needs a copy or a second pass).
- Source, destination, and auxiliary roles are not confused. The implementation does not assume a texture exists that was never requested (`cameraDepthTexture`, `cameraNormalsTexture`, `cameraOpaqueTexture`, `motionVectorColor` are invalid unless the pipeline produced them; see section 8).
- If the pass writes to the active color but the camera may render to the back buffer, it checks `resourceData.isActiveTargetBackBuffer` or sets `requiresIntermediateTexture = true` on the pass.
- After writing to a new texture meant to replace camera color, it assigns `resourceData.cameraColor = destination` instead of blitting back.

### 3. Execution structure and PassData

- The render function is `static` (static lambda or static method) and reads only from `PassData` and the graph context, never from pass instance fields.
- Every `PassData` field read in the render function is assigned in **every** `RecordRenderGraph` call. `PassData` objects are pooled; partial assignment leaks stale handles and values from earlier frames.
- The correct builder is used: `AddRasterRenderPass` (draws to attachments, `RasterCommandBuffer`), `AddComputePass` (dispatch), `AddUnsafePass` (needs `SetRenderTarget` or mixed work; flag when a raster pass would do).
- Passes with side effects and no consumed outputs call `builder.AllowPassCulling(false)`, otherwise Render Graph culls them. Passes that set global state call `builder.AllowGlobalStateModification(true)`.

### 4. Texture descriptors

- **Do not build a fresh `TextureDesc` by default.** Derive it from an existing graph resource and change only what must differ:

```csharp
TextureDesc desc = renderGraph.GetTextureDesc(resourceData.activeColorTexture);
desc.name = "_MyEffectColor";
desc.clearBuffer = false;
TextureHandle target = renderGraph.CreateTexture(desc);
```

- `TextureHandle.GetDescriptor(renderGraph)` is equivalent and also returns a `TextureDesc` (not a `RenderTextureDescriptor`).
- Flag manual width/height copies, `cameraTargetDescriptor` as the primary source when a graph resource exists, and accidental loss of MSAA, format, or dynamic-resolution scaling.
- When a `RenderTextureDescriptor` is genuinely needed (e.g. from `UniversalCameraData.cameraTargetDescriptor` for a downscaled buffer), `UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, name, clear)` is the helper; zero `depthBufferBits` for color-only targets.

### 5. Copy simplification

- A raster pass that only copies one texture to another without a material should be `renderGraph.AddBlitPass(source, destination, Vector2.one, Vector2.zero, passName: "...")`.
- Recommend `AddCopyPass` only when its constraints hold (same dimensions/format/MSAA; relies on framebuffer fetch on tile-based GPUs); otherwise prefer `AddBlitPass`.

### 6. Blit simplification

- A raster pass that reads one texture, writes one, and only calls a fullscreen material blit should be `renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, destination, material, passIndex), "Name")`.
- Keep a custom raster pass only for extra logic: multiple draws, conditional work, several inputs with explicit bindings, or property setup per frame.

### 7. Global resource exposure

- No global exposure by default. Prefer explicit handles passed between passes (through a custom `ContextItem` added with `frameData.Create<T>()` when passes live in different features).
- When a later shader truly needs a global, use `builder.SetGlobalTextureAfterPass(handle, shaderPropertyId)`; consumers declare `builder.UseGlobalTexture(id)`.
- Flag `cmd.SetGlobalTexture` inside Render Graph passes, unused global publication, and `UseAllGlobalTextures(true)` that extends lifetimes and blocks aliasing.

### 8. Input declaration

- If the pass reads depth, normals, opaque color, or motion vectors, the pass calls `ConfigureInput(ScriptableRenderPassInput.Depth | Normal | Color | Motion)` (typically in `AddRenderPasses` or the pass constructor) before the pipeline schedules prepasses.
- Flag inputs declared but unused: each can add a depth/normal prepass or a color copy.
- Confirmed issue when code reads the texture without declaring it; likely issue when the effect description implies it.

### 9. Feature hygiene

- `Create()` builds passes; `AddRenderPasses` only configures and enqueues. No allocation per frame in `AddRenderPasses` or `RecordRenderGraph` (materials, arrays, lambdas capturing locals).
- Skip preview and reflection cameras (`cameraData.cameraType`) when the effect is for game/scene cameras only.
- Materials created with `CoreUtils.CreateEngineMaterial` are destroyed in `Dispose(bool)` with `CoreUtils.Destroy`.
- The `RenderPassEvent` matches intent (for example `BeforeRenderingPostProcessing` to be tonemapped, `AfterRenderingPostProcessing` to stay untonemapped).

## Debugging tools to recommend

- **Render Graph Viewer** (*Window > Analysis > Render Graph Viewer*): shows passes, culled passes, resource reads/writes, and merged native render passes.
- **Frame Debugger**: confirms the pass ran and what was bound.
- Render Graph validation errors in the Console name the pass and resource; quote them in the review.

## Guardrails

- Do not guarantee runtime correctness; state when the provided code is insufficient.
- Explain why each issue matters (black screen, stale data, extra copies, broken on back-buffer cameras, culled pass).
- Flag hidden coupling and unnecessary global state.

## Related skills

- `urp-postprocessing` when a Volume override or Full Screen Pass Renderer Feature is enough.
- `migrate-birp-to-urp` for porting `OnRenderImage` effects.
- `shader-graph-create-custom-node` for the shader side of a fullscreen effect.
