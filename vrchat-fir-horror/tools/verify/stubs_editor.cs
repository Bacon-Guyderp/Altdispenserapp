using System;
using UnityEngine;
namespace UnityEditor {
  public class MenuItem : Attribute { public MenuItem(string p) {} public int priority; }
  public class InitializeOnLoadAttribute : Attribute { }
  public static class AssetDatabase {
    public static T LoadAssetAtPath<T>(string p) where T : UnityEngine.Object { return null; } public static UnityEngine.Object LoadAssetAtPath(string p, Type t) { return null; }
    public static void CreateAsset(UnityEngine.Object o, string p) {} public static void SaveAssets() {} public static void ImportAsset(string p) {} public static void ImportAsset(string p, ImportAssetOptions o) {}
    public static string[] FindAssets(string f, string[] dirs) { return null; } public static string GUIDToAssetPath(string g) { return null; } public static void ImportPackage(string p, bool i) {}
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
  public static class ShaderUtil { public static ShaderMessage[] GetShaderMessages(Shader s) { return null; } public static bool ShaderHasError(Shader s) { return false; } }
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
