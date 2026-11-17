// Minimal stand-in for Unity's UnityCG.cginc, just enough to type-check the shader code.
#define fixed half
#define fixed2 half2
#define fixed3 half3
#define fixed4 half4
cbuffer UnityGlobals {
    float4x4 unity_ObjectToWorld;
    float4x4 unity_WorldToObject;
    float4x4 unity_MatrixV;
    float4x4 unity_MatrixVP;
    float3 _WorldSpaceCameraPos;
    float4 _Time;
};
#define UNITY_MATRIX_V unity_MatrixV
#define UNITY_VERTEX_INPUT_INSTANCE_ID uint instanceID : SV_InstanceID;
#define UNITY_VERTEX_OUTPUT_STEREO uint stereoTargetEyeIndex : TEXCOORD7;
#define UNITY_SETUP_INSTANCE_ID(v)
#define UNITY_INITIALIZE_OUTPUT(type, name) name = (type)0;
#define UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o) o.stereoTargetEyeIndex = 0;
#define UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i) uint unity_StereoEyeIndex = i.stereoTargetEyeIndex;
#define TRANSFORM_TEX(tex, name) (tex.xy * name##_ST.xy + name##_ST.zw)
float4 UnityObjectToClipPos(float4 v) { return mul(unity_MatrixVP, mul(unity_ObjectToWorld, v)); }
float3 UnityObjectToWorldNormal(float3 n) { return normalize(mul(n, (float3x3)unity_WorldToObject)); }
