# Render Graph reference patterns (URP 17, Unity 6)

Use these as the "known good" shape when reviewing or correcting a feature. Namespaces: `UnityEngine.Rendering`, `UnityEngine.Rendering.Universal`, `UnityEngine.Rendering.RenderGraphModule`, `UnityEngine.Rendering.RenderGraphModule.Util`.

## Contents

- [Fullscreen material effect (AddBlitPass)](#fullscreen-material-effect-addblitpass)
- [Raster pass with PassData and extra inputs](#raster-pass-with-passdata-and-extra-inputs)
- [Sharing a texture between features](#sharing-a-texture-between-features)
- [Porting Execute() to RecordRenderGraph](#porting-execute-to-recordrendergraph)
- [Shader side](#shader-side)

## Fullscreen material effect (AddBlitPass)

```csharp
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public sealed class TintFeature : ScriptableRendererFeature
{
    [SerializeField] Material _material;
    [SerializeField] RenderPassEvent _passEvent = RenderPassEvent.BeforeRenderingPostProcessing;

    TintPass _pass;

    public override void Create()
    {
        _pass = new TintPass { renderPassEvent = _passEvent };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_material == null) return;
        var type = renderingData.cameraData.cameraType;
        if (type == CameraType.Preview || type == CameraType.Reflection) return;
        _pass.Setup(_material);
        renderer.EnqueuePass(_pass);
    }

    sealed class TintPass : ScriptableRenderPass
    {
        Material _material;

        public TintPass() => requiresIntermediateTexture = true;

        public void Setup(Material material) => _material = material;

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer) return;

            TextureHandle source = resourceData.activeColorTexture;
            TextureDesc desc = renderGraph.GetTextureDesc(source);
            desc.name = "_TintColor";
            desc.clearBuffer = false;
            TextureHandle destination = renderGraph.CreateTexture(desc);

            var parameters = new RenderGraphUtils.BlitMaterialParameters(source, destination, _material, 0);
            renderGraph.AddBlitPass(parameters, passName: "Tint");

            resourceData.cameraColor = destination;
        }
    }
}
```

Why this shape:

- `requiresIntermediateTexture = true` makes URP render to an intermediate color texture, so `activeColorTexture` is sampleable.
- The destination descriptor derives from the source, preserving format, size, MSAA, and dynamic resolution.
- `resourceData.cameraColor = destination` swaps camera color; later passes use the new texture with no copy back.
- The material samples `_BlitTexture` (bound by the blit helper), not `_MainTex`.

## Raster pass with PassData and extra inputs

```csharp
sealed class EdgePass : ScriptableRenderPass
{
    static readonly int ThicknessId = Shader.PropertyToID("_Thickness");

    sealed class PassData
    {
        public TextureHandle Source;
        public Material Material;
        public float Thickness;
    }

    Material _material;
    float _thickness;

    public EdgePass()
    {
        renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        requiresIntermediateTexture = true;
        ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
    }

    public void Setup(Material material, float thickness)
    {
        _material = material;
        _thickness = thickness;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        var resourceData = frameData.Get<UniversalResourceData>();
        TextureHandle source = resourceData.activeColorTexture;
        TextureDesc desc = renderGraph.GetTextureDesc(source);
        desc.name = "_EdgeColor";
        desc.clearBuffer = false;
        TextureHandle destination = renderGraph.CreateTexture(desc);

        using (var builder = renderGraph.AddRasterRenderPass<PassData>("Edge Detect", out var passData))
        {
            passData.Source = source;
            passData.Material = _material;
            passData.Thickness = _thickness;

            builder.UseTexture(source, AccessFlags.Read);
            builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
            builder.UseTexture(resourceData.cameraNormalsTexture, AccessFlags.Read);
            builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

            builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
            {
                data.Material.SetFloat(ThicknessId, data.Thickness);
                Blitter.BlitTexture(context.cmd, data.Source, new Vector4(1f, 1f, 0f, 0f), data.Material, 0);
            });
        }

        resourceData.cameraColor = destination;
    }
}
```

Points to check against this shape:

- `ConfigureInput` requests depth and normals, so `cameraDepthTexture` / `cameraNormalsTexture` are valid. The shader reads them through URP's `DeclareDepthTexture.hlsl` / `DeclareNormalsTexture.hlsl` globals; declaring `UseTexture` on them keeps them alive and ordered.
- Every `PassData` field is assigned each recording.
- The lambda is `static`, so it cannot capture `this` or locals.
- Setting a material float inside the render function mutates a shared asset; for per-camera values, use a `MaterialPropertyBlock` stored in `PassData` or set the value during recording when the value is per-frame constant.

## Sharing a texture between features

```csharp
public sealed class MaskData : ContextItem
{
    public TextureHandle Mask;
    public override void Reset() => Mask = TextureHandle.nullHandle;
}

Writer pass, in its `RecordRenderGraph`:

```csharp
var mask = frameData.Create<MaskData>();
mask.Mask = renderGraph.CreateTexture(maskDesc);
```

Reader pass (later `RenderPassEvent`), inside its builder block:

```csharp
if (frameData.Contains<MaskData>())
    builder.UseTexture(frameData.Get<MaskData>().Mask, AccessFlags.Read);
```

 This keeps lifetimes explicit, unlike globals.

## Porting Execute() to RecordRenderGraph

| Compatibility Mode | Render Graph |
|---|---|
| `OnCameraSetup` + `RTHandle` + `RenderingUtils.ReAllocateIfNeeded` | `renderGraph.CreateTexture(desc)` per frame (pooled by the graph) |
| `renderingData.cameraData.renderer.cameraColorTargetHandle` | `frameData.Get<UniversalResourceData>().activeColorTexture` / `cameraColor` |
| `CommandBufferPool.Get` + `context.ExecuteCommandBuffer` | Builder + static render function; the graph executes |
| `Blitter.BlitCameraTexture(cmd, src, tmp, mat, 0)` then back to src | `AddBlitPass` into a new texture and assign `resourceData.cameraColor` |
| `cmd.SetGlobalTexture` | `builder.SetGlobalTextureAfterPass` (only when needed) |
| `ConfigureTarget` / `ConfigureClear` | `SetRenderAttachment` + `desc.clearBuffer` / `clearColor` |
| `ref RenderingData` everywhere | `ContextContainer frameData` |

Keep an `Execute` override only while the project still has to run with Compatibility Mode enabled; `RecordRenderGraph` must be the primary, tested path.

## Shader side

A fullscreen shader used with the Blitter helpers:

```hlsl
Shader "Hidden/Tint"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        Pass
        {
            Name "Tint"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _TintColor;

            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord);
                return color * _TintColor;
            }
            ENDHLSL
        }
    }
}
```

`Blit.hlsl` provides `Vert`, `Varyings`, `_BlitTexture`, and `_BlitScaleBias`. A Fullscreen Shader Graph material also works with `AddBlitPass` and with the built-in *Full Screen Pass Renderer Feature*.
