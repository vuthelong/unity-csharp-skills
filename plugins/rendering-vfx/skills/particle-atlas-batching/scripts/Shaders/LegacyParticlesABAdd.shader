Shader "ParticleVfx/Legacy AB Add"
{
    Properties
    {
        _TintAB ("Alpha Blend Tint", Color) = (0.5019608,0.5019608,0.5019608,0.5019608)
        _TintAdd ("Additive Tint", Color) = (1,1,1,0.6862745)
        _MainTex ("Particle Texture", 2D) = "white" {}
        [Enum(HDR,0,LDR,1)] _ClampSource ("Render Target", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "RenderPipeline"="UniversalPipeline" }
        Blend One OneMinusSrcAlpha
        Cull Off Lighting Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float3 uvAndMode : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float3 uvAndMode : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _TintAB;
                half4 _TintAdd;
                half _ClampSource;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uvAndMode.xy = TRANSFORM_TEX(input.uvAndMode.xy, _MainTex);
                output.uvAndMode.z = input.uvAndMode.z;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half mode = saturate(input.uvAndMode.z);
                half4 tint = lerp(_TintAdd, _TintAB, mode);
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uvAndMode.xy);
                half4 source = 2.0h * input.color * tint * texColor;
                source = lerp(source, saturate(source), _ClampSource);
                return half4(source.rgb * source.a, source.a * mode);
            }
            ENDHLSL
        }
    }
}
