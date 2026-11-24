#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Orlando, Florida: two roller coasters, two dark rides, swan boats and an observation wheel, all rideable.</summary>
public static partial class FirCheckBuilder
{
    public static readonly List<string> coasterInfo = new List<string>();

    /// <summary>A closed Catmull-Rom curve through the control points, sampled about every `step` metres; seg[i] is the control segment of point i.</summary>
    static List<Vector3> Spline(Vector3[] c, float step, List<int> seg)
    {
        var pts = new List<Vector3>();
        int n = c.Length;
        for (int i = 0; i < n; i++)
        {
            Vector3 p0 = c[(i + n - 1) % n], p1 = c[i], p2 = c[(i + 1) % n], p3 = c[(i + 2) % n];
            int m = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(p1, p2) / step));
            for (int j = 0; j < m; j++)
            {
                float t = j / (float)m, t2 = t * t, t3 = t2 * t;
                pts.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                seg.Add(i);
            }
        }
        return pts;
    }

    static float Flat2(Vector3 a, Vector3 b) { float dx = a.x - b.x, dz = a.z - b.z; return Mathf.Sqrt(dx * dx + dz * dz); }

    static void Piece(Transform p, string name, Vector3 pos, Quaternion rot, Vector3 size, Material m)
    {
        var go = Prim(PrimitiveType.Cube, name, p, pos, size, m);
        go.transform.localRotation = rot;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
    }

    /// <summary>
    /// A roller coaster: a track along a closed curve through the control points (the first `stationCtrl` control segments are the
    /// station, then the lift chain to the highest point, the last one is the brake run), supports, trains of seats, and a FirRide
    /// (kind 3) to run it. Checks the track never passes through itself.
    /// </summary>
    static FirRide Coaster(Transform g, string name, Vector3[] ctrl, int stationCtrl, int trains, int rows, float liftSpeed, string railMat, string spineMat, string carMat, Transform exit)
    {
        var cg = Group(g, name, Vector3.zero);
        var seg = new List<int>();
        var pts = Spline(ctrl, 2.4f, seg);
        int n = pts.Count, crestCtrl = 0;
        for (int i = 1; i < ctrl.Length; i++) if (ctrl[i].y > ctrl[crestCtrl].y) crestCtrl = i;
        var pace = new float[n];
        int sfxSeg = -1;
        float top = pts.Max(p => p.y), len = 0f, vmax = 0f;
        for (int i = 0; i < n; i++)
        {
            int s = seg[i];
            pace[i] = s < stationCtrl ? -1.1f : s < crestCtrl ? -liftSpeed : s == ctrl.Length - 1 ? -3f : 1f;
            if (s == crestCtrl && sfxSeg < 0) sfxSeg = i;
            float mid = (pts[i].y + pts[(i + 1) % n].y) / 2f;
            if (pace[i] > 0f) vmax = Mathf.Max(vmax, Mathf.Max(4f, 0.85f * Mathf.Sqrt(2f * 9.81f * Mathf.Max(0f, top - mid))));
            len += Vector3.Distance(pts[i], pts[(i + 1) % n]);
        }
        // the track: two rails and a spine on every piece, a tie on every other one, rolled into the turns like the carts
        var track = Group(cg, "Track", Vector3.zero);
        var rail = M(railMat); var spine = M(spineMat);
        var roll = new float[n];
        for (int i = 0; i < n; i++)
        {
            Vector3 h0 = pts[i] - pts[(i + n - 1) % n], h1 = pts[(i + 1) % n] - pts[i]; h0.y = 0f; h1.y = 0f;
            roll[i] = h0.magnitude > 0.01f && h1.magnitude > 0.01f ? Mathf.Clamp(-Vector3.SignedAngle(h0, h1, Vector3.up) * 1.6f, -55f, 55f) : 0f;
        }
        for (int i = 0; i < n; i++)
        {
            Vector3 a = pts[i], b = pts[(i + 1) % n], d = b - a, mid = (a + b) / 2f;
            var rot = Quaternion.LookRotation(d.normalized, Vector3.up) * Quaternion.Euler(0, 0, (roll[i] + roll[(i + 1) % n]) / 2f);
            float l = d.magnitude + 0.1f;
            Piece(track, "Rail", mid + rot * V(-0.55f, -0.32f, 0), rot, V(0.12f, 0.12f, l), rail);
            Piece(track, "Rail", mid + rot * V(0.55f, -0.32f, 0), rot, V(0.12f, 0.12f, l), rail);
            Piece(track, "Spine", mid + rot * V(0, -0.62f, 0), rot, V(0.34f, 0.34f, l), spine);
            if (i % 2 == 0) Piece(track, "Tie", mid + rot * V(0, -0.42f, 0), rot, V(1.3f, 0.08f, 0.22f), spine);
            if (i % 3 == 0 && a.y > 2.2f)
            {
                float h = a.y - 0.8f;
                DPrim(PrimitiveType.Cylinder, "Support", track, V(a.x, h / 2f, a.z), V(0.38f, h / 2f, 0.38f), spine);
                DPrim(PrimitiveType.Cylinder, "Footing", track, V(a.x, 0.15f, a.z), V(0.9f, 0.15f, 0.9f), M("Concrete"));
            }
        }
        // the track must never run through itself
        int clash = 0; string where = "";
        for (int i = 0; i < n; i++) for (int j = i + 1; j < n; j++)
        {
            int gap = Mathf.Min(j - i, n - (j - i));
            if (gap < 6) continue;
            Vector3 a = pts[i], b = pts[j];
            if (Flat2(a, b) < 2.4f && Mathf.Abs(a.y - b.y) < 3.4f) { clash++; if (where == "") where = a + " / " + b; }
        }
        if (clash > 0) report.Add("problem: " + name + "'s track runs through itself at " + clash + " places, e.g. " + where);
        // the trains
        var carts = new List<Transform>();
        float carLen = rows * 1.15f + 0.6f;
        for (int t = 0; t < trains; t++)
        {
            var car = Group(cg, name + " train " + t, pts[0]);
            Box(car, "Floor", V(0, -0.12f, 0), V(1.45f, 0.24f, carLen), M(carMat), false);
            foreach (float sx in new[] { -0.74f, 0.74f }) Box(car, "Side", V(sx, 0.2f, 0), V(0.08f, 0.5f, carLen), M(carMat), false);
            Box(car, "Nose", V(0, 0.25f, carLen / 2f + 0.15f), V(1.45f, 0.6f, 0.4f), M(carMat), false);
            Prim(PrimitiveType.Sphere, "Headlamp", car, V(0, 0.35f, carLen / 2f + 0.36f), Vector3.one * 0.22f, M("GlowYellow"));
            for (int r = 0; r < rows; r++)
            {
                float z = carLen / 2f - 0.6f - r * 1.15f;
                Box(car, "Seat back", V(0, 0.45f, z - 0.4f), V(1.3f, 0.7f, 0.12f), M("Black"), false);
                Box(car, "Lap bar", V(0, 0.55f, z + 0.35f), V(1.2f, 0.06f, 0.06f), M("Metal"), false);
                RideSeat(car, name + " seat " + t + "." + r + "a", V(-0.33f, 0.02f, z), 0f, exit);
                RideSeat(car, name + " seat " + t + "." + r + "b", V(0.33f, 0.02f, z), 0f, exit);
            }
            carts.Add(car);
        }
        var ride = Add<FirRide>(cg.gameObject);
        ride.kind = 3; ride.carts = carts.ToArray(); ride.route = pts.ToArray(); ride.pace = pace; ride.speed = 4f; ride.bank = 1.6f;
        ride.sfx = Src(carts[0], V(0, 1f, 0), Clip("scream1"), false, 1f, 1f, 45f, false);
        ride.sfxSeg = sfxSeg;
        roomRides++;
        coasterInfo.Add(name + "|" + len.ToString("F0") + "|" + top.ToString("F1") + "|" + vmax.ToString("F1"));
        report.Add(name + ": a roller coaster, " + len.ToString("F0") + " m of track, " + top.ToString("F0") + " m lift, top speed " + (vmax * 3.6f).ToString("F0") + " km/h, " + trains + " trains of " + rows * 2 + " seats");
        return ride;
    }

    static void OrangeTree(Transform g, Vector3 at, bool dead = false)
    {
        DPrim(PrimitiveType.Cylinder, "Orange tree trunk", g, at + V(0, 0.8f, 0), V(0.25f, 0.8f, 0.25f), M("Trunk"));
        if (!dead) DPrim(PrimitiveType.Sphere, "Orange tree leaves", g, at + V(0, 2.2f, 0), V(2.2f, 1.9f, 2.2f), RM("OrangeLeaf", 0.2f, 0.45f, 0.15f));
        for (int k = 0; k < 5; k++) Prim(PrimitiveType.Sphere, "Orange", g, at + V(Mathf.Sin(k * 1.26f) * 0.95f, 2f + (k % 2) * 0.5f, Mathf.Cos(k * 1.26f) * 0.95f), Vector3.one * 0.24f, M("GlowOrange"));
    }

    // ================================================================================================ 15 Orlando, Florida

    static void RoomOrlando(Transform root)
    {
        coasterInfo.Clear();
        float cx = 3960f;
        var g = Room(root, "Orlando", 3880f, 4040f, 140f, 50f, "Grass", "PixelSky", "Sky",
                     "ORLANDO, FLORIDA", new Color(1f, 0.55f, 0.1f), "theme parks, orange groves, gators, a thunderstorm at three o'clock every afternoon, and the I-4", "ORLANDO, FL");
        int seats0 = rideSeats;
        // ---- the entrance: an arch, palms, an orange grove, a gator pond, the I-4
        foreach (float px in new[] { 3950f, 3970f }) Box(g, "Arch pillar", V(px, 3.5f, 30f), V(1.2f, 7f, 1.2f), M("CaneWhite"));
        Box(g, "Arch beam", V(cx, 7.6f, 30f), V(22f, 1.6f, 1f), M("GlowOrange"));
        Text("Welcome", g, V(cx, 7.6f, 29.45f), Face(Vector3.back), V2(20f, 1.4f), 1.1f, "<b>WELCOME TO ORLANDO</b>").color = Color.white;
        for (int k = 0; k < 11; k++) { float px = 3887f + k * 14f; if (px > 3944f && px < 3976f) continue; Palm(g, V(px, 0, 25.5f), 7f + (k % 3), k); }
        for (int ix = 0; ix < 3; ix++) for (int iz = 0; iz < 3; iz++) OrangeTree(g, V(3886f + ix * 6f, 0, 31f + iz * 5f));
        var oj = Stall(g, "FRESH OJ", V(3908f, 0, 36f), 0f, "GlowOrange", "CaneWhite");
        Box(g, "Gator pond", V(3926f, 0.02f, 32f), V(10f, 0.04f, 5f), M("PoolGreen"), false);
        Box(g, "I-4", V(4019f, 0.01f, 30f), V(40f, 0.02f, 8f), M("TurfGrey"), false);
        for (int k = 0; k < 6; k++) Box(g, "Lane line", V(4002f + k * 6.5f, 0.025f, 30f), V(2.5f, 0.01f, 0.15f), M("CaneWhite"), false);
        string[] carCol = { "CaneRed", "GiftBlue", "CaneWhite", "GlowYellow", "GiftGreen" };
        for (int k = 0; k < 5; k++) { Box(g, "Stuck car", V(4006f + k * 6f, 0.6f, 28f + (k % 2) * 4f), V(4f, 1.2f, 1.8f), M(carCol[k]), false); Box(g, "Car roof", V(4006f + k * 6f, 1.45f, 28f + (k % 2) * 4f), V(2.2f, 0.5f, 1.6f), M(carCol[k]), false); }
        var i4 = Group(g, "I-4 sign", V(4034f, 0, 36f));
        foreach (float px in new[] { -1.4f, 1.4f }) DPrim(PrimitiveType.Cylinder, "Post", i4, V(px, 2f, 0), V(0.12f, 2f, 0.12f), M("Metal"));
        Box(i4, "Plate", V(0, 4.2f, 0), V(3.4f, 1.3f, 0.06f), RM("HighwayGreen", 0.05f, 0.4f, 0.2f), false);
        Text("I4", i4, V(0, 4.2f, -0.04f), Quaternion.identity, V2(3.2f, 1.1f), 0.4f, "<b>I-4</b>\n<size=60%>average speed: 4</size>").color = Color.white;
        var souvenir = Stall(g, "SOUVENIRS", V(3990f, 0, 34f), 0f, "GiftBlue", "GlowOrange");

        // ---- Gator Gulch: the family coaster (west)
        Box(g, "Gator platform", V(3928f, 0.15f, 42.2f), V(24f, 0.3f, 1.6f), M("DeskWood"));
        foreach (float px in new[] { 3915f, 3941f }) DPrim(PrimitiveType.Cylinder, "Sign post", g, V(px, 2.4f, 41.3f), V(0.2f, 2.4f, 0.2f), M("Trunk"));
        Box(g, "Sign beam", V(3928f, 4.9f, 41.3f), V(26.4f, 1.2f, 0.3f), RM("GatorGreen", 0.25f, 0.5f, 0.2f), false);
        Text("GatorSign", g, V(3928f, 4.9f, 41.12f), Face(Vector3.back), V2(25f, 1f), 0.8f, "<b>GATOR GULCH</b>  <size=50%>the family coaster. you must be this tall: a gator's knee</size>").color = new Color(1f, 0.95f, 0.5f);
        var gatorExit = Marker(g, "Gator Gulch exit", V(3928f, 0.35f, 42.2f), 180f);
        var gator = new[] {
            V(3940f, 0.6f, 44f), V(3928f, 0.6f, 44f), V(3916f, 0.6f, 44f), V(3906f, 0.9f, 45f), V(3899f, 1.8f, 50f), V(3896f, 4f, 57f), V(3895f, 8f, 65f), V(3895f, 12f, 73f),
            V(3896f, 14f, 79f), V(3899f, 13f, 84f), V(3906f, 8f, 89f), V(3916f, 2.5f, 91f), V(3926f, 7f, 92f), V(3936f, 3f, 90f), V(3944f, 3.5f, 84f), V(3936f, 6f, 76f),
            V(3922f, 2.5f, 72f), V(3912f, 3.5f, 64f), V(3917f, 5f, 56f), V(3930f, 2.5f, 54f), V(3943f, 1.5f, 52f), V(3947f, 0.9f, 48f) };
        Coaster(g, "Gator Gulch", gator, 2, 2, 2, 2.6f, "GatorGreen", "Metal", "GatorGreen", gatorExit);

        // ---- the Storm Chaser: the big one (east)
        Box(g, "Storm platform", V(3990f, 0.15f, 42.2f), V(24f, 0.3f, 1.6f), M("Concrete"));
        foreach (float px in new[] { 3977f, 4003f }) DPrim(PrimitiveType.Cylinder, "Sign post", g, V(px, 2.4f, 41.3f), V(0.2f, 2.4f, 0.2f), M("Metal"));
        Box(g, "Sign beam", V(3990f, 4.9f, 41.3f), V(26.4f, 1.2f, 0.3f), RM("StormBlue", 0.15f, 0.2f, 0.45f), false);
        Text("StormSign", g, V(3990f, 4.9f, 41.12f), Face(Vector3.back), V2(25f, 1f), 0.8f, "<b>THE STORM CHASER</b>  <size=50%>30 metres up. 70 km/h down. lightning not included (usually)</size>").color = new Color(0.7f, 0.9f, 1f);
        var stormExit = Marker(g, "Storm Chaser exit", V(3990f, 0.35f, 42.2f), 180f);
        var storm = new[] {
            V(3978f, 0.6f, 44f), V(3990f, 0.6f, 44f), V(4002f, 0.6f, 44f), V(4014f, 0.9f, 45f), V(4026f, 2f, 48f), V(4031f, 4f, 54f), V(4032f, 9f, 62f), V(4032f, 15f, 72f),
            V(4032f, 21f, 82f), V(4032f, 27f, 92f), V(4032f, 30f, 99f), V(4031f, 28.5f, 104f), V(4027f, 20f, 112f), V(4020f, 9f, 119f), V(4010f, 2.5f, 124f), V(3998f, 2f, 126f),
            V(3988f, 14f, 126f), V(3980f, 13f, 123f), V(3976f, 7f, 116f), V(3976f, 3f, 106f), V(3978f, 3f, 98f), V(3980f, 12f, 88f), V(3983f, 4f, 78f), V(3990f, 3f, 70f),
            V(4001f, 4f, 70f), V(4012f, 6f, 76f), V(4016f, 7f, 86f), V(4009f, 5f, 95f), V(3999f, 8f, 90f), V(3995f, 10f, 80f), V(3992f, 9.5f, 70f), V(3986f, 5f, 60f),
            V(3977f, 2.5f, 55f), V(3972f, 1.2f, 49f), V(3973f, 0.7f, 45.5f) };
        Coaster(g, "The Storm Chaser", storm, 2, 2, 3, 3.2f, "StormBlue", "CaneWhite", "StormBlue", stormExit);

        // ---- the Haunted Orange Grove: a dark ride (middle, south)
        var grove = DarkShed(g, "Haunted Orange Grove", 3951f, 3969f, 40f, 64f, 6f, new[] { 3954f, 3966.3f }, "THE HAUNTED ORANGE GROVE", "the trees out here bear fruit all year. the fruit has eyes.", new Color(1f, 0.6f, 0.15f));
        foreach (float px in new[] { 3964.1f, 3960f, 3956f }) Box(grove, "Partition", V(px, 2.5f, 52f), V(0.2f, 5f, 14f), M("WallDark"));
        for (int k = 0; k < 8; k++) Prim(PrimitiveType.Sphere, "Glowing orange", grove, V(3954f + (k % 4) * 4.1f, 4.3f + (k % 2) * 0.4f, 46f + (k / 4) * 10f), Vector3.one * 0.45f, M("GlowOrange"));
        Box(g, "Grove platform", V(3960f, 0.15f, 35.4f), V(12f, 0.3f, 1.6f), M("DeskWood"));
        var groveExit = Marker(g, "Grove exit", V(3960f, 0.35f, 35.4f), 180f);
        var groveRoute = new[] { V(3955f, 0, 37.2f), V(3965f, 0, 37.2f), V(3966.3f, 0, 40.5f), V(3966.3f, 0, 61.5f), V(3962f, 0, 62.5f), V(3962f, 0, 43f),
                                 V(3958f, 0, 42.5f), V(3958f, 0, 61.5f), V(3954f, 0, 62.5f), V(3954f, 0, 40.5f), V(3954.4f, 0, 38f) };
        CartRide(g, "Grove carts", groveRoute, 2, 1.8f, groveExit, "GlowOrange", "Grove seat");
        var gs = Group(grove, "Scares", Vector3.zero);
        ParkScare(gs, "Grove scare 1", V(3964.1f, 0, 63.6f), 180f, 1.6f, "scream1", 2.4f);
        ParkScare(gs, "Grove scare 2", V(3960f, 0, 40.4f), 0f, 2.4f, "scream2", 2.4f);
        ParkScare(gs, "Grove scare 3", V(3956f, 0, 63.6f), 180f, 1.6f, "scream3", 2.4f);
        var ghost = RideGag(RG(g, "The orange ghost", V(3958f, 0, 52f), 0f, "orl_ghost", "fly", PrimitiveType.Sphere, V(0.8f, 1f, 0.8f), "GlowOrange", 2f), 0f);
        ghost.target.localPosition = V(0, 2.4f, 0);

        // ---- Mission: Space Coast: a dark ride (middle, north of the grove)
        var space = DarkShed(g, "Mission Space Coast", 3951f, 3969f, 72f, 96f, 6f, new[] { 3954f, 3966.3f }, "MISSION: SPACE COAST", "strap in. countdown in ten. or so. the computer's a bit old.", new Color(0.5f, 0.8f, 1f));
        foreach (float px in new[] { 3964.1f, 3960f, 3956f }) Box(space, "Partition", V(px, 2.5f, 84f), V(0.2f, 5f, 14f), M("NightSky"));
        for (int k = 0; k < 40; k++) Prim(PrimitiveType.Sphere, "Star", space, V(3951.5f + Mathf.Repeat(k * 0.618f, 1f) * 17f, 5.85f, 73f + Mathf.Repeat(k * 0.382f + 0.1f, 1f) * 22f), Vector3.one * 0.08f, M("GlowWarm"));
        DPrim(PrimitiveType.Sphere, "Moon", space, V(3960f, 4.4f, 94.4f), V(2.6f, 2.6f, 1f), M("LabGrey"));
        Box(g, "Space platform", V(3960f, 0.15f, 67.4f), V(12f, 0.3f, 1.6f), M("LabFloor"));
        var spaceExit = Marker(g, "Space exit", V(3960f, 0.35f, 67.4f), 180f);
        var spaceRoute = new Vector3[groveRoute.Length];
        for (int i = 0; i < groveRoute.Length; i++) spaceRoute[i] = groveRoute[i] + V(0, 0, 32f);
        CartRide(g, "Space capsules", spaceRoute, 2, 1.8f, spaceExit, "LabWhite", "Capsule seat");
        var rocket = RideGag(RG(g, "The countdown", V(3966.3f, 0, 84f), 0f, "orl_countdown", "fly", PrimitiveType.Capsule, V(0.6f, 2f, 0.6f), "CaneWhite", 3f), 0f);
        rocket.target.localPosition = V(0, 2.2f, 0);
        Prim(PrimitiveType.Sphere, "Flame", rocket.target, V(0, -0.2f, 0), V(0.5f, 0.6f, 0.5f), M("GlowFlame"));
        var alien = RideGag(RG(g, "The moonwalk", V(3962f, 0, 84f), 180f, "orl_moonwalk", "pop", PrimitiveType.Sphere, V(0.5f, 0.6f, 0.5f), "GlowGreen", 2f), 0f);
        Mini(alien.target, "Alien", V(0, 0.6f, 0), 0f, 0.5f, "#76ff03");
        var meteor = RideGag(RG(g, "The meteor", V(3958f, 0, 84f), 0f, "orl_meteor", "spin", PrimitiveType.Sphere, V(1f, 1f, 1f), "GlowOrange"), 0f);
        meteor.target.localPosition = V(0, 2.6f, 0);

        // ---- the lake, its fountain and the swan boats (north-west)
        Box(g, "Lake", V(3920f, 0.02f, 122f), V(50f, 0.04f, 28f), M("PoolBlue"), false);
        DPrim(PrimitiveType.Cylinder, "Fountain", g, V(3920f, 1f, 122f), V(3f, 1f, 3f), M("CaneWhite"));
        DPrim(PrimitiveType.Cylinder, "Fountain spray", g, V(3920f, 4f, 122f), V(0.6f, 2.5f, 0.6f), M("Glass"));
        Box(g, "Swan dock", V(3920f, 0.15f, 109.6f), V(6f, 0.3f, 1.4f), M("DeskWood"));
        var swanExit = Marker(g, "Swan dock exit", V(3920f, 0.35f, 109.4f), 180f);
        var swanRoute = new Vector3[14];
        for (int k = 0; k < 14; k++) { float a = -90f + k * (360f / 14f); swanRoute[k] = V(3920f + Mathf.Cos(a * Mathf.Deg2Rad) * 18f, 0f, 122f + Mathf.Sin(a * Mathf.Deg2Rad) * 10f); }
        var swans = CartRide(g, "Swan boats", swanRoute, 3, 1.1f, swanExit, "CaneWhite", "Swan seat", 0.5f);
        foreach (var s in swans.carts)
        {
            Prim(PrimitiveType.Capsule, "Swan neck", s, V(0, 1.1f, 0.85f), V(0.25f, 0.6f, 0.25f), M("CaneWhite")).transform.localRotation = Quaternion.Euler(-15, 0, 0);
            Prim(PrimitiveType.Sphere, "Swan head", s, V(0, 1.75f, 1.05f), V(0.35f, 0.3f, 0.45f), M("CaneWhite"));
            Prim(PrimitiveType.Cube, "Swan beak", s, V(0, 1.72f, 1.35f), V(0.1f, 0.08f, 0.2f), M("GlowOrange"));
        }

        // ---- the observation wheel (between the lake and the Storm Chaser)
        var ow = Group(g, "Observation Wheel", Vector3.zero);
        var hub = V(3958f, 14.7f, 120f);
        foreach (float lx in new[] { -2f, 2f }) foreach (float lz in new[] { -6f, 6f })
            {
                var foot = V(hub.x + lx, 0, hub.z + lz); var mid = (foot + hub) / 2f; var d = hub - foot;
                DPrim(PrimitiveType.Cylinder, "Leg", ow, mid, V(0.4f, d.magnitude / 2f, 0.4f), M("CaneWhite"), Quaternion.FromToRotation(Vector3.up, d.normalized));
            }
        var wheel = Group(ow, "Rotor", hub);
        for (int k = 0; k < 12; k++) { float a = k * 30f; DPrim(PrimitiveType.Cylinder, "Spoke", wheel, Quaternion.Euler(a, 0, 0) * V(0, 6.25f, 0), V(0.15f, 6.25f, 0.15f), M("CaneWhite"), Quaternion.Euler(a, 0, 0)); }
        for (int k = 0; k < 36; k++) { float a = k * 10f; Prim(PrimitiveType.Cube, "Rim", wheel, Quaternion.Euler(a, 0, 0) * V(0, 12.5f, 0), V(0.2f, 0.2f, 2.3f), M(k % 2 == 0 ? "GlowOrange" : "CaneWhite")).transform.localRotation = Quaternion.Euler(a, 0, 0); }
        Prim(PrimitiveType.Cylinder, "Hub", wheel, Vector3.zero, V(1.6f, 0.6f, 1.6f), M("GlowOrange")).transform.localRotation = Quaternion.Euler(0, 0, 90);
        Box(g, "Wheel platform", V(3958f, 0.3f, 120f), V(3f, 0.6f, 3f), M("Concrete"));
        Box(g, "Wheel step", V(3958f, 0.15f, 117.9f), V(3f, 0.3f, 1.2f), M("Concrete"));
        var wheelExit = Marker(g, "Wheel exit", V(3958f, 0.35f, 117.6f), 180f);
        var gondolas = new List<Transform>();
        for (int k = 0; k < 8; k++)
        {
            var gp = Group(wheel, "Gondola " + k, Quaternion.Euler(k * 45f, 0, 0) * V(0, -12.5f, 0));
            Box(gp, "Floor", V(0, -1.5f, 0), V(1.4f, 0.1f, 1.4f), M(carCol[k % 5]), false);
            Box(gp, "Roof", V(0, -0.05f, 0), V(1.5f, 0.1f, 1.5f), M(carCol[k % 5]), false);
            foreach (float px in new[] { -0.65f, 0.65f }) foreach (float pz in new[] { -0.65f, 0.65f }) Box(gp, "Post", V(px, -0.75f, pz), V(0.06f, 1.4f, 0.06f), M("Metal"), false);
            RideSeat(gp, "Wheel gondola seat " + k, V(0, -1.45f, 0), 180f, wheelExit);
            gondolas.Add(gp);
        }
        var wr = Add<FirRide>(ow.gameObject); wr.kind = 0; wr.rotor = wheel; wr.axis = Vector3.right; wr.degPerSec = 4f; wr.upright = gondolas.ToArray();
        roomRides++;

        // ---- the people and the gags
        RF(g, "Tourist Trudy", "#ff8a80", 21, V(3944f, 0, 27.5f), 200f, "orl_tourist");
        Hat("Tourist Trudy", PrimitiveType.Cylinder, V(0, 0.12f, 0), V(0.4f, 0.03f, 0.4f), "CornYellow");
        RF(g, "Retiree Ruth", "#e1bee7", 9, V(3976f, 0, 27.5f), 160f, "orl_retiree");
        RF(g, "Ride Operator Rhonda", "#80deea", 20, V(3918f, 0.3f, 42.2f), 0f, "orl_rhonda");
        RF(g, "Ride Operator Ray", "#9fa8da", 16, V(4000f, 0.3f, 42.2f), 0f, "orl_ray");
        RF(g, "Gator Wrangler Wade", "#a1887f", 11, V(3933f, 0, 30f), 270f, "orl_wade");
        Hat("Gator Wrangler Wade", PrimitiveType.Cylinder, V(0, 0.12f, 0), V(0.42f, 0.05f, 0.42f), "Trunk");
        var gatorGag = RG(g, "The gator", V(3926f, 0, 32f), 90f, "orl_gator", "pop", PrimitiveType.Capsule, V(0.6f, 0.5f, 2.4f), "GatorGreen", 0.6f);
        foreach (float ex in new[] { -0.15f, 0.15f }) Prim(PrimitiveType.Sphere, "Eye", gatorGag.target, V(ex, 0.5f, 1f), Vector3.one * 0.14f, M("GlowYellow"));
        var cloud = RG(g, "The three o'clock thunderstorm", V(3940f, 0, 36f), 180f, "orl_storm", "shake", PrimitiveType.Sphere, V(4f, 1.6f, 3f), "TurfGrey");
        cloud.target.localPosition = V(0, 7f, 0);
        DPrim(PrimitiveType.Cube, "Lightning", cloud.target, V(0.4f, -1.5f, 0), V(0.2f, 1.8f, 0.05f), M("GlowYellow"), Quaternion.Euler(0, 0, 20));
        RG(g, "The traffic jam", V(4016f, 0, 30f), 90f, "orl_traffic", "slide", PrimitiveType.Cube, V(1.8f, 1.2f, 4f), "GlowOrange", 0.5f);
        report.Add("Orlando, Florida: 2 roller coasters (Gator Gulch, the Storm Chaser), 2 dark rides (the Haunted Orange Grove, Mission: Space Coast), swan boats on the lake, an observation wheel (" + (rideSeats - seats0) + " seats in all), an orange grove, a gator pond and the I-4; 5 voiced people, 7 gags");
    }
}
#endif
