Shader "Prototype/Test1ObservationGlass"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.86, 0.94, 1.0, 1.0)
        _EdgeColor("Edge Color", Color) = (0.98, 0.995, 1.0, 1.0)
        _BaseAlpha("Base Alpha", Range(0.0, 1.0)) = 0.0025
        _EdgeAlpha("Edge Alpha", Range(0.0, 1.0)) = 0.075
        _FresnelPower("Fresnel Power", Range(0.1, 8.0)) = 4.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Back
        ZWrite Off

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EdgeColor;
                half _BaseAlpha;
                half _EdgeAlpha;
                half _FresnelPower;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(_WorldSpaceCameraPos.xyz - input.positionWS);
                half fresnel = pow(saturate(1.0h - dot(normalWS, viewDirWS)), _FresnelPower);
                half3 color = lerp(_BaseColor.rgb, _EdgeColor.rgb, fresnel);
                half alpha = lerp(_BaseAlpha, _EdgeAlpha, fresnel);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
