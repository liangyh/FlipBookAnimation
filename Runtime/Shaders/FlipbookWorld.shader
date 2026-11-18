Shader "KingdomTD/Flipbook/World"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _EffectTex ("Effect Texture", 2D) = "white" {}
        _Columns ("Columns", Float) = 1
        _Rows ("Rows", Float) = 1
        _CurrentFrame ("Current Frame", Float) = 0
        [HideInInspector] _FlipX ("Flip X", Float) = 0
        [HideInInspector] _FlipbookGroundFoot ("Ground Shadow Foot", Float) = 0
        [HideInInspector] _FlipbookGroundProjection ("Ground Shadow Projection", Vector) = (0.001, 0.7, 0.70710678, 0.70710678)
        [HideInInspector] _FlipbookGroundColor ("Ground Shadow Color", Color) = (0.18, 0.17, 0.2, 1)
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
            // Dynamic batching would bake away the object-space foot and scale.
            "DisableBatching" = "True"
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
                UNITY_DEFINE_INSTANCED_PROP(float, _FlipX)
                UNITY_DEFINE_INSTANCED_PROP(float, _FlipbookGroundFoot)
                UNITY_DEFINE_INSTANCED_PROP(float4, _FlipbookGroundProjection)
                UNITY_DEFINE_INSTANCED_PROP(half4, _FlipbookGroundColor)
                UNITY_DEFINE_INSTANCED_PROP(half4, _ChangeColor)
                UNITY_DEFINE_INSTANCED_PROP(half, _ChangeRate)
                UNITY_DEFINE_INSTANCED_PROP(half, _EffectChangeRate)
            UNITY_INSTANCING_BUFFER_END(FlipbookPerInstance)

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 uv : TEXCOORD0;
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

            float3 GroundShadowPosition(float2 corner, float footHeight, float4 projection)
            {
                float3 foot = TransformObjectToWorld(float3(0.0, footHeight - 0.5, 0.0));
                foot.y = projection.x;
                float3 screenAxis = UNITY_MATRIX_V[0].xyz * projection.z + UNITY_MATRIX_V[1].xyz * projection.w;
                float3 ray = IsPerspectiveProjection() ? foot - GetCameraPositionWS() : GetViewForwardDir();
                // Intersect the view plane through the foot with the ground to get the requested screen direction.
                float2 axis = ray.xz * screenAxis.y - screenAxis.xz * ray.y;
                float orientation = ray.y > 0.0 ? -1.0 : 1.0;
                float2 direction = axis * (orientation * rsqrt(max(dot(axis, axis), 1e-12)));
                // Rotate the width with the footprint so a horizontal heading still has area.
                float2 right = float2(direction.y, -direction.x) * orientation;

                float3 localX = TransformObjectToWorldDir(float3(1, 0, 0), false);
                float3 localY = TransformObjectToWorldDir(float3(0, 1, 0), false);
                float width = length(localX) * (dot(localX, UNITY_MATRIX_V[0].xyz) < 0.0 ? -1.0 : 1.0);
                float height = length(localY) * (dot(localY, UNITY_MATRIX_V[1].xyz) < 0.0 ? -1.0 : 1.0);
                foot.xz += right * ((corner.x - 0.5) * width);
                foot.xz += direction * ((corner.y - footHeight) * height * projection.y);
                return foot;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                float currentFrame = UNITY_ACCESS_INSTANCED_PROP(FlipbookPerInstance, _CurrentFrame);
                float columns = UNITY_ACCESS_INSTANCED_PROP(FlipbookPerInstance, _Columns);
                float rows = UNITY_ACCESS_INSTANCED_PROP(FlipbookPerInstance, _Rows);
                float flipX = UNITY_ACCESS_INSTANCED_PROP(FlipbookPerInstance, _FlipX);

                float2 uv = input.uv.xy;
                uv.x = lerp(uv.x, 1.0 - uv.x, step(0.5, flipX));

                output.uv = GetFrameUV(TRANSFORM_TEX(uv, _MainTex), currentFrame, columns, rows);
                if (input.uv.z > 0.5)
                {
                    float footHeight = UNITY_ACCESS_INSTANCED_PROP(FlipbookPerInstance, _FlipbookGroundFoot);
                    float4 projection = UNITY_ACCESS_INSTANCED_PROP(FlipbookPerInstance, _FlipbookGroundProjection);
                    half3 shadowColor = UNITY_ACCESS_INSTANCED_PROP(FlipbookPerInstance, _FlipbookGroundColor).rgb;
                    output.positionCS = TransformWorldToHClip(GroundShadowPosition(input.uv.xy, footHeight, projection));
                    output.changeColor = half4(shadowColor, 1.0h);
                    output.rates = half2(1.0h, 0.0h);
                }
                else
                {
                    output.positionCS = TransformObjectToHClip(input.positionOS);
                    output.changeColor = UNITY_ACCESS_INSTANCED_PROP(FlipbookPerInstance, _ChangeColor);
                    output.changeColor.a = 0.0h;
                    output.rates.x = UNITY_ACCESS_INSTANCED_PROP(FlipbookPerInstance, _ChangeRate);
                    output.rates.y = UNITY_ACCESS_INSTANCED_PROP(FlipbookPerInstance, _EffectChangeRate);
                }
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
                color.a = max(color.a, input.changeColor.a);
                return color;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
