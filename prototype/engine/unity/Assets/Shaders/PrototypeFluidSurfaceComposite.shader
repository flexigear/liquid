Shader "Prototype/SPlisHSPlasHFluidSurfaceComposite"
{
    Properties
    {
        _FluidSurfaceTex("Fluid Surface Texture", 2D) = "black" {}
        _ShallowColor("Shallow Color", Color) = (0.74, 0.94, 1.0, 1.0)
        _DeepColor("Deep Color", Color) = (0.08, 0.28, 0.42, 1.0)
        _SpecularColor("Specular Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _Threshold("Threshold", Range(0.0, 3.0)) = 0.22
        _Softness("Softness", Range(0.001, 1.0)) = 0.18
        _BlurScale("Blur Scale", Range(0.0, 4.0)) = 1.35
        _NormalStrength("Normal Strength", Range(0.0, 8.0)) = 4.2
        _RefractionStrength("Refraction Strength", Range(0.0, 0.08)) = 0.018
        _TintStrength("Tint Strength", Range(0.0, 1.0)) = 0.24
        _AbsorptionStrength("Absorption Strength", Range(0.0, 4.0)) = 0.7
        _BaseAlpha("Base Alpha", Range(0.0, 1.0)) = 0.28
        _FresnelPower("Fresnel Power", Range(0.1, 8.0)) = 4.0
        _HighlightStrength("Highlight Strength", Range(0.0, 4.0)) = 1.2
        _HighlightExponent("Highlight Exponent", Range(4.0, 128.0)) = 52.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent-10"
            "RenderType" = "Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest Always

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

            TEXTURE2D(_FluidSurfaceTex);
            SAMPLER(sampler_FluidSurfaceTex);
            float4 _FluidSurfaceTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _SpecularColor;
                half _Threshold;
                half _Softness;
                half _BlurScale;
                half _NormalStrength;
                half _RefractionStrength;
                half _TintStrength;
                half _AbsorptionStrength;
                half _BaseAlpha;
                half _FresnelPower;
                half _HighlightStrength;
                half _HighlightExponent;
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
                half fogFactor : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half SampleThickness(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_FluidSurfaceTex, sampler_FluidSurfaceTex, uv).r;
            }

            half SampleBlurredThickness(float2 uv, float2 texel)
            {
                half center = SampleThickness(uv) * 4.0;
                half cross =
                    SampleThickness(uv + float2(texel.x, 0.0)) +
                    SampleThickness(uv - float2(texel.x, 0.0)) +
                    SampleThickness(uv + float2(0.0, texel.y)) +
                    SampleThickness(uv - float2(0.0, texel.y));
                half diagonal =
                    SampleThickness(uv + texel) +
                    SampleThickness(uv + float2(texel.x, -texel.y)) +
                    SampleThickness(uv + float2(-texel.x, texel.y)) +
                    SampleThickness(uv - texel);

                return (center + cross * 2.0 + diagonal) / 16.0;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 texel = _FluidSurfaceTex_TexelSize.xy * _BlurScale;
                half thickness = SampleBlurredThickness(input.uv, texel);
                half mask = smoothstep(_Threshold, _Threshold + _Softness, thickness);
                clip(mask - 0.001);

                half right = SampleBlurredThickness(input.uv + float2(texel.x, 0.0), texel);
                half left = SampleBlurredThickness(input.uv - float2(texel.x, 0.0), texel);
                half up = SampleBlurredThickness(input.uv + float2(0.0, texel.y), texel);
                half down = SampleBlurredThickness(input.uv - float2(0.0, texel.y), texel);

                float2 gradient = float2(right - left, up - down);
                float3 normalVS = normalize(float3(-gradient.x * _NormalStrength, -gradient.y * _NormalStrength, 1.0));
                float3 normalWS = normalize(mul((float3x3)UNITY_MATRIX_I_V, normalVS));
                float3 viewDirectionWS = normalize(mul((float3x3)UNITY_MATRIX_I_V, float3(0.0, 0.0, 1.0)));

                Light mainLight = GetMainLight();
                float3 lightDirectionWS = normalize(mainLight.direction);
                float3 halfDirectionWS = normalize(lightDirectionWS + viewDirectionWS);

                half absorption = saturate(thickness * _AbsorptionStrength);
                half fresnel = pow(1.0 - saturate(dot(normalWS, viewDirectionWS)), _FresnelPower);
                half specular = pow(saturate(dot(normalWS, halfDirectionWS)), _HighlightExponent) *
                    _HighlightStrength * (0.2 + 0.8 * fresnel);

                float2 refractionOffset = normalVS.xy * _RefractionStrength * (0.35 + absorption);
                float3 sceneColor = SampleSceneColor(input.uv + refractionOffset).rgb;
                float3 waterTint = lerp(_ShallowColor.rgb, _DeepColor.rgb, absorption);

                float3 surfaceColor = lerp(sceneColor, waterTint, saturate(_TintStrength + absorption * 0.25));
                surfaceColor += fresnel * _SpecularColor.rgb * 0.18;
                surfaceColor += specular * _SpecularColor.rgb * mainLight.color;
                surfaceColor = MixFog(surfaceColor, input.fogFactor);

                half alpha = saturate(mask * (_BaseAlpha + absorption * 0.55));
                return half4(surfaceColor, alpha);
            }
            ENDHLSL
        }
    }
}
