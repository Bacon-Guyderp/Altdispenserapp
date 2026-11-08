#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// GeoQuest > One-Click Setup. Imports TextMeshPro's essentials if they're missing,
/// creates "Assets/GeoQuest/GeoQuest NC.unity", builds the whole world into it, saves it
/// and adds it to Build Settings. Offered automatically the first time the package is
/// imported. The steps survive the editor reload that a package import causes.
/// </summary>
[InitializeOnLoad]
public static class GeoQuestSetup
{
    public const string ScenePath = "Assets/GeoQuest/GeoQuest NC.unity";
    const string PendingKey = "GeoQuest.setupPending";
    const string TmpTriedKey = "GeoQuest.tmpImportTried";
    static string AskedKey => "GeoQuest.askedOnce." + Application.dataPath.GetHashCode();

    static GeoQuestSetup()
    {
        EditorApplication.delayCall += OnEditorLoaded;
    }

    static void OnEditorLoaded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorPrefs.GetBool(PendingKey)) { Continue(); return; }
        if (File.Exists(ScenePath) || EditorPrefs.GetBool(AskedKey)) return;
        EditorPrefs.SetBool(AskedKey, true);
        if (EditorUtility.DisplayDialog("GeoQuest NC",
                "Set up GeoQuest now?\n\nThis creates the scene 'GeoQuest NC' with the whole world built in, " +
                "ready to test and upload. You can also run it later from GeoQuest > One-Click Setup.",
                "Set it up", "Later"))
            Run();
    }

    [MenuItem("GeoQuest/One-Click Setup", priority = 0)]
    public static void Run()
    {
        if (EditorSceneManager.GetActiveScene().isDirty && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        EditorPrefs.SetBool(PendingKey, true);
        EditorPrefs.DeleteKey(TmpTriedKey);
        Continue();
    }

    static void Continue()
    {
        // 1. VRChat Worlds SDK present?
        if (FindType("VRCSceneDescriptor") == null)
        {
            Stop("This project doesn't have the VRChat Worlds SDK.\n\nCreate the project with the VRChat Creator Companion " +
                 "(Worlds, SDK 3.7 or newer), then import GeoQuestNC.unitypackage again.");
            return;
        }

        // 2. TextMeshPro essentials (fonts + settings) present? If not, import them and resume after the reload.
        if (Resources.Load("TMP Settings") == null)
        {
            if (EditorPrefs.GetBool(TmpTriedKey))
            {
                Stop("TextMeshPro's essentials didn't import.\n\nUse Window > TextMeshPro > Import TMP Essential Resources, " +
                     "then run GeoQuest > One-Click Setup again.");
                return;
            }
            EditorPrefs.SetBool(TmpTriedKey, true);
            string pkg = new[]
            {
                "Packages/com.unity.textmeshpro/Package Resources/TMP Essential Resources.unitypackage",
                "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage",
            }.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
            if (pkg == null)
            {
                Stop("Couldn't find TextMeshPro's essentials package.\n\nUse Window > TextMeshPro > Import TMP Essential Resources, " +
                     "then run GeoQuest > One-Click Setup again.");
                return;
            }
            AssetDatabase.importPackageCompleted += OnTmpImported;
            AssetDatabase.ImportPackage(pkg, false);
            return;  // continues in OnTmpImported, or after the reload via PendingKey
        }

        // 3. Make the scene and build the world into it.
        EditorPrefs.DeleteKey(PendingKey);
        EditorPrefs.DeleteKey(TmpTriedKey);
        var scene = File.Exists(ScenePath)
            ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
            : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorUtility.DisplayProgressBar("GeoQuest", "Building the world...", 0.5f);
        bool ok;
        int places;
        try { ok = GeoQuestWorldBuilder.BuildWithDefaults(out places); }
        finally { EditorUtility.ClearProgressBar(); }
        if (!ok) return;  // the builder already explained what's missing

        EditorSceneManager.SaveScene(scene, ScenePath);
        var list = EditorBuildSettings.scenes.ToList();
        if (!list.Any(s => s.path == ScenePath))
        {
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
        var root = GameObject.Find("GeoQuest");
        if (root != null) { Selection.activeGameObject = root; EditorGUIUtility.PingObject(root); }
        EditorUtility.DisplayDialog("GeoQuest NC is ready",
            $"Built the scene '{Path.GetFileNameWithoutExtension(ScenePath)}' with {places:N0} North Carolina places.\n\n" +
            "Next: open VRChat SDK > Show Control Panel > Builder, then Build & Test (PC). " +
            "To publish for Quest, switch the platform to Android and upload again.",
            "OK");
    }

    static void OnTmpImported(string packageName)
    {
        AssetDatabase.importPackageCompleted -= OnTmpImported;
        EditorApplication.delayCall += Continue;
    }

    static void Stop(string message)
    {
        EditorPrefs.DeleteKey(PendingKey);
        EditorPrefs.DeleteKey(TmpTriedKey);
        EditorUtility.DisplayDialog("GeoQuest setup", message, "OK");
    }

    static Type FindType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
        .FirstOrDefault(t => t.Name == name);
}
#endif
