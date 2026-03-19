Shader "Prototype/SPlisHSPlasHFluidMaskParticle"
{
    Properties
    {
        _MaskStrength("Mask Strength", Range(0.0, 2.0)) = 1.0
        _BodyFalloff("Body Falloff", Range(0.5, 4.0)) = 1.4
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Blend One One
        Cull Off
        ZWrite Off
        ZTest LEqual

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
                half _MaskStrength;
                half _BodyFalloff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 sphereUV = input.uv * 2.0 - 1.0;
                float radiusSquared = dot(sphereUV, sphereUV);
                clip(1.0 - radiusSquared);

                half thickness = pow(saturate(1.0 - radiusSquared), _BodyFalloff) *
                    _MaskStrength * input.color.a;

                return half4(thickness, thickness, thickness, thickness);
            }
            ENDHLSL
        }
    }
}
