Shader "Hidden/FlipFluidComposite"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_FluidSmoothedDepthTex);
            SAMPLER(sampler_FluidSmoothedDepthTex);
            float4 _FluidSmoothedDepthTex_TexelSize;

            TEXTURE2D(_FluidThicknessTex);
            SAMPLER(sampler_FluidThicknessTex);

            // Tunable parameters (hardcoded for M5, expose in M6)
            static const float3 SHALLOW_COLOR = float3(0.4, 0.75, 0.9);
            static const float3 DEEP_COLOR = float3(0.05, 0.2, 0.4);
            static const float REFRACTION_STRENGTH = 0.025;
            static const float ABSORPTION_STRENGTH = 2.0;
            static const float FRESNEL_POWER = 4.0;
            static const float SPECULAR_STRENGTH = 1.5;
            static const float SPECULAR_EXPONENT = 64.0;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(uint vertexID : SV_VertexID)
            {
                v2f o;
                o.uv = float2((vertexID << 1) & 2, vertexID & 2);
                o.pos = float4(o.uv * 2.0 - 1.0, 0.5, 1.0);
                #if UNITY_UV_STARTS_AT_TOP
                    o.uv.y = 1.0 - o.uv.y;
                #endif
                return o;
            }

            float3 ReconstructViewPos(float2 uv, float eyeDepth)
            {
                // From screen UV + eye depth to view-space position
                float2 ndc = uv * 2.0 - 1.0;
                float3 viewPos;
                viewPos.x = ndc.x / UNITY_MATRIX_P[0][0] * eyeDepth;
                viewPos.y = ndc.y / UNITY_MATRIX_P[1][1] * eyeDepth;
                viewPos.z = -eyeDepth;
                return viewPos;
            }

            half4 frag(v2f i) : SV_Target
            {
                float centerDepth = SAMPLE_TEXTURE2D(_FluidSmoothedDepthTex, sampler_FluidSmoothedDepthTex, i.uv).r;
                if (centerDepth <= 0.0) return half4(0, 0, 0, 0); // no fluid

                float thickness = SAMPLE_TEXTURE2D(_FluidThicknessTex, sampler_FluidThicknessTex, i.uv).r;

                // Reconstruct normal from depth via central differences
                float2 texel = _FluidSmoothedDepthTex_TexelSize.xy;
                float depthR = SAMPLE_TEXTURE2D(_FluidSmoothedDepthTex, sampler_FluidSmoothedDepthTex, i.uv + float2(texel.x, 0)).r;
                float depthL = SAMPLE_TEXTURE2D(_FluidSmoothedDepthTex, sampler_FluidSmoothedDepthTex, i.uv - float2(texel.x, 0)).r;
                float depthU = SAMPLE_TEXTURE2D(_FluidSmoothedDepthTex, sampler_FluidSmoothedDepthTex, i.uv + float2(0, texel.y)).r;
                float depthD = SAMPLE_TEXTURE2D(_FluidSmoothedDepthTex, sampler_FluidSmoothedDepthTex, i.uv - float2(0, texel.y)).r;

                float3 posCenter = ReconstructViewPos(i.uv, centerDepth);
                float3 posR = ReconstructViewPos(i.uv + float2(texel.x, 0), depthR > 0 ? depthR : centerDepth);
                float3 posL = ReconstructViewPos(i.uv - float2(texel.x, 0), depthL > 0 ? depthL : centerDepth);
                float3 posU = ReconstructViewPos(i.uv + float2(0, texel.y), depthU > 0 ? depthU : centerDepth);
                float3 posD = ReconstructViewPos(i.uv - float2(0, texel.y), depthD > 0 ? depthD : centerDepth);

                float3 ddx = posR - posL;
                float3 ddy = posU - posD;
                float3 normalVS = normalize(cross(ddy, ddx));

                // Convert to world space
                float3 normalWS = normalize(mul((float3x3)UNITY_MATRIX_I_V, normalVS));
                float3 viewDirWS = normalize(mul((float3x3)UNITY_MATRIX_I_V, normalize(-posCenter)));

                // Lighting
                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction);
                float3 halfDir = normalize(lightDir + viewDirWS);

                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), FRESNEL_POWER);
                float specular = pow(saturate(dot(normalWS, halfDir)), SPECULAR_EXPONENT) * SPECULAR_STRENGTH;

                // Refraction
                float2 refractionOffset = normalVS.xy * REFRACTION_STRENGTH;
                float3 sceneColor = SampleSceneColor(i.uv + refractionOffset).rgb;

                // Absorption (Beer-Lambert)
                float absorption = saturate(thickness * ABSORPTION_STRENGTH);
                float3 waterColor = lerp(SHALLOW_COLOR, DEEP_COLOR, absorption);
                float transmission = exp(-absorption * 1.5);

                // Composite
                float3 color = lerp(waterColor, sceneColor * transmission, 1.0 - absorption * 0.6);
                color += fresnel * float3(0.8, 0.9, 1.0) * 0.15;
                color += specular * mainLight.color;

                float alpha = saturate(0.7 + absorption * 0.25 + fresnel * 0.1);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
