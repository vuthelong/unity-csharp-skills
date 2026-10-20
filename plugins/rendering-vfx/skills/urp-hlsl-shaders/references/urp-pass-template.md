# URP pass template (lit, alpha-tested, displaced)

Read when writing a new URP shader or adding the missing passes to an existing one. The template compiles on Unity 6000.6 (D3D11, every pass, Forward+ and DOTS instancing variants). Its keyword block is a subset of URP's `Lit.shader`; re-copy it from the installed `Lit.shader` after every URP upgrade, and compile with `ShaderUtil.GetShaderMessages` before trusting it.

## Shared code first

Everything a pass might need goes in `HLSLINCLUDE`, so all passes see the same CBUFFER, the same displacement and the same clip.

```hlsl
Shader "Project/FoliageLit"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _WindStrength ("Wind Strength", Float) = 0.1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Cutoff;
            float _WindStrength;
        CBUFFER_END

        // Globals set by C# (Shader.SetGlobal*): outside the CBUFFER.
        float4 _GlobalWind; // xyz direction, w time scale

        float3 DisplaceOS(float3 positionOS, float weight)
        {
            float3 dirOS = TransformWorldToObjectDir(_GlobalWind.xyz);
            return positionOS + dirOS * sin(_Time.y * _GlobalWind.w + positionOS.x) * _WindStrength * weight;
        }

        void ClipAlpha(float2 uv)
        {
            half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a * _BaseColor.a;
            clip(a - _Cutoff);
        }
        ENDHLSL
```

The displacement weight here comes from vertex color red (0 at the trunk, 1 at leaf tips); pass whatever mask the mesh carries.

## UniversalForward

```hlsl
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            // Subset of the installed Lit.shader block (checked on 6000.6). Re-copy after upgrades.
            // Left out on purpose: lightmaps, shadowmask, SSAO, light layers, cookies, reflection
            // probe blending, decals, LOD cross-fade, debug display. Add the ones the scene uses.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                float3 posOS = DisplaceOS(input.positionOS.xyz, input.color.r);
                VertexPositionInputs p = GetVertexPositionInputs(posOS);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half3 Shade(Light light, half3 albedo, half3 n)
            {
                half ndl = saturate(dot(n, light.direction));
                return albedo * light.color * ndl * light.distanceAttenuation * light.shadowAttenuation;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                ClipAlpha(i.uv);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                half3 n = normalize(i.normalWS);

                // LIGHT_LOOP_BEGIN reads these two fields under the cluster light loop.
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                half4 shadowMask = half4(1, 1, 1, 1);

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                half3 color = albedo * SampleSH(n);
                color += Shade(GetMainLight(shadowCoord), albedo, n);

            #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
            #if USE_CLUSTER_LIGHT_LOOP
                // Directional lights other than the main one are skipped by LIGHT_LOOP_BEGIN.
                for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    color += Shade(GetAdditionalLight(lightIndex, i.positionWS, shadowMask), albedo, n);
                }
            #endif
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    color += Shade(GetAdditionalLight(lightIndex, i.positionWS, shadowMask), albedo, n);
                LIGHT_LOOP_END
            #endif

                color = MixFog(color, i.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }
```

## ShadowCaster

```hlsl
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(DisplaceOS(input.positionOS.xyz, input.color.r));
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                o.positionCS = ApplyShadowClamping(positionCS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target { ClipAlpha(i.uv); return 0; }
            ENDHLSL
        }
```

On URP versions without `ApplyShadowClamping`, clamp by hand: `positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE)` under `UNITY_REVERSED_Z`, else `max`.

## DepthOnly and DepthNormals

```hlsl
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                o.positionCS = TransformObjectToHClip(DisplaceOS(input.positionOS.xyz, input.color.r));
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half Frag(Varyings i) : SV_Target { ClipAlpha(i.uv); return i.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half3 normalWS : TEXCOORD1; };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                o.positionCS = TransformObjectToHClip(DisplaceOS(input.positionOS.xyz, input.color.r));
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            // Raw world normal, not n * 0.5 + 0.5.
            half4 Frag(Varyings i) : SV_Target { ClipAlpha(i.uv); return half4(NormalizeNormalPerPixel(i.normalWS), 0); }
            ENDHLSL
        }
    }
}
```

If the project uses Deferred or `_GBUFFER_NORMALS_OCT`, copy the octahedral packing branch from the installed `LitDepthNormalsPass.hlsl`.

## Two-sided foliage

`Cull Off` in every pass, and flip the normal for back faces in the fragment (`VFACE`/`SV_IsFrontFace`) in ForwardLit and DepthNormals. A shadow pass that culls differently from the forward pass casts the wrong silhouette.

## DOTS instanced properties (GPU Resident Drawer)

Per-instance overrides under the GPU Resident Drawer go through `UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)` / `UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)` and `UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT` inside `#ifdef UNITY_DOTS_INSTANCING_ENABLED`. Copy the exact pattern from the installed `LitInput.hlsl`.
