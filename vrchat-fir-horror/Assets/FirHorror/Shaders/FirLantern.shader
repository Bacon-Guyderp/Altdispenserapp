// Fir Warning's one shader. There are no realtime lights anywhere in the world (Quest can't afford
// them), so each player's own camera acts as a lantern: surfaces are lit by how close they are to
// the viewer and how near the centre of their view they are. Every player sees their own
// lantern, for free, in single-pass stereo.
//
// Globals, set from Udon with VRCShader.SetGlobalFloat (names must start with _Udon):
//   _UdonFirLight    lantern reach in metres (0 = default 7)
//   _UdonFirDark     0 = normal, 1 = pitch black (blackouts and flicker)
//   _UdonFirAmbient  extra ambient light added on top of the base 0.006 (beyond your lantern it is nearly black)
//   _UdonFirAmbR/G/B the room's coloured ambient light (each room has its own mood)
//   _UdonFirLanR/G/B the lantern's colour (0,0,0 = white)
// Per material:
//   _Fullbright      1 = ignore the lantern (lobby, signs), 0 = lantern lit
//   _Glow            colour added after lighting (eyes, furnace, exit signs); ignores blackouts
//   _VertexColor     1 = multiply by the mesh's vertex colours (the tree canopies)
Shader "FirHorror/Lantern"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Fullbright ("Fullbright", Range(0,1)) = 0
        _Glow ("Glow", Color) = (0,0,0,0)
        _VertexColor ("Use vertex colours", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float4 _Glow;
            float _Fullbright;
            float _VertexColor;
            // globals set from Udon
            float _UdonFirLight;
            float _UdonFirDark;
            float _UdonFirAmbient;
            float _UdonFirAmbR, _UdonFirAmbG, _UdonFirAmbB;
            float _UdonFirLanR, _UdonFirLanG, _UdonFirLanB;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 wpos : TEXCOORD1;
                float3 wn : TEXCOORD2;
                float4 color : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.color = v.color;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                // needed in VR (single-pass stereo) so the camera position/direction below are this eye's
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float4 albedo = tex2D(_MainTex, i.uv) * _Color * lerp(float4(1.0, 1.0, 1.0, 1.0), i.color, _VertexColor);
                float3 toSurf = i.wpos - _WorldSpaceCameraPos.xyz;
                float d = max(length(toSurf), 0.0001);
                float3 dir = toSurf / d;
                float reach = lerp(6.0, _UdonFirLight, step(0.0001, _UdonFirLight));
                float fall = saturate(1.0 - d / reach);
                fall = fall * fall;
                float3 camFwd = -normalize(UNITY_MATRIX_V[2].xyz);
                float cone = smoothstep(0.55, 0.92, dot(dir, camFwd));
                float facing = saturate(dot(normalize(i.wn), -dir)) * 0.6 + 0.4;

                float3 lanCol = float3(_UdonFirLanR, _UdonFirLanG, _UdonFirLanB);
                float hasLan = step(0.0001, lanCol.r + lanCol.g + lanCol.b);
                lanCol = lerp(float3(1.0, 1.0, 1.0), lanCol, hasLan);
                float3 ambCol = float3(_UdonFirAmbR, _UdonFirAmbG, _UdonFirAmbB);

                float3 light = float3(1.0, 1.0, 1.0) * (0.006 + _UdonFirAmbient)
                             + ambCol * (0.6 + 0.4 * facing)
                             + lanCol * (fall * facing * (0.3 + 0.9 * cone));
                light = light * (1.0 - saturate(_UdonFirDark));

                float3 lit = albedo.rgb * light;
                float3 col = lerp(lit, albedo.rgb, _Fullbright) + _Glow.rgb;
                return float4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback "Unlit/Texture"
}
