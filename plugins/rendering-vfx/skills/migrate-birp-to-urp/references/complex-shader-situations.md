# Complex Shader Situations

Use deliberate URP migration strategies for higher-risk Built-in rendering patterns instead of quick shader rewrites.

## Table of Contents

- [Official References](#official-references)
- [Ground Truth](#ground-truth)
- [Case 1: Surface Shaders](#case-1-surface-shaders)
- [Case 2: GrabPass, Refraction, and Scene-Color Effects](#case-2-grabpass-refraction-and-scene-color-effects)
- [Case 3: Built-in Fullscreen Effects and `OnRenderImage`](#case-3-built-in-fullscreen-effects-and-onrenderimage)
- [Case 4: Replacement Shader and Alternate-Camera Rendering Workflows](#case-4-replacement-shader-and-alternate-camera-rendering-workflows)
- [Case 5: Multi-Pass, Custom Lighting, and Deferred-Specific Shaders](#case-5-multi-pass-custom-lighting-and-deferred-specific-shaders)
- [Case 6: Package-Owned and Framework Shaders](#case-6-package-owned-and-framework-shaders)
- [Stop-and-Escalate Signals](#stop-and-escalate-signals)

## Official References

- [Render pipeline feature comparison](https://docs.unity3d.com/Manual/render-pipelines-feature-comparison.html)
- [Upgrade custom shaders for URP compatibility](https://docs.unity3d.com/Manual/urp/urp-shaders/birp-urp-custom-shader-upgrade-guide.html)
- [Camera.OnRenderImage](https://docs.unity3d.com/ScriptReference/Camera.OnRenderImage.html)
- [Custom post-processing in URP](https://docs.unity3d.com/Manual/urp/post-processing/custom-post-processing.html)
- [Make a shader compatible with the Deferred rendering path in URP](https://docs.unity3d.com/Manual/urp/rendering/make-shader-compatible-with-deferred.html)

## Ground Truth

- The Render Pipeline Converter does not auto-upgrade custom shaders.
- URP does not support `GrabPass`.
- URP does not support Surface Shaders.
- `OnRenderImage` is a Built-in image-effect path; URP custom fullscreen effects should move toward `ScriptableRenderPass`, Renderer Features, or URP custom post-processing.
- Hand-written shaders can work in URP, but Shader Graph is often the safer option for simpler effects.

## Case 1: Surface Shaders

Indicators:

- `#pragma surface`
- Surface-shader lighting models
- heavy reliance on Built-in lighting helpers

Implication:

- Surface shaders do not carry over directly to URP.

Safer migration path:

1. Do not attempt a blind syntax patch.
2. Decide whether the shader should be:
   - rebuilt in Shader Graph
   - rewritten as a URP HLSL shader
   - replaced with an existing URP shader if the effect is not special
3. Port and validate one representative shader before scaling.

## Case 2: GrabPass, Refraction, and Scene-Color Effects

Indicators:

- `GrabPass`
- screen-copy distortion or refraction effects
- shaders that expect to sample the already-rendered scene the Built-in way

Implication:

- URP does not support `GrabPass`.

Safer migration path:

1. Determine whether the effect really needs scene color.
2. Enable **Opaque Texture** in the URP Asset if the effect needs to sample already-rendered opaque color.
3. For simple material-level effects, consider a Shader Graph Scene Color path when appropriate.
4. For hand-written HLSL, prefer URP's opaque texture include instead of a raw `GrabPass` replacement:

   ```hlsl
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

   float2 screenUV = input.positionCS.xy / _ScaledScreenParams.xy;
   half3 sceneColor = SampleSceneColor(screenUV);
   ```

5. For broader effect pipelines, plan a Renderer Feature or custom pass approach.
6. Call out that Built-in `GrabPass` and URP opaque texture are not perfectly equivalent. Transparent sorting, when the texture is captured, and camera stacking can affect parity.
7. Do not claim direct parity until the effect is revalidated in-scene.

## Case 3: Built-in Fullscreen Effects and `OnRenderImage`

Indicators:

- `OnRenderImage(RenderTexture, RenderTexture)`
- custom image-effect scripts
- blit-style camera callbacks

Implication:

- In URP, these effects should move toward `ScriptableRenderPass`, Renderer Features, or URP custom post-processing.

Safer migration path:

1. Identify what the effect actually needs: full-screen color, depth, normals, or custom buffers.
2. Choose a URP-compatible implementation path.
3. For a normal fullscreen blit in Unity 6, propose a `ScriptableRendererFeature` whose pass implements `RecordRenderGraph` and uses `renderGraph.AddBlitPass` into a new texture, rather than preserving `OnRenderImage`. `Execute`, `OnCameraSetup`, `cameraColorTargetHandle`, and `RenderingUtils.ReAllocateIfNeeded` belong to the deprecated Compatibility Mode and do nothing when Render Graph is active.
4. Port one effect at a time.
5. Validate in Game View, not only in Scene View.

Avoid source-to-source blits (`Blitter.BlitCameraTexture(cmd, source, source, material, 0)`, or a blit pass whose source and destination are the same handle). Write to a new texture and make it the camera color.

Minimal shape for an answer:

```csharp
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public sealed class CustomFullscreenEffectFeature : ScriptableRendererFeature
{
    [SerializeField] Material m_Material;
    CustomFullscreenEffectPass m_Pass;

    public override void Create()
    {
        m_Pass = new CustomFullscreenEffectPass { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (m_Material == null) return;
        m_Pass.Material = m_Material;
        renderer.EnqueuePass(m_Pass);
    }

    sealed class CustomFullscreenEffectPass : ScriptableRenderPass
    {
        public Material Material;

        public CustomFullscreenEffectPass() => requiresIntermediateTexture = true;

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer) return;

            TextureHandle source = resourceData.activeColorTexture;
            TextureDesc desc = renderGraph.GetTextureDesc(source);
            desc.name = "_CustomFullscreenEffect";
            desc.clearBuffer = false;
            TextureHandle destination = renderGraph.CreateTexture(desc);

            renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, destination, Material, 0), "Custom Fullscreen Effect");
            resourceData.cameraColor = destination;
        }
    }
}
```

The material's shader samples `_BlitTexture` (include `Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl`), not `_MainTex` as `OnRenderImage` shaders did, so port the shader too. Pick the pass event deliberately: `BeforeRenderingPostProcessing` gets tonemapped, `AfterRenderingPostProcessing` does not. For depth/normal inputs, `ConfigureInput`, and review criteria, see the `validate-urp-render-graph-renderer-feature` skill. For simple material-only effects, the built-in *Full Screen Pass Renderer Feature* with a Fullscreen Shader Graph needs no code.

## Case 4: Replacement Shader and Alternate-Camera Rendering Workflows

Indicators:

- `RenderWithShader`
- `SetReplacementShader`
- camera-based normal/depth/edge pipelines

Implication:

- These workflows often depend on Built-in rendering assumptions and pass tags.

Safer migration path:

1. Identify the purpose of the replacement pass — outlines, selection, depth, normals, masks, and so on.
2. Determine whether URP already exposes a better path through renderer configuration, depth/normal textures, or a custom pass.
3. If a custom shader is still required, port the shader with explicit URP pass/tag expectations instead of assuming Built-in tags still drive the same behavior.

## Case 5: Multi-Pass, Custom Lighting, and Deferred-Specific Shaders

Indicators:

- many passes in one shader
- custom forward-add or deferred assumptions
- lighting code tightly coupled to Built-in helper includes
- shaders that need specific deferred compatibility

Implication:

- These are not simple "replace includes and tags" conversions.

Safer migration path:

1. Determine which passes are still needed in URP.
2. If the shader must support URP Deferred, validate the required URP pass tags and rendering-path behavior.
3. If the shader can be forward-only, say so explicitly and scope the port accordingly.
4. Expect multiple validation rounds.

## Case 6: Package-Owned and Framework Shaders

Indicators:

- Asset Store shader frameworks
- water, foliage, toon, outline, dissolve, or special rendering packages
- package namespace includes or generated shader code

Implication:

- These are not safe to rewrite blindly. They may need maintainer guidance, a scoped custom replacement, or manual follow-up if the project does not already contain an approved URP-compatible path.

Safer migration path:

1. Identify the exact materials and shader files affected.
2. Do not edit immutable package files or generated shader internals in bulk.
3. Use local material copies or scoped custom replacements when the user approves that approach.
4. Limit manual ports to the exact materials the user needs and call out the risk clearly.

## Case 7: Foliage, Grass, Tree, and Billboard Shaders

Indicators:

- grass, tree, bush, leaf, terrain detail, SpeedTree, billboard, or vegetation material names
- `Nature/`, `TreeCreator`, `Grass`, `Foliage`, or similar shader names
- cutout or alpha-test properties such as `_Cutoff`, `_AlphaClip`, `_AlphaTest`, or alpha threshold keywords
- culling, two-sided, billboard, wind, bend, hue variation, or terrain-detail keywords

Implication:

- A grass or tree material can be "not pink" but still wrong if the converter drops alpha clipping, two-sided leaf rendering, wind, billboard behavior, normals, tint, or terrain detail integration.
- Plain URP Lit can be acceptable only for simple opaque bark or non-specialized meshes. It is usually not enough for grass cards, leaf cards, billboards, or wind-driven foliage.

Safer migration path:

1. Separate bark/opaque mesh materials from leaf, grass, billboard, and terrain-detail materials.
2. For simple bark/opaque materials, preserve base map, color, normal, metallic/smoothness, and tiling/offset.
3. For foliage cards, preserve base texture, tint, alpha cutoff, transparent/cutout intent, culling/two-sided intent, and normal/detail maps.
4. Do not call foliage complete until a representative scene/camera confirms grass and leaves are not solid opaque quads.
5. If wind, billboard, or terrain-detail behavior cannot be preserved in the current pass, report that exact behavior as manual/custom-shader follow-up instead of claiming full visual parity.

## Stop-and-Escalate Signals

Stop and summarize the blocker instead of guessing when:

- multiple high-risk shader categories overlap in the same project
- the effect depends on undocumented package internals
- the shader compiles but visual parity is clearly not explainable by parameter tuning
- the project relies heavily on custom fullscreen or replacement-shader rendering paths
- the user expects production-safe parity but the port strategy is still exploratory
