// Christmas light bulbs: always glowing, twinkling on their own (no Udon needed), dimmed but
// still visible during blackouts so the dark keeps a little colour. Quest friendly.
Shader "FirHorror/Bulb"
{
    Properties
    {
        _Color ("Color", Color) = (1,0,0,1)
        _Speed ("Twinkle speed", Float) = 2.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            fixed4 _Color;
            float _Speed, _UdonFirDark;
            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float phase : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                // bulbs sit in the middle of 0.5 m cells, so the cell gives each bulb its own phase
                // (works after static batching, which merges the bulbs into one mesh)
                float3 wv = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.phase = dot(floor(wv.xz * 2.0), float2(1.7, 2.3)) * 2.1;
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float tw = 0.55 + 0.45 * sin(_Time.y * _Speed + i.phase);
                float dark = lerp(1.0, 0.3, saturate(_UdonFirDark));
                return fixed4(_Color.rgb * (0.35 + 0.65 * tw) * dark, 1);
            }
            ENDCG
        }
    }
}
