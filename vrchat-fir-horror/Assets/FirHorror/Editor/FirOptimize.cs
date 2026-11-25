#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UdonSharp;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Optimising without changing how anything looks or sounds:
///  * static batching for everything that never moves (and never for anything a script moves);
///  * a FirCuller that keeps only the Station area you're in awake (floors, walls and doors always stay on).
/// </summary>
public static partial class FirCheckBuilder
{
    public static readonly List<string> culledAreas = new List<string>();

    static readonly string[] ShellPrefixes = { "Floor", "Ceiling", "Wall", "Arrive", "Wing arrive", "Lintel", "Inside floor", "Back wall", "Side wall", "Front wall", "Roof" };

    static bool IsShell(Transform t)
    {
        foreach (var p in ShellPrefixes) if (t.name.StartsWith(p)) return true;
        return t.GetComponent<FirTeleport>() != null;
    }

    static void BuildOptimize(Transform root)
    {
        // ---- static batching: anything a script owns, moves or points at stays dynamic; everything else is static
        var dyn = new HashSet<Transform>();
        foreach (var b in root.GetComponentsInChildren<UdonSharpBehaviour>(true))
        {
            dyn.Add(b.transform);
            foreach (var f in b.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var v = f.GetValue(b);
                if (v is Transform) dyn.Add((Transform)v);
                else if (v is GameObject) dyn.Add(((GameObject)v).transform);
                else if (v is Transform[]) foreach (var t in (Transform[])v) { if (t != null) dyn.Add(t); }
                else if (v is GameObject[]) foreach (var o in (GameObject[])v) { if (o != null) dyn.Add(o.transform); }
            }
        }
        foreach (var st in root.GetComponentsInChildren<VRC.SDK3.Components.VRCStation>(true)) dyn.Add(st.transform);
        int made = 0, kept = 0;
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            bool moving = false;
            for (var t = r.transform; t != null; t = t.parent) if (dyn.Contains(t)) { moving = true; break; }
            if (moving) { GameObjectUtility.SetStaticEditorFlags(r.gameObject, (StaticEditorFlags)0); kept++; }
            else { GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic); made++; }
        }

        // ---- the area culler
        culledAreas.Clear();
        var parts = new List<GameObject>(); var partArea = new List<int>();
        var bx0 = new List<float>(); var bz0 = new List<float>(); var bx1 = new List<float>(); var bz1 = new List<float>();
        for (int ci = 0; ci < root.childCount; ci++)
        {
            var area = root.GetChild(ci);
            if (area.Find("Floor") == null || area.Find("Arrive") == null) continue;
            bool lobbyLink = area.GetComponentsInChildren<FirTeleport>(true).Any(t => t.name == "Back to the lobby");
            bool shared = area.GetComponentsInChildren<FirHunt>(true).Length > 0 || area.GetComponentsInChildren<FirStalker>(true).Length > 0
                          || area.GetComponentsInChildren<FirNight>(true).Length > 0 || area.GetComponentsInChildren<FirThrowable>(true).Length > 0
                          || area.GetComponentsInChildren<VRC.SDK3.Components.VRCObjectSync>(true).Length > 0;
            if (!lobbyLink || shared) continue;
            // its footprint: its floor and every wing's floor
            float mnx = 1e9f, mnz = 1e9f, mxx = -1e9f, mxz = -1e9f;
            var groups = new List<Transform> { area };
            for (int k = 0; k < area.childCount; k++) { var c = area.GetChild(k); if (c.Find("Floor") != null) groups.Add(c); }
            foreach (var gr in groups)
            {
                var fl = gr.Find("Floor"); var bc = fl.GetComponent<BoxCollider>();
                var s = bc != null ? bc.size : Vector3.one; var c0 = fl.position;
                mnx = Mathf.Min(mnx, c0.x - s.x / 2f); mxx = Mathf.Max(mxx, c0.x + s.x / 2f); mnz = Mathf.Min(mnz, c0.z - s.z / 2f); mxz = Mathf.Max(mxz, c0.z + s.z / 2f);
            }
            int ai = bx0.Count;
            bx0.Add(mnx - 2f); bz0.Add(mnz - 2f); bx1.Add(mxx + 2f); bz1.Add(mxz + 2f);
            foreach (var gr in groups)
                for (int k = 0; k < gr.childCount; k++)
                {
                    var c = gr.GetChild(k);
                    if (gr == area && groups.Contains(c)) continue;   // a wing: its own contents are listed separately
                    if (IsShell(c)) continue;
                    parts.Add(c.gameObject); partArea.Add(ai);
                }
            culledAreas.Add(area.name);
        }
        var go = new GameObject("Culler");
        go.transform.SetParent(root, false);
        var cu = Add<FirCuller>(go);
        cu.parts = parts.ToArray(); cu.partArea = partArea.ToArray();
        cu.ax0 = bx0.ToArray(); cu.az0 = bz0.ToArray(); cu.ax1 = bx1.ToArray(); cu.az1 = bz1.ToArray();
        report.Add("Optimised: " + made + " meshes static-batched (" + kept + " moving ones left dynamic); " + culledAreas.Count + " Station areas sleep unless you're in them (" + parts.Count + " switchable groups; floors, walls and doors always on)");
    }
}
#endif
