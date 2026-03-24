Shader "Hidden/FlipSmoothDepth"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_FluidDepthTex);
            SAMPLER(sampler_FluidDepthTex);
            float4 _FluidDepthTex_TexelSize;
            float2 _BlurDir;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(uint vertexID : SV_VertexID)
            {
                // Full-screen triangle
                v2f o;
                o.uv = float2((vertexID << 1) & 2, vertexID & 2);
                o.pos = float4(o.uv * 2.0 - 1.0, 0.5, 1.0);
                #if UNITY_UV_STARTS_AT_TOP
                    o.uv.y = 1.0 - o.uv.y;
                #endif
                return o;
            }

            float frag(v2f i) : SV_Target
            {
                float centerDepth = SAMPLE_TEXTURE2D(_FluidDepthTex, sampler_FluidDepthTex, i.uv).r;
                if (centerDepth <= 0.0) return 0.0; // no particle here

                // Narrow-range bilateral filter
                static const int FILTER_RADIUS = 7;
                static const float SIGMA_SPATIAL = 4.0;
                static const float SIGMA_DEPTH = 0.1; // depth range threshold

                float2 texelOffset = _FluidDepthTex_TexelSize.xy * _BlurDir;

                float weightSum = 0.0;
                float depthSum = 0.0;

                for (int t = -FILTER_RADIUS; t <= FILTER_RADIUS; t++)
                {
                    float2 sampleUv = i.uv + texelOffset * (float)t;
                    float sampleDepth = SAMPLE_TEXTURE2D(_FluidDepthTex, sampler_FluidDepthTex, sampleUv).r;

                    if (sampleDepth <= 0.0) continue;

                    float spatialWeight = exp(-0.5 * (float)(t * t) / (SIGMA_SPATIAL * SIGMA_SPATIAL));
                    float depthDiff = (sampleDepth - centerDepth) / SIGMA_DEPTH;
                    float rangeWeight = exp(-0.5 * depthDiff * depthDiff);

                    float w = spatialWeight * rangeWeight;
                    depthSum += sampleDepth * w;
                    weightSum += w;
                }

                return (weightSum > 0.0) ? depthSum / weightSum : centerDepth;
            }
            ENDHLSL
        }
    }
}
