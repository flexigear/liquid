Shader "Hidden/FlipContainer"
{
    Properties
    {
        _Color ("Color", Color) = (0.7, 0.85, 1.0, 0.12)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "FlipContainer"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _Color;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 worldPos = TransformObjectToWorld(v.vertex.xyz);
                o.pos = TransformWorldToHClip(worldPos);
                o.worldNormal = TransformObjectToWorldNormal(v.normal);
                o.worldPos = worldPos;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 normal = normalize(i.worldNormal);
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.worldPos));
                float fresnel = 1.0 - saturate(dot(normal, viewDir));
                fresnel = fresnel * fresnel * fresnel;
                float alpha = _Color.a + fresnel * 0.4;
                return half4(_Color.rgb + fresnel * 0.3, alpha);
            }
            ENDHLSL
        }
    }
}
