// Additive hologram / energy glow with a bright rim. Used for the dial, flashes and powers.
Shader "XenoDial/Glow"
{
    Properties { _Color ("Color", Color) = (0.2,1,0.9,0.8) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; half rim : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                half3 n = normalize(UnityObjectToWorldNormal(v.normal));
                half3 vd = normalize(_WorldSpaceCameraPos - wp);
                o.rim = 1.0 - saturate(dot(n, vd));
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                return fixed4(_Color.rgb, _Color.a * (0.35 + 0.65 * i.rim * i.rim));
            }
            ENDCG
        }
    }
}
