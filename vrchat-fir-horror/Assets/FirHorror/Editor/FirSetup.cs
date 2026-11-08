#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Fir Warning > One-Click Setup. Imports TextMeshPro's essentials if they're missing, creates
/// "Assets/FirHorror/Fir Warning.unity", builds the world into it (including the NavMesh), saves
/// it and adds it to Build Settings. Offered automatically the first time the package is imported.
/// </summary>
[InitializeOnLoad]
public static class FirSetup
{
    public const string ScenePath = "Assets/FirHorror/Fir Warning.unity";
    const string PendingKey = "FirWarning.setupPending";
    const string TmpTriedKey = "FirWarning.tmpImportTried";
    static string AskedKey => "FirWarning.askedOnce." + Application.dataPath.GetHashCode();

    static FirSetup() { EditorApplication.delayCall += OnEditorLoaded; }

    static void OnEditorLoaded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorPrefs.GetBool(PendingKey)) { Continue(); return; }
        if (File.Exists(ScenePath) || EditorPrefs.GetBool(AskedKey)) return;
        EditorPrefs.SetBool(AskedKey, true);
        if (EditorUtility.DisplayDialog("Fir Warning",
                "Set up Fir Warning now?\n\nThis creates the scene 'Fir Warning' with the whole horror world built in, " +
                "ready to test and upload. You can also run it later from Fir Warning > One-Click Setup.",
                "Set it up", "Later"))
            Run();
    }

    [MenuItem("Fir Warning/One-Click Setup", priority = 0)]
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
        if (FindType("VRCSceneDescriptor") == null)
        {
            Stop("This project doesn't have the VRChat Worlds SDK.\n\nCreate the project with the VRChat Creator Companion " +
                 "(Worlds, SDK 3.5 or newer), then import FirWarning.unitypackage again.");
            return;
        }
        if (Resources.Load("TMP Settings") == null)
        {
            if (EditorPrefs.GetBool(TmpTriedKey))
            {
                Stop("TextMeshPro's essentials didn't import.\n\nUse Window > TextMeshPro > Import TMP Essential Resources, " +
                     "then run Fir Warning > One-Click Setup again.");
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
                     "then run Fir Warning > One-Click Setup again.");
                return;
            }
            AssetDatabase.importPackageCompleted += OnTmpImported;
            AssetDatabase.ImportPackage(pkg, false);
            return;
        }

        EditorPrefs.DeleteKey(PendingKey);
        EditorPrefs.DeleteKey(TmpTriedKey);
        var scene = File.Exists(ScenePath)
            ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
            : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, ScenePath);  // the NavMesh bake needs a saved scene
        EditorUtility.DisplayProgressBar("Fir Warning", "Building the world and baking the NavMesh...", 0.5f);
        bool ok;
        try { ok = FirWorldBuilder.Build(true); }
        finally { EditorUtility.ClearProgressBar(); }
        if (!ok) return;

        EditorSceneManager.SaveScene(scene, ScenePath);
        var list = EditorBuildSettings.scenes.ToList();
        if (!list.Any(s => s.path == ScenePath))
        {
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
        EditorUtility.DisplayDialog("Fir Warning is ready",
            "Built the scene 'Fir Warning'.\n\nNext: VRChat SDK > Show Control Panel > Builder, then Build & Test (PC). " +
            "For Quest, switch the platform to Android and upload the same world again.",
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
        EditorUtility.DisplayDialog("Fir Warning setup", message, "OK");
    }

    static Type FindType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
        .FirstOrDefault(t => t.Name == name);
}
#endif
