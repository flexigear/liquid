Shader "Hidden/FlipParticleThickness"
{
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Blend One One
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            StructuredBuffer<float> _ParticleBuf;
            float _ParticleRadius;
            float4x4 _LocalToWorld;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(uint vertexID : SV_VertexID)
            {
                uint particleIndex = vertexID / 6u;
                uint cornerIndex = vertexID % 6u;

                static const float2 corners[6] = {
                    float2(-1, -1), float2(1, -1), float2(-1, 1),
                    float2(-1, 1), float2(1, -1), float2(1, 1)
                };

                uint base = particleIndex * 6u;
                float3 localPos = float3(
                    _ParticleBuf[base + 0u],
                    _ParticleBuf[base + 1u],
                    _ParticleBuf[base + 2u]);

                float4 worldPos = mul(_LocalToWorld, float4(localPos, 1.0));
                float4 viewPos = mul(UNITY_MATRIX_V, worldPos);
                viewPos.xy += corners[cornerIndex] * _ParticleRadius;

                v2f o;
                o.pos = mul(UNITY_MATRIX_P, viewPos);
                o.uv = corners[cornerIndex];
                return o;
            }

            float frag(v2f i) : SV_Target
            {
                float dist = length(i.uv);
                if (dist > 1.0) discard;

                // Normalized sphere thickness: sphereZ / radius
                float thickness = sqrt(1.0 - dist * dist);
                return thickness * 0.5; // scale factor, tunable
            }
            ENDHLSL
        }
    }
}
