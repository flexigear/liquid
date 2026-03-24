Shader "Hidden/FlipParticleDepth"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            ZWrite On
            ZTest LEqual
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
                float viewZ : TEXCOORD1;
            };

            v2f vert(uint vertexID : SV_VertexID)
            {
                uint particleIndex = vertexID / 6u;
                uint cornerIndex = vertexID % 6u;

                static const float2 corners[6] = {
                    float2(-1, -1), float2(1, -1), float2(-1, 1),
                    float2(-1, 1), float2(1, -1), float2(1, 1)
                };

                float2 corner = corners[cornerIndex];
                uint base = particleIndex * 6u;
                float3 localPos = float3(
                    _ParticleBuf[base + 0u],
                    _ParticleBuf[base + 1u],
                    _ParticleBuf[base + 2u]);

                float4 worldPos = mul(_LocalToWorld, float4(localPos, 1.0));
                float4 viewPos = mul(UNITY_MATRIX_V, worldPos);

                v2f o;
                o.viewZ = -viewPos.z; // positive eye-space depth
                viewPos.xy += corner * _ParticleRadius;
                o.pos = mul(UNITY_MATRIX_P, viewPos);
                o.uv = corner;
                return o;
            }

            struct DepthOutput
            {
                float color : SV_Target;
                float depth : SV_Depth;
            };

            DepthOutput frag(v2f i)
            {
                float dist = length(i.uv);
                if (dist > 1.0) discard;

                // Sphere imposter: push depth forward by sphere z
                float sphereZ = sqrt(1.0 - dist * dist);
                float eyeDepth = i.viewZ - sphereZ * _ParticleRadius;

                // Convert eye depth back to clip depth for SV_Depth
                float4 clipPos = mul(UNITY_MATRIX_P, float4(0, 0, -eyeDepth, 1));

                DepthOutput o;
                o.color = eyeDepth;
                o.depth = clipPos.z / clipPos.w;
                return o;
            }
            ENDHLSL
        }
    }
}
