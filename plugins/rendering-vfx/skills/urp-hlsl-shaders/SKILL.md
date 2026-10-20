---
name: urp-hlsl-shaders
description: "Writes, fixes and verifies hand-written HLSL/ShaderLab shaders for URP on Unity 6: UniversalForward, ShadowCaster, DepthOnly and DepthNormals passes that agree, SRP Batcher CBUFFERs, main and additional lights under Forward and Forward+, keywords (shader_feature vs multi_compile vs dynamic_branch), variant stripping, PSO warm-up for first-use hitches, and GPU cost measured in ms. Use for foliage, terrain, water, wind, snow or wetness shaders, or when a material turns pink or invisible, casts no shadow or the full unclipped shadow, ignores extra lights, breaks SSAO, loses the SRP Batcher, or a build bloats with variants. Not for Shader Graph node authoring (shader-graph-create-custom-node), renderer features (validate-urp-render-graph-renderer-feature) or post effects (urp-postprocessing)."
license: MIT
metadata:
  category: rendering-vfx
  sources: "https://github.com/scenario-labs/skills/tree/main/skills/game-engines/unity/scenario-unity-shaders, https://docs.unity3d.com/6000.0/Documentation/Manual/urp/writing-custom-shaders-urp.html, https://docs.unity3d.com/6000.0/Documentation/Manual/SRPBatcher.html, https://docs.unity3d.com/6000.0/Documentation/Manual/shader-keywords.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ShaderUtil.GetShaderMessages.html"
  unity: "6000.0+"
---

# URP HLSL shaders

A finished shader is one you can prove: it compiles for the target graphics API, every pass agrees, it stays SRP Batcher compatible, its variants are counted after stripping, its first use is warm, and its look is checked in a capture. Hand-written HLSL is the route an agent can verify through text; Shader Graph is for artist-edited graphs (`shader-graph-create-custom-node`).

## Rules (the expert delta)

1. **Every pass must agree.** Vertex displacement (wind, snow push, waves) and `clip()` (alpha cutout, dissolve) must run in ShadowCaster, DepthOnly and DepthNormals too. Put them in one shared function in `HLSLINCLUDE` and call it from every pass. Otherwise shadows show the undisplaced or unclipped shape, SSAO and depth-based effects disagree with the visible mesh.
2. **Opaque shaders need DepthOnly.** With Depth Priming on (or forced), a shader without a `DepthOnly` pass renders nothing. Ship UniversalForward, ShadowCaster, DepthOnly and DepthNormals for any opaque or alpha-tested surface.
3. **DepthNormals writes the raw world normal** (`half4(NormalizeNormalPerPixel(normalWS), 0)`), not `n * 0.5 + 0.5`; packed normals read back wrong in SSAO, outlines and decals.
4. **One `UnityPerMaterial` CBUFFER, identical in every pass.** Declare every material property inside `CBUFFER_START(UnityPerMaterial)` in `HLSLINCLUDE`. A property outside it, or a pass with a different layout, turns the SRP Batcher off for that shader. Textures and samplers stay outside the CBUFFER.
5. **Read the installed pipeline, not a tutorial.** Copy the keyword block and include paths from the installed `Lit.shader` (`Library/PackageCache/com.unity.render-pipelines.universal@*/Shaders/Lit.shader`), because they change between minors: `_CLUSTER_LIGHT_LOOP` replaced `_FORWARD_PLUS` in 6.1, soft shadows gained `_LOW/_MEDIUM/_HIGH` variants, `SHADOWS_SCREEN` and `ComputeScreenPos` are deprecated (use `GetMainLight(shadowCoord)` and `GetNormalizedScreenSpaceUV(positionCS)`).
6. **Forward+ needs a directional pre-loop.** `LIGHT_LOOP_BEGIN` skips directional lights under the cluster light loop, so a custom loop must first iterate `URP_FP_DIRECTIONAL_LIGHTS_COUNT` additional lights, then run `LIGHT_LOOP_BEGIN/END`. The macro reads `inputData.positionWS` and `inputData.normalizedScreenSpaceUV`, so those must be in scope. Template: [references/urp-pass-template.md](references/urp-pass-template.md).
7. **Variants are a budget counted after stripping.** `shader_feature(_local)` for material toggles set in the Editor; `multi_compile(_local)` only for keywords toggled from runtime C#; `dynamic_branch` only when tested with `if`, never `#if`. A `shader_feature` variant ships only if a material in the build enables it, so never toggle one from runtime C# (pink or nearest-match in the player). Above about 128 keywords per shader compile slows.
8. **First use is proven warm.** A first-use hitch shows as `Shader.CreateGPUProgram` in the Profiler. Trace a `GraphicsStateCollection` per graphics API in a development player, warm it up behind a loading screen, and show gameplay frames with zero `CreateGPUProgram` samples. `GraphicsStateCollection` is experimental; check its API on the project's minor.
9. **Cost is GPU milliseconds, not instruction count.** Loops count at max trip, float4 math costs about 4x float, transcendentals (`sin`, `pow`, `exp`) cost many cycles. `half` pays only where a device profiler shows ALU-bound; desktop compiles `half` as `float`. Compare A/B in GPU ms at the same resolution, interleaved, against run-to-run noise (about 10 %).
10. **Global surface state goes through globals.** Weather-driven values (wetness, snow cover, wind) set once per frame with `Shader.SetGlobalFloat`/`SetGlobalVector` and declared outside the CBUFFER; per-object values through material instances, `MeshRenderer.SetShaderUserValue` (6.3+, read as `unity_RendererUserValue`), or DOTS instanced properties for the GPU Resident Drawer. `MaterialPropertyBlock` breaks the SRP Batcher and the GPU Resident Drawer.

## Establish first

- Graphics APIs and platforms (DX12/DX11/Vulkan/Metal, GLES, WebGL/WebGPU). Compute needs GLES 3.1+ and is absent on WebGL 2.
- Rendering path per renderer asset (Forward, Forward+, Deferred/Deferred+) and every URP asset per quality level: Depth Texture, Opaque Texture, soft shadows, SSAO. A depth-faded effect needs Depth Texture on every asset that renders it.
- Whether the GPU Resident Drawer is on (needs DOTS instancing support in custom shaders).
- Budgets: GPU ms for the effect, variants per shader, overdraw for alpha-tested foliage.

## Workflow

1. **Inventory.** List the project's shaders (`AssetDatabase.FindAssets("t:Shader", new[] { "Assets" })`) and their passes; read the installed URP version from `Packages/manifest.json` or `Library/PackageCache`. Open the installed `Lit.shader` for the current keyword block.
2. **Author** from [references/urp-pass-template.md](references/urp-pass-template.md): shared `HLSLINCLUDE` (CBUFFER, displacement, clip), then the four passes.
3. **Compile and read errors** in the Editor through `unity command eval` (see `unity-cli`):

   ```csharp
   var s = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/Foliage.shader");
   foreach (var m in ShaderUtil.GetShaderMessages(s))
       Debug.Log($"{m.severity} {m.platform} {m.file}:{m.line} {m.message}");
   Debug.Log($"hasError={ShaderUtil.ShaderHasError(s)} passes={s.passCount}");
   ```

   GATE: zero errors, every required pass present (`Shader.FindPassTagValue` or `passCount`).
4. **Look.** Capture the material in the scene under the project's own lighting (see the project's capture rules), with an extra point light and a second directional light, in Forward and Forward+. GATE: no magenta, shadow under the object darker than open ground, a clipped or displaced shape casts the matching shadow, extra lights change the lit side.
5. **Batcher and passes.** Frame Debugger: draws land in an SRP Batch; force Depth Priming on in the URP asset and confirm the object still renders; turn SSAO off and confirm the effect is unchanged.
6. **Variants.** Build once with Strict Shader Variant Matching and read the build log stripping funnel; strip with `IPreprocessShaders` or Shader Build Settings. GATE: shipped variants per shader under budget, no growth without a reason.
7. **Cost and warm-up** before a release: GPU ms A/B in a development player (`optimize-3d-rendering`), PSO trace per API.

Score the result with [references/shader-checklist.md](references/shader-checklist.md) before calling it done.

## Common mistakes

| Mistake | What it looks like | Fix |
|---|---|---|
| No DepthOnly pass | object vanishes with Depth Priming | add DepthOnly sharing the vertex and clip code |
| `clip()` or wind only in the forward pass | full shadow under cut-out leaves; shadow does not sway | shared function called by every pass |
| DepthNormals returns `n * 0.5 + 0.5` | wrong SSAO and outlines | return the raw normalized world normal |
| `LIGHT_LOOP_BEGIN` without a directional pre-loop | a second directional light lights nothing under Forward+ | loop `URP_FP_DIRECTIONAL_LIGHTS_COUNT` first |
| `_FORWARD_PLUS`, `SHADOWS_SCREEN`, `ComputeScreenPos` | warnings, dead shadow branch on 6.1+ | `_CLUSTER_LIGHT_LOOP`, `GetMainLight(shadowCoord)`, `GetNormalizedScreenSpaceUV` |
| `shader_feature_local` toggled from runtime C# | pink or wrong variant in the player only | `multi_compile_local`, or swap materials |
| `dynamic_branch` tested with `#if` | variant count unchanged | test with `if (KEYWORD)`, declare without a stage suffix |
| `if (KW)` on a `_fragment`-suffixed keyword | "known keyword but not declared in this pass" | declare it for all stages |
| Property outside `UnityPerMaterial`, or CBUFFER differs per pass | SRP Batcher off, values bleed between materials | one CBUFFER in `HLSLINCLUDE` |
| Depth Texture off on one URP asset | depth-faded water or soft edges break on one quality level only | enable it on every asset that renders the effect |
| Stray VolumeProfiles under `Assets/` | post-processing variants for effects no scene uses | move samples and tests out of the build |
| HDR emissive edge 4 or more | edge washes to white under tonemapping | 1 to 1.5, let Bloom carry the glow |
| Judging cost by instruction count, or `half` on desktop | "optimized" shader no faster | GPU ms A/B; profile `half` on the device |
| `MaterialPropertyBlock` for per-object tint | SRP Batcher and GRD fall back | material instances, `SetShaderUserValue`, DOTS instanced props |

## Related skills

- `shader-graph-create-custom-node` - wrapping this HLSL as Custom Function nodes for artists.
- `validate-urp-render-graph-renderer-feature` - full-screen passes and renderer features on Render Graph.
- `optimize-3d-rendering` - GPU-bound frames, draw calls, variant stripping settings, GPU Resident Drawer.
- `day-night-weather` - the global wetness, snow and wind values these shaders read.
- `unity-editor-safety` - confirming the compile in Editor.log after edits.
