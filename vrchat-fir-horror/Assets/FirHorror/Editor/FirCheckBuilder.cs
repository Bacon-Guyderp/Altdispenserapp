#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UdonSharp;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Components;
using Object = UnityEngine.Object;

/// <summary>
/// Fir Warning > Build Systems Check. Stage 1 of the rebuild: a small world that proves, inside your
/// own VRChat setup, that every basic piece the new map will use works. Each test is a button that
/// writes PASS or FAIL on itself. Boards in the world also show the UdonSharp and SDK versions and
/// what the builder found, so one photo of the lobby says everything.
/// </summary>
public static class FirCheckBuilder
{
    const string Root = "Assets/FirHorror";
    const string Gen = Root + "/Generated";
    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();
    static readonly Dictionary<string, Texture2D> baked = new Dictionary<string, Texture2D>();
    static Material fontMat;
    static readonly List<string> report = new List<string>();

    [MenuItem("Fir Warning/Build Systems Check", priority = 1)]
    public static void BuildMenu() { Build(false); }

    public static bool Build(bool quiet)
    {
        if (TMP_Settings.defaultFontAsset == null)
        {
            EditorUtility.DisplayDialog("Fir Warning", "Import TextMeshPro first:\nWindow > TextMeshPro > Import TMP Essential Resources", "OK");
            return false;
        }
        if (!EnsureProgramAssets()) return false;
        Directory.CreateDirectory(Gen);
        mats.Clear(); report.Clear(); meshCache.Clear(); skelParts = null;
        BakeTextures();   // the wallpaper, carpet, ceiling, needles and skeleton textures (Data/textures_baked.txt)
        foreach (var name in new[] { "FirWarning", "FirWarningCheck" })
        {
            var old = GameObject.Find(name);
            if (old != null) Undo.DestroyObjectImmediate(old);
        }
        var root = new GameObject("FirWarningCheck").transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Build Fir Warning systems check");
        fontMat = FontMaterial();
        MakeMaterials();

        // ---- the lobby: 14 x 12 m, everything fullbright so nothing depends on lighting
        var lobby = Group(root, "Lobby", Vector3.zero);
        Box(lobby, "Floor", new Vector3(0, -0.1f, 0), new Vector3(14, 0.2f, 12), M("Floor"));
        Box(lobby, "Ceiling", new Vector3(0, 3.7f, 0), new Vector3(14, 0.2f, 12), M("Ceiling"));
        Box(lobby, "WallN", new Vector3(0, 1.8f, 6.1f), new Vector3(14.4f, 3.6f, 0.2f), M("Wall"));
        Box(lobby, "WallS", new Vector3(0, 1.8f, -6.1f), new Vector3(14.4f, 3.6f, 0.2f), M("Wall"));
        Box(lobby, "WallW", new Vector3(-7.1f, 1.8f, 0), new Vector3(0.2f, 3.6f, 12.4f), M("Wall"));
        Box(lobby, "WallE", new Vector3(7.1f, 1.8f, 0), new Vector3(0.2f, 3.6f, 12.4f), M("Wall"));
        var spawn = Marker(lobby, "Spawn", new Vector3(0, 0.05f, -4), 0);
        // dark notice boards behind the text, so it reads clearly against the wallpaper
        Box(lobby, "TestBoard", new Vector3(0, 1.85f, 5.995f), new Vector3(13.2f, 3.3f, 0.01f), M("Board"));
        Box(lobby, "InfoBoard", new Vector3(-6.995f, 1.65f, 0.45f), new Vector3(0.01f, 3.0f, 9.4f), M("Board"));
        Box(lobby, "PanelBoard", new Vector3(6.995f, 1.75f, 3.5f), new Vector3(0.01f, 1.8f, 2.2f), M("Board"));

        var n = Vector3.back;   // the north wall faces the players
        Text("Title", lobby, new Vector3(0, 3.15f, 5.98f), Face(n), new Vector2(10, 0.6f), 0.5f, "<b>FIR WARNING</b>  systems check").color = new Color(0.9f, 0.15f, 0.15f);
        Text("Help", lobby, new Vector3(0, 2.72f, 5.98f), Face(n), new Vector2(12, 0.35f), 0.17f,
             "Press every button. Each one says <color=#70ff90>PASS</color> or <color=#ff6060>FAIL</color> on itself. Stuck on <color=#ffd040>testing...</color> = that test stopped. Then send a photo of the walls.").color = Color.white;

        // ---- the hub (everything that talks to another script talks to this)
        var hubGo = new GameObject("Hub");
        hubGo.transform.SetParent(root, false);
        var hub = Add<FirCheckHub>(hubGo);
        var w = Vector3.right;   // west wall faces east
        hub.board = Text("HubBoard", lobby, new Vector3(-6.98f, 2.2f, 2.5f), Face(w), new Vector2(5f, 1.6f), 0.2f,
                         "<color=#ff6060><b>HUB: not running</b></color>\nIf this never changes, UdonSharp didn't run in this world.");
        hub.board.color = Color.white;

        // ---- the test room (teleport target), far from the lobby
        var room = Group(root, "TestRoom", new Vector3(40, 0, 0));
        Box(room, "Floor", new Vector3(0, -0.1f, 0), new Vector3(8, 0.2f, 8), M("Floor2"));
        Box(room, "WallN", new Vector3(0, 1.5f, 4.1f), new Vector3(8.4f, 3f, 0.2f), M("Wall2"));
        Box(room, "WallS", new Vector3(0, 1.5f, -4.1f), new Vector3(8.4f, 3f, 0.2f), M("Wall2"));
        Box(room, "WallW", new Vector3(-4.1f, 1.5f, 0), new Vector3(0.2f, 3f, 8.4f), M("Wall2"));
        Box(room, "WallE", new Vector3(4.1f, 1.5f, 0), new Vector3(0.2f, 3f, 8.4f), M("Wall2"));
        var roomArrive = Marker(room, "Arrive", new Vector3(0, 0.05f, -2), 0);
        Text("RoomTitle", room, new Vector3(0, 2.4f, 3.98f), Face(n), new Vector2(7, 0.5f), 0.35f, "<b>TEST ROOM</b>: teleport worked if you're reading this").color = Color.white;

        // ---- the test buttons, 4 + 4 + 2 on the north wall
        var testMat = M("TestPanel");
        var sound = Src(lobby, new Vector3(0, 1.5f, 5), Clip("tree_laugh"), false, 0f, 1f, 20f, false);
        Func<int, Vector3> Spot = i => new Vector3(-4.8f + (i % 4) * 3.2f, 2.05f - (i / 4) * 0.78f, 5.85f);
        Func<int, int, string, FirCheckButton> Test = (slot, test, title) =>
        {
            TextMeshPro label;
            var b = MakeButton<FirCheckButton>(lobby, "Test " + test, Spot(slot), n, out label);
            b.test = test; b.title = title; b.label = label; b.hub = UB(hub);
            return b;
        };
        var t1 = Test(0, 1, "1. Teleport"); t1.destination = roomArrive;
        Test(1, 2, "2. Button's own script");
        Test(2, 3, "3. Event to another script");
        Test(3, 4, "4. Values between scripts");
        var typed = MakeButton<FirCheckTyped>(lobby, "Test 5", Spot(4), n, out var typedLabel);
        typed.hub = hub; typed.label = typedLabel;
        Test(5, 6, "6. Network sync");
        var t7 = Test(6, 7, "7. Skeleton (by event)");
        var t8 = Test(7, 8, "8. Tree (by event)");
        var t9 = Test(8, 9, "9. Lighting (material)"); t9.testMaterial = testMat;
        var t10 = Test(9, 10, "10. Sound"); t10.sound = sound;
        // test 11 is in the test room: teleport back
        var t11 = MakeButton<FirCheckButton>(room, "Test 11", new Vector3(0, 1.3f, 3.85f), n, out var backLabel);
        t11.test = 11; t11.title = "11. Teleport back"; t11.label = backLabel; t11.destination = spawn; t11.hub = UB(hub);

        // the lighting test panel (lantern-lit, so _Dark shows) on the east wall
        var e = Vector3.left;
        var panel = Box(lobby, "TestPanel", new Vector3(6.95f, 1.6f, 3.5f), new Vector3(0.05f, 1.2f, 1.6f), testMat);
        Text("PanelLabel", lobby, new Vector3(6.9f, 2.45f, 3.5f), Face(e), new Vector2(2f, 0.3f), 0.15f, "test 9 panel").color = Color.white;

        // ---- your skeleton
        var skelGo = new GameObject("Skeleton");
        skelGo.transform.SetParent(lobby, false);
        skelGo.transform.localPosition = new Vector3(-2.5f, 0, 1.5f);
        skelGo.transform.localRotation = Quaternion.Euler(0, 180, 0);
        var pivots = SkeletonRig(skelGo.transform, "Rig", M("Bone"));
        var sc = skelGo.AddComponent<BoxCollider>(); sc.center = new Vector3(0, 0.95f, 0); sc.size = new Vector3(0.7f, 1.9f, 0.5f);
        var skel = Add<FirCheckSkeleton>(skelGo);
        skel.pivots = pivots;
        skel.voice = Src(pivots[2], Vector3.zero, Clip("skeleton_white"), false, 1f, 1f, 15f, false);
        skel.label = Text("SkeletonLabel", lobby, new Vector3(-2.5f, 2.35f, 1.5f), Face(Vector3.back), new Vector2(2.2f, 0.45f), 0.14f,
                          "<b>Skeleton</b>\n<color=#ff6060>its script isn't running</color>");
        skel.label.color = Color.white;
        SetInteract(skel, "Press the skeleton", 3f);
        t7.other = UB(skel);

        // ---- a talking tree: the original Fir Warning model
        var treeGo = new GameObject("Tree");
        treeGo.transform.SetParent(lobby, false);
        treeGo.transform.localPosition = new Vector3(2.5f, 0, 1.5f);
        treeGo.transform.localRotation = Quaternion.Euler(0, 180, 0);
        Transform mouth;
        var head = TreeModel(treeGo.transform, 1.9f, true, out mouth);
        var tc = treeGo.AddComponent<BoxCollider>(); tc.center = new Vector3(0, 0.95f, 0); tc.size = new Vector3(1.3f, 1.9f, 1.3f);
        var tree = Add<FirCheckTree>(treeGo);
        tree.head = head; tree.mouth = mouth;
        tree.voice = Src(head, new Vector3(0, 0.75f, 0.35f), Clip("tree_talk"), false, 1f, 1f, 15f, false);
        tree.label = Text("TreeLabel", lobby, new Vector3(2.5f, 2.35f, 1.5f), Face(Vector3.back), new Vector2(2.2f, 0.45f), 0.14f,
                          "<b>Tree</b>\n<color=#ff6060>its script isn't running</color>");
        tree.label.color = Color.white;
        SetInteract(tree, "Press the tree", 3f);
        t8.other = UB(tree);

        // ---- copy everything into Udon, then check what UdonSharp stored
        foreach (var b in root.GetComponentsInChildren<UdonSharpBehaviour>(true)) Sync(b);
        var info = Text("BuildInfo", lobby, new Vector3(-6.98f, 1.0f, -1.6f), Face(w), new Vector2(5f, 1.5f), 0.15f, "");
        info.color = Color.white;
        info.text = BuildReport(root);

        SetupDescriptor(spawn);
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.3f, 0.3f, 0.3f);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = root.gameObject;
        foreach (var r in report) Debug.Log("Fir Warning systems check: " + r);
        if (!quiet) EditorUtility.DisplayDialog("Fir Warning", "Built the systems check.\n\n" + string.Join("\n", report.Take(14)) +
                                                "\n\nNow press Build & Test (or upload) and press every button in the lobby.", "OK");
        return true;
    }

    // =================================================================== what the builder can see

    static string BuildReport(Transform root)
    {
        string usharp = PackageVersion(typeof(UdonSharpBehaviour).Assembly);
        string sdk = PackageVersion(typeof(VRC.SDKBase.VRCPlayerApi).Assembly);
        report.Add("UdonSharp " + usharp + ", VRChat Worlds SDK " + sdk + ", Unity " + Application.unityVersion);
        int behaviours = 0, backed = 0, programs = 0, linksStored = 0, links = 0;
        var missing = new List<string>();
        foreach (var b in root.GetComponentsInChildren<UdonSharpBehaviour>(true))
        {
            behaviours++;
            var u = Backing(b);
            if (u == null) { missing.Add(b.GetType().Name + " has no UdonBehaviour"); continue; }
            backed++;
            var ps = GetMember(u, "programSource");
            if (ps != null) programs++; else missing.Add(b.GetType().Name + " has no program");
            foreach (var f in b.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                bool isLink = typeof(VRC.Udon.UdonBehaviour).IsAssignableFrom(f.FieldType) || typeof(UdonSharpBehaviour).IsAssignableFrom(f.FieldType);
                if (!isLink || f.GetValue(b) == null) continue;
                links++;
                if (StoredInUdon(u, f.Name)) linksStored++;
            }
        }
        report.Add(behaviours + " scripts, " + backed + " with an UdonBehaviour, " + programs + " with a program");
        report.Add(linksStored + " of " + links + " links between scripts found in the Udon data at build time");
        foreach (var m in missing.Take(6)) report.Add("problem: " + m);
        return "<b>BUILD INFO</b>\n" + string.Join("\n", report.Take(9)) + "\n<size=70%>(built " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ")</size>";
    }

    static string PackageVersion(Assembly a)
    {
        try
        {
            var pi = UnityEditor.PackageManager.PackageInfo.FindForAssembly(a);
            return pi != null ? pi.version : "unknown";
        }
        catch (Exception) { return "unknown"; }
    }

    static object GetMember(object o, string name)
    {
        var t = o.GetType();
        var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null) return f.GetValue(o);
        var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        return p != null ? p.GetValue(o) : null;
    }

    /// <summary>Whether the UdonBehaviour's public variable table holds a non-null value for this name.</summary>
    static bool StoredInUdon(VRC.Udon.UdonBehaviour u, string name)
    {
        try
        {
            var table = GetMember(u, "publicVariables");
            if (table == null) return false;
            var m = table.GetType().GetMethods().FirstOrDefault(x => x.Name == "TryGetVariableValue" && x.GetParameters().Length == 2 && !x.IsGenericMethod);
            if (m == null) return false;
            var args = new object[] { name, null };
            return (bool)m.Invoke(table, args) && args[1] != null;
        }
        catch (Exception) { return false; }
    }

    // =================================================================== props

    static T MakeButton<T>(Transform parent, string name, Vector3 pos, Vector3 normal, out TextMeshPro label) where T : UdonSharpBehaviour
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Face(normal);
        var size = new Vector3(0.9f, 0.28f, 0.12f);
        go.AddComponent<BoxCollider>().size = size;
        Prim(PrimitiveType.Cube, "Visual", go.transform, Vector3.zero, size, M("Button"));
        Text("Press", go.transform, new Vector3(0, 0, -size.z / 2 - 0.003f), Quaternion.identity, new Vector2(0.85f, 0.24f), 0.13f, "PRESS").color = Color.white;
        label = Text("Label", go.transform, new Vector3(0, 0.36f, -size.z / 2 - 0.003f), Quaternion.identity, new Vector2(2.9f, 0.4f), 0.12f, name + "\n<color=#ff6060>script not running</color>");
        label.color = Color.white;
        var b = Add<T>(go);
        SetInteract(b, name, 4f);
        return b;
    }

    /// <summary>
    /// The original Fir Warning talking tree: a jagged, tiered fir canopy with big eyes, red lips, teeth and
    /// red scream eyes, on a pot (or a stand when tall). Returns the head pivot (canopy and face) and the mouth.
    /// </summary>
    static Transform TreeModel(Transform root, float height, bool hat, out Transform mouth)
    {
        float potH = Mathf.Max(0.08f, height * 0.09f);
        float trunkH = height * 0.08f;
        float canopyH = height - potH - trunkH;
        float radius = height * 0.36f;
        Prim(PrimitiveType.Cylinder, "Pot", root, new Vector3(0, potH / 2, 0), new Vector3(radius * 0.55f, potH / 2, radius * 0.55f), M(height > 1.8f ? "Stand" : "Pot"));
        Prim(PrimitiveType.Cylinder, "Trunk", root, new Vector3(0, potH + trunkH / 2, 0), new Vector3(radius * 0.18f, trunkH / 2 + 0.02f, radius * 0.18f), M("Trunk"));
        var head = Group(root, "Head", new Vector3(0, potH + trunkH, 0));
        var canopy = new GameObject("Canopy");
        canopy.transform.SetParent(head, false);
        canopy.AddComponent<MeshFilter>().sharedMesh = FirMesh(Mathf.Clamp(Mathf.RoundToInt(height * 2.2f), 3, 6), canopyH, radius, 14, 0.7f);
        NoShadows(canopy.AddComponent<MeshRenderer>(), M("Needles"));
        float faceY = canopyH * 0.5f;
        float rAt = radius * (1f - faceY / canopyH) * 1.05f;
        float eye = Mathf.Max(0.035f, canopyH * 0.055f);
        float faceZ = rAt * 0.92f;
        var eyeL = Prim(PrimitiveType.Sphere, "EyeL", head, new Vector3(-eye * 1.25f, faceY + eye * 1.1f, faceZ), Vector3.one * eye * 2f, M("Eye"));
        var eyeR = Prim(PrimitiveType.Sphere, "EyeR", head, new Vector3(eye * 1.25f, faceY + eye * 1.1f, faceZ), Vector3.one * eye * 2f, M("Eye"));
        Prim(PrimitiveType.Sphere, "PupilL", head, eyeL.transform.localPosition + new Vector3(0, 0, eye * 0.85f), Vector3.one * eye * 0.8f, M("Pupil"));
        Prim(PrimitiveType.Sphere, "PupilR", head, eyeR.transform.localPosition + new Vector3(0, 0, eye * 0.85f), Vector3.one * eye * 0.8f, M("Pupil"));
        var mouthPivot = Group(head, "Mouth", new Vector3(0, faceY - eye * 1.4f, faceZ - eye * 0.2f));
        Prim(PrimitiveType.Sphere, "Lips", mouthPivot, Vector3.zero, new Vector3(eye * 3.2f, eye * 1.6f, eye * 1.2f), M("Mouth"));
        Prim(PrimitiveType.Sphere, "Throat", mouthPivot, new Vector3(0, 0, eye * 0.25f), new Vector3(eye * 2.4f, eye * 1.1f, eye * 0.9f), M("Black"));
        var toothMesh = FirMesh(1, eye * 0.55f, eye * 0.16f, 5, 1f);
        for (int row = 0; row < 2; row++)
            for (int k = 0; k < 6; k++)
            {
                var tooth = new GameObject("Tooth");
                tooth.transform.SetParent(mouthPivot, false);
                tooth.transform.localPosition = new Vector3((-1.05f + k * 0.42f) * eye, (row == 0 ? 0.42f : -0.42f) * eye, eye * 0.5f);
                tooth.transform.localRotation = Quaternion.Euler(row == 0 ? 180 : 0, 0, 0);
                tooth.AddComponent<MeshFilter>().sharedMesh = toothMesh;
                NoShadows(tooth.AddComponent<MeshRenderer>(), M("Teeth"));
            }
        if (hat) Hat(head, new Vector3(0, canopyH - 0.08f * height, 0), height * 0.55f, "Hat");
        mouth = mouthPivot;
        return head;
    }

    static Mesh FirMesh(int tiers, float height, float radius, int seg, float jag)
    {
        string key = "fir" + tiers + "_" + height.ToString("F3") + "_" + radius.ToString("F3") + "_" + seg + "_" + jag;
        if (meshCache.TryGetValue(key, out var cached)) return cached;
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var col = new List<Color>(); var tri = new List<int>();
        float tierH = height / (tiers * 0.7f + 0.3f);
        for (int k = 0; k < tiers; k++)
        {
            float y0 = k * tierH * 0.7f;
            float y1 = Mathf.Min(height, y0 + tierH);
            float r = radius * (1f - (float)k / (tiers + 0.4f));
            var apex = new Vector3(0, y1, 0);
            var inner = new Vector3(0, y0 + tierH * 0.25f, 0);
            int pts = seg * 2;
            for (int i = 0; i < pts; i++)
            {
                float a0 = i * Mathf.PI * 2 / pts, a1 = (i + 1) * Mathf.PI * 2 / pts;
                float r0 = i % 2 == 0 ? r : r * jag, r1 = (i + 1) % 2 == 0 ? r : r * jag;
                float d0 = i % 2 == 0 ? -tierH * 0.06f : 0, d1 = (i + 1) % 2 == 0 ? -tierH * 0.06f : 0;
                var p0 = new Vector3(Mathf.Cos(a0) * r0, y0 + d0, Mathf.Sin(a0) * r0);
                var p1 = new Vector3(Mathf.Cos(a1) * r1, y0 + d1, Mathf.Sin(a1) * r1);
                var outward = new Vector3(Mathf.Cos((a0 + a1) / 2), 0.3f, Mathf.Sin((a0 + a1) / 2));
                AddTri(v, uv, col, tri, apex, p0, p1, outward, height, 1f, 0.55f);
                AddTri(v, uv, col, tri, inner, p1, p0, Vector3.down, height, 0.3f, 0.45f);
            }
        }
        var m = new Mesh { name = key };
        m.SetVertices(v); m.SetUVs(0, uv); m.SetColors(col); m.SetTriangles(tri, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        meshCache[key] = m;
        return m;
    }

    static void AddTri(List<Vector3> v, List<Vector2> uv, List<Color> col, List<int> tri, Vector3 a, Vector3 b, Vector3 c, Vector3 outward,
                       float h, float shadeA, float shadeB)
    {
        if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0) { var t = b; b = c; c = t; }
        int s = v.Count;
        v.Add(a); v.Add(b); v.Add(c);
        foreach (var p in new[] { a, b, c })
            uv.Add(new Vector2((Mathf.Atan2(p.z, p.x) / (Mathf.PI * 2) + 0.5f) * 3f, p.y / Mathf.Max(0.1f, h) * 3f));
        col.Add(Color.white * shadeA); col.Add(Color.white * shadeB); col.Add(Color.white * shadeB);
        tri.Add(s); tri.Add(s + 1); tri.Add(s + 2);
    }

    static GameObject Hat(Transform parent, Vector3 pos, float size, string name)
    {
        var hat = new GameObject(name);
        hat.transform.SetParent(parent, false);
        hat.transform.localPosition = pos;
        hat.transform.localRotation = Quaternion.Euler(0, 0, 14);
        var cone = new GameObject("Cone");
        cone.transform.SetParent(hat.transform, false);
        cone.AddComponent<MeshFilter>().sharedMesh = FirMesh(1, size * 0.55f, size * 0.17f, 12, 1f);
        NoShadows(cone.AddComponent<MeshRenderer>(), M("HatRed"));
        Prim(PrimitiveType.Cylinder, "Brim", hat.transform, new Vector3(0, 0.01f, 0), new Vector3(size * 0.38f, size * 0.03f, size * 0.38f), M("HatWhite"));
        Prim(PrimitiveType.Sphere, "Pompom", hat.transform, new Vector3(0, size * 0.55f, 0), Vector3.one * size * 0.09f, M("HatWhite"));
        return hat;
    }

    // ---- your skeleton model (Models/skeleton_parts.txt, made from the Skeleton.zip you uploaded)

    class SkelPart { public string name, parent; public Vector3 pivot; public List<Vector3> v = new List<Vector3>(); public List<Vector2> uv = new List<Vector2>(); public List<int> t = new List<int>(); public Mesh mesh; }
    static List<SkelPart> skelParts;

    static List<SkelPart> LoadSkeletonParts()
    {
        if (skelParts != null) return skelParts;
        var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Models/skeleton_parts.txt");
        if (ta == null) { report.Add("problem: Models/skeleton_parts.txt is missing, so the skeleton is made of boxes"); return null; }
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        Func<string, float> F = x => float.Parse(x, inv);
        var list = new List<SkelPart>();
        SkelPart cur = null;
        foreach (var raw in ta.text.Split('\n'))
        {
            var p = raw.Trim().Split(' ');
            if (p.Length == 0 || p[0] == "" || p[0] == "#") continue;
            if (p[0] == "part") { cur = new SkelPart { name = p[1], parent = p[2], pivot = new Vector3(F(p[3]), F(p[4]), F(p[5])) }; list.Add(cur); }
            else if (p[0] == "v") { cur.v.Add(new Vector3(F(p[1]), F(p[2]), F(p[3]))); cur.uv.Add(new Vector2(F(p[4]), F(p[5]))); }
            else if (p[0] == "t") { cur.t.Add(int.Parse(p[1])); cur.t.Add(int.Parse(p[2])); cur.t.Add(int.Parse(p[3])); }
        }
        foreach (var sp in list)
        {
            string path = Gen + "/Skeleton_" + sp.name + ".asset";
            var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = m == null;
            if (isNew) m = new Mesh();
            m.Clear();
            m.name = "Skeleton " + sp.name;
            m.SetVertices(sp.v); m.SetUVs(0, sp.uv); m.SetTriangles(sp.t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
            sp.mesh = m;
        }
        skelParts = list;
        return list;
    }

    /// <summary>Your skeleton, facing the parent's +Z. Returns 10 pivots: root, torso, skull, shoulderL, shoulderR, elbowL, elbowR, legL, legR, jaw.</summary>
    static Transform[] SkeletonRig(Transform parent, string name, Material bone)
    {
        var parts = LoadSkeletonParts();
        if (parts == null)
        {
            var r = Group(parent, name, Vector3.zero);
            var ps = new Transform[10];
            ps[0] = r;
            string[] nm = { "", "torso", "skull", "shL", "shR", "elL", "elR", "legL", "legR", "jaw" };
            Vector3[] at = { Vector3.zero, new Vector3(0, 0.95f, 0), new Vector3(0, 1.55f, 0), new Vector3(-0.2f, 1.4f, 0), new Vector3(0.2f, 1.4f, 0),
                             new Vector3(-0.2f, 1.1f, 0), new Vector3(0.2f, 1.1f, 0), new Vector3(-0.1f, 0.9f, 0), new Vector3(0.1f, 0.9f, 0), new Vector3(0, 1.48f, 0.05f) };
            for (int i = 1; i < 10; i++) { ps[i] = Group(r, nm[i], at[i]); Prim(PrimitiveType.Cube, "Bone", ps[i], new Vector3(0, -0.12f, 0), new Vector3(0.08f, 0.25f, 0.08f), bone); }
            return ps;
        }
        var root = Group(parent, name, Vector3.zero);
        var by = new Dictionary<string, Transform>();
        foreach (var sp in parts)
        {
            Transform t = sp.parent == "-" ? root : Group(by[sp.parent], sp.name, sp.pivot);
            var go = new GameObject(sp.name + " Mesh");
            go.transform.SetParent(t, false);
            go.AddComponent<MeshFilter>().sharedMesh = sp.mesh;
            NoShadows(go.AddComponent<MeshRenderer>(), bone);
            by[sp.name] = t;
        }
        return new[] { root, by["torso"], by["skull"], by["upperL"], by["upperR"], by["foreL"], by["foreR"], by["legL"], by["legR"], by["jaw"] };
    }

    // =================================================================== basics

    static Transform Group(Transform parent, string name, Vector3 pos)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        return t;
    }

    static Transform Marker(Transform parent, string name, Vector3 pos, float yaw)
    {
        var t = Group(parent, name, pos);
        t.localRotation = Quaternion.Euler(0, yaw, 0);
        return t;
    }

    /// <summary>A static box with a collider; its texture tiles every 2 m instead of stretching.</summary>
    static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size);
        NoShadows(go.AddComponent<MeshRenderer>(), mat);
        go.AddComponent<BoxCollider>().size = size;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    static Mesh BoxMesh(Vector3 size)
    {
        string key = "box" + size.x.ToString("F3") + "_" + size.y.ToString("F3") + "_" + size.z.ToString("F3");
        if (meshCache.TryGetValue(key, out var cached)) return cached;
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        var h = size * 0.5f;
        foreach (var N in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
        {
            var V = Mathf.Abs(N.y) > 0.5f ? Vector3.forward : Vector3.up;
            var U = Vector3.Cross(N, V);
            float hu = Mathf.Abs(Vector3.Dot(h, U)), hv = Mathf.Abs(Vector3.Dot(h, V)), hn = Mathf.Abs(Vector3.Dot(h, N));
            var c = N * hn;
            int s = v.Count;
            var corners = new[] { c - U * hu - V * hv, c - U * hu + V * hv, c + U * hu + V * hv, c + U * hu - V * hv };
            foreach (var p in corners)
            {
                v.Add(p); n.Add(N);
                uv.Add(new Vector2(Vector3.Dot(p, U) / 2f, Vector3.Dot(p, V) / 2f));
            }
            tri.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
        }
        var m = new Mesh { name = key };
        m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
        m.RecalculateBounds();
        meshCache[key] = m;
        return m;
    }

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        var col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);   // only the objects with scripts get colliders
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        NoShadows(go.GetComponent<MeshRenderer>(), mat);
        return go;
    }

    static void NoShadows(MeshRenderer r, Material mat)
    {
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    static Quaternion Face(Vector3 normal) { return Quaternion.LookRotation(-normal, Vector3.up); }

    static TextMeshPro Text(string name, Transform parent, Vector3 pos, Quaternion rot, Vector2 size, float lineHeight, string text)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshPro>();
        t.rectTransform.sizeDelta = size;
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        t.fontSharedMaterial = fontMat;
        t.text = text;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = true;
        // lineHeight is in metres; a 3D TextMeshPro's font size is in points of about 0.1 m
        t.enableAutoSizing = true;
        t.fontSizeMin = 0.1f;
        t.fontSizeMax = lineHeight * 10f;
        t.richText = true;
        return t;
    }

    static AudioSource Src(Transform parent, Vector3 pos, AudioClip clip, bool loop, float spatial, float vol, float maxDist, bool playOnAwake)
    {
        var go = new GameObject("Audio" + (clip != null ? " " + clip.name : ""));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var a = go.AddComponent<AudioSource>();
        a.clip = clip; a.loop = loop; a.spatialBlend = spatial; a.volume = vol; a.playOnAwake = playOnAwake;
        a.rolloffMode = AudioRolloffMode.Linear; a.minDistance = 1f; a.maxDistance = maxDist; a.dopplerLevel = 0;
        return a;
    }

    static AudioClip Clip(string name)
    {
        var c = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/" + name + ".ogg");
        if (c == null) report.Add("problem: sound " + name + " didn't load");
        return c;
    }

    // ---- materials: the Lantern shader, fullbright (the test panel is lantern-lit so test 9 shows)

    static Material M(string name) { return mats[name]; }

    static void MakeMaterials()
    {
        var sh = Shader.Find("FirHorror/Lantern");
        if (sh == null) report.Add("problem: the FirHorror/Lantern shader didn't load");
        Action<string, string, Color, float, Color> A = (n, texName, c, full, glow) =>
        {
            string path = Gen + "/Check_" + n + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = m == null;
            if (isNew) m = new Material(sh != null ? sh : Shader.Find("Unlit/Texture"));
            if (sh != null) m.shader = sh;
            m.name = "Check_" + n;
            m.mainTexture = texName != null ? Tex(texName) : null;
            m.SetColor("_Color", c);
            m.SetFloat("_Fullbright", full);
            m.SetColor("_Glow", glow);
            m.SetFloat("_VertexColor", n == "Needles" ? 1f : 0f);
            m.SetFloat("_Reach", 14f);
            m.SetFloat("_Dark", 0f);
            m.SetFloat("_Ambient", 0.35f);
            m.SetColor("_AmbColor", new Color(0.2f, 0.2f, 0.2f, 1f));
            m.SetColor("_LanColor", new Color(1f, 0.95f, 0.85f, 1f));
            m.enableInstancing = true;
            if (isNew) AssetDatabase.CreateAsset(m, path);
            EditorUtility.SetDirty(m);
            mats[n] = m;
        };
        var white = Color.white;
        // the original lobby: red carpet, wallpaper, ceiling tiles (fullbright)
        A("Floor", "carpet_red", white, 1, Color.black);
        A("Wall", "wallpaper", new Color(0.8f, 0.8f, 0.8f), 1, Color.black);
        A("Ceiling", "ceiling", new Color(0.7f, 0.7f, 0.7f), 1, Color.black);
        // the test room: wood floor, wallpaper in another tint
        A("Floor2", "wood_floor", white, 1, Color.black);
        A("Wall2", "wallpaper", new Color(0.7f, 0.75f, 0.95f), 1, Color.black);
        A("Button", null, new Color(0.15f, 0.45f, 0.75f), 1, Color.black);
        A("Board", null, new Color(0.07f, 0.06f, 0.06f), 1, Color.black);
        A("TestPanel", null, new Color(0.8f, 0.8f, 0.8f), 0, Color.black);
        A("Bone", "skeleton", new Color(1f, 0.16f, 0.12f), 1, new Color(0.2f, 0.02f, 0.01f));   // the RED skeleton
        // the original tree materials
        A("Needles", "needles", white, 1, Color.black);
        A("Pot", null, new Color(0.55f, 0.12f, 0.1f), 1, Color.black);
        A("Stand", null, new Color(0.25f, 0.2f, 0.15f), 1, Color.black);
        A("Trunk", null, new Color(0.3f, 0.2f, 0.12f), 1, Color.black);
        A("Eye", null, new Color(0.9f, 0.9f, 0.85f), 1, new Color(0.22f, 0.22f, 0.2f));
        A("Pupil", null, new Color(0.01f, 0.01f, 0.01f), 1, Color.black);
        A("Mouth", null, new Color(0.7f, 0.05f, 0.08f), 1, new Color(0.08f, 0f, 0f));
        A("Teeth", null, new Color(0.85f, 0.82f, 0.7f), 1, new Color(0.08f, 0.07f, 0.05f));
        A("Black", null, new Color(0.03f, 0.03f, 0.03f), 1, Color.black);
        A("HatRed", null, new Color(0.75f, 0.05f, 0.08f), 1, Color.black);
        A("HatWhite", null, new Color(0.95f, 0.95f, 0.95f), 1, Color.black);
    }

    static Texture2D Tex(string name)
    {
        if (baked.TryGetValue(name, out var t) && t != null) return t;
        t = AssetDatabase.LoadAssetAtPath<Texture2D>(Gen + "/Tex_" + name + ".asset");   // baked earlier
        if (t == null) { Debug.LogWarning("Fir Warning: missing texture " + name); report.Add("problem: texture " + name + " is missing"); }
        return t;
    }

    public static int BakeTextures()
    {
        baked.Clear();
        Directory.CreateDirectory(Gen);
        var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Data/textures_baked.txt");
        if (ta == null) { report.Add("problem: Data/textures_baked.txt is missing"); return 0; }
        bool android = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android;
        var fmt = android ? TextureFormat.ASTC_6x6 : TextureFormat.DXT1;
        int n = 0;
        foreach (var raw in ta.text.Split('\n'))
        {
            var line = raw.Trim();
            if (line == "" || line.StartsWith("#")) continue;
            var f = line.Split('|');
            if (f.Length < 5) continue;
            string name = f[0];
            byte[] png;
            try { png = Convert.FromBase64String(f[4]); }
            catch (FormatException) { report.Add("problem: texture " + name + " has bad data"); continue; }
            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!decoded.LoadImage(png)) { report.Add("problem: texture " + name + " couldn't be decoded"); Object.DestroyImmediate(decoded); continue; }
            var tex = new Texture2D(decoded.width, decoded.height, TextureFormat.RGBA32, true, false) { name = name };
            tex.SetPixels32(decoded.GetPixels32());
            tex.Apply(true, false);
            Object.DestroyImmediate(decoded);
            tex.wrapMode = f[1] == "clamp" ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 2;
            EditorUtility.CompressTexture(tex, fmt, TextureCompressionQuality.Normal);

            string path = Gen + "/Tex_" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(tex, existing);   // keep the asset (and every reference to it)
                existing.name = name;
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(tex);
                tex = existing;
            }
            else AssetDatabase.CreateAsset(tex, path);
            baked[name] = tex;
            n++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Fir Warning: baked " + n + " textures (" + (android ? "ASTC 6x6, Android" : "DXT1, PC") + ")");
        return n;
    }

    static Material FontMaterial()
    {
        string path = Gen + "/FontMobile.mat";
        var font = TMP_Settings.defaultFontAsset;
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(font.material); AssetDatabase.CreateAsset(m, path); }
        m.CopyPropertiesFromMaterial(font.material);
        var mobile = Shader.Find("TextMeshPro/Mobile/Distance Field");
        if (mobile != null) m.shader = mobile;
        EditorUtility.SetDirty(m);
        return m;
    }

    static void SetupDescriptor(Transform spawn)
    {
        var desc = Object.FindObjectOfType<VRCSceneDescriptor>();
        if (desc == null) desc = new GameObject("VRCWorld").AddComponent<VRCSceneDescriptor>();
        desc.spawns = new[] { spawn };
        desc.RespawnHeightY = -20f;
        EditorUtility.SetDirty(desc);
    }

    // =================================================================== UdonSharp glue (reflection keeps this working across U# versions)

    static readonly string[] Scripts = { "FirCheckHub", "FirCheckButton", "FirCheckTyped", "FirCheckSkeleton", "FirCheckTree" };

    static bool EnsureProgramAssets()
    {
        bool created = false;
        foreach (var s in Scripts)
        {
            string asset = Root + "/Scripts/" + s + ".asset";
            if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(asset) != null) continue;
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(Root + "/Scripts/" + s + ".cs");
            if (script == null) { Debug.LogError("Fir Warning: missing script " + s); return false; }
            var pa = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
            pa.sourceCsScript = script;
            AssetDatabase.CreateAsset(pa, asset);
            created = true;
        }
        if (created)
        {
            AssetDatabase.SaveAssets();
            var m = typeof(UdonSharpProgramAsset).GetMethod("CompileAllCsPrograms", BindingFlags.Public | BindingFlags.Static);
            if (m != null) m.Invoke(null, m.GetParameters().Select(p => p.ParameterType == typeof(bool) ? (object)true : p.DefaultValue).ToArray());
        }
        return true;
    }

    static T Add<T>(GameObject go) where T : UdonSharpBehaviour
    {
        var b = go.AddComponent<T>();
        Setup(b);
        return b;
    }

    static Type EditorUtil => FindAnyType("UdonSharpEditor.UdonSharpEditorUtility");

    static Type FindAnyType(string full) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(full)).FirstOrDefault(t => t != null);

    static object[] Args(MethodInfo m, object first)
    {
        var ps = m.GetParameters();
        var args = new object[ps.Length];
        args[0] = first;
        for (int i = 1; i < ps.Length; i++)
        {
            if (ps[i].HasDefaultValue) { args[i] = ps[i].DefaultValue; continue; }
            var t = ps[i].ParameterType;
            var def = t.GetField("Default", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
                   ?? t.GetProperty("Default", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            args[i] = def ?? (t.IsValueType ? Activator.CreateInstance(t) : null);
        }
        return args;
    }

    static void Setup(UdonSharpBehaviour b)
    {
        var ms = EditorUtil?.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(x => x.Name == "RunBehaviourSetup").ToArray() ?? new MethodInfo[0];
        var m = ms.FirstOrDefault(x => x.GetParameters().Length == 1) ?? ms.FirstOrDefault();
        if (m == null) { report.Add("problem: UdonSharp's RunBehaviourSetup wasn't found"); return; }
        m.Invoke(null, Args(m, b));
    }

    static VRC.Udon.UdonBehaviour Backing(UdonSharpBehaviour b)
    {
        var m = EditorUtil?.GetMethod("GetBackingUdonBehaviour", BindingFlags.Public | BindingFlags.Static);
        var u = m?.Invoke(null, new object[] { b }) as VRC.Udon.UdonBehaviour;
        if (u == null)
        {
            var all = b.GetComponents<VRC.Udon.UdonBehaviour>();
            if (all.Length == 1) u = all[0];
        }
        return u;
    }

    static VRC.Udon.UdonBehaviour UB(UdonSharpBehaviour b)
    {
        var u = Backing(b);
        if (u == null) report.Add("problem: no UdonBehaviour behind " + b.name);
        return u;
    }

    static void SetInteract(UdonSharpBehaviour b, string text, float proximity)
    {
        var u = Backing(b);
        if (u == null) return;
        u.interactText = text;
        u.proximity = proximity;
    }

    static void Sync(UdonSharpBehaviour b)
    {
        var ms = EditorUtil?.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(x => x.Name == "CopyProxyToUdon").ToArray() ?? new MethodInfo[0];
        var m = ms.FirstOrDefault(x => x.GetParameters().Length == 1) ?? ms.FirstOrDefault(x => x.GetParameters().Length >= 1);
        if (m != null)
        {
            try { m.Invoke(null, Args(m, b)); }
            catch (Exception e) { report.Add("problem: UdonSharp couldn't copy " + b.name + ": " + (e.InnerException ?? e).Message); }
        }
        EditorUtility.SetDirty(b);
        var u = Backing(b);
        if (u != null) EditorUtility.SetDirty(u);
    }
}
#endif
