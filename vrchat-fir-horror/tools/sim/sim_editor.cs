using System;
using UnityEngine;
namespace UnityEditor {
  public class MenuItem : Attribute { public MenuItem(string p) {} public int priority; }
  public class InitializeOnLoadAttribute : Attribute { }
  public static class AssetDatabase {
    public static T LoadAssetAtPath<T>(string p) where T : UnityEngine.Object {
      if (typeof(T) == typeof(TextAsset) || typeof(T) == typeof(MonoScript)) { var f = System.IO.Path.Combine(Environment.GetEnvironmentVariable("FIR_PROJECT") ?? ".", p); if (!System.IO.File.Exists(f)) return null; var ta = typeof(T) == typeof(MonoScript) ? new MonoScript() : new TextAsset(); ta.text = System.IO.File.ReadAllText(f); ta.name = System.IO.Path.GetFileNameWithoutExtension(p); return (T)(UnityEngine.Object)ta; }
      if (typeof(T) == typeof(AudioClip)) return (T)(UnityEngine.Object)new AudioClip { name = System.IO.Path.GetFileNameWithoutExtension(p) }; if (typeof(T) == typeof(Shader)) return (T)(UnityEngine.Object)new Shader(); if (typeof(T) == typeof(UdonSharp.UdonSharpProgramAsset)) return (T)(UnityEngine.Object)new UdonSharp.UdonSharpProgramAsset(); return null; } public static UnityEngine.Object LoadAssetAtPath(string p, Type t) { return null; }
    public static void CreateAsset(UnityEngine.Object o, string p) {} public static void SaveAssets() {} public static void ImportAsset(string p) {} public static void ImportAsset(string p, ImportAssetOptions o) {}
    public static string[] FindAssets(string f, string[] dirs) { return new string[0]; } public static string GUIDToAssetPath(string g) { return null; } public static void ImportPackage(string p, bool i) {}
    public delegate void ImportPackageCallback(string packageName); public static event ImportPackageCallback importPackageCompleted; public static string GetAssetPath(UnityEngine.Object o) { return null; } }
  [Flags] public enum ImportAssetOptions { Default = 0, ForceUpdate = 1, ForceSynchronousImport = 8, ImportRecursive = 256 }
  public static class EditorUtility { public static bool DisplayDialog(string a, string b, string c) { return true; } public static bool DisplayDialog(string a, string b, string c, string d) { return true; } public static void DisplayProgressBar(string a, string b, float f) {} public static void ClearProgressBar() {} public static void SetDirty(UnityEngine.Object o) {} public static void CopySerialized(UnityEngine.Object a, UnityEngine.Object b) {} public static void CompressTexture(Texture2D t, TextureFormat f, TextureCompressionQuality q) {} }
  public static class Undo { public static void DestroyObjectImmediate(UnityEngine.Object o) {} public static void RegisterCreatedObjectUndo(UnityEngine.Object o, string n) {} }
  public static class Selection { public static GameObject activeGameObject; }
  public static class EditorGUIUtility { public static void PingObject(UnityEngine.Object o) {} }
  [Flags] public enum StaticEditorFlags { ContributeGI = 1, OccluderStatic = 2, BatchingStatic = 4, NavigationStatic = 8, OccludeeStatic = 16 }
  public static class GameObjectUtility { public static void SetStaticEditorFlags(GameObject g, StaticEditorFlags f) {} }
  public static class EditorApplication { public delegate void CallbackFunction(); public static CallbackFunction delayCall; public static bool isPlayingOrWillChangePlaymode; }
  public static class EditorPrefs { public static bool GetBool(string k) { return false; } public static void SetBool(string k, bool v) {} public static void DeleteKey(string k) {} }
  public class EditorBuildSettingsScene { public EditorBuildSettingsScene(string p, bool e) {} public string path; }
  public static class EditorBuildSettings { public static EditorBuildSettingsScene[] scenes; }
  public class MonoScript : TextAsset { }
  public class AssetImporter : UnityEngine.Object { public static AssetImporter GetAtPath(string p) { return null; } public void SaveAndReimport() {} }
  public struct AudioImporterSampleSettings { public AudioClipLoadType loadType; public AudioCompressionFormat compressionFormat; public float quality; }
  public class AudioImporter : AssetImporter { public bool forceToMono, loadInBackground; public AudioImporterSampleSettings defaultSampleSettings; }
  public enum BuildTarget { StandaloneWindows64, Android }
  public static class EditorUserBuildSettings { public static BuildTarget activeBuildTarget; }
  public struct ShaderMessage { public string message; public int line; public UnityEditor.Rendering.ShaderCompilerPlatform platform; public UnityEditor.Rendering.ShaderCompilerMessageSeverity severity; }
  public static class ShaderUtil { public static ShaderMessage[] GetShaderMessages(Shader s) { return new ShaderMessage[0]; } public static bool ShaderHasError(Shader s) { return false; } }
}
namespace UnityEngine { public enum AudioClipLoadType { DecompressOnLoad, CompressedInMemory, Streaming } public enum AudioCompressionFormat { PCM, Vorbis, ADPCM } }
namespace UnityEditor.Rendering { public enum ShaderCompilerMessageSeverity { Error, Warning } public enum ShaderCompilerPlatform { D3D, GLES3x, Vulkan } }
namespace UnityEditor.Build { public interface IOrderedCallback { int callbackOrder { get; } } public interface IActiveBuildTargetChanged : IOrderedCallback { void OnActiveBuildTargetChanged(UnityEditor.BuildTarget previousTarget, UnityEditor.BuildTarget newTarget); } }
namespace UnityEditor.AI { public static class NavMeshBuilder { public static void BuildNavMesh() {} } }
namespace UnityEditor.SceneManagement {
  public enum NewSceneSetup { EmptyScene, DefaultGameObjects } public enum NewSceneMode { Single, Additive } public enum OpenSceneMode { Single, Additive }
  public static class EditorSceneManager { public static UnityEngine.SceneManagement.Scene GetActiveScene() { return default(UnityEngine.SceneManagement.Scene); } public static bool SaveCurrentModifiedScenesIfUserWantsTo() { return true; } public static UnityEngine.SceneManagement.Scene OpenScene(string p, OpenSceneMode m) { return default(UnityEngine.SceneManagement.Scene); } public static UnityEngine.SceneManagement.Scene NewScene(NewSceneSetup s, NewSceneMode m) { return default(UnityEngine.SceneManagement.Scene); } public static bool SaveScene(UnityEngine.SceneManagement.Scene s, string p) { return true; } }
}
namespace UnityEngine.SceneManagement { public struct Scene { public bool isDirty { get { return false; } } public string path { get { return ""; } } } }

// ---- engine types only the world builder uses (rendering is irrelevant to the simulation)
namespace UnityEngine {
  public enum PrimitiveType { Sphere, Capsule, Cylinder, Cube, Plane, Quad }
  public class Shader : Object { public static Shader Find(string n) { return new Shader { name = n }; } }
  public class Texture : Object { public int width, height; public TextureWrapMode wrapMode; public FilterMode filterMode; public int anisoLevel; }
  public class Texture2D : Texture { public Texture2D(int w, int h, TextureFormat f, bool mip, bool lin) { width = w; height = h; } public Texture2D(int w, int h) { width = w; height = h; } public bool LoadImage(byte[] b) { return true; } public Color32[] GetPixels32() { return new Color32[0]; } public void SetPixels32(Color32[] c) { } public void Apply(bool a, bool b) { } }
  public struct Color32 { }
  public enum TextureFormat { RGBA32, RGB24, DXT1, DXT5, ASTC_6x6 }
  public enum TextureWrapMode { Repeat, Clamp }
  public enum FilterMode { Point, Bilinear, Trilinear }
  public enum TextureCompressionQuality { Fast, Normal, Best }
  public class TextAsset : Object { public string text = ""; }
  public class Mesh : Object { public void SetVertices(System.Collections.Generic.List<Vector3> v) { } public void SetNormals(System.Collections.Generic.List<Vector3> v) { } public void SetUVs(int c, System.Collections.Generic.List<Vector2> v) { } public void SetColors(System.Collections.Generic.List<Color> v) { } public void SetTriangles(System.Collections.Generic.List<int> t, int s) { } public void RecalculateNormals() { } public void RecalculateBounds() { } public void Clear() { } }
  public class MeshFilter : Component { public Mesh sharedMesh; }
  public class MeshRenderer : Renderer { }
}
namespace UnityEngine.Rendering { public enum ShadowCastingMode { Off, On } public enum LightProbeUsage { Off } public enum ReflectionProbeUsage { Off } public enum AmbientMode { Flat } }

namespace UnityEngine {
  public class Camera : Behaviour { public float nearClipPlane, farClipPlane; public CameraClearFlags clearFlags; public Color backgroundColor; }
  public enum CameraClearFlags { Skybox, SolidColor }
  public class Light : Behaviour { }
  public static class RenderSettings { public static Material skybox; public static UnityEngine.Rendering.AmbientMode ambientMode; public static Color ambientLight; public static bool fog; }
  public class RectTransform : Component { public Vector2 sizeDelta; }
  public class ScriptableObject : Object { public static T CreateInstance<T>() where T : ScriptableObject, new() { return new T(); } }
  public static class Application { public static string dataPath = "/sim/Assets"; }
}
namespace TMPro {
  public enum TextAlignmentOptions { Center, Left, TopLeft, Top, Midline }
  public enum FontStyles { Normal, Bold }
  public class TMP_FontAsset : UnityEngine.Object { public UnityEngine.Material material = new UnityEngine.Material(); }
  public static class TMP_Settings { public static TMP_FontAsset defaultFontAsset = new TMP_FontAsset(); }
}
namespace UdonSharp { public class UdonSharpProgramAsset : UnityEngine.ScriptableObject { public UnityEngine.Object sourceCsScript; } }

// UdonSharp's editor helper, as the builder finds it by reflection: each U# behaviour gets a backing UdonBehaviour
namespace UdonSharpEditor {
  public static class UdonSharpEditorUtility {
    static readonly System.Collections.Generic.Dictionary<UdonSharp.UdonSharpBehaviour, VRC.Udon.UdonBehaviour> backing = new System.Collections.Generic.Dictionary<UdonSharp.UdonSharpBehaviour, VRC.Udon.UdonBehaviour>();
    public static void RunBehaviourSetup(UdonSharp.UdonSharpBehaviour b) { if (!backing.ContainsKey(b)) backing[b] = Sim.UB(b); }
    public static VRC.Udon.UdonBehaviour GetBackingUdonBehaviour(UdonSharp.UdonSharpBehaviour b) { VRC.Udon.UdonBehaviour u; return backing.TryGetValue(b, out u) ? u : null; }
    public static void CopyProxyToUdon(UdonSharp.UdonSharpBehaviour b) { }
  }
}
