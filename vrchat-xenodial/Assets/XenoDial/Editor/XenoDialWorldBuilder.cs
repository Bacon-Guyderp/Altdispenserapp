#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UdonSharp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Components;
using Object = UnityEngine.Object;

/// <summary>
/// XenoDial > Build World. Builds the whole world into the open scene from primitives:
/// the crash site with the pod and the watch, the training arena, the sky tower, the
/// speed track, the secret vault, the per-player alien bodies and the local watch.
/// Re-run it any time; it replaces the previous "XenoDial" root object.
/// </summary>
public static class XenoDialWorldBuilder
{
    const string Root = "Assets/XenoDial";
    const string Gen = Root + "/Generated";

    const float K_ICON = 0.021f;   // alien model -> dial icon scale
    const int PAGE = 10;           // aliens shown on the dial at once

    [MenuItem("XenoDial/Build World", priority = 1)]
    static void BuildMenu()
    {
        if (Build())
            EditorUtility.DisplayDialog("XenoDial", "World built.\n\nSave the scene, then Build & Test from the VRChat SDK panel.", "Nice");
    }

    // ------------------------------------------------------------------ build

    public static bool Build()
    {
        if (TMP_Settings.defaultFontAsset == null)
        {
            EditorUtility.DisplayDialog("XenoDial", "Import TextMeshPro first:\nWindow > TextMeshPro > Import TMP Essential Resources", "OK");
            return false;
        }
        if (FindType("VRCPlayerObject") == null)
        {
            EditorUtility.DisplayDialog("XenoDial", "This world needs VRChat Worlds SDK 3.7.0 or newer (Player Objects). Update it in the VRChat Creator Companion.", "OK");
            return false;
        }
        string rosterError;
        var roster = XenoDialAliens.LoadRoster(out rosterError);
        if (rosterError != null || roster.Count == 0)
        {
            EditorUtility.DisplayDialog("XenoDial", rosterError ?? "aliens.txt has no aliens in it.", "OK");
            return false;
        }
        if (!EnsureProgramAssets()) return false;
        Directory.CreateDirectory(Gen);

        var old = GameObject.Find("XenoDial");
        if (old != null) Undo.DestroyObjectImmediate(old);
        var root = new GameObject("XenoDial");
        Undo.RegisterCreatedObjectUndo(root, "Build XenoDial");
        var env = new GameObject("Environment").transform;
        env.SetParent(root.transform, false);

        var font = FontMaterial();
        var clips = LoadClips();

        // ---- every alien's model, once. Bodies, the dial and the gallery copy from here.
        var libRoot = new GameObject("Alien Library (templates)");
        libRoot.transform.SetParent(root.transform, false);
        libRoot.transform.localPosition = new Vector3(0, -50, 0);
        var alienMat = MatAsset("AlienVertexColor", Shader.Find("XenoDial/ToonVertexColor"));
        alienMat.SetColor("_Color", Color.white);
        alienMat.SetFloat("_Emission", 0f);
        var library = BuildLibrary(roster, libRoot.transform, alienMat);
        libRoot.SetActive(false);

        // ---- the land
        var grass = Toon("Grass", new Color(0.33f, 0.52f, 0.24f));
        var dirt = Toon("Dirt", new Color(0.52f, 0.41f, 0.28f));
        var rock = Toon("Rock", new Color(0.48f, 0.46f, 0.44f));
        var board = Toon("Board", new Color(0.07f, 0.09f, 0.11f));
        var post = Toon("Wood", new Color(0.42f, 0.28f, 0.16f));
        var teal = Toon("TealGlow", new Color(0.15f, 1f, 0.8f), 1f);
        var btn = Toon("Button", new Color(0.12f, 0.45f, 0.4f));

        Prim(PrimitiveType.Cube, "Ground", env, new Vector3(0, -0.5f, 0), new Vector3(240, 1, 240), grass, true);
        Prim(PrimitiveType.Cylinder, "Clearing", env, new Vector3(0, 0.005f, 3), new Vector3(28, 0.005f, 28), dirt, false);
        BuildScenery(env, rock, post);
        Bounds(env);

        var spawn = new GameObject("Spawn").transform;
        spawn.SetParent(root.transform, false);
        spawn.localPosition = new Vector3(0, 0.05f, -1f);

        // ---- the watch (local, interactive) and its controller
        var sysGo = new GameObject("XenoDial System");
        sysGo.transform.SetParent(root.transform, false);
        var watch = sysGo.AddComponent<XenoDialWatch>();
        Setup(watch);
        var ui = sysGo.AddComponent<AudioSource>();
        ui.playOnAwake = false;
        ui.spatialBlend = 0f;
        ui.volume = 0.6f;
        BuildLocalWatch(root.transform, watch, font);

        // ---- crash site: the pod hands you the watch
        BuildCrashSite(env, root.transform, watch, font, teal, board);

        // ---- per-player alien bodies
        var body = BuildBodyTemplate(root.transform, clips, library, roster);

        // ---- areas
        var targets = new List<XenoTarget>();
        var score = BuildArena(env, root.transform, new Vector3(42, 0, 0), font, board, targets, clips);
        BuildTower(env, root.transform, new Vector3(0, 0, 48), font, board, rock, clips);
        BuildRaceTrack(env, root.transform, new Vector3(-52, 0, 0), font, board, post, clips);
        BuildVault(env, root.transform, new Vector3(0, 0, -40), watch, spawn, font, board, rock, btn, clips);
        BuildMirror(env, root.transform, new Vector3(-7.5f, 0, 2.5f), font, btn, board);
        BuildSigns(env, font, board, post);
        BuildGallery(env, new Vector3(-45, 0, -62), roster, library, font, board, rock);

        // ---- wire the watch
        watch.bodyTemplate = body;
        watch.targets = targets.ToArray();
        watch.scoreText = score;
        watch.ui = ui;
        watch.clickClip = clips["click"];
        watch.openClip = clips["open"];
        watch.beepClip = clips["beep"];
        watch.library = library;
        watch.formNames = roster.Select(a => a.name).ToArray();
        watch.formPowers = roster.Select(a => a.Description).ToArray();
        watch.formColors = roster.Select(a => a.glow).ToArray();
        watch.powerType = roster.Select(a => a.power).ToArray();
        watch.strength = roster.Select(a => a.strength).ToArray();
        watch.walkSpeed = roster.Select(a => a.walk).ToArray();
        watch.runSpeed = roster.Select(a => a.run).ToArray();
        watch.jumpImpulse = roster.Select(a => a.jump).ToArray();
        watch.gravity = roster.Select(a => a.gravity).ToArray();
        watch.eyeHeight = roster.Select(a => a.eyeHeight).ToArray();
        watch.cooldown = roster.Select(a => a.cooldown).ToArray();
        watch.pickKeys = new[] { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5,
                                 KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0 };

        foreach (var b in root.GetComponentsInChildren<UdonSharpBehaviour>(true)) Sync(b);

        foreach (var t in env.GetComponentsInChildren<Transform>(true))
            if (t.GetComponent<TMP_Text>() == null)   // text meshes must stay dynamic
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);

        SetupDescriptor(spawn);
        SetupSky();

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        return true;
    }

    // ------------------------------------------------------------------ the watch you wear

    static void BuildLocalWatch(Transform parent, XenoDialWatch watch, Material font)
    {
        var w = new GameObject("Watch (yours)").transform;
        w.SetParent(parent, false);
        Transform core; Renderer face, glow;
        WatchModel(w, out core, out face, out glow);

        var dial = new GameObject("Dial").transform;
        dial.SetParent(w, false);
        dial.localPosition = new Vector3(0, 0.03f, 0);
        var slots = new Transform[PAGE];
        var discs = new Renderer[PAGE];
        var minis = new Transform[PAGE];
        for (int i = 0; i < PAGE; i++)
        {
            float a = Mathf.PI * 0.5f - i * Mathf.PI * 2f / PAGE;
            var slot = new GameObject("Slot " + (i + 1)).transform;
            slot.SetParent(dial, false);
            slot.localPosition = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.085f;
            var disc = Prim(PrimitiveType.Cylinder, "Disc", slot, new Vector3(0, 0, -0.008f), new Vector3(0.032f, 0.0008f, 0.032f),
                            Glow("DialDisc", new Color(0.2f, 1f, 0.85f, 0.45f)), false);
            disc.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var mini = new GameObject("Mini").transform;
            mini.SetParent(slot, false);
            mini.localPosition = new Vector3(0, -0.019f, 0);
            mini.localScale = Vector3.one * K_ICON;
            slots[i] = slot;
            discs[i] = disc.GetComponent<Renderer>();
            minis[i] = mini;
        }
        var sel = Prim(PrimitiveType.Sphere, "Selector", dial, Vector3.zero, Vector3.one * 0.048f, Glow("Selector", new Color(1, 1, 1, 0.3f)), false);
        var arrowMat = Glow("PageArrow", new Color(1f, 1f, 1f, 0.7f));
        var prev = Prim(PrimitiveType.Sphere, "PrevPage", dial, new Vector3(-0.13f, 0, 0), new Vector3(0.022f, 0.022f, 0.022f), arrowMat, false);
        var next = Prim(PrimitiveType.Sphere, "NextPage", dial, new Vector3(0.13f, 0, 0), new Vector3(0.022f, 0.022f, 0.022f), arrowMat, false);
        Text("Prev", prev.transform, Vector3.zero, new Vector2(1.6f, 1.6f), 1.2f, font, "<").transform.localScale = Vector3.one * 0.9f;
        Text("Next", next.transform, Vector3.zero, new Vector2(1.6f, 1.6f), 1.2f, font, ">").transform.localScale = Vector3.one * 0.9f;

        var label = Text("Label", w, new Vector3(0, 0.1f, 0), new Vector2(0.24f, 0.1f), 0.3f, font, "XenoDial");
        label.fontSizeMin = 0.01f;

        watch.watchRoot = w;
        watch.core = core;
        watch.faceRenderer = face;
        watch.coreRenderer = glow;
        watch.dialRoot = dial;
        watch.slots = slots;
        watch.slotDiscs = discs;
        watch.slotMinis = minis;
        watch.prevPage = prev.transform;
        watch.nextPage = next.transform;
        watch.selector = sel.transform;
        watch.label = label;
    }

    /// <summary>The watch itself, in metres. Face normal = +Y, the strap runs across the wrist (X).</summary>
    static void WatchModel(Transform w, out Transform core, out Renderer face, out Renderer glow)
    {
        var strap = Toon("WatchStrap", new Color(0.12f, 0.14f, 0.13f));
        var casing = Toon("WatchCase", new Color(0.55f, 0.58f, 0.6f));
        var dark = Toon("WatchCore", new Color(0.05f, 0.05f, 0.07f));
        Prim(PrimitiveType.Cube, "Strap", w, new Vector3(0, -0.006f, 0), new Vector3(0.095f, 0.008f, 0.038f), strap, false);
        Prim(PrimitiveType.Cylinder, "Case", w, Vector3.zero, new Vector3(0.056f, 0.008f, 0.056f), casing, false);
        face = Prim(PrimitiveType.Cylinder, "Face", w, new Vector3(0, 0.0085f, 0), new Vector3(0.045f, 0.001f, 0.045f),
                    Toon("WatchFace", new Color(0.15f, 1f, 0.8f), 1f), false).GetComponent<Renderer>();
        var c = Prim(PrimitiveType.Cube, "Core", w, new Vector3(0, 0.0105f, 0), new Vector3(0.019f, 0.003f, 0.019f), dark, false);
        c.transform.localRotation = Quaternion.Euler(0, 45, 0);
        core = c.transform;
        glow = Prim(PrimitiveType.Sphere, "CoreGlow", w, new Vector3(0, 0.012f, 0), new Vector3(0.03f, 0.008f, 0.03f),
                    Glow("WatchGlow", new Color(0.15f, 1f, 0.8f, 0.8f)), false).GetComponent<Renderer>();
        for (int s = -1; s <= 1; s += 2)
            Prim(PrimitiveType.Cube, "Button", w, new Vector3(0.03f * s, 0.002f, 0.012f), new Vector3(0.008f, 0.006f, 0.01f), casing, false);
    }

    // ------------------------------------------------------------------ alien library

    const string AlienMeshDir = Gen + "/Aliens";

    /// <summary>Builds every alien's model (see XenoDialAliens) under <paramref name="parent"/>, in roster order.</summary>
    static GameObject[] BuildLibrary(List<XenoDialAliens.Alien> roster, Transform parent, Material mat)
    {
        if (AssetDatabase.IsValidFolder(AlienMeshDir)) AssetDatabase.DeleteAsset(AlienMeshDir);
        AssetDatabase.CreateFolder(Gen, "Aliens");
        var result = new GameObject[roster.Count];
        try
        {
            for (int i = 0; i < roster.Count; i++)
            {
                var a = roster[i];
                EditorUtility.DisplayProgressBar("XenoDial", "Growing aliens... " + a.name, (float)i / roster.Count);
                var root = new GameObject($"{i + 1:000} {a.name}");
                root.transform.SetParent(parent, false);
                string path = $"{AlienMeshDir}/{i + 1:000}_{new string(a.name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray())}.asset";
                bool first = true;
                foreach (var piece in XenoDialAliens.Generate(a))
                {
                    if (first) { AssetDatabase.CreateAsset(piece.mesh, path); first = false; }
                    else AssetDatabase.AddObjectToAsset(piece.mesh, path);
                    var go = new GameObject(piece.name);
                    go.transform.SetParent(root.transform, false);
                    go.transform.localPosition = piece.origin;
                    go.AddComponent<MeshFilter>().sharedMesh = piece.mesh;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = mat;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                    r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                }
                result[i] = root;
            }
        }
        finally { EditorUtility.ClearProgressBar(); }
        AssetDatabase.SaveAssets();
        return result;
    }

    // ------------------------------------------------------------------ alien bodies

    static XenoBody BuildBodyTemplate(Transform parent, Dictionary<string, AudioClip> clips, GameObject[] library,
                                      List<XenoDialAliens.Alien> roster)
    {
        var go = new GameObject("XenoBody (per player)");
        go.transform.SetParent(parent, false);
        go.AddComponent(FindType("VRCPlayerObject"));
        var body = go.AddComponent<XenoBody>();
        Setup(body);

        var modelRoot = new GameObject("Model").transform;
        modelRoot.SetParent(go.transform, false);
        var voice = modelRoot.gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 1f;
        voice.minDistance = 3f;
        voice.maxDistance = 40f;
        voice.rolloffMode = AudioRolloffMode.Linear;
        voice.volume = 0.8f;

        var wrist = new GameObject("WristWatch").transform;
        wrist.SetParent(go.transform, false);
        Transform c; Renderer f, g;
        WatchModel(wrist, out c, out f, out g);

        var fx = new GameObject("Effects").transform;
        fx.SetParent(go.transform, false);
        var flash = Prim(PrimitiveType.Sphere, "Flash", fx, Vector3.zero, Vector3.one, Glow("Flash", new Color(0.2f, 1f, 0.85f, 0.9f)), false);
        var fire = Prim(PrimitiveType.Sphere, "Fireball", fx, Vector3.zero, Vector3.one * 0.35f, Toon("Fireball", new Color(1f, 0.6f, 0.1f), 1f), false);
        Prim(PrimitiveType.Sphere, "Halo", fire.transform, Vector3.zero, Vector3.one * 1.8f, Glow("FireHalo", new Color(1f, 0.35f, 0.05f, 0.8f)), false);
        var burst = Prim(PrimitiveType.Sphere, "Burst", fx, Vector3.zero, Vector3.one, Glow("Burst", new Color(1f, 0.45f, 0.1f, 0.9f)), false);
        var ring = Prim(PrimitiveType.Cylinder, "Ring", fx, Vector3.zero, Vector3.one, Glow("Ring", new Color(1f, 1f, 1f, 0.8f)), false);
        var trail = Prim(PrimitiveType.Capsule, "Trail", fx, Vector3.zero, Vector3.one, Glow("Trail", new Color(0.3f, 0.7f, 1f, 0.8f)), false);
        var crystalMat = Toon("CrystalPlatform", new Color(1f, 0.5f, 0.92f), 0.45f);
        var crystals = new Transform[8];
        for (int i = 0; i < crystals.Length; i++)
        {
            var cr = new GameObject("Crystal " + i).transform;
            cr.SetParent(fx, false);
            cr.gameObject.AddComponent<BoxCollider>().size = new Vector3(1.5f, 0.25f, 1.5f);
            Prim(PrimitiveType.Cube, "Slab", cr, Vector3.zero, new Vector3(1.5f, 0.25f, 1.5f), crystalMat, false).transform.localRotation = Quaternion.identity;
            var shard = Prim(PrimitiveType.Cube, "Shard", cr, new Vector3(0.45f, 0.3f, 0.45f), new Vector3(0.18f, 0.5f, 0.18f), crystalMat, false);
            shard.transform.localRotation = Quaternion.Euler(15, 45, 10);
            Prim(PrimitiveType.Cube, "Under", cr, new Vector3(0, -0.35f, 0), new Vector3(0.6f, 0.5f, 0.6f), crystalMat, false)
                .transform.localRotation = Quaternion.Euler(45, 0, 45);
            crystals[i] = cr;
        }

        body.modelRoot = modelRoot;
        body.library = library;
        body.glowColors = roster.Select(x => x.glow).ToArray();
        body.wristWatch = wrist;
        body.flash = flash.GetComponent<Renderer>();
        body.fireball = fire.transform;
        body.fireballHalo = fire.transform.GetChild(0).GetComponent<Renderer>();
        body.burst = burst.GetComponent<Renderer>();
        body.ring = ring.transform;
        body.trail = trail.transform;
        body.crystals = crystals;
        body.voice = voice;
        // indexed by power type: fire slam dash flap hop crystal lightning blink thrust grapple launch spin blast
        body.powerClips = new[] { clips["fire"], clips["slam"], clips["dash"], clips["flap"], clips["hop"], clips["crystal"],
                                  clips["lightning"], clips["blink"], clips["thrust"], clips["grapple"], clips["launch"],
                                  clips["spin"], clips["blast"] };
        body.shiftClip = clips["shift"];
        body.revertClip = clips["revert"];
        return body;
    }

    // ------------------------------------------------------------------ areas

    static void BuildCrashSite(Transform env, Transform root, XenoDialWatch watch, Material font, Material teal, Material board)
    {
        var at = new Vector3(0, 0, 9);
        var scorch = Toon("Scorch", new Color(0.2f, 0.17f, 0.14f));
        var hull = Toon("PodHull", new Color(0.62f, 0.65f, 0.7f));
        var hullDark = Toon("PodDark", new Color(0.18f, 0.2f, 0.24f));
        var rubble = Toon("Rubble", new Color(0.4f, 0.33f, 0.25f));
        Prim(PrimitiveType.Cylinder, "Crater", env, at + new Vector3(0, 0.012f, 0), new Vector3(8, 0.01f, 8), scorch, false);
        for (int i = 0; i < 16; i++)
        {
            float a = i * Mathf.PI * 2 / 16;
            var r = Prim(PrimitiveType.Sphere, "CraterRim", env, at + new Vector3(Mathf.Cos(a) * 4.1f, 0.05f, Mathf.Sin(a) * 4.1f),
                         new Vector3(1.6f, 0.55f + 0.2f * Mathf.Sin(i * 2.3f), 1.1f), rubble, false);
            r.transform.localRotation = Quaternion.Euler(0, 90 - a * Mathf.Rad2Deg, 0);
        }
        // Skid mark the pod dug when it landed
        Prim(PrimitiveType.Cube, "Skid", env, at + new Vector3(0, 0.015f, 9), new Vector3(2.2f, 0.01f, 14), scorch, false);

        var pod = new GameObject("Pod").transform;
        pod.SetParent(root, false);
        pod.localPosition = at;
        var shell = new GameObject("Shell").transform;
        shell.SetParent(pod, false);
        shell.localPosition = new Vector3(0, 0.55f, 0);
        shell.localRotation = Quaternion.Euler(18, 25, 12);
        Prim(PrimitiveType.Sphere, "Hull", shell, Vector3.zero, new Vector3(1.9f, 1.5f, 2.6f), hull, false);
        Prim(PrimitiveType.Sphere, "Hatch", shell, new Vector3(0, 0.45f, 0.35f), new Vector3(1.1f, 0.6f, 1.2f), hullDark, false);
        for (int i = -1; i <= 1; i++)
            Prim(PrimitiveType.Cube, "Stripe", shell, new Vector3(0.95f * Mathf.Sign(i + 0.5f), 0.1f * i, 0), new Vector3(0.05f, 0.08f, 2.1f), teal, false);
        Prim(PrimitiveType.Cylinder, "Fin", shell, new Vector3(0, 0, -1.25f), new Vector3(0.9f, 0.15f, 0.9f), hullDark, false)
            .transform.localRotation = Quaternion.Euler(90, 0, 0);
        var col = pod.gameObject.AddComponent<SphereCollider>();
        col.center = new Vector3(0, 0.9f, 0);
        col.radius = 1.4f;

        var display = new GameObject("FloatingWatch").transform;
        display.SetParent(pod, false);
        display.localPosition = new Vector3(0, 1.9f, 0);
        var big = new GameObject("Big").transform;
        big.SetParent(display, false);
        big.localScale = Vector3.one * 4f;
        Transform c; Renderer f, g;
        WatchModel(big, out c, out f, out g);
        Prim(PrimitiveType.Sphere, "Beam", display, Vector3.zero, Vector3.one * 0.5f, Glow("PodBeam", new Color(0.15f, 1f, 0.8f, 0.35f)), false);

        var p = pod.gameObject.AddComponent<XenoPod>();
        Setup(p);
        p.watch = watch;
        p.displayWatch = display.gameObject;
        var udon = Backing(p);
        if (udon != null) { udon.interactText = "Take the XenoDial"; udon.proximity = 4f; }

        Board("PodSign", env, at + new Vector3(3.2f, 1.4f, -3.4f), new Vector2(2.2f, 1.25f), new Vector3(0, 1.5f, -1), board, font,
              "<b><color=#2effcc>THE XENODIAL</color></b>\n<size=65%>Something crashed here. Use the pod to take the watch.\n\n" +
              "<b>VR:</b> tap the core on your left wrist with your right index finger, tap an alien, tap the core again.\n" +
              "<b>Desktop:</b> Q dial, 1-6 pick, F transform. Click or F uses your power, Q changes back.</size>");
    }

    static TextMeshPro BuildArena(Transform env, Transform root, Vector3 at, Material font, Material board,
                                  List<XenoTarget> targets, Dictionary<string, AudioClip> clips)
    {
        var sand = Toon("ArenaSand", new Color(0.78f, 0.67f, 0.46f));
        var wall = Toon("ArenaWall", new Color(0.55f, 0.45f, 0.35f));
        var red = Toon("TargetRed", new Color(0.92f, 0.18f, 0.18f));
        var white = Toon("TargetWhite", new Color(0.95f, 0.95f, 0.95f));
        var eye = Toon("TargetEye", new Color(1f, 0.85f, 0.2f), 1f);
        var boulder = Toon("Boulder", new Color(0.52f, 0.44f, 0.36f));
        var redDebris = Glow("TargetDebris", new Color(1f, 0.4f, 0.15f, 0.9f));
        var rockDebris = Glow("RockDebris", new Color(0.85f, 0.7f, 0.5f, 0.9f));

        Prim(PrimitiveType.Cylinder, "ArenaFloor", env, at + new Vector3(0, 0.01f, 0), new Vector3(34, 0.01f, 34), sand, false);
        for (int i = 0; i < 28; i++)
        {
            float a = i * Mathf.PI * 2 / 28;
            if (Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, 180)) < 20) continue;   // entrance facing the crash site
            var w = Prim(PrimitiveType.Cube, "ArenaWall", env, at + new Vector3(Mathf.Cos(a) * 17.2f, 0.7f, Mathf.Sin(a) * 17.2f),
                         new Vector3(4f, 1.4f, 0.6f), wall, true);
            w.transform.localRotation = Quaternion.Euler(0, 90 - a * Mathf.Rad2Deg, 0);
        }
        var holder = new GameObject("Targets").transform;
        holder.SetParent(root, false);

        // Floating targets at the far side, for fireballs
        for (int i = 0; i < 9; i++)
        {
            var pos = at + new Vector3(7f + (i % 3) * 2.8f, 1.6f + (i / 3) * 1.6f, -6f + i * 1.5f);
            var t = Target("Target " + i, holder, pos, 0.55f, true, redDebris, clips["smash"]);
            Prim(PrimitiveType.Sphere, "Ball", t.visual.transform, Vector3.zero, Vector3.one * 0.95f, red, false);
            Prim(PrimitiveType.Cylinder, "Band", t.visual.transform, Vector3.zero, new Vector3(1.02f, 0.06f, 1.02f), white, false);
            Prim(PrimitiveType.Sphere, "Eye", t.visual.transform, new Vector3(0, 0.12f, 0.4f), Vector3.one * 0.25f, eye, false);
            t.hitRadius = 0.9f;
            targets.Add(t);
        }
        // Boulders in the middle, for slams and dashes
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI * 2 / 8 + 0.3f;
            var pos = at + new Vector3(Mathf.Cos(a) * 5f - 3f, 0.6f, Mathf.Sin(a) * 5f);
            var t = Target("Boulder " + i, holder, pos, 0.75f, false, rockDebris, clips["smash"]);
            var v = Prim(PrimitiveType.Sphere, "Rock", t.visual.transform, Vector3.zero, new Vector3(1.6f, 1.25f, 1.4f), boulder, false);
            v.transform.localRotation = Quaternion.Euler(10 * i, 37 * i, 0);
            t.hitRadius = 1.1f;
            targets.Add(t);
        }
        var score = Board("ArenaScore", root, at + new Vector3(-15f, 2.2f, -6f), new Vector2(2.4f, 1.3f), at + new Vector3(-30, 2.2f, -6f), board, font, "");
        Board("ArenaInfo", env, at + new Vector3(-15f, 2.2f, 6f), new Vector2(2.4f, 1.3f), at + new Vector3(-30, 2.2f, 6f), board, font,
              "<b>TRAINING ARENA</b>\n<size=60%>Fireball the floating drones (Emberjack). Slam the boulders (Bedrock). " +
              "Dash straight through them (Zipwire). Everything rebuilds itself.</size>");
        return score;
    }

    static XenoTarget Target(string name, Transform parent, Vector3 pos, float colRadius, bool hover, Material debris, AudioClip smash)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var col = go.AddComponent<SphereCollider>();
        col.radius = colRadius;
        var visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        var d = Prim(PrimitiveType.Sphere, "Debris", go.transform, Vector3.zero, Vector3.one, debris, false);
        var audio = go.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.clip = smash;
        audio.spatialBlend = 1f;
        audio.minDistance = 4f;
        audio.maxDistance = 45f;
        audio.rolloffMode = AudioRolloffMode.Linear;
        var t = go.AddComponent<XenoTarget>();
        Setup(t);
        t.visual = visual;
        t.hitCollider = col;
        t.debris = d.GetComponent<Renderer>();
        t.sound = audio;
        t.hover = hover;
        return t;
    }

    static void BuildTower(Transform env, Transform root, Vector3 at, Material font, Material board, Material rock,
                           Dictionary<string, AudioClip> clips)
    {
        const float H = 24f;
        var stone = Toon("TowerStone", new Color(0.42f, 0.47f, 0.55f));
        var ledge = Toon("TowerLedge", new Color(0.62f, 0.66f, 0.72f));
        var gold = Toon("Gold", new Color(1f, 0.78f, 0.25f), 0.3f);
        Prim(PrimitiveType.Cube, "Pillar", env, at + new Vector3(0, H / 2, 0), new Vector3(5, H, 5), stone, true);
        Prim(PrimitiveType.Cylinder, "Summit", env, at + new Vector3(0, H + 0.15f, 0), new Vector3(9, 0.15f, 9), ledge, true);
        // Ledges spiral up the tower, 1.5 m apart: too far for a normal jump, fine for Bedrock, Skyfin and Shardling.
        int n = Mathf.FloorToInt((H - 1f) / 1.5f);
        for (int i = 0; i < n; i++)
        {
            int side = i % 4;
            float y = 1.2f + i * 1.5f;
            float along = ((i / 4) % 2 == 0 ? -1.2f : 1.2f);
            Vector3 off = side == 0 ? new Vector3(along, y, -3.2f) : side == 1 ? new Vector3(3.2f, y, along)
                        : side == 2 ? new Vector3(-along, y, 3.2f) : new Vector3(-3.2f, y, -along);
            Prim(PrimitiveType.Cube, "Ledge " + i, env, at + off, new Vector3(side % 2 == 0 ? 1.8f : 1.5f, 0.3f, side % 2 == 0 ? 1.5f : 1.8f), ledge, true);
        }
        // The bell on top
        var frame = at + new Vector3(0, H + 0.3f, 0);
        for (int s = -1; s <= 1; s += 2)
            Prim(PrimitiveType.Cube, "BellPost", env, frame + new Vector3(1.1f * s, 1.4f, 0), new Vector3(0.2f, 2.8f, 0.2f), stone, true);
        Prim(PrimitiveType.Cube, "BellBeam", env, frame + new Vector3(0, 2.85f, 0), new Vector3(2.5f, 0.25f, 0.3f), stone, false);
        var bellGo = new GameObject("Bell");
        bellGo.transform.SetParent(root, false);
        bellGo.transform.localPosition = frame + new Vector3(0, 2.7f, 0);
        var bell = new GameObject("Swing").transform;
        bell.SetParent(bellGo.transform, false);
        Prim(PrimitiveType.Cylinder, "Cup", bell, new Vector3(0, -0.6f, 0), new Vector3(0.9f, 0.45f, 0.9f), gold, false);
        Prim(PrimitiveType.Sphere, "Dome", bell, new Vector3(0, -0.2f, 0), new Vector3(0.85f, 0.5f, 0.85f), gold, false);
        var bc = bellGo.AddComponent<BoxCollider>();
        bc.center = new Vector3(0, -0.6f, 0);
        bc.size = new Vector3(1f, 1.1f, 1f);
        var src = bellGo.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.clip = clips["bell"];
        src.spatialBlend = 0.6f;
        src.minDistance = 30f;
        src.maxDistance = 200f;
        var xb = bellGo.AddComponent<XenoBell>();
        Setup(xb);
        xb.sound = src;
        xb.bell = bell;
        var udon = Backing(xb);
        if (udon != null) { udon.interactText = "Ring the bell"; udon.proximity = 3f; }

        Board("TowerSign", env, at + new Vector3(0, 2f, -4.3f), new Vector2(2.6f, 1.1f), at + new Vector3(0, 2f, -12f), board, font,
              "<b>SKY TOWER</b>\n<size=60%>Ring the bell at the top. The ledges are too far apart for a human. " +
              "Try Bedrock's jump, Skyfin's wings or Shardling's crystals.</size>");
    }

    static void BuildRaceTrack(Transform env, Transform root, Vector3 at, Material font, Material board, Material post,
                               Dictionary<string, AudioClip> clips)
    {
        const float A = 24f, B = 14f, W = 4.5f;
        const int N = 64;
        var asphalt = Toon("Asphalt", new Color(0.17f, 0.17f, 0.19f));
        var curbR = Toon("CurbRed", new Color(0.85f, 0.15f, 0.15f));
        var curbW = Toon("CurbWhite", new Color(0.95f, 0.95f, 0.95f));
        var neon = Toon("Neon", new Color(0.2f, 0.65f, 1f), 1f);
        Vector3 P(float t) => at + new Vector3(Mathf.Cos(t) * A, 0, Mathf.Sin(t) * B);
        for (int i = 0; i < N; i++)
        {
            float t0 = i * Mathf.PI * 2 / N, t1 = (i + 1) * Mathf.PI * 2 / N;
            Vector3 p0 = P(t0), p1 = P(t1);
            Vector3 mid = (p0 + p1) / 2, dir = (p1 - p0);
            var rot = Quaternion.LookRotation(dir.normalized);
            var seg = Prim(PrimitiveType.Cube, "Track", env, mid + new Vector3(0, 0.02f, 0), new Vector3(W, 0.02f, dir.magnitude + 0.15f), asphalt, false);
            seg.transform.localRotation = rot;
            for (int s = -1; s <= 1; s += 2)
            {
                var c = Prim(PrimitiveType.Cube, "Curb", env, mid + rot * new Vector3(s * (W / 2 + 0.2f), 0.06f, 0),
                             new Vector3(0.4f, 0.12f, dir.magnitude + 0.1f), i % 2 == 0 ? curbR : curbW, false);
                c.transform.localRotation = rot;
            }
        }
        var timerGo = new GameObject("RaceTimer");
        timerGo.transform.SetParent(root, false);
        var snd = timerGo.AddComponent<AudioSource>();
        snd.playOnAwake = false;
        snd.spatialBlend = 0f;
        var timer = timerGo.AddComponent<XenoRaceTimer>();
        Setup(timer);
        timer.startClip = clips["beep"];
        timer.finishClip = clips["win"];
        timer.sound = snd;
        for (int g = 0; g < 2; g++)
        {
            float t = g == 0 ? 0f : Mathf.PI;
            Vector3 p = P(t);
            var gate = new GameObject(g == 0 ? "StartGate" : "HalfwayGate");
            gate.transform.SetParent(root, false);
            gate.transform.localPosition = p;
            for (int s = -1; s <= 1; s += 2)
                Prim(PrimitiveType.Cube, "Post", gate.transform, new Vector3(s * (W / 2 + 0.6f), 2f, 0), new Vector3(0.35f, 4f, 0.35f), post, false);
            Prim(PrimitiveType.Cube, "Bar", gate.transform, new Vector3(0, 4f, 0), new Vector3(W + 1.6f, 0.4f, 0.3f), neon, false);
            if (g == 0)
                Prim(PrimitiveType.Cube, "Line", gate.transform, new Vector3(0, 0.035f, 0), new Vector3(W, 0.01f, 0.4f), curbW, false);
            var trig = gate.AddComponent<BoxCollider>();
            trig.isTrigger = true;
            trig.center = new Vector3(0, 2f, 0);
            trig.size = new Vector3(W + 1f, 4f, 0.8f);
            var rg = gate.AddComponent<XenoRaceGate>();
            Setup(rg);
            rg.timer = timer;
            rg.gateIndex = g;
        }
        timer.board = Board("RaceBoard", root, P(0) + new Vector3(4.5f, 2.6f, -6f), new Vector2(3f, 1.5f), P(0) + new Vector3(16f, 2.6f, -4f),
                            board, font, "SPEED TRACK");
    }

    static void BuildVault(Transform env, Transform root, Vector3 at, XenoDialWatch watch, Transform spawn, Material font, Material board,
                           Material rock, Material btn, Dictionary<string, AudioClip> clips)
    {
        // A rock mesa with a room inside. The only way in is a tunnel 45 cm tall.
        const float WX = 14f, WZ = 12f, H = 6f, T = 1f, HOLE_W = 0.55f, HOLE_H = 0.45f;
        var mesa = Toon("Mesa", new Color(0.55f, 0.42f, 0.33f));
        var inside = Toon("VaultInside", new Color(0.2f, 0.16f, 0.26f));
        var gem = Toon("VaultGem", new Color(0.45f, 1f, 0.3f), 0.8f);
        var gold = Toon("Gold", new Color(1f, 0.78f, 0.25f), 0.3f);
        float front = at.z + WZ / 2;   // the face towards the crash site
        // front wall in three pieces around the hole
        float side = (WX - HOLE_W) / 2;
        Prim(PrimitiveType.Cube, "FrontL", env, new Vector3(at.x - HOLE_W / 2 - side / 2, H / 2, front - T / 2), new Vector3(side, H, T), mesa, true);
        Prim(PrimitiveType.Cube, "FrontR", env, new Vector3(at.x + HOLE_W / 2 + side / 2, H / 2, front - T / 2), new Vector3(side, H, T), mesa, true);
        Prim(PrimitiveType.Cube, "FrontTop", env, new Vector3(at.x, HOLE_H + (H - HOLE_H) / 2, front - T / 2), new Vector3(HOLE_W, H - HOLE_H, T), mesa, true);
        Prim(PrimitiveType.Cube, "Back", env, new Vector3(at.x, H / 2, at.z - WZ / 2 + T / 2), new Vector3(WX, H, T), mesa, true);
        Prim(PrimitiveType.Cube, "Left", env, new Vector3(at.x - WX / 2 + T / 2, H / 2, at.z), new Vector3(T, H, WZ), mesa, true);
        Prim(PrimitiveType.Cube, "Right", env, new Vector3(at.x + WX / 2 - T / 2, H / 2, at.z), new Vector3(T, H, WZ), mesa, true);
        Prim(PrimitiveType.Cube, "Roof", env, new Vector3(at.x, H + 0.5f, at.z), new Vector3(WX + 1f, 1f, WZ + 1f), mesa, true);
        Prim(PrimitiveType.Cube, "Floor", env, new Vector3(at.x, 0.03f, at.z), new Vector3(WX - 2 * T, 0.06f, WZ - 2 * T), inside, true);
        // inner lining so it reads as a cave
        Prim(PrimitiveType.Cube, "Lining", env, new Vector3(at.x, H - 0.02f, at.z), new Vector3(WX - 2 * T, 0.02f, WZ - 2 * T), inside, false);
        Prim(PrimitiveType.Cube, "Tunnel", env, new Vector3(at.x, 0.005f, front - T / 2), new Vector3(HOLE_W, 0.01f, T), inside, false);
        for (int i = 0; i < 10; i++)
        {
            float a = i * 0.7f;
            var g = Prim(PrimitiveType.Cube, "Gem", env, new Vector3(at.x + Mathf.Cos(a) * 4.5f, 0.5f + (i % 3) * 0.3f, at.z + Mathf.Sin(a) * 3.8f),
                         new Vector3(0.3f, 1f + (i % 3) * 0.4f, 0.3f), gem, false);
            g.transform.localRotation = Quaternion.Euler(10 * (i % 3) - 10, i * 33, 15 * (i % 2) - 7);
        }
        // the prize
        Prim(PrimitiveType.Cylinder, "Plinth", env, new Vector3(at.x, 0.5f, at.z - 2f), new Vector3(1.2f, 0.5f, 1.2f), rock, true);
        Prim(PrimitiveType.Cylinder, "CupBase", env, new Vector3(at.x, 1.05f, at.z - 2f), new Vector3(0.4f, 0.05f, 0.4f), gold, false);
        Prim(PrimitiveType.Cylinder, "CupStem", env, new Vector3(at.x, 1.25f, at.z - 2f), new Vector3(0.1f, 0.2f, 0.1f), gold, false);
        Prim(PrimitiveType.Sphere, "Cup", env, new Vector3(at.x, 1.6f, at.z - 2f), new Vector3(0.6f, 0.5f, 0.6f), gold, false);
        Board("VaultText", env, new Vector3(at.x, 3.2f, at.z - 4.4f), new Vector2(4f, 1.4f), new Vector3(at.x, 3.2f, at.z + 6f), board, font,
              "<b><color=#7dff5a>THE PIP VAULT</color></b>\n<size=60%>Only the very small ever find this room. You did.</size>");

        var inSpot = new GameObject("VaultArrival").transform;
        inSpot.SetParent(root, false);
        inSpot.localPosition = new Vector3(at.x, 0.1f, front - T - 1.2f);
        inSpot.localRotation = Quaternion.Euler(0, 180, 0);

        var door = new GameObject("TunnelMouth");
        door.transform.SetParent(root, false);
        door.transform.localPosition = new Vector3(at.x, 0.5f, front + 0.4f);
        var trig = door.AddComponent<BoxCollider>();
        trig.isTrigger = true;
        trig.size = new Vector3(1.6f, 1f, 1.2f);
        var snd = door.AddComponent<AudioSource>();
        snd.playOnAwake = false;
        snd.clip = clips["win"];
        snd.spatialBlend = 0f;
        var vd = door.AddComponent<XenoVaultDoor>();
        Setup(vd);
        vd.watch = watch;
        vd.destination = inSpot;
        vd.sound = snd;

        var exit = MakeButton("Leave the vault", root, new Vector3(at.x + 2.5f, 1.2f, front - T - 0.05f), new Vector3(1.2f, 0.35f, 0.1f), btn, font);
        exit.teleportTo = spawn;

        Board("VaultSign", env, new Vector3(at.x + 2.2f, 1.6f, front + 0.3f), new Vector2(2.4f, 0.9f), new Vector3(at.x + 2.2f, 1.6f, front + 8f), board, font,
              "<b>???</b>\n<size=60%>There's a tunnel down here. It's far too small to fit through.</size>");
    }

    static void BuildMirror(Transform env, Transform root, Vector3 at, Material font, Material btn, Material board)
    {
        var frame = Toon("MirrorFrame", new Color(0.25f, 0.2f, 0.16f));
        var holder = new GameObject("Mirror").transform;
        holder.SetParent(root, false);
        holder.localPosition = at;
        holder.localRotation = Quaternion.Euler(0, -90, 0);   // faces +X, toward the spawn
        Prim(PrimitiveType.Cube, "Frame", holder, new Vector3(0, 1.6f, 0.06f), new Vector3(4.3f, 3.1f, 0.1f), frame, true);
        var glass = Prim(PrimitiveType.Quad, "Glass", holder, new Vector3(0, 1.6f, 0f), new Vector3(4f, 2.8f, 1f), Toon("MirrorOff", new Color(0.1f, 0.12f, 0.14f)), false);
        var mirror = Prim(PrimitiveType.Quad, "MirrorSurface", holder, new Vector3(0, 1.6f, -0.01f), new Vector3(4f, 2.8f, 1f), Toon("MirrorTmp", Color.white), false);
        mirror.AddComponent<VRCMirrorReflection>();
        mirror.SetActive(false);
        var b = MakeButton("Mirror on/off", holder, new Vector3(2.6f, 1.2f, -0.05f), new Vector3(0.7f, 0.3f, 0.08f), btn, font);
        b.toggle = mirror;
        Text("MirrorHint", holder, new Vector3(0, 3.35f, -0.02f), new Vector2(4f, 0.35f), 0.6f, font, "See your alien form here");
    }

    static void BuildSigns(Transform env, Material font, Material board, Material post)
    {
        var hub = new Vector3(3.6f, 0, 1.2f);
        Prim(PrimitiveType.Cylinder, "SignPost", env, hub + new Vector3(0, 1.2f, 0), new Vector3(0.15f, 1.2f, 0.15f), post, true);
        string[] labels = { "Training Arena  >", "<  Speed Track", "Sky Tower  ^", "Strange tunnel  v", "Alien Codex  v<" };
        for (int i = 0; i < labels.Length; i++)
            Board("Sign" + i, env, hub + new Vector3(0, 2.3f - i * 0.36f, -0.1f), new Vector2(1.9f, 0.3f), new Vector3(0, 2.3f - i * 0.36f, -4f), board, font, labels[i]);
    }

    /// <summary>The Alien Codex: every alien as a numbered statue, ten to a row, in dial order.</summary>
    static void BuildGallery(Transform env, Vector3 at, List<XenoDialAliens.Alien> roster, GameObject[] library,
                             Material font, Material board, Material rock)
    {
        const float DX = 4.2f, DZ = 4.6f;
        int rows = (roster.Count + PAGE - 1) / PAGE;
        var tiles = Toon("CodexTiles", new Color(0.62f, 0.6f, 0.56f));
        var g = new GameObject("Alien Codex").transform;
        g.SetParent(env, false);
        Prim(PrimitiveType.Cube, "Plaza", g, at + new Vector3(0, 0.02f, 0), new Vector3(PAGE * DX + 4, 0.04f, rows * DZ + 6), tiles, false);
        for (int i = 0; i < roster.Count; i++)
        {
            var a = roster[i];
            int row = i / PAGE, col = i % PAGE;
            var p = at + new Vector3((col - (PAGE - 1) / 2f) * DX, 0, ((rows - 1) / 2f - row) * DZ);
            Prim(PrimitiveType.Cylinder, "Pedestal", g, p + new Vector3(0, 0.15f, 0), new Vector3(1.7f, 0.15f, 1.7f), rock, true);
            var statue = Object.Instantiate(library[i], g);
            statue.name = "Statue " + a.name;
            statue.transform.position = p + new Vector3(0, 0.3f, 0);
            statue.transform.rotation = Quaternion.identity;
            statue.transform.localScale = Vector3.one * XenoDialAliens.StatueScale(a);
            statue.SetActive(true);
            var lp = p + new Vector3(0, 0.55f, 1.2f);
            Board("Plaque " + (i + 1), g, lp, new Vector2(2f, 0.55f), lp + new Vector3(0, 0, 5), board, font,
                  $"<b>#{i + 1} {a.name}</b>\n<size=70%>{a.Description}</size>");
        }
        var top = at + new Vector3(0, 3.4f, (rows * DZ + 6) / 2f + 0.5f);
        Board("CodexSign", g, top, new Vector2(6f, 1.6f), top + new Vector3(0, 0, 10), board, font,
              $"<b><color=#2effcc>ALIEN CODEX</color></b>\n<size=60%>All {roster.Count} forms in the XenoDial, in dial order " +
              "(ten to a page). Find one you like and dial its number.</size>");
    }

    static void BuildScenery(Transform env, Material rock, Material bark)
    {
        var leaves = new[] { Toon("Leaves1", new Color(0.18f, 0.42f, 0.2f)), Toon("Leaves2", new Color(0.24f, 0.5f, 0.22f)), Toon("Leaves3", new Color(0.3f, 0.45f, 0.18f)) };
        var hill = Toon("Hills", new Color(0.35f, 0.47f, 0.42f));
        var rnd = new System.Random(10);
        float R() => (float)rnd.NextDouble();
        // Keep the play areas clear.
        var clear = new[] { (new Vector3(0, 0, 3), 16f), (new Vector3(42, 0, 0), 20f), (new Vector3(-52, 0, 0), 29f),
                            (new Vector3(0, 0, 48), 12f), (new Vector3(0, 0, -40), 11f), (new Vector3(-45, 0, -62), 36f), (new Vector3(0, 0, -20), 4f),
                            (new Vector3(20, 0, 0), 4f), (new Vector3(-20, 0, 0), 4f), (new Vector3(0, 0, 25), 4f) };
        int placed = 0;
        for (int tries = 0; placed < 170 && tries < 3000; tries++)
        {
            var p = new Vector3(R() * 180 - 90, 0, R() * 180 - 90);
            if (clear.Any(c => (p - c.Item1).magnitude < c.Item2)) continue;
            if (Mathf.Abs(p.x) < 3.5f && p.z < 0 && p.z > -28) continue;   // path to the vault
            if (Mathf.Abs(p.z) < 3.5f && Mathf.Abs(p.x) < 26) continue;    // paths east and west
            if (Mathf.Abs(p.x) < 3.5f && p.z > 0 && p.z < 38) continue;    // path north
            float h = 2.2f + R() * 2.5f, sz = 1.8f + R() * 1.8f;
            var tree = new GameObject("Tree").transform;
            tree.SetParent(env, false);
            tree.localPosition = p;
            Prim(PrimitiveType.Cylinder, "Trunk", tree, new Vector3(0, h / 2, 0), new Vector3(0.35f, h / 2, 0.35f), bark, true);
            var m = leaves[rnd.Next(leaves.Length)];
            Prim(PrimitiveType.Sphere, "Crown", tree, new Vector3(0, h + sz * 0.35f, 0), new Vector3(sz * 1.3f, sz, sz * 1.3f), m, false);
            Prim(PrimitiveType.Sphere, "Crown2", tree, new Vector3(sz * 0.3f, h + sz * 0.8f, -sz * 0.1f), Vector3.one * sz * 0.8f, m, false);
            placed++;
        }
        for (int i = 0; i < 40; i++)
        {
            var p = new Vector3(R() * 180 - 90, 0, R() * 180 - 90);
            if (clear.Any(c => (p - c.Item1).magnitude < c.Item2)) continue;
            var r = Prim(PrimitiveType.Sphere, "Rock", env, p, new Vector3(0.8f + R() * 1.5f, 0.5f + R(), 0.8f + R() * 1.5f), rock, true);
            r.transform.localRotation = Quaternion.Euler(0, R() * 360, 0);
        }
        for (int i = 0; i < 18; i++)   // far hills on the horizon
        {
            float a = i * Mathf.PI * 2 / 18 + R() * 0.2f, d = 135 + R() * 25;
            Prim(PrimitiveType.Sphere, "Hill", env, new Vector3(Mathf.Cos(a) * d, -6, Mathf.Sin(a) * d),
                 new Vector3(70 + R() * 40, 30 + R() * 30, 50 + R() * 20), hill, false);
        }
    }

    static void Bounds(Transform env)
    {
        for (int i = 0; i < 4; i++)
        {
            var w = new GameObject("Boundary" + i);
            w.transform.SetParent(env, false);
            w.transform.localRotation = Quaternion.Euler(0, i * 90, 0);
            w.transform.localPosition = w.transform.localRotation * new Vector3(0, 25, 100);
            w.AddComponent<BoxCollider>().size = new Vector3(200, 50, 1);
        }
    }

    // ------------------------------------------------------------------ scene setup

    static void SetupDescriptor(Transform spawn)
    {
        var desc = Object.FindObjectOfType<VRCSceneDescriptor>();
        if (desc == null) desc = new GameObject("VRCWorld").AddComponent<VRCSceneDescriptor>();
        desc.spawns = new[] { spawn };
        desc.RespawnHeightY = -30f;
        var camGo = new GameObject("ReferenceCamera");
        camGo.transform.SetParent(spawn.parent, false);
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.01f;   // Pip is very small
        cam.farClipPlane = 600f;
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.enabled = false;
        desc.ReferenceCamera = camGo;
        EditorUtility.SetDirty(desc);
    }

    static void SetupSky()
    {
        string path = Gen + "/Sky.mat";
        var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("Skybox/Procedural");
        if (sky == null && shader != null) { sky = new Material(shader); AssetDatabase.CreateAsset(sky, path); }
        if (sky != null)
        {
            sky.SetFloat("_SunSize", 0.05f);
            sky.SetFloat("_AtmosphereThickness", 1.1f);
            sky.SetColor("_SkyTint", new Color(0.45f, 0.6f, 0.75f));
            sky.SetColor("_GroundColor", new Color(0.35f, 0.4f, 0.35f));
            sky.SetFloat("_Exposure", 1.25f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;
        }
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 70f;
        RenderSettings.fogEndDistance = 220f;
        RenderSettings.fogColor = new Color(0.68f, 0.78f, 0.86f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.white;
    }

    static Dictionary<string, AudioClip> LoadClips()
    {
        var d = new Dictionary<string, AudioClip>();
        foreach (var n in new[] { "shift", "revert", "click", "open", "beep", "fire", "slam", "dash", "flap", "hop", "crystal", "smash", "bell", "win",
                                   "lightning", "blink", "thrust", "grapple", "launch", "spin", "blast" })
        {
            var c = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/" + n + ".wav");
            if (c == null) Debug.LogWarning("XenoDial: missing sound " + n);
            d[n] = c;
        }
        return d;
    }

    // ------------------------------------------------------------------ helpers

    static Type FindType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
        .FirstOrDefault(t => t.Name == name && typeof(Component).IsAssignableFrom(t));

    static Material Toon(string name, Color c, float emission = 0f)
    {
        var m = MatAsset(name, Shader.Find("XenoDial/Toon"));
        m.SetColor("_Color", c);
        m.SetFloat("_Emission", emission);
        return m;
    }

    static Material Glow(string name, Color c)
    {
        var m = MatAsset(name, Shader.Find("XenoDial/Glow"));
        m.SetColor("_Color", c);
        return m;
    }

    static Material MatAsset(string name, Shader shader)
    {
        string path = Gen + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
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

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool keepCollider)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        var col = go.GetComponent<Collider>();
        if (!keepCollider) Object.DestroyImmediate(col);
        else if (type == PrimitiveType.Cylinder)
        {
            // A capsule collider on a flat disc would be a dome; use the real shape.
            Object.DestroyImmediate(col);
            go.AddComponent<MeshCollider>();
        }
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return go;
    }

    static Transform Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3 euler = default)
    {
        var go = Prim(type, type.ToString(), parent, pos, scale, mat, false);
        go.transform.localRotation = Quaternion.Euler(euler);
        return go.transform;
    }

    static Transform Pivot(Transform parent, Vector3 pos)
    {
        var t = new GameObject("Pivot").transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        return t;
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
        t.fontSizeMin = 0.05f;
        t.fontSizeMax = fontMax;
        t.richText = true;
        t.color = Color.white;
        return t;
    }

    /// <summary>A dark sign facing the point <paramref name="viewer"/>.</summary>
    static TextMeshPro Board(string name, Transform parent, Vector3 pos, Vector2 size, Vector3 viewer, Material bg, Material fontMat, string text)
    {
        var holder = new GameObject(name).transform;
        holder.SetParent(parent, false);
        holder.localPosition = pos;
        var look = pos - viewer;
        look.y = 0;
        holder.localRotation = Quaternion.LookRotation(look.sqrMagnitude > 0.001f ? look : Vector3.forward);
        Prim(PrimitiveType.Quad, "Background", holder, new Vector3(0, 0, 0.01f), new Vector3(size.x + 0.15f, size.y + 0.15f, 1), bg, false);
        var back = Prim(PrimitiveType.Quad, "Back", holder, new Vector3(0, 0, 0.012f), new Vector3(size.x + 0.15f, size.y + 0.15f, 1), bg, false);
        back.transform.localRotation = Quaternion.Euler(0, 180, 0);
        return Text("Text", holder, Vector3.zero, size, Mathf.Min(1.2f, size.y * 0.45f), fontMat, text);
    }

    static XenoButton MakeButton(string label, Transform parent, Vector3 pos, Vector3 size, Material mat, Material fontMat)
    {
        var go = new GameObject("Button " + label);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.AddComponent<BoxCollider>().size = size;
        Prim(PrimitiveType.Cube, "Visual", go.transform, Vector3.zero, size, mat, false);
        Text("Label", go.transform, new Vector3(0, 0, -size.z / 2 - 0.003f), new Vector2(size.x * 0.92f, size.y * 0.85f), 0.5f, fontMat, label);
        var b = go.AddComponent<XenoButton>();
        Setup(b);
        var udon = Backing(b);
        if (udon != null) { udon.interactText = label; udon.proximity = 4f; }
        return b;
    }

    // ------------------------------------------------------------------ UdonSharp glue (reflection keeps this working across U# versions)

    static readonly string[] Scripts = { "XenoDialWatch", "XenoBody", "XenoTarget", "XenoRaceGate", "XenoRaceTimer",
                                         "XenoPod", "XenoBell", "XenoVaultDoor", "XenoButton" };

    static bool EnsureProgramAssets()
    {
        bool created = false;
        foreach (var s in Scripts)
        {
            string asset = Root + "/Scripts/" + s + ".asset";
            if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(asset) != null) continue;
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(Root + "/Scripts/" + s + ".cs");
            if (script == null) { Debug.LogError("XenoDial: missing script " + s); return false; }
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
