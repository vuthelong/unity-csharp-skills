Shader "ParticleVfx/Atlas AB Add"
{
    Properties
    {
        _Atlas ("Particle Atlas", 2D) = "white" {}
        [HideInInspector] _ClampSource ("LDR Source Clamp", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            Name "AtlasABAdd"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local __ _ATLAS_NATIVE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_Atlas);
            CBUFFER_START(UnityPerMaterial)
                float _ClampSource;
            CBUFFER_END

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float4 uvFlags : TEXCOORD0;
                float4 mainRect : TEXCOORD1;
                float4 tint : TEXCOORD2;
            };

            #if defined(_ATLAS_NATIVE)
            #define COLUMN_MAIN_RECT 0
            #define COLUMN_MAIN_ST 1
            #define COLUMN_FLAGS 2
            #define COLUMN_TINT 3
            #define COLUMN_PROXY 4
            TEXTURE2D(_ParticleAtlasTable);
            float4 SourceData(int row, int column) { return LOAD_TEXTURE2D_LOD(_ParticleAtlasTable, int2(column, row), 0); }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float3 uvRow : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                int row = (int)round(input.uvRow.z);
                float4 proxy = SourceData(row, COLUMN_PROXY);
                float2 uv = (input.uvRow.xy - proxy.xy) / proxy.zw;
                float4 mainST = SourceData(row, COLUMN_MAIN_ST);
                float4 flags = SourceData(row, COLUMN_FLAGS);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.color = input.color;
                o.uvFlags = float4(uv * mainST.xy + mainST.zw, flags.x, flags.y);
                o.mainRect = SourceData(row, COLUMN_MAIN_RECT);
                o.tint = SourceData(row, COLUMN_TINT);
                return o;
            }
            #else
            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float4 uvFlags : TEXCOORD0;
                float4 mainRect : TEXCOORD1;
                float4 tint : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.color = input.color;
                o.uvFlags = input.uvFlags;
                o.mainRect = input.mainRect;
                o.tint = input.tint;
                return o;
            }
            #endif

            float WrapUV(float value, float mode)
            {
                if (mode < 0.5) return frac(value);
                if (mode < 1.5) return saturate(value);
                if (mode < 2.5) return 1 - abs(frac(value * 0.5) * 2 - 1);
                return saturate(abs(value));
            }

            float4 SampleRegion(float2 uv, float4 rect, float flags)
            {
                flags = round(flags);
                float pointFilter = fmod(flags, 2);
                float wrapU = fmod(floor(flags / 2), 4);
                float wrapV = fmod(floor(flags / 8), 4);
                uv = float2(WrapUV(uv.x, wrapU), WrapUV(uv.y, wrapV));
                float2 atlasUV = rect.xy + uv * rect.zw;
                if (pointFilter > 0.5) return SAMPLE_TEXTURE2D_LOD(_Atlas, sampler_PointClamp, atlasUV, 0);
                return SAMPLE_TEXTURE2D_LOD(_Atlas, sampler_LinearClamp, atlasUV, 0);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float4 tex = SampleRegion(input.uvFlags.xy, input.mainRect, input.uvFlags.w);
                float blend = round(input.uvFlags.z);
                float4 source = input.color * input.tint * tex;
                if (blend > 1.5 && blend < 2.5) source.a = saturate(source.a);
                source = lerp(source, saturate(source), _ClampSource);
                return float4(source.rgb * source.a, blend < 1.5 ? 0 : source.a);
            }
            ENDHLSL
        }
    }
}
