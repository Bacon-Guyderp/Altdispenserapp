// Draws an equirectangular 360x180 photo around the viewer like a skybox.
// Cheap enough for Quest: one texture fetch, no lighting, single-pass-instanced/multiview safe.
Shader "GeoQuest/Panorama"
{
    Properties
    {
        _MainTex ("Panorama (equirectangular)", 2D) = "gray" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _Yaw ("Yaw offset (0-1)", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Front
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Tint;
            float _Yaw;

            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 d = normalize(i.worldPos - _WorldSpaceCameraPos);
                float u = atan2(d.x, d.z) * (0.5 / UNITY_PI) + 0.5 + _Yaw;
                float v = asin(clamp(d.y, -1, 1)) / UNITY_PI + 0.5;
                // tex2Dlod: no mip derivatives, so no seam where u wraps.
                return tex2Dlod(_MainTex, float4(frac(u), v, 0, 0)) * _Tint;
            }
            ENDCG
        }
    }
    Fallback Off
}
