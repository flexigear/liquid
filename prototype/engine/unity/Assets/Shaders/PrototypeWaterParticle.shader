Shader "Prototype/SPlisHSPlasHWaterParticle"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.78, 0.92, 1.0, 0.18)
        _ShallowColor("Shallow Color", Color) = (0.72, 0.93, 1.0, 1.0)
        _DeepColor("Deep Color", Color) = (0.07, 0.30, 0.44, 1.0)
        _SpecularColor("Specular Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _RefractionStrength("Refraction Strength", Range(0.0, 0.08)) = 0.018
        _TintStrength("Tint Strength", Range(0.0, 1.0)) = 0.28
        _FresnelPower("Fresnel Power", Range(0.1, 8.0)) = 4.2
        _BodyFalloff("Body Falloff", Range(0.5, 4.0)) = 1.55
        _HighlightStrength("Highlight Strength", Range(0.0, 3.0)) = 1.2
        _HighlightExponent("Highlight Exponent", Range(4.0, 128.0)) = 54.0
        _AlphaBoost("Alpha Boost", Range(0.0, 4.0)) = 1.35
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
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
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _SpecularColor;
                half _RefractionStrength;
                half _TintStrength;
                half _FresnelPower;
                half _BodyFalloff;
                half _HighlightStrength;
                half _HighlightExponent;
                half _AlphaBoost;
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
                float4 screenPos : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half4 color : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.screenPos = ComputeScreenPos(positionInputs.positionCS);
                output.uv = input.uv;
                output.color = input.color;
                output.positionWS = positionInputs.positionWS;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 sphereUV = input.uv * 2.0 - 1.0;
                float radiusSquared = dot(sphereUV, sphereUV);
                clip(1.0 - radiusSquared);

                float thickness = pow(saturate(1.0 - radiusSquared), _BodyFalloff);
                float3 normalVS = normalize(float3(sphereUV, sqrt(saturate(1.0 - radiusSquared))));
                float3 normalWS = normalize(mul((float3x3)UNITY_MATRIX_I_V, normalVS));
                float3 viewDirectionWS = normalize(_WorldSpaceCameraPos - input.positionWS);

                Light mainLight = GetMainLight();
                float3 lightDirectionWS = normalize(mainLight.direction);
                float3 halfDirectionWS = normalize(lightDirectionWS + viewDirectionWS);

                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirectionWS)), _FresnelPower);
                float specular = pow(saturate(dot(normalWS, halfDirectionWS)), _HighlightExponent) *
                    _HighlightStrength * (0.25 + 0.75 * fresnel);

                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float2 refractionOffset = normalVS.xy * _RefractionStrength * (0.35 + 0.65 * thickness);
                float3 sceneColor = SampleSceneColor(screenUV + refractionOffset).rgb;

                float3 waterTint = lerp(_ShallowColor.rgb, _DeepColor.rgb, saturate(1.0 - thickness));
                waterTint *= _BaseColor.rgb;

                float3 surfaceColor = lerp(sceneColor, waterTint, _TintStrength);
                surfaceColor += fresnel * _SpecularColor.rgb * 0.14;
                surfaceColor += specular * _SpecularColor.rgb * mainLight.color;
                surfaceColor = MixFog(surfaceColor, input.fogFactor);

                half alpha = saturate(
                    input.color.a * _BaseColor.a * (_AlphaBoost * thickness + 0.20 * fresnel));

                return half4(surfaceColor, alpha);
            }
            ENDHLSL
        }
    }
}
