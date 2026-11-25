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
    const string WaitsKey = "GeoQuest.setupWaits";
    const int MaxWaits = 20;   // about a minute of waiting for Unity to finish importing and compiling
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
        EditorPrefs.DeleteKey(WaitsKey);
        Continue();
    }

    static void Continue()
    {
        // 0. On a first import Unity is still importing files and compiling scripts for a while.
        //    Building before that finishes leaves the map blank and U# behaviours unset, so wait.
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { if (!WaitThenContinue()) Stop("Unity is still importing or compiling. Wait for the spinner in the bottom-right corner to stop, then run GeoQuest > One-Click Setup again."); return; }

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

        // 3. Every map, photo list and U# program asset imported and compiled?
        if (!GeoQuestWorldBuilder.EnsureProgramAssets(out bool created)) { Stop("A GeoQuest script is missing. Unzip the GeoQuest folder again."); return; }
        if (created) { if (!WaitThenContinue(10)) Stop("Unity is still importing or compiling. Wait for the spinner in the bottom-right corner to stop, then run GeoQuest > One-Click Setup again."); return; }   // new program assets compile first
        string missing = GeoQuestWorldBuilder.ImportMissingAssets();
        if (missing != null)
        {
            if (WaitThenContinue()) return;
            Stop("These GeoQuest files didn't import:\n\n" + missing + "\n\nUnzip the GeoQuest folder again, then run GeoQuest > One-Click Setup.");
            return;
        }

        // 4. Make the scene and build the world into it.
        EditorPrefs.DeleteKey(PendingKey);
        EditorPrefs.DeleteKey(TmpTriedKey);
        EditorPrefs.DeleteKey(WaitsKey);
        var scene = File.Exists(ScenePath)
            ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
            : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorUtility.DisplayProgressBar("GeoQuest", "Building the world...", 0.5f);
        bool ok;
        int places;
        try { ok = GeoQuestWorldBuilder.BuildWithDefaults(out places); }
        catch (Exception e) when (StillCompiling(e))
        {
            // U# hadn't finished compiling after all: compile, wait and build again.
            EditorUtility.ClearProgressBar();
            EditorPrefs.SetBool(PendingKey, true);
            if (!WaitThenContinue(10))
                Stop("UdonSharp is still compiling. Wait for it to finish, then run GeoQuest > One-Click Setup again.");
            return;
        }
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

    /// <summary>U#'s "outdated script version, wait until program assets have compiled" error,
    /// possibly wrapped by the reflection call that hit it.</summary>
    static bool StillCompiling(Exception e)
    {
        for (; e != null; e = e.InnerException)
            if (e is InvalidOperationException && (e.Message.Contains("outdated") || e.Message.Contains("compiled"))) return true;
        return false;
    }

    /// <summary>Runs Continue again after a few seconds, once Unity is idle. False when we've waited too long.</summary>
    static bool WaitThenContinue(double seconds = 3)
    {
        int waits = EditorPrefs.GetInt(WaitsKey) + 1;
        if (waits > MaxWaits) { EditorPrefs.DeleteKey(WaitsKey); return false; }
        EditorPrefs.SetInt(WaitsKey, waits);
        double at = EditorApplication.timeSinceStartup + seconds;
        EditorApplication.CallbackFunction tick = null;
        tick = () =>
        {
            if (EditorApplication.timeSinceStartup < at || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= tick;
            if (EditorPrefs.GetBool(PendingKey)) Continue();
        };
        EditorApplication.update += tick;
        return true;
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
        EditorPrefs.DeleteKey(WaitsKey);
        EditorUtility.DisplayDialog("GeoQuest setup", message, "OK");
    }

    static Type FindType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
        .FirstOrDefault(t => t.Name == name);
}
#endif
