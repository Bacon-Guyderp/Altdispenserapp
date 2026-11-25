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

    /// <summary>Parts that must never sleep: anything synced or shared (the horror game, the voxel world, throwables), and floors you could land on.</summary>
    static bool KeepAwake(Transform t, bool floors)
    {
        if (t.GetComponentsInChildren<FirHunt>(true).Length > 0 || t.GetComponentsInChildren<FirStalker>(true).Length > 0 || t.GetComponentsInChildren<FirNight>(true).Length > 0
            || t.GetComponentsInChildren<FirVoxel>(true).Length > 0 || t.GetComponentsInChildren<FirCheckHub>(true).Length > 0 || t.GetComponentsInChildren<FirThrowable>(true).Length > 0
            || t.GetComponentsInChildren<FirShare>(true).Length > 0 || t.GetComponentsInChildren<VRC.SDK3.Components.VRCObjectSync>(true).Length > 0) return true;
        if (!floors) return false;   // a Station area's floor is always on as part of its shell
        foreach (var c in t.GetComponentsInChildren<BoxCollider>(true))
        {
            var s = Vector3.Scale(c.size, c.transform.lossyScale);
            float top = c.transform.TransformPoint(c.center).y + Mathf.Abs(s.y) / 2f;
            if (Mathf.Abs(s.x) >= 4f && Mathf.Abs(s.z) >= 4f && Mathf.Abs(s.y) <= 1.2f && top > -0.6f && top < 0.6f) return true;   // a floor
        }
        return false;
    }

    /// <summary>The ground-plan rectangle covered by these transforms' box colliders.</summary>
    static bool Footprint(Transform[] ts, out float mnx, out float mnz, out float mxx, out float mxz)
    {
        mnx = mnz = 1e9f; mxx = mxz = -1e9f; bool any = false;
        foreach (var t in ts)
            if (t != null)
                foreach (var c in t.GetComponentsInChildren<BoxCollider>(true))
                {
                    var h = c.size * 0.5f;
                    for (int k = 0; k < 8; k++)
                    {
                        var p = c.transform.TransformPoint(c.center + new Vector3((k & 1) == 0 ? -h.x : h.x, (k & 2) == 0 ? -h.y : h.y, (k & 4) == 0 ? -h.z : h.z));
                        mnx = Mathf.Min(mnx, p.x); mxx = Mathf.Max(mxx, p.x); mnz = Mathf.Min(mnz, p.z); mxz = Mathf.Max(mxz, p.z); any = true;
                    }
                }
        return any;
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

        // ---- the area culler: every top-level area of the world sleeps while you're not in it (the lobby never does)
        culledAreas.Clear();
        var parts = new List<GameObject>(); var partArea = new List<int>();
        var bx0 = new List<float>(); var bz0 = new List<float>(); var bx1 = new List<float>(); var bz1 = new List<float>();
        var never = new HashSet<string> { "Lobby", "TestRoom", "Hunt", "Head", "Share", "Culler" };
        Transform lobbyTop = lobbyArrive; while (lobbyTop != null && lobbyTop.parent != root) lobbyTop = lobbyTop.parent;
        for (int ci = 0; ci < root.childCount; ci++)
        {
            var area = root.GetChild(ci);
            if (never.Contains(area.name) || area == lobbyTop) continue;
            // the horror game reaches into its whole map from outside it (rounds, doors, closets, keys): leave those areas exactly as they were
            if (area.GetComponentsInChildren<FirHunt>(true).Length > 0 || area.GetComponentsInChildren<FirStalker>(true).Length > 0 || area.GetComponentsInChildren<FirNight>(true).Length > 0) continue;
            bool station = area.Find("Floor") != null && area.Find("Arrive") != null;
            var groups = new List<Transform> { area };
            for (int k = 0; k < area.childCount; k++) { var c = area.GetChild(k); if (c.Find("Floor") != null && c.Find("WallN") != null) groups.Add(c); }
            float mnx, mnz, mxx, mxz;
            if (!Footprint(station ? groups.Select(gr => gr.Find("Floor")).ToArray() : new[] { area }, out mnx, out mnz, out mxx, out mxz)) continue;
            float margin = 2f;
            int ai = bx0.Count, before = parts.Count;
            foreach (var gr in groups)
                for (int k = 0; k < gr.childCount; k++)
                {
                    var c = gr.GetChild(k);
                    if (gr == area && groups.Contains(c)) continue;   // a wing: its own contents are listed separately
                    if (IsShell(c) || KeepAwake(c, !station)) continue;
                    parts.Add(c.gameObject); partArea.Add(ai);
                }
            if (parts.Count == before) continue;
            bx0.Add(mnx - margin); bz0.Add(mnz - margin); bx1.Add(mxx + margin); bz1.Add(mxz + margin);
            culledAreas.Add(area.name);
        }
        var go = new GameObject("Culler");
        go.transform.SetParent(root, false);
        var cu = Add<FirCuller>(go);
        foreach (var tp in root.GetComponentsInChildren<FirTeleport>(true)) tp.culler = cu;   // a teleport wakes where it's going before you land
        cu.parts = parts.ToArray(); cu.partArea = partArea.ToArray();
        cu.ax0 = bx0.ToArray(); cu.az0 = bz0.ToArray(); cu.ax1 = bx1.ToArray(); cu.az1 = bz1.ToArray();
        report.Add("Optimised: " + made + " meshes static-batched (" + kept + " moving ones left dynamic); " + culledAreas.Count + " areas sleep unless you're in them (" + parts.Count + " switchable groups; floors, walls, doors and anything synced always on)");
    }
}
#endif
