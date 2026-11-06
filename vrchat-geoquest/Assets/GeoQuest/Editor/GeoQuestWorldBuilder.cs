#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UdonSharp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Networking;
using VRC.SDK3.Components;
using VRC.SDKBase;
using Object = UnityEngine.Object;

/// <summary>
/// GeoQuest > Build World. Builds the whole game into the open scene: panorama
/// sphere, map table, buttons, score boards, player objects and the location
/// table. Re-run it any time (e.g. after harvesting more places); it replaces
/// the previous "GeoQuest" root object.
/// </summary>
public class GeoQuestWorldBuilder : EditorWindow
{
    const string Root = "Assets/GeoQuest";
    const string Gen = Root + "/Generated";

    [Serializable] class Loc { public int id; public float lat, lon; public string title, country, iso, author, license, page, src, file, source, kind; }
    [Serializable] class Bounds { public float min_lon = -84.7f, max_lon = -75.1f, min_lat = 33.25f, max_lat = 37.15f; }
    [Serializable] class LocFile { public string region; public Bounds bounds; public float score_km; public Loc[] locations; }

    TextAsset locationsJson;
    string imageBaseUrl = "https://YOUR-GITHUB-NAME.github.io/YOUR-REPO/p/";
    bool useCommonsUrls;
    int maxPlaces = 10000;
    Texture2D worldMap, lobbyPanorama;

    [MenuItem("GeoQuest/Build World")]
    static void Open() => GetWindow<GeoQuestWorldBuilder>("GeoQuest").minSize = new Vector2(460, 360);

    void OnEnable()
    {
        imageBaseUrl = EditorPrefs.GetString("GeoQuest.baseUrl", imageBaseUrl);
        useCommonsUrls = EditorPrefs.GetBool("GeoQuest.commons", false);
        locationsJson = locationsJson ?? AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Data/locations.json");
        worldMap = worldMap ?? AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/NCMap.png");
        lobbyPanorama = lobbyPanorama ?? AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/LobbyPanorama.png");
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "1. Host the photos: push this repo to GitHub, enable Pages (GitHub Actions) and run the 'GeoQuest photos' workflow.\n" +
            "2. Paste your Pages URL below and press 'Download locations.json'.\n" +
            "3. Press 'Build World', then build & upload for both PC and Android (Quest).", MessageType.Info);
        imageBaseUrl = EditorGUILayout.TextField("Photo base URL", imageBaseUrl);
        useCommonsUrls = EditorGUILayout.ToggleLeft(
            "Hot-link original photo URLs (testing only; Mapillary links expire)", useCommonsUrls);
        locationsJson = (TextAsset)EditorGUILayout.ObjectField("locations.json", locationsJson, typeof(TextAsset), false);
        maxPlaces = EditorGUILayout.IntField("Max places", maxPlaces);
        worldMap = (Texture2D)EditorGUILayout.ObjectField("Map (NCMap or WorldMap)", worldMap, typeof(Texture2D), false);
        lobbyPanorama = (Texture2D)EditorGUILayout.ObjectField("Lobby panorama", lobbyPanorama, typeof(Texture2D), false);
        EditorPrefs.SetString("GeoQuest.baseUrl", imageBaseUrl);
        EditorPrefs.SetBool("GeoQuest.commons", useCommonsUrls);

        GUILayout.Space(8);
        if (GUILayout.Button("Download locations.json from the photo site", GUILayout.Height(26))) DownloadLocations();
        if (GUILayout.Button("Build World", GUILayout.Height(40))) Build();
    }

    // ------------------------------------------------------------------ data

    string SiteRoot()
    {
        string u = imageBaseUrl.Trim();
        if (!u.EndsWith("/")) u += "/";
        return u.EndsWith("/p/") ? u.Substring(0, u.Length - 2) : u;
    }

    void DownloadLocations()
    {
        string url = SiteRoot() + "locations.json";
        using (var req = UnityWebRequest.Get(url))
        {
            var op = req.SendWebRequest();
            while (!op.isDone) EditorUtility.DisplayProgressBar("GeoQuest", "Downloading " + url, req.downloadProgress);
            EditorUtility.ClearProgressBar();
            if (req.result != UnityWebRequest.Result.Success)
            {
                EditorUtility.DisplayDialog("GeoQuest", "Couldn't download " + url + "\n" + req.error, "OK");
                return;
            }
            Directory.CreateDirectory(Root + "/Data");
            File.WriteAllText(Root + "/Data/locations.json", req.downloadHandler.text);
        }
        AssetDatabase.ImportAsset(Root + "/Data/locations.json");
        locationsJson = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Data/locations.json");
        EditorUtility.DisplayDialog("GeoQuest", "Got " + LoadLocs().Length + " places.", "OK");
    }

    LocFile LoadFile() => locationsJson == null ? null : JsonUtility.FromJson<LocFile>(locationsJson.text);

    Loc[] LoadLocs()
    {
        if (locationsJson == null) return new Loc[0];
        var f = LoadFile();
        return (f?.locations ?? new Loc[0]).Where(l => l != null && !string.IsNullOrEmpty(l.src)).Take(maxPlaces).ToArray();
    }

    // ------------------------------------------------------------------ build

    void Build()
    {
        if (TMP_Settings.defaultFontAsset == null)
        {
            EditorUtility.DisplayDialog("GeoQuest", "Import TextMeshPro first:\nWindow > TextMeshPro > Import TMP Essential Resources", "OK");
            return;
        }
        if (FindType("VRCPlayerObject") == null)
        {
            EditorUtility.DisplayDialog("GeoQuest", "This world needs VRChat Worlds SDK 3.7.0 or newer (Player Objects). Update it in the VRChat Creator Companion.", "OK");
            return;
        }
        if (!EnsureProgramAssets()) return;

        var locs = LoadLocs();
        if (locs.Length == 0 && !EditorUtility.DisplayDialog("GeoQuest",
                "No places found in locations.json. Build an empty world anyway?", "Build", "Cancel")) return;

        Directory.CreateDirectory(Gen);
        ConfigureTexture(worldMap, 4096, false);
        ConfigureTexture(lobbyPanorama, 2048, false);

        var old = GameObject.Find("GeoQuest");
        if (old != null) Undo.DestroyObjectImmediate(old);
        var root = new GameObject("GeoQuest");
        Undo.RegisterCreatedObjectUndo(root, "Build GeoQuest");

        // ---- materials
        var panoMat = Mat("Panorama", Shader.Find("GeoQuest/Panorama"), Color.white, lobbyPanorama);
        var mapMat = Mat("Map", Shader.Find("Unlit/Texture"), Color.white, worldMap);
        var screenMat = Mat("PhotoScreen", Shader.Find("Unlit/Texture"), Color.white);
        var cMine = Mat("PinMine", ColorShader(), new Color(0.2f, 0.9f, 0.3f));
        var cAnswer = Mat("PinAnswer", ColorShader(), new Color(1f, 0.2f, 0.2f));
        var cOther = Mat("PinOther", ColorShader(), new Color(0.3f, 0.6f, 1f));
        var cHover = Mat("Hover", ColorShader(), Color.white);
        var cLine = Mat("Line", ColorShader(), new Color(1f, 0.95f, 0.3f));
        var cLaser = Mat("Laser", ColorShader(), new Color(0.6f, 0.9f, 1f));
        var cFloor = Mat("Floor", ColorShader(), new Color(0.12f, 0.13f, 0.15f));
        var cFrame = Mat("Frame", ColorShader(), new Color(0.32f, 0.22f, 0.14f));
        var cBoard = Mat("Board", ColorShader(), new Color(0.06f, 0.07f, 0.1f));
        var cBtn = Mat("Button", ColorShader(), new Color(0.22f, 0.28f, 0.38f));
        var cGo = Mat("ButtonGo", ColorShader(), new Color(0.15f, 0.6f, 0.25f));
        var cWarn = Mat("ButtonWarn", ColorShader(), new Color(0.6f, 0.3f, 0.15f));
        var fontMat = FontMaterial();

        // ---- panorama sphere (radius 100 m, sampled by view direction so it behaves like a skybox)
        var sphere = Prim(PrimitiveType.Sphere, "PanoramaSphere", root.transform, Vector3.zero, Vector3.one * 200f, panoMat, false);

        // ---- big screen for flat (non-360) photos, straight ahead above the map
        var screen = Prim(PrimitiveType.Quad, "PhotoScreen", root.transform, new Vector3(0, 3.0f, 5f), new Vector3(4f, 3f, 1f), screenMat, false);
        screen.SetActive(false);

        // ---- floor: invisible walkable area plus a small visible disc
        var floor = new GameObject("Floor");
        floor.transform.SetParent(root.transform, false);
        floor.transform.localPosition = new Vector3(0, -0.05f, 0);
        floor.AddComponent<BoxCollider>().size = new Vector3(7f, 0.1f, 7f);
        Prim(PrimitiveType.Cylinder, "FloorDisc", root.transform, new Vector3(0, -0.005f, 0.3f), new Vector3(3.2f, 0.005f, 3.2f), cFloor, false);
        // invisible walls so nobody walks off into the photo
        for (int i = 0; i < 4; i++)
        {
            var w = new GameObject("Wall" + i);
            w.transform.SetParent(root.transform, false);
            w.transform.localRotation = Quaternion.Euler(0, i * 90, 0);
            w.transform.localPosition = w.transform.localRotation * new Vector3(0, 1.5f, 1.8f) + new Vector3(0, 0, 0.3f);
            w.AddComponent<BoxCollider>().size = new Vector3(4f, 3f, 0.1f);
        }

        var spawn = new GameObject("Spawn").transform;
        spawn.SetParent(root.transform, false);
        spawn.localPosition = new Vector3(0, 0, -0.7f);

        // ---- map table
        var mapRoot = new GameObject("MapTable").transform;
        mapRoot.SetParent(root.transform, false);
        mapRoot.localPosition = new Vector3(0, 1.05f, 0.75f);
        mapRoot.localRotation = Quaternion.Euler(35f, 0, 0);
        const float W = 2f, H = 1f;
        var quad = Prim(PrimitiveType.Quad, "Map", mapRoot, Vector3.zero, new Vector3(W, H, 1), mapMat, false);
        var mapCol = quad.AddComponent<BoxCollider>();
        mapCol.size = new Vector3(1, 1, 0.02f);
        Prim(PrimitiveType.Cube, "Frame", mapRoot, new Vector3(0, -0.06f, 0.025f), new Vector3(W + 0.08f, H + 0.32f, 0.03f), cFrame, false);
        Prim(PrimitiveType.Cylinder, "Leg", root.transform, new Vector3(0, 0.45f, 0.95f), new Vector3(0.08f, 0.45f, 0.08f), cFrame, false);
        var pinRoot = new GameObject("Pins").transform;
        pinRoot.SetParent(mapRoot, false);

        var myPin = Prim(PrimitiveType.Sphere, "MyPin", pinRoot, Vector3.zero, Vector3.one * 0.035f, cMine, false);
        var answerPin = Prim(PrimitiveType.Sphere, "AnswerPin", pinRoot, Vector3.zero, Vector3.one * 0.05f, cAnswer, false);
        var hover = Prim(PrimitiveType.Sphere, "Hover", pinRoot, Vector3.zero, Vector3.one * 0.018f, cHover, false);
        var myLine = MakeLine("MyLine", pinRoot, cLine, 0.006f, false);
        const int Others = 16;
        var otherPins = new Transform[Others];
        var otherLabels = new TextMeshPro[Others];
        var otherLines = new LineRenderer[Others];
        for (int i = 0; i < Others; i++)
        {
            var holder = new GameObject("OtherPin" + i).transform;
            holder.SetParent(pinRoot, false);
            otherPins[i] = holder;
            Prim(PrimitiveType.Sphere, "Dot", holder, Vector3.zero, Vector3.one * 0.03f, cOther, false);
            var lab = Text("Label", holder, new Vector3(0, 0.035f, -0.01f), new Vector2(0.5f, 0.05f), 0.25f, fontMat, "");
            lab.enableAutoSizing = false;
            lab.fontSize = 0.25f;
            lab.enableWordWrapping = false;
            otherLabels[i] = lab;
            otherLines[i] = MakeLine("OtherLine" + i, pinRoot, cOther, 0.004f, false);
            otherPins[i].gameObject.SetActive(false);
        }
        var laser = MakeLine("Laser", root.transform, cLaser, 0.004f, true);

        // ---- text boards
        // Status sits above the scores on the left so the photo screen ahead stays clear.
        var status = Board("StatusBoard", root.transform, new Vector3(-1.75f, 2.85f, 1.15f), new Vector2(1.6f, 0.75f), cBoard, fontMat, out _);
        var score = Board("ScoreBoard", root.transform, new Vector3(-1.75f, 1.55f, 1.15f), new Vector2(1.3f, 1.6f), cBoard, fontMat, out _);
        score.alignment = TextAlignmentOptions.TopLeft;
        var info = Board("InfoBoard", root.transform, new Vector3(1.75f, 1.55f, 1.15f), new Vector2(1.3f, 1.2f), cBoard, fontMat, out _);
        info.text = "";

        // ---- manager + map behaviours
        var mgrGo = new GameObject("GeoGameManager");
        mgrGo.transform.SetParent(root.transform, false);
        var mgr = mgrGo.AddComponent<GeoGameManager>();
        Setup(mgr);
        var map = quad.AddComponent<MapBoard>();
        Setup(map);

        // ---- buttons (laid out under the map, in map-local space)
        float by = -H / 2 - 0.1f;
        Vector3 Bp(float x, float y = 0) => new Vector3(x, by + y, -0.04f);
        var bs = new Vector3(0.16f, 0.1f, 0.05f);
        Button("Zoom +", mapRoot, Bp(-0.9f), bs, cBtn, fontMat, map, nameof(MapBoard.ZoomIn));
        Button("Zoom -", mapRoot, Bp(-0.72f), bs, cBtn, fontMat, map, nameof(MapBoard.ZoomOut));
        Button("<", mapRoot, Bp(-0.52f), bs, cBtn, fontMat, map, nameof(MapBoard.PanLeft));
        Button(">", mapRoot, Bp(-0.34f), bs, cBtn, fontMat, map, nameof(MapBoard.PanRight));
        Button("^", mapRoot, Bp(-0.16f), bs, cBtn, fontMat, map, nameof(MapBoard.PanUp));
        Button("v", mapRoot, Bp(0.02f), bs, cBtn, fontMat, map, nameof(MapBoard.PanDown));
        var zoomText = Text("ZoomText", mapRoot, Bp(-0.81f, -0.1f), new Vector2(0.4f, 0.08f), 0.5f, fontMat, "x1");

        var big = new Vector3(0.3f, 0.1f, 0.05f);
        var guessBtn = Button("GUESS!", mapRoot, Bp(0.42f), big, cGo, fontMat, mgr, nameof(GeoGameManager.SubmitGuess));
        var skipBtn = Button("Skip photo", mapRoot, Bp(0.75f), big, cWarn, fontMat, mgr, nameof(GeoGameManager.Reroll));
        var endBtn = Button("End round", mapRoot, Bp(0.75f, -0.13f), big, cWarn, fontMat, mgr, nameof(GeoGameManager.EndRoundNow));
        var startBtn = Button("START", mapRoot, Bp(0.42f), big, cGo, fontMat, mgr, nameof(GeoGameManager.StartGame));
        var roundsBtn = Button("Rounds", mapRoot, Bp(0.75f), big, cBtn, fontMat, mgr, nameof(GeoGameManager.CycleRounds));
        var timerBtn = Button("Timer", mapRoot, Bp(0.75f, -0.13f), big, cBtn, fontMat, mgr, nameof(GeoGameManager.CycleTimer));
        var modeBtn = Button("Photo type", mapRoot, Bp(0.42f, -0.13f), big, cBtn, fontMat, mgr, nameof(GeoGameManager.CycleMode));
        var nextBtn = Button("Next round", mapRoot, Bp(0.42f), big, cGo, fontMat, mgr, nameof(GeoGameManager.NextRound));
        var lobbyBtn = Button("Quit to lobby", mapRoot, Bp(0.75f), big, cWarn, fontMat, mgr, nameof(GeoGameManager.BackToLobby));

        // ---- player objects (one PlayerGuess per player)
        var tmplGo = new GameObject("PlayerGuess (per player)");
        tmplGo.transform.SetParent(root.transform, false);
        tmplGo.AddComponent(FindType("VRCPlayerObject"));
        var tmpl = tmplGo.AddComponent<PlayerGuess>();
        Setup(tmpl);

        // ---- wire everything
        map.game = mgr;
        map.mapRenderer = quad.GetComponent<Renderer>();
        map.mapCollider = mapCol;
        map.pinRoot = pinRoot;
        map.width = W; map.height = H;
        var file = LoadFile();
        bool world = file != null && file.region == "world";
        var bounds = world ? new Bounds { min_lon = -180, max_lon = 180, min_lat = -90, max_lat = 90 }
                           : (file?.bounds != null && file.bounds.max_lon > file.bounds.min_lon ? file.bounds : new Bounds());
        map.minLon = bounds.min_lon; map.maxLon = bounds.max_lon;
        map.minLat = bounds.min_lat; map.maxLat = bounds.max_lat;
        mgr.scoreScaleKm = file != null && file.score_km > 0 ? file.score_km : (world ? 1500f : 90f);
        mgr.gameTitle = world ? "GEOQUEST" : "GEOQUEST: NORTH CAROLINA";
        map.myPin = myPin.transform;
        map.answerPin = answerPin.transform;
        map.hoverDot = hover.transform;
        map.otherPins = otherPins;
        map.otherLabels = otherLabels;
        map.myLine = myLine;
        map.otherLines = otherLines;
        map.laser = laser;
        map.zoomText = zoomText;

        mgr.panoramaMaterial = panoMat;
        mgr.lobbyPanorama = lobbyPanorama;
        mgr.map = map;
        mgr.guessTemplate = tmpl;
        mgr.statusText = status;
        mgr.scoreboardText = score;
        mgr.infoText = info;
        mgr.lobbyObjects = new[] { startBtn, roundsBtn, timerBtn, modeBtn };
        mgr.photoScreen = screen.transform;
        mgr.screenMaterial = screenMat;
        mgr.guessObjects = new[] { guessBtn, skipBtn, endBtn };
        mgr.revealObjects = new[] { nextBtn, lobbyBtn };

        mgr.urls = new VRCUrl[locs.Length];
        mgr.lats = new float[locs.Length];
        mgr.lons = new float[locs.Length];
        mgr.titles = new string[locs.Length];
        mgr.countries = new string[locs.Length];
        mgr.credits = new string[locs.Length];
        mgr.kinds = new int[locs.Length];
        string baseUrl = imageBaseUrl.Trim();
        if (!baseUrl.EndsWith("/")) baseUrl += "/";
        for (int i = 0; i < locs.Length; i++)
        {
            var l = locs[i];
            mgr.urls[i] = new VRCUrl(useCommonsUrls ? l.src : baseUrl + (string.IsNullOrEmpty(l.file) ? l.id.ToString("D5") + ".jpg" : l.file));
            mgr.lats[i] = l.lat;
            mgr.lons[i] = l.lon;
            mgr.titles[i] = l.title ?? "";
            mgr.countries[i] = l.country ?? "";
            mgr.kinds[i] = l.kind == "flat" ? 1 : 0;
            mgr.credits[i] = (l.author ?? "Unknown") + " (" + l.license + "), " + (string.IsNullOrEmpty(l.source) ? "Wikimedia Commons" : l.source);
        }

        foreach (var b in new UdonSharpBehaviour[] { mgr, map, tmpl }) Sync(b);
        foreach (var b in root.GetComponentsInChildren<GeoButton>(true)) Sync(b);

        // start states
        foreach (var g in mgr.guessObjects.Concat(mgr.revealObjects)) g.SetActive(false);
        myPin.SetActive(false); answerPin.SetActive(false); hover.SetActive(false);

        SetupDescriptor(spawn);
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.white;
        RenderSettings.fog = false;

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        EditorUtility.DisplayDialog("GeoQuest", "Built with " + locs.Length + " places.\n\nSave the scene, then build & test / upload for PC and for Android (Quest).", "Nice");
    }

    // ------------------------------------------------------------------ helpers

    static Type FindType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
        .FirstOrDefault(t => t.Name == name && typeof(Component).IsAssignableFrom(t));

    static Shader ColorShader() => Shader.Find("GeoQuest/UnlitColor");

    static Material Mat(string name, Shader shader, Color c, Texture tex = null)
    {
        string path = Gen + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (tex != null) m.mainTexture = tex;
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
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

    static void ConfigureTexture(Texture2D t, int max, bool mips)
    {
        if (t == null) return;
        var imp = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(t)) as TextureImporter;
        if (imp == null) return;
        imp.textureType = TextureImporterType.Default;
        imp.mipmapEnabled = mips;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.maxTextureSize = max;
        imp.npotScale = TextureImporterNPOTScale.None;
        imp.SetPlatformTextureSettings(new TextureImporterPlatformSettings
        { name = "Android", overridden = true, maxTextureSize = max, format = TextureImporterFormat.ASTC_6x6 });
        imp.SaveAndReimport();
    }

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool keepCollider)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return go;
    }

    static LineRenderer MakeLine(string name, Transform parent, Material mat, float width, bool world)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var l = go.AddComponent<LineRenderer>();
        l.sharedMaterial = mat;
        l.useWorldSpace = world;
        l.positionCount = 2;
        l.startWidth = l.endWidth = width;
        l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        l.receiveShadows = false;
        l.enabled = false;
        return l;
    }

    static TextMeshPro Text(string name, Transform parent, Vector3 pos, Vector2 size, float fontMax, Material fontMat, string text)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshPro>();
        t.rectTransform.sizeDelta = size;
        go.transform.localPosition = pos;
        t.fontSharedMaterial = fontMat;
        t.text = text;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = true;
        t.enableAutoSizing = true;
        t.fontSizeMin = 0.1f;
        t.fontSizeMax = fontMax;
        t.richText = true;
        return t;
    }

    static TextMeshPro Board(string name, Transform parent, Vector3 pos, Vector2 size, Material bg, Material fontMat, out GameObject back)
    {
        var holder = new GameObject(name).transform;
        holder.SetParent(parent, false);
        holder.localPosition = pos;
        var look = pos - new Vector3(0, pos.y, -0.7f);
        holder.localRotation = Quaternion.LookRotation(new Vector3(look.x, 0, look.z));
        back = Prim(PrimitiveType.Quad, "Background", holder, new Vector3(0, 0, 0.01f), new Vector3(size.x + 0.1f, size.y + 0.1f, 1), bg, false);
        return Text("Text", holder, Vector3.zero, size, 1.2f, fontMat, "GeoQuest");
    }

    static GameObject Button(string label, Transform parent, Vector3 pos, Vector3 size, Material mat, Material fontMat, UdonSharpBehaviour target, string evt)
    {
        var go = new GameObject("Button " + label);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.AddComponent<BoxCollider>().size = size;
        Prim(PrimitiveType.Cube, "Visual", go.transform, Vector3.zero, size, mat, false);
        var t = Text("Label", go.transform, new Vector3(0, 0, -size.z / 2 - 0.002f), new Vector2(size.x * 0.95f, size.y * 0.9f), 0.45f, fontMat, label);
        t.color = Color.white;
        var b = go.AddComponent<GeoButton>();
        Setup(b);
        b.target = target;
        b.eventName = evt;
        var udon = Backing(b);
        if (udon != null)
        {
            udon.interactText = label;
            udon.proximity = 4f;
        }
        return go;
    }

    void SetupDescriptor(Transform spawn)
    {
        var desc = Object.FindObjectOfType<VRCSceneDescriptor>();
        if (desc == null)
        {
            var go = new GameObject("VRCWorld");
            desc = go.AddComponent<VRCSceneDescriptor>();
        }
        desc.spawns = new[] { spawn };
        desc.RespawnHeightY = -20f;
        var camGo = new GameObject("ReferenceCamera");
        camGo.transform.SetParent(spawn.parent, false);
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.02f;
        cam.farClipPlane = 500f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.enabled = false;
        desc.ReferenceCamera = camGo;
        EditorUtility.SetDirty(desc);
    }

    // ------------------------------------------------------------------ UdonSharp glue (reflection keeps this working across U# versions)

    static readonly string[] Scripts = { "GeoGameManager", "MapBoard", "PlayerGuess", "GeoButton" };

    static bool EnsureProgramAssets()
    {
        bool created = false;
        foreach (var s in Scripts)
        {
            string asset = Root + "/Scripts/" + s + ".asset";
            if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(asset) != null) continue;
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(Root + "/Scripts/" + s + ".cs");
            if (script == null) { Debug.LogError("GeoQuest: missing script " + s); return false; }
            var pa = CreateInstance<UdonSharpProgramAsset>();
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

    static Type EditorUtil => FindAnyType("UdonSharpEditor.UdonSharpEditorUtility");

    static Type FindAnyType(string full) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType(full)).FirstOrDefault(t => t != null);

    static void Setup(UdonSharpBehaviour b)
    {
        var m = EditorUtil?.GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(x => x.Name == "RunBehaviourSetup");
        if (m == null) return;
        var ps = m.GetParameters();
        var args = new object[ps.Length];
        args[0] = b;
        for (int i = 1; i < ps.Length; i++) args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : null;
        m.Invoke(null, args);
    }

    static VRC.Udon.UdonBehaviour Backing(UdonSharpBehaviour b)
    {
        var m = EditorUtil?.GetMethod("GetBackingUdonBehaviour", BindingFlags.Public | BindingFlags.Static);
        return m?.Invoke(null, new object[] { b }) as VRC.Udon.UdonBehaviour;
    }

    static void Sync(UdonSharpBehaviour b)
    {
        var m = EditorUtil?.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(x => x.Name == "CopyProxyToUdon" && x.GetParameters().Length >= 1);
        if (m != null)
        {
            var ps = m.GetParameters();
            var args = new object[ps.Length];
            args[0] = b;
            for (int i = 1; i < ps.Length; i++) args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : null;
            m.Invoke(null, args);
        }
        EditorUtility.SetDirty(b);
        var u = Backing(b);
        if (u != null) EditorUtility.SetDirty(u);
    }
}
#endif
