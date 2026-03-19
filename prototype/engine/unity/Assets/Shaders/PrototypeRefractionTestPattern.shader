Shader "Prototype/RefractionTestPattern"
{
    Properties
    {
        _BackgroundColor("Background Color", Color) = (0.98,0.98,0.98,1)
        _FaceColor("Face Color", Color) = (1.0,0.87,0.18,1)
        _FeatureColor("Feature Color", Color) = (0.08,0.08,0.08,1)
        _AccentColor("Accent Color", Color) = (1.0,0.62,0.0,1)
        _FaceScale("Face Scale", Range(0.3,0.95)) = 0.78
        _OutlineThickness("Outline Thickness", Range(0.005,0.08)) = 0.028
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BackgroundColor;
                half4 _FaceColor;
                half4 _FeatureColor;
                half4 _AccentColor;
                half _FaceScale;
                half _OutlineThickness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 centeredUv = input.uv * 2.0 - 1.0;
                centeredUv.y *= -1.0;

                float faceRadius = _FaceScale;
                float faceDistance = length(centeredUv);
                float faceMask = 1.0 - smoothstep(faceRadius, faceRadius + 0.015, faceDistance);
                float outlineMask = smoothstep(faceRadius - _OutlineThickness, faceRadius - _OutlineThickness * 0.25, faceDistance) * faceMask;

                float2 leftEyeUv = centeredUv - float2(-0.28, 0.2);
                float2 rightEyeUv = centeredUv - float2(0.28, 0.2);
                float leftEye = 1.0 - smoothstep(0.12, 0.14, length(leftEyeUv * float2(0.9, 1.05)));
                float rightEye = 1.0 - smoothstep(0.12, 0.14, length(rightEyeUv * float2(0.9, 1.05)));

                float2 smileUv = centeredUv - float2(0.0, -0.05);
                float smileRadius = abs(length(smileUv * float2(0.95, 0.8)) - 0.48);
                float smileBand = 1.0 - smoothstep(0.02, 0.045, smileRadius);
                float smileCut = smoothstep(-0.08, 0.02, smileUv.y);
                float smile = smileBand * smileCut * smoothstep(-0.56, -0.08, smileUv.y);

                float2 leftHookUv = smileUv - float2(-0.50, -0.03);
                float2 rightHookUv = smileUv - float2(0.50, -0.03);
                float leftHookLine = abs(dot(leftHookUv, normalize(float2(0.68, 0.74))));
                float rightHookLine = abs(dot(rightHookUv, normalize(float2(-0.68, 0.74))));
                float leftHookSide = dot(leftHookUv, normalize(float2(-0.74, 0.68)));
                float rightHookSide = dot(rightHookUv, normalize(float2(0.74, 0.68)));
                float leftHook = (1.0 - smoothstep(0.016, 0.032, leftHookLine)) * smoothstep(-0.18, 0.06, leftHookSide) * smoothstep(0.06, -0.16, leftHookSide);
                float rightHook = (1.0 - smoothstep(0.016, 0.032, rightHookLine)) * smoothstep(-0.18, 0.06, rightHookSide) * smoothstep(0.06, -0.16, rightHookSide);

                float accentRing = smoothstep(faceRadius - 0.14, faceRadius - 0.04, faceDistance) * faceMask;

                half3 color = _BackgroundColor.rgb;
                color = lerp(color, _FaceColor.rgb, faceMask);
                color = lerp(color, _AccentColor.rgb, accentRing * 0.45);
                float featureMask = saturate(max(max(max(leftEye, rightEye), smile), max(leftHook, rightHook)));
                color = lerp(color, _FeatureColor.rgb, featureMask);
                color = lerp(color, _FeatureColor.rgb, outlineMask * 0.85);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
