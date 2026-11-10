// The guessing map. Counties that are switched off in the county picker are greyed out:
// _IdTex holds a county index per pixel (255 = not NC) and _SelTex has one pixel per county
// (white = in play). Unlit, one pass, Quest friendly.
Shader "GeoQuest/Map"
{
    Properties
    {
        _MainTex ("Map", 2D) = "white" {}
        [NoScaleOffset] _IdTex ("County ids (R = index, point sampled, linear)", 2D) = "white" {}
        [NoScaleOffset] _SelTex ("County selection (128x1)", 2D) = "white" {}
        _OffColor ("Tint of counties that are off", Color) = (0.62, 0.64, 0.68, 1)
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _MainTex, _IdTex, _SelTex;
            float4 _MainTex_ST;
            fixed4 _OffColor;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);   // zoom / pan applies to every layer
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                float id = floor(tex2Dlod(_IdTex, float4(i.uv, 0, 0)).r * 255.0 + 0.5);
                float on = tex2Dlod(_SelTex, float4((id + 0.5) / 128.0, 0.5, 0, 0)).r;
                // Outside North Carolina (id 255) stays as drawn.
                float keep = id > 127.5 ? 1.0 : step(0.5, on);
                col.rgb *= lerp(_OffColor.rgb, fixed3(1, 1, 1), keep);
                return col;
            }
            ENDCG
        }
    }
    Fallback "Unlit/Texture"
}
