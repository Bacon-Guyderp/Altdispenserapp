// Fir Warning's one shader. There are no realtime lights anywhere in the world (Quest can't afford
// them), so each player's own camera acts as a lantern: surfaces are lit by how close they are to
// the viewer and how near the centre of their view they are. Every player sees their own
// lantern, for free, in single-pass stereo.
//
// No globals: everything is a plain material property. The game manager sets _Reach, _Dark and
// _Ambient on every world material (Material.SetFloat) when they change; each room has its own
// material set, so its colours live on the material:
//   _Reach           lantern reach in metres
//   _Dark            0 = normal, 1 = pitch black (blackouts and flicker)
//   _Ambient         extra ambient light on top of the base 0.006 (beyond your lantern it is nearly black)
//   _AmbColor        the room's coloured ambient light (each room has its own mood)
//   _LanColor        the lantern's colour
//   _Fullbright      1 = ignore the lantern (lobby, signs), 0 = lantern lit
//   _Glow            colour added after lighting (eyes, furnace, exit signs); ignores blackouts
//   _VertexColor     1 = multiply by the mesh's vertex colours (the tree canopies)
//   _Remaster        1 = remastered shading: a soft key light and sky/ground fill give every surface its
//                    form, a fine world-space grain breaks up flat colours, and edges catch a faint rim.
//                    0 = the original flat look (the talking trees keep it)
Shader "FirHorror/Lantern"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Fullbright ("Fullbright", Range(0,1)) = 0
        _Glow ("Glow", Color) = (0,0,0,0)
        _VertexColor ("Use vertex colours", Range(0,1)) = 0
        _Reach ("Lantern reach (m)", Float) = 6
        _Dark ("Darkness", Range(0,1)) = 0
        _Ambient ("Extra ambient", Float) = 0.004
        _AmbColor ("Room ambient colour", Color) = (0.02,0.02,0.03,1)
        _LanColor ("Lantern colour", Color) = (1,0.9,0.75,1)
        _Remaster ("Remastered shading", Range(0,1)) = 1
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
            float _Reach;
            float _Dark;
            float _Ambient;
            float4 _AmbColor;
            float4 _LanColor;
            float _Remaster;

            // a sine-free hash (cheap on Quest's GPU, and steady at large world coordinates); same look as before
            float hash2(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }
            float vnoise(float2 p)
            {
                float2 c = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash2(c), b = hash2(c + float2(1.0, 0.0)), e = hash2(c + float2(0.0, 1.0)), g = hash2(c + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(e, g, f.x), f.y);
            }

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
                float4 shade : TEXCOORD4;   // rgb: the remaster's form and warm/cool shading; a: the sky term
                float3 grainUV : TEXCOORD5; // xy: the grain's plane coordinates; z: the rim
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
                // the remaster's shading depends only on the surface's facing, so it's worked out per vertex (on the
                // world's flat faces that's exactly the per-pixel result; on round props it's indistinguishable)
                float3 n = normalize(o.wn);
                float key = saturate(dot(n, float3(0.42, 0.78, 0.46)));
                float sky = n.y * 0.5 + 0.5;
                float form = 0.68 + 0.22 * key + 0.14 * sky;
                float3 warmCool = lerp(float3(0.93, 0.96, 1.04), float3(1.04, 1.0, 0.95), key);
                o.shade = float4(form * warmCool, sky);
                float3 an = abs(n);
                o.grainUV.xy = an.x > an.y ? (an.x > an.z ? o.wpos.zy : o.wpos.xy) : (an.y > an.z ? o.wpos.xz : o.wpos.xy);
                float3 vdir = normalize(o.wpos - _WorldSpaceCameraPos.xyz);
                o.grainUV.z = pow(1.0 - saturate(dot(n, -vdir)), 4.0) * 0.07;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                // needed in VR (single-pass stereo) so the camera position/direction below are this eye's
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float4 albedo = tex2D(_MainTex, i.uv) * _Color * lerp(float4(1.0, 1.0, 1.0, 1.0), i.color, _VertexColor);
                float3 toSurf = i.wpos - _WorldSpaceCameraPos.xyz;
                float d = max(length(toSurf), 0.0001);

                // the grain: fine world-space noise, faded out by 30 m (so it isn't worked out beyond that)
                float grain = 1.0;
                if (_Remaster > 0.001 && d < 30.0)
                {
                    float gn = vnoise(i.grainUV.xy * 2.3) * 0.6 + vnoise(i.grainUV.xy * 9.7) * 0.4;
                    grain = 1.0 + (gn - 0.5) * 0.16 * (1.0 - d / 30.0);
                }

                float3 col;
                if (_Fullbright > 0.999)
                {
                    // fullbright surfaces (almost all of the world): no lantern to work out
                    float3 fullShade = lerp(float3(1.0, 1.0, 1.0), i.shade.rgb * grain, _Remaster);
                    col = albedo.rgb * fullShade + i.grainUV.z * _Remaster * float3(0.85, 0.9, 1.0);
                }
                else
                {
                    float3 dir = toSurf / d;
                    float reach = max(_Reach, 0.5);
                    float fall = saturate(1.0 - d / reach);
                    fall = fall * fall;
                    float3 camFwd = -normalize(UNITY_MATRIX_V[2].xyz);
                    float cone = smoothstep(0.55, 0.92, dot(dir, camFwd));
                    float facing = saturate(dot(normalize(i.wn), -dir)) * 0.6 + 0.4;
                    float3 light = float3(1.0, 1.0, 1.0) * (0.006 + _Ambient)
                                 + _AmbColor.rgb * (0.6 + 0.4 * facing)
                                 + _LanColor.rgb * (fall * facing * (0.3 + 0.9 * cone));
                    light = light * (1.0 - saturate(_Dark));
                    float3 lanShade = lerp(float3(1.0, 1.0, 1.0), grain * (0.9 + 0.1 * i.shade.a) * float3(1.0, 1.0, 1.0), _Remaster);
                    float3 lit = albedo.rgb * light * lanShade;
                    if (_Fullbright > 0.001)
                    {
                        float3 fullShade = lerp(float3(1.0, 1.0, 1.0), i.shade.rgb * grain, _Remaster);
                        float3 full = albedo.rgb * fullShade + i.grainUV.z * _Remaster * float3(0.85, 0.9, 1.0);
                        lit = lerp(lit, full, _Fullbright);
                    }
                    col = lit;
                }
                return float4(col + _Glow.rgb, 1.0);
            }
            ENDCG
        }
    }
    Fallback "Unlit/Texture"
}
