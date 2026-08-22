Shader "KingdomTD/Flipbook/World"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _EffectTex ("Effect Texture", 2D) = "white" {}
        _Columns ("Columns", Float) = 1
        _Rows ("Rows", Float) = 1
        _CurrentFrame ("Current Frame", Float) = 0
        [HDR] _ChangeColor ("Change Color", Color) = (1, 1, 1, 1)
        _ChangeRate ("Change Rate", Range(0, 1)) = 0
        _EffectChangeRate ("Effect Rate", Range(0, 1)) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Geometry+2"
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "Flipbook"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            TEXTURE2D(_EffectTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _Cutoff;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(FlipbookPerInstance)
                UNITY_DEFINE_INSTANCED_PROP(float, _CurrentFrame)
                UNITY_DEFINE_INSTANCED_PROP(float, _Columns)
                UNITY_DEFINE_INSTANCED_PROP(float, _Rows)
                UNITY_DEFINE_INSTANCED_PROP(half4, _ChangeColor)
                UNITY_DEFINE_INSTANCED_PROP(half, _ChangeRate)
                UNITY_DEFINE_INSTANCED_PROP(half, _EffectChangeRate)
            UNITY_INSTANCING_BUFFER_END(FlipbookPerInstance)

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 changeColor : COLOR0;
                half2 rates : TEXCOORD1;
            };

            float2 GetFrameUV(float2 uv, float frame, float columns, float rows)
            {
                float row = floor(frame / columns);
                float column = frame - row * columns;
                return (uv + float2(column, rows - 1.0 - row)) / float2(columns, rows);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                float currentFrame = UNITY_ACCESS_INSTANCED_PROP(FlipbookPerInstance, _CurrentFrame);
                float columns = UNITY_ACCESS_INSTANCED_PROP(FlipbookPerInstance, _Columns);
                float rows = UNITY_ACCESS_INSTANCED_PROP(FlipbookPerInstance, _Rows);

                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = GetFrameUV(TRANSFORM_TEX(input.uv, _MainTex), currentFrame, columns, rows);
                output.changeColor = UNITY_ACCESS_INSTANCED_PROP(FlipbookPerInstance, _ChangeColor);
                output.rates.x = UNITY_ACCESS_INSTANCED_PROP(FlipbookPerInstance, _ChangeRate);
                output.rates.y = UNITY_ACCESS_INSTANCED_PROP(FlipbookPerInstance, _EffectChangeRate);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                if (input.rates.y > 0.0h)
                {
                    half4 effectColor = SAMPLE_TEXTURE2D(_EffectTex, sampler_MainTex, input.uv);
                    color = lerp(color, effectColor, input.rates.y);
                }

                color.rgb = lerp(color.rgb, input.changeColor.rgb, input.rates.x);
                clip(color.a - _Cutoff);
                return color;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
