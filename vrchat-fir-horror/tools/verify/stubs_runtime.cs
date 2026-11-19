// Minimal API stand-ins for Unity / VRChat / UdonSharp / TMP, for type-checking only.
using System;
namespace UnityEngine {
  public class Object { public string name; public static void Destroy(Object o) {} public static void DestroyImmediate(Object o) {} public static implicit operator bool(Object o) { return o != null; } public static T[] FindObjectsOfType<T>() where T : Object { return null; } public static T FindObjectOfType<T>() where T : Object { return null; } }
  public class Component : Object { public Transform transform; public GameObject gameObject; public T GetComponent<T>() { return default(T); } public T[] GetComponents<T>() { return null; } public Component GetComponent(Type t) { return null; } public T[] GetComponentsInChildren<T>(bool inactive) { return null; } public T[] GetComponentsInChildren<T>() { return null; } public T GetComponentInChildren<T>(bool inactive) { return default(T); } public string tag; }
  public class Behaviour : Component { public bool enabled; }
  public class MonoBehaviour : Behaviour { }
  public class GameObject : Object { public GameObject() {} public GameObject(string n) {} public Transform transform; public bool activeSelf; public bool activeInHierarchy; public int layer; public void SetActive(bool b) {} public T AddComponent<T>() where T : Component { return null; } public Component AddComponent(Type t) { return null; } public T GetComponent<T>() { return default(T); } public T[] GetComponentsInChildren<T>(bool b) { return null; } public static GameObject Find(string n) { return null; } public static GameObject CreatePrimitive(PrimitiveType t) { return null; } }
  public enum PrimitiveType { Sphere, Capsule, Cylinder, Cube, Plane, Quad }
  public class Transform : Component, System.Collections.IEnumerable { public Vector3 position, localPosition, localScale, eulerAngles, localEulerAngles; public Quaternion rotation, localRotation; public Transform parent; public Transform root; public int childCount; public Transform GetChild(int i) { return null; } public void SetParent(Transform p) {} public void SetParent(Transform p, bool w) {} public Vector3 TransformPoint(Vector3 v) { return v; } public Vector3 InverseTransformPoint(Vector3 v) { return v; } public Vector3 TransformDirection(Vector3 v) { return v; } public void SetPositionAndRotation(Vector3 p, Quaternion r) {} public Transform Find(string n) { return null; } public bool IsChildOf(Transform t) { return false; } public System.Collections.IEnumerator GetEnumerator() { return null; } public Vector3 forward, up, right; }
  public struct Vector2 { public float x, y; public Vector2(float a, float b) { x = a; y = b; } public float magnitude { get { return 0; } } public static Vector2 operator *(Vector2 a, float b) { return a; } }
  public struct Vector3 { public float x, y, z; public Vector3(float a, float b, float c) { x = a; y = b; z = c; } public static Vector3 zero { get { return default(Vector3); } } public static Vector3 one { get { return default(Vector3); } } public static Vector3 up { get { return default(Vector3); } } public static Vector3 down { get { return default(Vector3); } } public static Vector3 forward { get { return default(Vector3); } } public static Vector3 back { get { return default(Vector3); } } public static Vector3 left { get { return default(Vector3); } } public static Vector3 right { get { return default(Vector3); } } public float magnitude { get { return 0; } } public float sqrMagnitude { get { return 0; } } public Vector3 normalized { get { return this; } } public void Normalize() {}
    public static Vector3 operator +(Vector3 a, Vector3 b) { return a; } public static Vector3 operator -(Vector3 a, Vector3 b) { return a; } public static Vector3 operator -(Vector3 a) { return a; } public static Vector3 operator *(Vector3 a, float b) { return a; } public static Vector3 operator *(float b, Vector3 a) { return a; } public static Vector3 operator /(Vector3 a, float b) { return a; }
    public static bool operator ==(Vector3 a, Vector3 b) { return true; } public static bool operator !=(Vector3 a, Vector3 b) { return false; } public override bool Equals(object o) { return false; } public override int GetHashCode() { return 0; }
    public static float Distance(Vector3 a, Vector3 b) { return 0; } public static float Dot(Vector3 a, Vector3 b) { return 0; } public static float Angle(Vector3 a, Vector3 b) { return 0; } public static Vector3 Cross(Vector3 a, Vector3 b) { return a; } public static Vector3 Lerp(Vector3 a, Vector3 b, float t) { return a; } public static Vector3 Max(Vector3 a, Vector3 b) { return a; } public static Vector3 MoveTowards(Vector3 a, Vector3 b, float d) { return a; } public string ToString(string f) { return ""; } public static Vector3 Scale(Vector3 a, Vector3 b) { return a; } }
  public struct Quaternion { public float x, y, z, w; public static Quaternion identity { get { return default(Quaternion); } } public static Quaternion Euler(float x, float y, float z) { return identity; } public static Quaternion Euler(Vector3 v) { return identity; } public static Quaternion LookRotation(Vector3 f) { return identity; } public static Quaternion LookRotation(Vector3 f, Vector3 u) { return identity; } public static Quaternion Slerp(Quaternion a, Quaternion b, float t) { return a; } public static Quaternion RotateTowards(Quaternion a, Quaternion b, float d) { return a; } public static Quaternion FromToRotation(Vector3 a, Vector3 b) { return identity; } public static Quaternion operator *(Quaternion a, Quaternion b) { return a; } public static Vector3 operator *(Quaternion a, Vector3 b) { return b; } public Vector3 eulerAngles { get { return default(Vector3); } } }
  public struct Color { public float r, g, b, a; public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; a = 1; } public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; } public static Color white { get { return default(Color); } } public static Color black { get { return default(Color); } } public static Color red { get { return default(Color); } } public static Color clear { get { return default(Color); } } public static Color operator *(Color c, float f) { return c; } }
  public static class Mathf { public static int CeilToInt(float f) { return (int)Math.Ceiling(f); } public static float Sign(float f) { return f >= 0 ? 1f : -1f; } public const float PI = 3.14159f, Rad2Deg = 57.29f, Deg2Rad = 0.01745f; public static float Sin(float f) { return 0; } public static float Cos(float f) { return 0; } public static float Abs(float f) { return 0; } public static int Abs(int f) { return 0; } public static float Max(float a, float b) { return 0; } public static float Min(float a, float b) { return 0; } public static int Max(int a, int b) { return 0; } public static int Min(int a, int b) { return 0; } public static float Clamp(float a, float b, float c) { return 0; } public static int Clamp(int a, int b, int c) { return 0; } public static float Clamp01(float a) { return 0; } public static float Lerp(float a, float b, float t) { return 0; } public static float Atan2(float a, float b) { return 0; } public static float Sqrt(float a) { return 0; } public static float MoveTowards(float a, float b, float d) { return 0; } public static float Repeat(float a, float b) { return 0; } public static float Floor(float a) { return 0; } public static int RoundToInt(float a) { return 0; } public static int FloorToInt(float a) { return 0; } }
  public static class Time { public static float time, deltaTime; public static int frameCount; }
  public static class Random { public static float value; public static float Range(float a, float b) { return 0; } public static int Range(int a, int b) { return 0; } public static Vector3 insideUnitSphere { get { return default(Vector3); } } }
  public class AudioClip : Object { public float length; }
  public class AudioSource : Behaviour { public AudioClip clip; public bool loop, isPlaying, playOnAwake; public float volume, pitch, spatialBlend, minDistance, maxDistance, dopplerLevel; public AudioRolloffMode rolloffMode; public void Play() {} public void Stop() {} public void Pause() {} public void UnPause() {} public void PlayOneShot(AudioClip c) {} public void PlayOneShot(AudioClip c, float v) {} }
  public enum AudioRolloffMode { Logarithmic, Linear }
  public class Collider : Component { public bool isTrigger; public Bounds bounds; }
  public class BoxCollider : Collider { public Vector3 center, size; }
  public struct Bounds { public Vector3 center, size; }
  public class Collision { public Vector3 relativeVelocity; }
  public class Rigidbody : Component { public Vector3 velocity; public float mass; public bool useGravity, isKinematic; public RigidbodyInterpolation interpolation; public CollisionDetectionMode collisionDetectionMode; }
  public enum RigidbodyInterpolation { None, Interpolate }
  public enum CollisionDetectionMode { Discrete, Continuous, ContinuousDynamic }
  public enum QueryTriggerInteraction { UseGlobal, Ignore, Collide }
  public struct RaycastHit { public Collider collider; public Vector3 point; }
  public static class Physics { public static bool Linecast(Vector3 a, Vector3 b, int mask, QueryTriggerInteraction q) { return false; } public static bool Raycast(Vector3 o, Vector3 d, out RaycastHit h, float m, int mask, QueryTriggerInteraction q) { h = default(RaycastHit); return false; } public static bool Raycast(Vector3 o, Vector3 d, float m, int mask, QueryTriggerInteraction q) { return false; } public static bool CheckSphere(Vector3 p, float r, int m, QueryTriggerInteraction q) { return false; } public static void SyncTransforms() {} }
  public static class Debug { public static void Log(object o) {} public static void LogWarning(object o) {} public static void LogWarning(object o, Object c) {} public static void LogError(object o) {} public static void LogError(object o, Object c) {} }
  public class Material : Object { public Material(Shader s) {} public Material(Material m) {} public Shader shader; public Texture mainTexture; public bool enableInstancing; public void SetColor(string n, Color c) {} public void SetFloat(string n, float f) {} public bool HasProperty(string n) { return true; } public void CopyPropertiesFromMaterial(Material m) {} }
  public class Shader : Object { public static Shader Find(string n) { return null; } public static void SetGlobalFloat(string n, float f) {} }
  public class Texture : Object { public int width, height; public TextureWrapMode wrapMode; public FilterMode filterMode; public int anisoLevel; }
  public class Texture2D : Texture { public Texture2D(int w, int h, TextureFormat f, bool mip, bool lin) {} public Texture2D(int w, int h) {} public bool LoadImage(byte[] b) { return true; } public Color32[] GetPixels32() { return null; } public void SetPixels32(Color32[] c) {} public void Apply(bool a, bool b) {} }
  public struct Color32 {}
  public enum TextureFormat { RGBA32, RGB24, DXT1, DXT5, ASTC_6x6 }
  public enum TextureWrapMode { Repeat, Clamp }
  public enum FilterMode { Point, Bilinear, Trilinear }
  public enum TextureCompressionQuality { Fast, Normal, Best }
  public class Renderer : Component { public Material sharedMaterial; public UnityEngine.Rendering.ShadowCastingMode shadowCastingMode; public bool receiveShadows; public UnityEngine.Rendering.LightProbeUsage lightProbeUsage; public UnityEngine.Rendering.ReflectionProbeUsage reflectionProbeUsage; }
  public class MeshRenderer : Renderer {}
  public class LineRenderer : Renderer {}
  public class MeshFilter : Component { public Mesh sharedMesh; }
  public class Mesh : Object { public void SetVertices(System.Collections.Generic.List<Vector3> v) {} public void SetNormals(System.Collections.Generic.List<Vector3> v) {} public void SetUVs(int c, System.Collections.Generic.List<Vector2> v) {} public void SetColors(System.Collections.Generic.List<Color> v) {} public void SetTriangles(System.Collections.Generic.List<int> t, int s) {} public void RecalculateNormals() {} public void RecalculateBounds() {} public void Clear() {} }
  public class Camera : Behaviour { public float nearClipPlane, farClipPlane; public CameraClearFlags clearFlags; public Color backgroundColor; }
  public enum CameraClearFlags { Skybox, SolidColor }
  public class Light : Behaviour {}
  public class TextAsset : Object { public string text; }
  public class ScriptableObject : Object { public static T CreateInstance<T>() where T : ScriptableObject { return null; } }
  public static class RenderSettings { public static Material skybox; public static UnityEngine.Rendering.AmbientMode ambientMode; public static Color ambientLight; public static bool fog; }
  public class RectTransform : Transform { public Vector2 sizeDelta; }
  public class LayerMask { }
  public class Resources { public static Object Load(string p) { return null; } }
  public static class Application { public static string dataPath; public static string unityVersion; }
  public class GUILayout { }
}
namespace UnityEngine.Rendering { public enum ShadowCastingMode { Off, On } public enum LightProbeUsage { Off } public enum ReflectionProbeUsage { Off } public enum AmbientMode { Flat } }
namespace UnityEngine.AI {
  public class NavMeshAgent : UnityEngine.Behaviour { public float speed, radius, height, acceleration, angularSpeed, stoppingDistance; public bool autoBraking, isStopped, isOnNavMesh; public UnityEngine.Vector3 velocity; public bool SetDestination(UnityEngine.Vector3 v) { return true; } public bool Warp(UnityEngine.Vector3 v) { return true; } }
  public class NavMeshObstacle : UnityEngine.Behaviour { public NavMeshObstacleShape shape; public UnityEngine.Vector3 size; public bool carving; }
  public enum NavMeshObstacleShape { Capsule, Box }
}
namespace TMPro {
  public class TextMeshPro : UnityEngine.Renderer { public string text; public UnityEngine.Color color; public UnityEngine.RectTransform rectTransform; public UnityEngine.Material fontSharedMaterial; public TextAlignmentOptions alignment; public bool enableWordWrapping, enableAutoSizing, richText; public float fontSizeMin, fontSizeMax; public FontStyles fontStyle; }
  public enum TextAlignmentOptions { Center }
  public enum FontStyles { Normal, Bold }
  public class TMP_FontAsset : UnityEngine.Object { public UnityEngine.Material material; }
  public static class TMP_Settings { public static TMP_FontAsset defaultFontAsset; }
}
namespace VRC.SDKBase {
  public class VRCPlayerApi { public bool isLocal; public struct TrackingData { public UnityEngine.Vector3 position { get { return default(UnityEngine.Vector3); } } public UnityEngine.Quaternion rotation { get { return default(UnityEngine.Quaternion); } } } public enum TrackingDataType { Head, LeftHand, RightHand, Origin } public TrackingData GetTrackingData(TrackingDataType t) { return default(TrackingData); } public UnityEngine.Vector3 GetPosition() { return default(UnityEngine.Vector3); } public void TeleportTo(UnityEngine.Vector3 p, UnityEngine.Quaternion r) {} public static int GetPlayerCount() { return 0; } public static VRCPlayerApi[] GetPlayers(VRCPlayerApi[] a) { return a; } }
  public static class Networking { public static VRCPlayerApi LocalPlayer; public static bool IsOwner(UnityEngine.GameObject g) { return true; } public static bool IsMaster; public static void SetOwner(VRCPlayerApi p, UnityEngine.GameObject g) {} public static int GetServerTimeInMilliseconds() { return 0; } }
  public static class Utilities { public static bool IsValid(object o) { return o != null; } }
  public static class VRCShader { public static int PropertyToID(string n) { return 0; } public static void SetGlobalFloat(int id, float f) {} }
  public class VRC_Pickup : UnityEngine.MonoBehaviour { public bool IsHeld; public void Drop() {} public string InteractionText, UseText; public float proximity, ThrowVelocityBoostScale; public AutoHoldMode AutoHold; public enum AutoHoldMode { AutoDetect, Yes, No } }
  public class VRC_SceneDescriptor : UnityEngine.MonoBehaviour { public UnityEngine.Transform[] spawns; public float RespawnHeightY; public UnityEngine.GameObject ReferenceCamera; }
}
namespace VRC.SDK3.Components {
  public class VRCPickup : VRC.SDKBase.VRC_Pickup {}
  public class VRCObjectSync : UnityEngine.MonoBehaviour { public void Respawn() {} }
  public class VRCSceneDescriptor : VRC.SDKBase.VRC_SceneDescriptor {}
}
namespace VRC.Udon.Common.Interfaces { public enum NetworkEventTarget { All, Owner } }
namespace VRC.Udon { public class UdonBehaviour : UnityEngine.MonoBehaviour { public string interactText; public float proximity; public void SendCustomEvent(string e) {} public void SetProgramVariable(string n, object v) {} public object GetProgramVariable(string n) { return null; } } }
namespace UdonSharp {
  public enum BehaviourSyncMode { Any, None, Continuous, Manual, NoVariableSync }
  public class UdonBehaviourSyncModeAttribute : Attribute { public UdonBehaviourSyncModeAttribute(BehaviourSyncMode m) {} }
  public class UdonSyncedAttribute : Attribute { }
  public class UdonSharpBehaviour : UnityEngine.MonoBehaviour {
    public void SendCustomEvent(string e) {} public void SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget t, string e) {} public void SendCustomEventDelayedSeconds(string e, float s) {} public void RequestSerialization() {}
    public virtual void Interact() {} public virtual void OnDeserialization() {} public virtual void OnPlayerTriggerEnter(VRC.SDKBase.VRCPlayerApi p) {} public virtual void OnOwnershipTransferred(VRC.SDKBase.VRCPlayerApi p) {} public virtual void OnPickup() {} public virtual void OnDrop() {} public virtual void OnPickupUseDown() {}
  }
  public class UdonSharpProgramAsset : UnityEngine.ScriptableObject { public UnityEngine.Object sourceCsScript; }
}
