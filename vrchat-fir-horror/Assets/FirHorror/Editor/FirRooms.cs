#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// The new rooms: fifteen more Station stops. The cartoon rooms are original takes on a kind of show (new names,
/// new characters, new jokes; no logos, no catchphrases); the two real places are as they are.
/// </summary>
public static partial class FirCheckBuilder
{
    public static readonly List<KeyValuePair<string, float[]>> rooms = new List<KeyValuePair<string, float[]>>();   // name, {x0, z0, x1, z1}
    public static readonly List<string> roomStops = new List<string>();
    static int roomFolk, roomGags, roomRides;

    static void BuildRooms(Transform root)
    {
        rooms.Clear(); roomStops.Clear(); roomFolk = 0; roomGags = 0; roomRides = 0;
        RoomCutaway(root); RoomYellow(root); RoomMansion(root); RoomPaper(root); RoomFourOaks(root);
        RoomDino(root); RoomBack(root); RoomQuiet(root); RoomCastaway(root); RoomWishing(root);
        RoomCounter(root); RoomClubhouse(root); RoomStarship(root); RoomMemes(root); RoomOrlando(root);
        RoomMonsters(root); RoomBureau(root); RoomDarkLake(root); RoomSpectrum(root);
        RoomCafe(root); RoomStarHills(root); RoomBrawl(root); RoomGlitch(root); RoomRhythm(root); RoomPonies(root); RoomElements(root); RoomGorilla(root); RoomSunnyBay(root);
        RoomWings(root);
        RoomWings3(root);
        report.Add("The new rooms: " + rooms.Count + " more Station stops (" + string.Join(", ", roomStops) + "); " + roomRides + " rideable rides");
    }

    /// <summary>A material made on first use (plain colour, optional glow and texture).</summary>
    static Material RM(string n, float r, float g, float b, float glow = 0f, string tex = null)
    {
        if (!mats.ContainsKey(n)) AddMat(n, tex, new Color(r, g, b), 1, new Color(r, g, b) * glow);
        return mats[n];
    }

    /// <summary>A new room: AreaShell from z 20, remembered for the tests.</summary>
    static Transform Room(Transform root, string name, float x0, float x1, float z1, float h, string floor, string wall, string ceil, string title, Color col, string sub, string stop)
    {
        var g = AreaShell(root, name, x0, x1, 20f, z1, h, floor, wall, ceil, title, col, sub, stop);
        rooms.Add(new KeyValuePair<string, float[]>(name, new[] { x0, 20f, x1, z1 }));
        roomStops.Add(stop);
        return g;
    }

    static void RF(Transform g, string who, string hex, int act, Vector3 pos, float yaw, string clip, float scale = 1f)
    {
        Folk(g, who, hex, act, pos, yaw, "Rooms/rm_" + clip, scale);
        roomFolk++;
    }

    static FirGag RG(Transform g, string name, Vector3 at, float yaw, string clip, string kind, PrimitiveType shape, Vector3 size, string mat, float amount = 2f)
    {
        roomGags++;
        return Gk(g, name, at, yaw, "Rooms/rm_" + clip, kind, shape, size, mat, amount);
    }

    static void Hat(string who, PrimitiveType t, Vector3 at, Vector3 size, string mat) { if (rigs.TryGetValue(who, out var r)) Prim(t, "Hat", r[2], at, size, M(mat)); }

    /// <summary>A plain building: a box with a roof slab, a door and a sign on its front (the side facing `front`).</summary>
    static Transform House(Transform g, string name, Vector3 at, Vector3 size, string wall, string roof, string sign, Vector3 front)
    {
        var h = Group(g, name, at);
        h.localRotation = Face(front);
        Box(h, "Walls", V(0, size.y / 2f, 0), size, M(wall));
        Box(h, "Roof", V(0, size.y + 0.15f, 0), V(size.x + 0.6f, 0.3f, size.z + 0.6f), M(roof), false);
        Box(h, "Door", V(0, 1.05f, -size.z / 2f - 0.03f), V(1.1f, 2.1f, 0.06f), M("DoorWood"), false);
        foreach (float wx in new[] { -size.x * 0.3f, size.x * 0.3f }) if (size.x > 4f) Box(h, "Window", V(wx, 1.7f, -size.z / 2f - 0.03f), V(1.1f, 0.9f, 0.05f), M("Glass"), false);
        if (sign != null) Text("Sign", h, V(0, size.y - 0.5f, -size.z / 2f - 0.07f), Quaternion.identity, V2(size.x * 0.9f, 0.6f), 0.45f, "<b>" + sign + "</b>").color = Color.white;
        return h;
    }

    static void Palm(Transform g, Vector3 at, float h, int seed)
    {
        float lean = (seed % 3 - 1) * 6f;
        for (int k = 0; k < 4; k++)
            DPrim(PrimitiveType.Cylinder, "Palm trunk", g, at + V(Mathf.Sin(lean * Mathf.Deg2Rad) * k * h / 4f, h / 8f + k * h / 4f, 0), V(0.4f - k * 0.05f, h / 8f, 0.4f - k * 0.05f), M("Trunk"), Quaternion.Euler(0, 0, -lean));
        var top = at + V(Mathf.Sin(lean * Mathf.Deg2Rad) * h, h, 0);
        for (int k = 0; k < 6; k++)
            DPrim(PrimitiveType.Cube, "Palm leaf", g, top + Quaternion.Euler(0, k * 60f + seed * 13f, 0) * V(0, -0.3f, 1.4f), V(0.5f, 0.06f, 3f), RM("PalmLeaf", 0.25f, 0.6f, 0.25f), Quaternion.Euler(25f, k * 60f + seed * 13f, 0));
    }

    static void Oak(Transform g, Vector3 at, float s)
    {
        DPrim(PrimitiveType.Cylinder, "Oak trunk", g, at + V(0, 2.5f * s, 0), V(0.9f * s, 2.5f * s, 0.9f * s), M("Trunk"));
        for (int k = 0; k < 5; k++)
            DPrim(PrimitiveType.Sphere, "Oak leaves", g, at + V(Mathf.Sin(k * 1.3f) * 2.2f * s, (5.5f + (k % 2) * 1.2f) * s, Mathf.Cos(k * 1.3f) * 2.2f * s), Vector3.one * 4f * s, RM("OakLeaf", 0.22f, 0.48f, 0.18f));
    }

    /// <summary>A friendly cartoon dragon made of shapes.</summary>
    static Transform Dragon(Transform g, string name, Vector3 at, float yaw, float s, string mat, bool twoHeads = false)
    {
        var d = Group(g, name, at);
        d.localRotation = Quaternion.Euler(0, yaw, 0);
        Prim(PrimitiveType.Capsule, "Body", d, V(0, 1.1f * s, 0), V(1.1f * s, 0.9f * s, 1.1f * s), M(mat)).transform.localRotation = Quaternion.Euler(90, 0, 0);
        Prim(PrimitiveType.Capsule, "Tail", d, V(0, 0.6f * s, -1.4f * s), V(0.35f * s, 0.8f * s, 0.35f * s), M(mat)).transform.localRotation = Quaternion.Euler(60, 0, 0);
        int heads = twoHeads ? 2 : 1;
        for (int k = 0; k < heads; k++)
        {
            float hx = twoHeads ? (k == 0 ? -0.35f : 0.35f) * s : 0f;
            Prim(PrimitiveType.Capsule, "Neck", d, V(hx, 1.8f * s, 0.6f * s), V(0.3f * s, 0.5f * s, 0.3f * s), M(mat)).transform.localRotation = Quaternion.Euler(20, 0, twoHeads ? (k == 0 ? 15f : -15f) : 0f);
            Prim(PrimitiveType.Sphere, "Head", d, V(hx * 1.6f, 2.4f * s, 0.8f * s), V(0.6f * s, 0.55f * s, 0.75f * s), M(mat));
            foreach (float ex in new[] { -0.15f, 0.15f }) Prim(PrimitiveType.Sphere, "Eye", d, V(hx * 1.6f + ex * s, 2.55f * s, 1.12f * s), Vector3.one * 0.13f * s, M("Black"));
        }
        foreach (float wx in new[] { -1f, 1f })
            Prim(PrimitiveType.Cube, "Wing", d, V(wx * 0.9f * s, 1.7f * s, -0.1f * s), V(1.4f * s, 0.06f * s, 0.9f * s), M(mat)).transform.localRotation = Quaternion.Euler(0, 0, wx * -25f);
        foreach (float lx in new[] { -0.35f, 0.35f }) Prim(PrimitiveType.Cylinder, "Leg", d, V(lx * s, 0.3f * s, 0.2f * s), V(0.3f * s, 0.3f * s, 0.3f * s), M(mat));
        return d;
    }

    /// <summary>A dark-ride shed: walls, roof, a door gap in the south wall at each of doorX, a sign over them.</summary>
    static Transform DarkShed(Transform g, string name, float x0, float x1, float z0, float z1, float h, float[] doorX, string sign, string sub, Color col)
    {
        var s = Group(g, name, Vector3.zero);
        Box(s, "WallW", V(x0, h / 2f, (z0 + z1) / 2f), V(0.3f, h, z1 - z0), M("Black"));
        Box(s, "WallE", V(x1, h / 2f, (z0 + z1) / 2f), V(0.3f, h, z1 - z0), M("Black"));
        Box(s, "WallN", V((x0 + x1) / 2f, h / 2f, z1), V(x1 - x0 + 0.3f, h, 0.3f), M("Black"));
        Box(s, "Roof", V((x0 + x1) / 2f, h + 0.1f, (z0 + z1) / 2f), V(x1 - x0 + 0.4f, 0.2f, z1 - z0 + 0.4f), M("Black"));
        var cuts = doorX.OrderBy(d => d).ToList();
        float a = x0;
        foreach (float dx in cuts)
        {
            float b = dx - 1.4f;
            if (b - a > 0.05f) Box(s, "WallS", V((a + b) / 2f, h / 2f, z0), V(b - a, h, 0.3f), M("Black"));
            Box(s, "WallS lintel", V(dx, (h + 2.6f) / 2f, z0), V(2.8f, h - 2.6f, 0.3f), M("Black"));
            a = dx + 1.4f;
        }
        if (x1 - a > 0.05f) Box(s, "WallS", V((a + x1) / 2f, h / 2f, z0), V(x1 - a, h, 0.3f), M("Black"));
        Text("RideSign", s, V((x0 + x1) / 2f, h - 1.0f, z0 - 0.2f), Face(Vector3.back), V2(x1 - x0 - 1f, 1f), 0.8f, "<b>" + sign + "</b>").color = col;
        Text("RideSub", s, V((x0 + x1) / 2f, h - 1.9f, z0 - 0.2f), Face(Vector3.back), V2(x1 - x0 - 1f, 0.4f), 0.22f, sub).color = Color.white;
        return s;
    }

    /// <summary>A loop ride of carts (kind 1): each cart has two seats side by side; the first segment is the slow boarding stretch.</summary>
    static FirRide CartRide(Transform g, string name, Vector3[] route, int carts, float speed, Transform exit, string cartMat, string seatName, float boardPace = 0.35f)
    {
        var rg = Group(g, name, Vector3.zero);
        var list = new List<Transform>();
        for (int c = 0; c < carts; c++)
        {
            var cart = Group(rg, name + " cart " + c, route[0]);
            Box(cart, "Body", V(0, 0.35f, 0), V(1.5f, 0.6f, 1.9f), M(cartMat), false);
            Box(cart, "Back", V(0, 0.85f, -0.85f), V(1.5f, 0.6f, 0.2f), M(cartMat), false);
            Prim(PrimitiveType.Sphere, "Lamp", cart, V(0, 0.75f, 0.95f), Vector3.one * 0.25f, M("GlowYellow"));
            RideSeat(cart, seatName + " " + c + "a", V(-0.36f, 0.3f, -0.2f), 0f, exit);
            RideSeat(cart, seatName + " " + c + "b", V(0.36f, 0.3f, -0.2f), 0f, exit);
            list.Add(cart);
        }
        var pace = new float[route.Length];
        for (int i = 0; i < pace.Length; i++) pace[i] = i == 0 ? boardPace : 1f;
        var r = Add<FirRide>(rg.gameObject);
        r.kind = 1; r.carts = list.ToArray(); r.route = route; r.pace = pace; r.speed = speed;
        roomRides++;
        return r;
    }

    /// <summary>Makes a walk-in gag (it fires when a rider, or a walker, comes into the zone `ahead` metres in front of it).</summary>
    static FirGag RideGag(FirGag gag, float ahead, float w = 2.6f)
    {
        gag.walkIn = true;
        gag.zoneCenter = V(0, 1f, ahead); gag.zoneSize = V(w, 2.6f, w);
        return gag;
    }

    // ================================================================================================ 1 the Cutaway House

    static void RoomCutaway(Transform root)
    {
        float cx = 2920f;
        var g = Room(root, "The Cutaway House", 2900f, 2940f, 56f, 6f, "Floor2", "Wall2", "Ceiling",
                     "THE CUTAWAY HOUSE", new Color(0.4f, 0.8f, 1f), "a cartoon family, a sofa, a television, and a cutaway every ninety seconds", "CUTAWAY HOUSE");
        Box(g, "Rug", V(cx, 0.006f, 48f), V(10f, 0.01f, 7f), M("Rug"), false);
        Box(g, "Couch", V(cx, 0.3f, 52.4f), V(4.4f, 0.6f, 1.2f), RM("CouchGreen", 0.45f, 0.6f, 0.35f));
        Box(g, "Couch back", V(cx, 0.85f, 53.1f), V(4.4f, 0.9f, 0.3f), RM("CouchGreen", 0.45f, 0.6f, 0.35f), false);
        Box(g, "Coffee table", V(cx, 0.25f, 49.6f), V(2f, 0.5f, 1f), M("DeskWood"));
        Box(g, "TV stand", V(cx, 0.35f, 45.5f), V(2.4f, 0.7f, 0.8f), M("DoorWood"));
        Box(g, "TV", V(cx, 1.15f, 45.5f), V(1.8f, 0.9f, 0.4f), M("Black"), false);
        Box(g, "TV glow", V(cx, 1.15f, 45.75f), V(1.6f, 0.75f, 0.02f), M("DarkGlowCyan"), false);
        Box(g, "Recliner", V(cx + 5f, 0.4f, 50.5f), V(1.3f, 0.8f, 1.3f), RM("Recliner", 0.55f, 0.35f, 0.25f));
        Box(g, "Kitchen counter", V(2902f, 0.45f, 38f), V(1.6f, 0.9f, 12f), M("LabWhite"));
        Box(g, "Fridge", V(2902f, 1f, 46f), V(1.3f, 2f, 1.1f), M("LabWhite"));
        Box(g, "Kitchen table", V(2909f, 0.4f, 34f), V(2.4f, 0.8f, 1.4f), M("DeskWood"));
        foreach (float tz in new[] { 32.8f, 35.2f }) Box(g, "Chair", V(2909f, 0.25f, tz), V(0.5f, 0.5f, 0.5f), M("DeskWood"));
        for (int k = 0; k < 6; k++) Box(g, "Stair", V(2936.5f, 0.25f + k * 0.5f, 40f + k * 0.6f), V(3f, 0.5f + k * 1f, 0.6f), M("Floor2"));
        Box(g, "Front door", V(2939.95f, 1.1f, 30f), V(0.06f, 2.2f, 1.1f), M("DoorWood"), false);
        Box(g, "Family portrait", V(2900.06f, 2.4f, 30f), V(0.04f, 1.2f, 2f), M("GlowGold"), false);
        Box(g, "Portrait print", V(2900.1f, 2.4f, 30f), V(0.02f, 1f, 1.8f), M("Paper"), false);
        RF(g, "Big Dad Gus", "#fafafa", 16, V(cx - 3f, 0, 48.5f), 180f, "cut_dad");
        Hat("Big Dad Gus", PrimitiveType.Cube, V(0, 0.02f, 0.13f), V(0.24f, 0.05f, 0.02f), "Black");
        RF(g, "Mom Lorraine", "#80cbc4", 9, V(2904.5f, 0, 40f), 90f, "cut_mom");
        Hat("Mom Lorraine", PrimitiveType.Capsule, V(0, 0.32f, -0.02f), V(0.26f, 0.32f, 0.26f), "GlowOrange");
        RF(g, "Baby Mortimer", "#ffe0b2", 20, V(cx + 2.5f, 0, 47.5f), 200f, "cut_baby", 0.55f);
        RF(g, "Wilbur the Dog", "#eeeeee", 11, V(cx + 6.5f, 0, 47f), 220f, "cut_dog");
        Hat("Wilbur the Dog", PrimitiveType.Sphere, V(0, 0.02f, 0.18f), V(0.1f, 0.08f, 0.1f), "Black");
        var cut = RG(g, "The cutaway", V(2932f, 0, 34f), 0f, "cut_cutaway", "slide", PrimitiveType.Cube, V(2.4f, 1.6f, 0.08f), "GlowWarm", 2.5f);
        cut.target.localPosition = V(0, 0.8f, 0);
        var sky = Mini(cut.target, "Cutaway actor", V(0, 0.1f, -0.1f), 180f, 0.5f, "#ffab91");
        var rooster = RG(g, "The giant rooster", V(2932f, 0, 50f), 220f, "cut_rooster", "pop", PrimitiveType.Capsule, V(1.4f, 2.6f, 1.4f), "GlowYellow", 3f);
        Prim(PrimitiveType.Cube, "Comb", rooster.target, V(0, 2.8f, 0), V(0.2f, 0.5f, 0.6f), M("GlowRed"));
        Prim(PrimitiveType.Cube, "Beak", rooster.target, V(0, 2.1f, 0.7f), V(0.25f, 0.2f, 0.4f), M("GlowOrange"));
        var knee = RG(g, "The bad knee", V(2912f, 0, 27f), 180f, "cut_knee", "shake", PrimitiveType.Sphere, V(0.3f, 0.3f, 0.3f), "BoneWhite");
        Mini(knee.target, "Knee victim", V(0, 0, 0), 180f, 0.8f, "#90caf9");
        report.Add("The Cutaway House: a cartoon family living room (sofa, TV, recliner, kitchen, stairs); 4 voiced people, 3 gags (the cutaway, the giant rooster, the bad knee)");
    }

    // ================================================================================================ 2 Yellowville

    static void RoomYellow(Transform root)
    {
        float cx = 2990f;
        var g = Room(root, "Yellowville", 2960f, 3020f, 80f, 20f, "Grass", "PixelSky", "Sky",
                     "YELLOWVILLE", new Color(1f, 0.85f, 0.1f), "a yellow cartoon town. pop. 30,000 and one very unsafe power plant", "YELLOWVILLE");
        Box(g, "Road", V(cx, 0.01f, 34f), V(60f, 0.02f, 6f), M("TurfGrey"), false);
        for (int k = 0; k < 10; k++) Box(g, "Road line", V(2963f + k * 6f, 0.025f, 34f), V(2.4f, 0.01f, 0.2f), M("GlowYellow"), false);
        var house = House(g, "The family house", V(cx, 0, 64f), V(16f, 7f, 10f), "GemPink", "PixelRoofBlue", null, Vector3.back);
        Box(g, "Garage", V(cx + 11f, 2f, 64f), V(6f, 4f, 9f), RM("YellowHouse", 0.95f, 0.6f, 0.55f));
        Box(g, "Living room rug", V(cx, 0.006f, 51f), V(8f, 0.01f, 5f), M("Rug"), false);
        Box(g, "TV", V(cx, 0.6f, 47.5f), V(1.6f, 1.2f, 0.9f), M("DoorWood"));
        Box(g, "Painting of a boat", V(cx, 2.5f, 58.95f), V(2f, 1.2f, 0.06f), M("PoolBlue"), false);
        foreach (float tx in new[] { 2967f, 2976f })
        {
            DPrim(PrimitiveType.Cylinder, "Cooling tower", g, V(tx, 7f, 72f + (tx - 2967f) * 0.5f), V(7f, 7f, 7f), M("Concrete"));
            DPrim(PrimitiveType.Sphere, "Steam", g, V(tx, 15.5f, 72f + (tx - 2967f) * 0.5f), V(6f, 3f, 6f), M("Mist"));
        }
        Text("PlantSign", g, V(2971f, 2.5f, 66.3f), Face(Vector3.back), V2(10f, 1f), 0.6f, "<b>POWER PLANT</b>\n<size=60%>days without an accident: 0</size>").color = Color.white;
        House(g, "The tavern", V(2970f, 0, 46f), V(8f, 4f, 6f), "BrickWall", "TrimDark", "THE TAVERN", Vector3.right);
        House(g, "The donut shop", V(3010f, 0, 46f), V(8f, 4f, 6f), "CaneWhite", "GemPink", "DONUTS", Vector3.left);
        House(g, "The corner shop", V(3010f, 0, 60f), V(8f, 4f, 7f), "LabWhite", "GiftGreen", "SHOP-N-GO", Vector3.left);
        Box(g, "Church", V(2968f, 4f, 58f), V(7f, 8f, 6f), M("CaneWhite"));
        DPrim(PrimitiveType.Cube, "Steeple", g, V(2968f, 9.5f, 58f), V(2f, 3f, 2f), M("CaneWhite"), Quaternion.Euler(0, 45, 0));
        RF(g, "Plant Worker Duane", "#ffd90f", 16, V(2984f, 0, 44f), 180f, "yel_dad");
        RF(g, "Mom Marigold", "#ffd90f", 9, V(2995f, 0, 55f), 180f, "yel_mom");
        Hat("Mom Marigold", PrimitiveType.Capsule, V(0, 0.45f, 0), V(0.25f, 0.5f, 0.25f), "GiftGreen");
        RF(g, "Skater Kid Skip", "#ffd90f", 21, V(2999f, 0, 39f), 200f, "yel_kid", 0.7f);
        RF(g, "Old Mr Ledger", "#fff59d", 3, V(2978f, 0, 62f), 160f, "yel_boss");
        var couch = RG(g, "The couch gag", V(cx, 0, 52.5f), 180f, "yel_couch", "slide", PrimitiveType.Cube, V(3.4f, 0.8f, 1f), "GemPurple", 2f);
        Prim(PrimitiveType.Cube, "Couch back", couch.target, V(0, 1f, -0.45f), V(3.4f, 0.8f, 0.2f), M("GemPurple"));
        RG(g, "The glowing rod", V(2981f, 0, 46f), 180f, "yel_rod", "drop", PrimitiveType.Capsule, V(0.25f, 0.6f, 0.25f), "GlowGreen", 2f);
        var donut = RG(g, "The giant donut", V(3004.5f, 0, 42f), 0f, "yel_donut", "spin", PrimitiveType.Cylinder, V(2.2f, 0.25f, 2.2f), "GemPink");
        Prim(PrimitiveType.Cylinder, "Hole", donut.target, V(0, 0.14f, 0), V(0.8f, 0.27f, 0.8f), M("Black"));
        RG(g, "The skateboard", V(2996f, 0, 30f), 90f, "yel_skate", "slide", PrimitiveType.Cube, V(0.3f, 0.12f, 0.9f), "GlowRed", 6f);
        report.Add("Yellowville: a yellow cartoon town (the family house, a power plant with two cooling towers, the tavern, a donut shop, a corner shop, a church); 4 voiced people, 4 gags");
    }

    // ================================================================================================ 3 the Spooky Mansion Case (with a dark ride)

    static void RoomMansion(Transform root)
    {
        var g = Room(root, "The Spooky Mansion Case", 3040f, 3090f, 70f, 10f, "Floor2", "WallDark", "Black",
                     "THE SPOOKY MANSION CASE", new Color(0.6f, 1f, 0.4f), "four teenagers, one dog, one van, and a ghost who is definitely the groundskeeper", "SPOOKY MANSION");
        var van = Group(g, "The mystery van", V(3048f, 0, 31f));
        Box(van, "Body", V(0, 1.2f, 0), V(2.2f, 1.8f, 4.6f), RM("VanTeal", 0.2f, 0.65f, 0.55f));
        Box(van, "Stripe", V(0, 1.2f, 0), V(2.25f, 0.4f, 4.4f), M("GlowOrange"), false);
        foreach (float wz in new[] { -1.5f, 1.5f }) foreach (float wx in new[] { -1.05f, 1.05f }) DPrim(PrimitiveType.Cylinder, "Wheel", van, V(wx, 0.4f, wz), V(0.8f, 0.12f, 0.8f), M("Black"), Quaternion.Euler(0, 0, 90));
        for (int k = 0; k < 5; k++) Prim(PrimitiveType.Sphere, "Flower", van, V(1.13f, 1.6f, -1.6f + k * 0.8f), V(0.05f, 0.35f, 0.35f), M(k % 2 == 0 ? "GlowOrange" : "GlowGreen"));
        for (int k = 0; k < 6; k++)
        {
            Box(g, "Hallway door", V(3040.06f, 1.2f, 36f + k * 5f), V(0.06f, 2.4f, 1.2f), M("DoorWood"), false);
            Prim(PrimitiveType.Sphere, "Knob", g, V(3040.12f, 1.1f, 36.4f + k * 5f), Vector3.one * 0.08f, M("Brass"));
        }
        foreach (float az in new[] { 40f, 56f }) { DPrim(PrimitiveType.Capsule, "Armour", g, V(3042f, 1f, az), V(0.6f, 1f, 0.5f), M("Metal")); DPrim(PrimitiveType.Sphere, "Helm", g, V(3042f, 2.2f, az), Vector3.one * 0.45f, M("Metal")); }
        Box(g, "Bookcase", V(3058f, 1.5f, 69.6f), V(4f, 3f, 0.6f), M("DoorWood"));
        for (int k = 0; k < 12; k++) Box(g, "Book", V(3056.4f + (k % 6) * 0.6f, 0.9f + (k / 6) * 1.1f, 69.25f), V(0.3f, 0.7f, 0.1f), M(new[] { "Book1", "Book2", "Book3" }[k % 3]), false);
        // the haunted dining hall (east side)
        Box(g, "Dining table", V(3077f, 0.45f, 52f), V(3f, 0.9f, 20f), M("DoorWood"));
        Box(g, "Tablecloth", V(3077f, 0.91f, 52f), V(3.1f, 0.02f, 20.1f), M("CaneWhite"), false);
        for (int k = 0; k < 8; k++) foreach (float sx in new[] { -2.2f, 2.2f }) Box(g, "Chair", V(3077f + sx, 0.5f, 44f + k * 2.3f), V(0.6f, 1f, 0.6f), M("DoorWood"));
        for (int k = 0; k < 3; k++) { DPrim(PrimitiveType.Cylinder, "Candelabra", g, V(3077f, 1.25f, 45f + k * 7f), V(0.12f, 0.35f, 0.12f), M("Brass")); Prim(PrimitiveType.Sphere, "Candle flame", g, V(3077f, 1.7f, 45f + k * 7f), V(0.12f, 0.2f, 0.12f), M("GlowFlame")); }
        for (int k = 0; k < 6; k++) Prim(PrimitiveType.Sphere, "Ghost", g, V(3071f + (k % 3) * 6f, 3.4f + (k % 2) * 0.8f, 40f + k * 4.5f), V(0.7f, 1f, 0.7f), M("GlowGhost"));
        Box(g, "Fireplace", V(3089.4f, 1.2f, 52f), V(1.2f, 2.4f, 3f), M("BrickWall"));
        Prim(PrimitiveType.Sphere, "Fire", g, V(3088.7f, 0.5f, 52f), V(0.8f, 0.6f, 1.4f), M("GlowFlame"));
        RG(g, "The unmasking", V(3083f, 0, 40f), 180f, "man_unmask", "flip", PrimitiveType.Sphere, V(0.6f, 0.7f, 0.5f), "GlowGhost").target.localPosition = V(0, 1.8f, 0);
        RF(g, "Detective Dot", "#ffb74d", 9, V(3050f, 0, 42f), 90f, "man_dot");
        Hat("Detective Dot", PrimitiveType.Cube, V(0, 0.02f, 0.14f), V(0.24f, 0.06f, 0.02f), "Black");
        RF(g, "Hungry Hank", "#aed581", 11, V(3045f, 0, 37f), 90f, "man_hank");
        RF(g, "Ascot Alex", "#90caf9", 7, V(3056f, 0, 50f), 200f, "man_alex");
        RF(g, "Groundskeeper Gribble", "#a1887f", 19, V(3060f, 0, 64f), 180f, "man_gribble");
        var chase = RG(g, "The door chase", V(3042.5f, 0, 46f), 0f, "man_chase", "slide", PrimitiveType.Sphere, V(0.6f, 0.9f, 0.6f), "GlowGhost", 9f);
        chase.target.localPosition = V(0, 1.1f, 0);
        var sand = RG(g, "The sandwich tower", V(3052f, 0, 28.5f), 180f, "man_sandwich", "grow", PrimitiveType.Cube, V(0.6f, 0.15f, 0.6f), "Bun");
        for (int k = 1; k < 7; k++) Prim(PrimitiveType.Cube, "Layer", sand.target, V(0, 0.07f + k * 0.15f, 0), V(0.62f, 0.12f, 0.62f), M(new[] { "GiftGreen", "GlowRed", "CornYellow", "Bun" }[k % 4]));
        var eyes = RG(g, "The portrait's eyes", V(3041.4f, 0, 62f), 90f, "man_portrait", "shake", PrimitiveType.Cube, V(1.2f, 1.4f, 0.06f), "Paper");
        eyes.target.localPosition = V(0, 1.6f, -0.5f);
        foreach (float ex in new[] { -0.2f, 0.2f }) Prim(PrimitiveType.Sphere, "Eye", eyes.target, V(ex, 1.0f, 0.05f), Vector3.one * 0.15f, M("GlowYellow"));
        report.Add("The Spooky Mansion Case: the mystery van, a hallway of doors, suits of armour, a bookcase and a haunted dining hall; 4 voiced people, 4 gags");
    }

    // ================================================================================================ 4 Paper Mountain Town

    static void RoomPaper(Transform root)
    {
        var g = Room(root, "Paper Mountain Town", 3110f, 3160f, 70f, 20f, "Snow", "PixelSky", "Sky",
                     "PAPER MOUNTAIN TOWN", new Color(1f, 1f, 1f), "a quiet little mountain town, cut out of construction paper. nothing happens here. constantly.", "PAPER MOUNTAIN");
        foreach (float mx in new[] { 3115f, 3125f, 3145f, 3155f })   // a pass in the middle, where the wing's doorway is
            DPrim(PrimitiveType.Cube, "Paper mountain", g, V(mx, 0, 69.4f), V(10f, 10f, 0.3f), M(mx % 20f < 10f ? "CaneWhite" : "LabGrey"), Quaternion.Euler(0, 0, 45));
        string[] wall = { "GiftBlue", "GiftGreen", "CaneRed", "GlowYellow" };
        for (int k = 0; k < 4; k++)
        {
            var h = Group(g, "Paper house " + k, V(3117f + k * 12f, 0, 54f));
            Box(h, "Front", V(0, 2f, 0), V(6f, 4f, 0.3f), M(wall[k]));
            DPrim(PrimitiveType.Cube, "Roof", h, V(0, 4f, 0), V(4.2f, 4.2f, 0.25f), M("TrimDark"), Quaternion.Euler(0, 0, 45));
            Box(h, "Door", V(0, 1f, -0.17f), V(1f, 2f, 0.04f), M("DoorWood"), false);
            Box(h, "Window", V(1.8f, 2.4f, -0.17f), V(1f, 1f, 0.04f), M("Glass"), false);
        }
        Box(g, "Icy pond", V(3118f, 0.02f, 40f), V(8f, 0.04f, 6f), M("Glass"), false);
        House(g, "The school", V(3150f, 0, 36f), V(10f, 5f, 6f), "BrickWall", "TrimDark", "PAPER MOUNTAIN ELEMENTARY", Vector3.left);
        DPrim(PrimitiveType.Cylinder, "Bus stop pole", g, V(3128f, 1.3f, 31f), V(0.1f, 1.3f, 0.1f), M("Metal"));
        Box(g, "Bus stop sign", V(3128f, 2.7f, 31f), V(0.7f, 0.5f, 0.05f), M("CaneRed"), false);
        RF(g, "Paper Kid Petey", "#ffccbc", 21, V(3126f, 0, 33f), 180f, "pap_petey", 0.75f);
        Hat("Paper Kid Petey", PrimitiveType.Sphere, V(0, 0.08f, 0), V(0.34f, 0.2f, 0.34f), "GiftBlue");
        RF(g, "Paper Kid Wendell", "#ffe0b2", 22, V(3130f, 0, 33f), 180f, "pap_wendell", 0.75f);
        Hat("Paper Kid Wendell", PrimitiveType.Sphere, V(0, 0.08f, 0), V(0.34f, 0.2f, 0.34f), "CaneRed");
        RF(g, "Lunch Chef Gravy", "#795548", 16, V(3145f, 0, 42.5f), 180f, "pap_chef");
        Hat("Lunch Chef Gravy", PrimitiveType.Cylinder, V(0, 0.2f, 0), V(0.25f, 0.18f, 0.25f), "CaneWhite");
        RF(g, "Mayor Muriel", "#f8bbd0", 7, V(3138f, 0, 46f), 180f, "pap_mayor");
        RG(g, "The paper bus", V(3114f, 0, 28f), 90f, "pap_bus", "slide", PrimitiveType.Cube, V(2f, 2f, 5f), "GlowYellow", 6f);
        var cow = RG(g, "The cutout cow", V(3150f, 0, 61f), 180f, "pap_cow", "pop", PrimitiveType.Cube, V(2f, 1.2f, 0.1f), "CaneWhite", 1.6f);
        foreach (float sx in new[] { -0.5f, 0.4f }) Prim(PrimitiveType.Cube, "Spot", cow.target, V(sx, 0.7f, -0.06f), V(0.4f, 0.4f, 0.02f), M("Black"));
        var mob = RG(g, "The angry mob", V(3138f, 0, 60f), 180f, "pap_mob", "shake", PrimitiveType.Cylinder, V(0.06f, 2f, 0.06f), "Trunk");
        for (int k = 0; k < 4; k++) { var c = Prim(PrimitiveType.Cube, "Cutout villager", mob.target, V(-1.2f + k * 0.8f, 0.8f, 0.3f), V(0.6f, 1.6f, 0.05f), M(wall[k])); Prim(PrimitiveType.Sphere, "Head", mob.target, V(-1.2f + k * 0.8f, 1.8f, 0.3f), V(0.55f, 0.45f, 0.05f), M("BonePink")); }
        RG(g, "The snowball", V(3122f, 0, 46f), 0f, "pap_snowball", "fly", PrimitiveType.Sphere, V(0.4f, 0.4f, 0.4f), "Snow", 4f);
        report.Add("Paper Mountain Town: a cut-out paper town (paper mountains, four paper houses, an icy pond, the school, the bus stop); 4 voiced people, 4 gags");
    }

    // ================================================================================================ 5 Four Oaks, NC

    static void RoomFourOaks(Transform root)
    {
        var g = Room(root, "Four Oaks", 3180f, 3240f, 80f, 25f, "Grass", "PixelSky", "Sky",
                     "FOUR OAKS, NC", new Color(0.55f, 0.35f, 0.15f), "Johnston County. a small town on the railroad, home of the Acorn Festival", "FOUR OAKS, NC");
        Box(g, "Main Street", V(3196f, 0.01f, 51f), V(6f, 0.02f, 54f), M("TurfGrey"), false);
        string[] shops = { "TOWN HALL", "HARDWARE", "DINER", "BARBER", "ANTIQUES" };
        for (int k = 0; k < shops.Length; k++)
        {
            var s = House(g, "Main Street " + shops[k], V(3186f, 0, 34f + k * 8f), V(7f, 5f + (k % 2), 7f), k % 2 == 0 ? "BrickWall" : "Brick", "TrimDark", shops[k], Vector3.right);
            DPrim(PrimitiveType.Cube, "Awning", s, V(0, 2.6f, -3.9f), V(5f, 0.08f, 1.2f), M(k % 2 == 0 ? "GiftGreen" : "CaneRed"), Quaternion.Euler(15, 0, 0));
        }
        // the water tower
        foreach (float lx in new[] { -2.5f, 2.5f }) foreach (float lz in new[] { -2.5f, 2.5f }) DPrim(PrimitiveType.Cylinder, "Tower leg", g, V(3228f + lx, 6f, 70f + lz), V(0.35f, 6f, 0.35f), M("Metal"));
        DPrim(PrimitiveType.Cylinder, "Tank", g, V(3228f, 14f, 70f), V(8f, 2.5f, 8f), M("LabWhite"));
        DPrim(PrimitiveType.Sphere, "Tank top", g, V(3228f, 16.4f, 70f), V(8f, 2f, 8f), M("LabWhite"));
        Text("TowerName", g, V(3228f, 14.2f, 65.9f), Face(Vector3.back), V2(7f, 1.4f), 1.1f, "<b>FOUR OAKS</b>").color = new Color(0.1f, 0.3f, 0.15f);
        Oak(g, V(3204f, 0, 73f), 1.1f); Oak(g, V(3216f, 0, 73f), 1f); Oak(g, V(3205f, 0, 52f), 1f); Oak(g, V(3226f, 0, 54f), 1.15f);
        // the old depot, where the railroad built the town
        House(g, "The old depot", V(3228f, 0, 26.5f), V(9f, 4f, 4f), "DeskWood", "TrimDark", "FOUR OAKS DEPOT", Vector3.back);
        Scatter(g, "Cotton", 40, 3217f, 3235f, 33f, 43f, PrimitiveType.Sphere, V(0.35f, 0.35f, 0.35f), new[] { "CaneWhite" }, 0.7f);
        Scatter(g, "Cotton plant", 40, 3217f, 3235f, 33f, 43f, PrimitiveType.Cylinder, V(0.08f, 0.3f, 0.08f), new[] { "GiftGreen" });
        var sweet = Stall(g, "SWEET TEA", V(3201f, 0, 30f), 90f, "GiftGreen", "CaneWhite");
        var sign = Group(g, "Marker", V(3233f, 0, 60f));
        DPrim(PrimitiveType.Cylinder, "Post", sign, V(0, 1f, 0), V(0.12f, 1f, 0.12f), M("Metal"));
        Box(sign, "Plate", V(0, 2.4f, 0), V(2.6f, 1.2f, 0.06f), RM("MarkerBlack", 0.1f, 0.1f, 0.1f), false);
        Text("MarkerText", sign, V(0, 2.4f, -0.04f), Quaternion.identity, V2(2.4f, 1f), 0.16f, "<b>BENTONVILLE BATTLEFIELD</b>\na few miles east. the largest Civil War battle fought in North Carolina, March 1865").color = new Color(0.95f, 0.85f, 0.4f);
        var i95 = Group(g, "Highway sign", V(3236f, 0, 76f));
        foreach (float px in new[] { -1.5f, 1.5f }) DPrim(PrimitiveType.Cylinder, "Post", i95, V(px, 2f, 0), V(0.12f, 2f, 0.12f), M("Metal"));
        Box(i95, "Plate", V(0, 4.2f, 0), V(3.6f, 1.4f, 0.06f), RM("HighwayGreen", 0.05f, 0.4f, 0.2f), false);
        Text("Highway", i95, V(0, 4.2f, -0.04f), Quaternion.identity, V2(3.4f, 1.2f), 0.4f, "<b>I-95</b>\n<size=60%>NORTH: virginia   SOUTH: florida</size>").color = Color.white;
        var acorns = new List<Vector3>(); for (int k = 0; k < 10; k++) acorns.Add(V(3192f + (k % 5) * 9f, 0.6f, 50f + (k / 5) * 20f + (k % 2) * 3f));
        Collectables(g, "Acorns", acorns.ToArray(), (t, i) => { Prim(PrimitiveType.Sphere, "Acorn", t, Vector3.zero, V(0.3f, 0.4f, 0.3f), M("Trunk")); Prim(PrimitiveType.Sphere, "Cap", t, V(0, 0.15f, 0), V(0.34f, 0.15f, 0.34f), M("TrimDark")); return true; }, V(3200f, 1.6f, 20.15f), Vector3.forward);
        RF(g, "Farmer Earl", "#ffcc80", 16, V(3222f, 0, 50f), 180f, "oak_farmer");
        Hat("Farmer Earl", PrimitiveType.Cylinder, V(0, 0.12f, 0), V(0.42f, 0.04f, 0.42f), "CornYellow");
        RF(g, "Festival Queen Darlene", "#f48fb1", 4, V(3199.5f, 0, 60f), 90f, "oak_queen");
        Hat("Festival Queen Darlene", PrimitiveType.Cylinder, V(0, 0.14f, 0), V(0.22f, 0.06f, 0.22f), "GlowGold");
        RF(g, "Sweet Tea Sue", "#ffe082", 9, V(3201f, 0, 29.7f), 90f, "oak_tea");
        RF(g, "Depot Master Hollis", "#b0bec5", 20, V(3222f, 0, 30f), 180f, "oak_depot");
        Hat("Depot Master Hollis", PrimitiveType.Cylinder, V(0, 0.12f, 0), V(0.3f, 0.08f, 0.3f), "PoliceBlue");
        var acorn = RG(g, "The giant acorn", V(3199.5f, 0, 66f), 90f, "oak_acorn", "drop", PrimitiveType.Sphere, V(1.4f, 1.8f, 1.4f), "Trunk", 4f);
        Prim(PrimitiveType.Sphere, "Cap", acorn.target, V(0, 1.6f, 0), V(1.55f, 0.7f, 1.55f), M("TrimDark"));
        var tractor = RG(g, "The tractor", V(3214f, 0, 62f), 90f, "oak_tractor", "slide", PrimitiveType.Cube, V(1.6f, 1.4f, 2.6f), "GiftGreen", 4f);
        foreach (float tz in new[] { -0.9f, 0.9f }) Prim(PrimitiveType.Cylinder, "Wheel", tractor.target, V(0.9f, 0.6f, tz), V(1.2f, 0.12f, 1.2f), M("Black")).transform.localRotation = Quaternion.Euler(0, 0, 90);
        RG(g, "The squirrel", V(3207.5f, 0, 54f), 180f, "oak_squirrel", "pop", PrimitiveType.Capsule, V(0.3f, 0.5f, 0.3f), "TurfGrey", 1.2f);
        report.Add("Four Oaks, NC: Main Street (town hall, hardware, diner, barber, antiques), the water tower, four oaks, a cotton field, the Bentonville marker, the I-95 sign, a sweet tea stand, the old depot and 10 acorns to collect; 4 voiced people, 3 gags");
    }

    // ================================================================================================ 6 the Dino Sing-Along Studio

    static void RoomDino(Transform root)
    {
        float cx = 3280f;
        var g = Room(root, "The Dino Sing-Along Studio", 3260f, 3300f, 56f, 8f, "Floor2", "WallGreen", "Ceiling",
                     "THE DINO SING-ALONG STUDIO", new Color(0.3f, 0.85f, 0.75f), "a kids' TV set. a very big, very friendly dinosaur. songs about sharing.", "DINO STUDIO");
        string[] bands = { "GlowRed", "GlowOrange", "GlowYellow", "GlowGreen", "GiftBlue", "GemPurple" };
        for (int k = 0; k < bands.Length; k++) Box(g, "Rainbow band", V(cx, 6.6f - k * 0.45f, 55.92f), V(30f - k * 2f, 0.45f, 0.06f), M(bands[k]), false);
        DPrim(PrimitiveType.Cylinder, "Treehouse trunk", g, V(3266f, 2f, 50f), V(1f, 2f, 1f), M("Trunk"));
        Box(g, "Treehouse floor", V(3266f, 4.1f, 50f), V(4f, 0.2f, 4f), M("DeskWood"));
        Box(g, "Treehouse", V(3266f, 5.2f, 50f), V(3f, 2f, 3f), M("GlowOrange"), false);
        for (int k = 0; k < 8; k++) Box(g, "Ladder rung", V(3267.6f, 0.4f + k * 0.5f, 50f), V(0.1f, 0.06f, 0.8f), M("DeskWood"), false);
        DPrim(PrimitiveType.Cube, "Slide", g, V(3294f, 1.2f, 50f), V(1f, 0.1f, 5f), M("GlowYellow"), Quaternion.Euler(-28f, 0, 0));
        Box(g, "Slide steps", V(3294f, 1.2f, 52.6f), V(1f, 2.4f, 0.6f), M("GiftBlue"));
        for (int k = 0; k < 3; k++) Box(g, "Bleacher", V(3294.5f, 0.25f + k * 0.5f, 27f + k * 1.2f), V(8f, 0.5f + k * 1f, 1.2f), M("Seat"));
        for (int k = 0; k < 2; k++)
        {
            DPrim(PrimitiveType.Cylinder, "Tripod", g, V(3270f + k * 20f, 0.7f, 34f), V(0.15f, 0.7f, 0.15f), M("Black"));
            DPrim(PrimitiveType.Cube, "Camera", g, V(3270f + k * 20f, 1.55f, 34f), V(0.5f, 0.4f, 0.8f), M("Black"));
        }
        Box(g, "Toy chest", V(3290f, 0.4f, 44f), V(1.6f, 0.8f, 1f), M("CaneRed"));
        RF(g, "Dudley the Dinosaur", "#4db6ac", 18, V(cx, 0, 49f), 180f, "din_dudley", 1.5f);
        if (rigs.TryGetValue("Dudley the Dinosaur", out var dr)) { Prim(PrimitiveType.Sphere, "Snout", dr[2], V(0, -0.03f, 0.16f), V(0.26f, 0.18f, 0.24f), M("GiftGreen")); Prim(PrimitiveType.Capsule, "Tail", dr[1], V(0, -0.2f, -0.35f), V(0.18f, 0.4f, 0.18f), M("GiftGreen")).transform.localRotation = Quaternion.Euler(-60, 0, 0); }
        RF(g, "Studio Kid Kayla", "#ffccbc", 21, V(cx - 3.5f, 0, 46f), 160f, "din_kayla", 0.7f);
        RF(g, "Studio Kid Marcus", "#8d6e63", 22, V(cx + 3.5f, 0, 46f), 200f, "din_marcus", 0.7f);
        RF(g, "Floor Manager Fran", "#b39ddb", 20, V(3267f, 0, 30f), 90f, "din_fran");
        var chest = RG(g, "The toy chest", V(3290f, 0, 44f), 180f, "din_chest", "flip", PrimitiveType.Cube, V(1.6f, 0.12f, 1f), "CaneRed");
        chest.target.localPosition = V(0, 0.8f, 0);
        var bub = RG(g, "The bubble machine", V(3272f, 0, 40f), 180f, "din_bubbles", "spin", PrimitiveType.Cube, V(0.6f, 0.5f, 0.6f), "GiftBlue");
        for (int k = 0; k < 6; k++) Prim(PrimitiveType.Sphere, "Bubble", bub.target, V(Mathf.Sin(k) * 0.8f, 0.8f + k * 0.2f, Mathf.Cos(k) * 0.8f), Vector3.one * 0.22f, M("Glass"));
        RG(g, "The big hug", V(3287f, 0, 37f), 180f, "din_hug", "grow", PrimitiveType.Sphere, V(0.9f, 0.9f, 0.9f), "BonePink");
        report.Add("The Dino Sing-Along Studio: a kids' TV set (a rainbow, a treehouse, a slide, bleachers, cameras, a toy chest) and Dudley the Dinosaur; 4 voiced people, 3 gags");
    }

    // ================================================================================================ 7 the Back Rooms

    static void RoomBack(Transform root)
    {
        float x0 = 3320f, x1 = 3380f;
        var wallMat = RM("BackYellow", 0.86f, 0.8f, 0.45f, 0f, "wallpaper");
        RM("BackCarpet", 0.72f, 0.64f, 0.42f, 0f, "office_carpet");
        RM("BackCeiling", 0.9f, 0.88f, 0.74f, 0f, "ceiling");
        var g = Room(root, "The Back Rooms", x0, x1, 80f, 3.2f, "BackCarpet", "BackYellow", "BackCeiling",
                     "THE BACK ROOMS", new Color(0.4f, 0.35f, 0.15f), "you were not supposed to find this place. the carpet is damp. the lights hum.", "BACK ROOMS");
        for (int ix = 0; ix < 10; ix++) for (int iz = 0; iz < 10; iz++)
            Box(g, "Light panel", V(x0 + 3f + ix * 6f, 3.17f, 23f + iz * 6f), V(1.2f, 0.04f, 0.6f), RM("BackLight", 1f, 1f, 0.92f, 0.9f), false);
        // the maze: a grid of wall pieces, each with a gap, so every room joins every other
        int walls = 0;
        for (int k = 1; k <= 5; k++) for (int m = 0; m < 5; m++)
        {
            float x = x0 + k * 10f, za = 30f + m * 10f, gap = za + 2f + Mathf.Repeat(k * 0.618f + m * 0.382f, 1f) * 6f;
            if ((k + m) % 4 == 3) continue;
            if (gap - 1.3f - za > 0.1f) { Box(g, "Maze wall", V(x, 1.6f, (za + gap - 1.3f) / 2f), V(0.25f, 3.2f, gap - 1.3f - za), wallMat); walls++; }
            if (za + 10f - gap - 1.3f > 0.1f) { Box(g, "Maze wall", V(x, 1.6f, (gap + 1.3f + za + 10f) / 2f), V(0.25f, 3.2f, za + 10f - gap - 1.3f), wallMat); walls++; }
        }
        for (int m = 0; m < 5; m++) for (int k = 0; k < 6; k++)
        {
            float z = 34f + m * 10f, xa = x0 + k * 10f, gap = xa + 2f + Mathf.Repeat(m * 0.618f + k * 0.47f, 1f) * 6f;
            if ((k * 3 + m) % 5 == 2) continue;
            if (gap - 1.3f - xa > 0.1f) { Box(g, "Maze wall", V((xa + gap - 1.3f) / 2f, 1.6f, z), V(gap - 1.3f - xa, 3.2f, 0.25f), wallMat); walls++; }
            if (xa + 10f - gap - 1.3f > 0.1f) { Box(g, "Maze wall", V((gap + 1.3f + xa + 10f) / 2f, 1.6f, z), V(xa + 10f - gap - 1.3f, 3.2f, 0.25f), wallMat); walls++; }
        }
        Src(g, V(3350f, 2f, 50f), Clip("amb_drone"), true, 1f, 0.35f, 45f, true);
        Box(g, "Lone office chair", V(3372f, 0.3f, 66f), V(0.6f, 0.6f, 0.6f), M("Black"));
        Box(g, "Wet patch", V(3357f, 0.004f, 47f), V(3f, 0.01f, 2f), RM("WetCarpet", 0.5f, 0.45f, 0.3f), false);
        RF(g, "Wanderer Wes", "#e6ee9c", 10, V(3335f, 0, 47f), 120f, "bak_wes");
        RF(g, "Lost Hiker Holly", "#ffab91", 19, V(3365f, 0, 58f), 200f, "bak_holly");
        RF(g, "Maintenance Man Moss", "#a5d6a7", 20, V(3346f, 0, 70f), 180f, "bak_moss");
        RG(g, "The entity", V(3355f, 0, 39f), 180f, "bak_entity", "pop", PrimitiveType.Capsule, V(0.8f, 2.6f, 0.8f), "Black", 2.8f);
        var ex = RG(g, "The exit sign", V(3325f, 0, 62f), 90f, "bak_exit", "shake", PrimitiveType.Cube, V(0.8f, 0.3f, 0.08f), "GlowGreen");
        ex.target.localPosition = V(0, 2.2f, 0);
        RG(g, "The almond water", V(3366f, 0, 41f), 180f, "bak_water", "spin", PrimitiveType.Cylinder, V(0.2f, 0.3f, 0.2f), "Glass");
        var flick = RG(g, "The flickering light", V(3344f, 0, 58f), 180f, "bak_light", "drop", PrimitiveType.Cube, V(1.2f, 0.06f, 0.6f), "BackLight", 1.5f);
        report.Add("The Back Rooms: 60 by 60 metres of yellow wallpaper, damp carpet and humming lights, a maze of " + walls + " wall pieces; 3 voiced people, 4 gags");
    }

    // ================================================================================================ 8 the Quiet Town, 1962 (black and white)

    static void RoomQuiet(Transform root)
    {
        RM("G1", 0.88f, 0.88f, 0.88f); RM("G2", 0.62f, 0.62f, 0.62f); RM("G3", 0.4f, 0.4f, 0.4f); RM("G4", 0.18f, 0.18f, 0.18f);
        RM("GGrass", 0.52f, 0.52f, 0.52f); RM("GSky", 0.78f, 0.78f, 0.78f); RM("GWood", 0.55f, 0.55f, 0.55f, 0f, "wood_floor"); RM("GBrick", 0.6f, 0.6f, 0.6f, 0f, "brick");
        var g = Room(root, "The Quiet Town", 3400f, 3450f, 70f, 20f, "GGrass", "GSky", "GSky",
                     "THE QUIET TOWN, 1962", new Color(0.95f, 0.95f, 0.95f), "a sleepy little town in black and white. the sheriff doesn't carry a gun. the deputy carries one bullet.", "QUIET TOWN 1962");
        Box(g, "Street", V(3421f, 0.01f, 46f), V(6f, 0.02f, 48f), M("G3"), false);
        // the sheriff's office: open to the street
        var o = Group(g, "Sheriff's office", Vector3.zero);
        Box(o, "Back wall", V(3402f, 2.5f, 46f), V(0.3f, 5f, 12f), M("GBrick"));
        Box(o, "Side wall S", V(3408f, 2.5f, 40f), V(12f, 5f, 0.3f), M("GBrick"));
        Box(o, "Side wall N", V(3408f, 2.5f, 52f), V(12f, 5f, 0.3f), M("GBrick"));
        Box(o, "Roof", V(3408f, 5.1f, 46f), V(12.4f, 0.2f, 12.4f), M("G4"), false);
        Box(o, "Floor", V(3408f, 0.01f, 46f), V(12f, 0.02f, 12f), M("GWood"), false);
        Text("OfficeSign", o, V(3414.2f, 4.3f, 46f), Face(Vector3.right), V2(6f, 0.7f), 0.5f, "<b>SHERIFF</b>").color = Color.white;
        Box(o, "Desk", V(3410f, 0.4f, 44f), V(2f, 0.8f, 1f), M("GWood"));
        for (int c = 0; c < 2; c++) for (int k = 0; k < 7; k++) DPrim(PrimitiveType.Cylinder, "Bar", o, V(3406f, 1.25f, 47f + c * 2.5f + k * 0.35f), V(0.05f, 1.25f, 0.05f), M("G4"));
        Box(o, "Cot", V(3403.5f, 0.3f, 48f), V(1.8f, 0.4f, 0.8f), M("G2"));
        Box(g, "Pond", V(3411f, 0.02f, 62f), V(10f, 0.04f, 7f), M("G3"), false);
        Box(g, "Dock", V(3414f, 0.1f, 58.2f), V(1.4f, 0.2f, 2.4f), M("GWood"));
        var barber = House(g, "The barber shop", V(3440f, 0, 40f), V(8f, 5f, 7f), "G1", "G4", "BARBER", Vector3.left);
        for (int k = 0; k < 6; k++) DPrim(PrimitiveType.Cylinder, "Barber pole", g, V(3435.6f, 0.9f + k * 0.25f, 36f), V(0.2f, 0.125f, 0.2f), M(k % 2 == 0 ? "G1" : "G4"));
        House(g, "The filling station", V(3440f, 0, 58f), V(8f, 4f, 7f), "G2", "G4", "GAS", Vector3.left);
        Box(g, "Pump", V(3434f, 0.8f, 60f), V(0.6f, 1.6f, 0.5f), M("G1"));
        Box(g, "Porch", V(3434.5f, 0.15f, 44.5f), V(2f, 0.3f, 3f), M("GWood"));
        Box(g, "Rocking chair", V(3434.5f, 0.6f, 45f), V(0.6f, 0.6f, 0.6f), M("GWood"));
        RF(g, "Sheriff Tate", "#bdbdbd", 3, V(3416f, 0, 47f), 90f, "qui_sheriff");
        Hat("Sheriff Tate", PrimitiveType.Cylinder, V(0, 0.12f, 0), V(0.32f, 0.07f, 0.32f), "G4");
        RF(g, "Deputy Virgil", "#9e9e9e", 10, V(3411f, 0, 42f), 90f, "qui_deputy");
        Hat("Deputy Virgil", PrimitiveType.Cylinder, V(0, 0.12f, 0), V(0.3f, 0.07f, 0.3f), "G3");
        RF(g, "Aunt Opal", "#e0e0e0", 9, V(3433f, 0, 52f), 270f, "qui_aunt");
        RF(g, "Little Ollie", "#cfcfcf", 21, V(3412f, 0, 57.2f), 0f, "qui_ollie", 0.7f);
        RG(g, "The one bullet", V(3410f, 0, 41.5f), 180f, "qui_bullet", "drop", PrimitiveType.Capsule, V(0.06f, 0.12f, 0.06f), "G1", 1.5f);
        var pie = RG(g, "The pie on the sill", V(3431f, 0, 48f), 90f, "qui_pie", "spin", PrimitiveType.Cylinder, V(0.6f, 0.08f, 0.6f), "G2");
        var fish = RG(g, "The fishing pole", V(3415.5f, 0, 59.5f), 0f, "qui_fish", "fly", PrimitiveType.Capsule, V(0.15f, 0.4f, 0.15f), "G1", 2f);
        RG(g, "The cell keys", V(3404f, 0, 44f), 90f, "qui_keys", "shake", PrimitiveType.Cylinder, V(0.25f, 0.03f, 0.25f), "G1").target.localPosition = V(0, 1.5f, 0);
        report.Add("The Quiet Town, 1962: a black-and-white small town (the sheriff's office with two cells, the barber, the filling station, a fishing pond, a porch); 4 voiced people, 4 gags");
    }

    // ================================================================================================ 9 Castaway Island

    static void RoomCastaway(Transform root)
    {
        var g = Room(root, "Castaway Island", 3470f, 3530f, 80f, 25f, "PixelSand", "PixelSky", "Sky",
                     "CASTAWAY ISLAND", new Color(1f, 0.85f, 0.4f), "it was supposed to be a short boat trip. that was nine years ago.", "CASTAWAY ISLAND");
        Box(g, "Sea", V(3500f, 0.02f, 75f), V(60f, 0.04f, 10f), M("PoolBlue"), false);
        Box(g, "Lagoon", V(3479f, 0.02f, 66f), V(10f, 0.04f, 8f), M("PoolGreen"), false);
        var boat = Group(g, "The wreck of the S.S. Sardine", V(3514f, 0, 66f));
        boat.localRotation = Quaternion.Euler(0, 20f, 18f);
        Box(boat, "Hull", V(0, 1.2f, 0), V(3f, 2.4f, 9f), M("CaneWhite"));
        Box(boat, "Cabin", V(0, 3f, -1f), V(2.2f, 1.4f, 3f), M("CaneWhite"), false);
        Box(boat, "Stripe", V(0, 1.6f, 0), V(3.05f, 0.3f, 8.8f), M("CaneRed"), false);
        Text("BoatName", boat, V(1.55f, 1.0f, 0), Face(Vector3.right), V2(5f, 0.5f), 0.35f, "<b>S.S. SARDINE</b>").color = new Color(0.1f, 0.2f, 0.5f);
        var thatch = RM("Thatch", 0.78f, 0.64f, 0.35f);
        foreach (var hp in new[] { V(3480f, 0, 40f), V(3480f, 0, 54f), V(3494f, 0, 58f), V(3522f, 0, 40f) })
        {
            Box(g, "Hut", hp + V(0, 1.4f, 0), V(4f, 2.8f, 4f), M("DeskWood"));
            DPrim(PrimitiveType.Cube, "Thatch roof", g, hp + V(0, 3.2f, 0), V(3.6f, 3.6f, 4.6f), thatch, Quaternion.Euler(0, 0, 45));
        }
        for (int k = 0; k < 8; k++) Palm(g, V(3474f + (k % 4) * 15f + (k / 4) * 6f, 0, 30f + (k / 4) * 32f + (k % 2) * 3f), 6f + (k % 3), k);
        for (int k = 0; k < 8; k++) DPrim(PrimitiveType.Sphere, "Rock", g, V(3500f + Mathf.Sin(k * 0.785f) * 1.2f, 0.15f, 44f + Mathf.Cos(k * 0.785f) * 1.2f), V(0.5f, 0.35f, 0.5f), M("Stone"));
        Prim(PrimitiveType.Sphere, "Signal fire", g, V(3500f, 0.5f, 44f), V(0.8f, 1f, 0.8f), M("GlowFlame"));
        Box(g, "Hammock posts", V(3510f, 1f, 50f), V(0.2f, 2f, 0.2f), M("Trunk"));
        Box(g, "Hammock posts", V(3510f, 1f, 54f), V(0.2f, 2f, 0.2f), M("Trunk"));
        RF(g, "Captain Barnacle", "#90caf9", 16, V(3504f, 0, 49f), 200f, "cas_captain");
        Hat("Captain Barnacle", PrimitiveType.Cylinder, V(0, 0.13f, 0), V(0.34f, 0.07f, 0.34f), "CaneWhite");
        RF(g, "First Mate Fumble", "#ef5350", 11, V(3497f, 0, 54f), 160f, "cas_mate");
        Hat("First Mate Fumble", PrimitiveType.Sphere, V(0, 0.08f, 0), V(0.3f, 0.12f, 0.3f), "CaneWhite");
        RF(g, "Inventor Ingrid", "#ffe082", 7, V(3518f, 0, 45f), 200f, "cas_inventor");
        RF(g, "Movie Star Marlowe", "#f48fb1", 4, V(3486f, 0, 47f), 120f, "cas_star");
        var radio = RG(g, "The coconut radio", V(3516f, 0, 38f), 180f, "cas_radio", "shake", PrimitiveType.Sphere, V(0.5f, 0.5f, 0.5f), "Trunk");
        radio.target.localPosition = V(0, 0.9f, 0);
        Prim(PrimitiveType.Cylinder, "Antenna", radio.target, V(0, 0.6f, 0), V(0.03f, 0.4f, 0.03f), M("DeskWood"));
        var plane = RG(g, "The rescue plane", V(3490f, 0, 36f), 90f, "cas_plane", "fly", PrimitiveType.Capsule, V(0.6f, 0.6f, 2.4f), "CaneWhite", 8f);
        plane.target.localPosition = V(0, 3f, 0);
        Prim(PrimitiveType.Cube, "Wings", plane.target, V(0, 0.3f, 0), V(3.2f, 0.08f, 0.6f), M("CaneWhite"));
        var ham = RG(g, "The hammock", V(3510f, 0, 52f), 0f, "cas_hammock", "flip", PrimitiveType.Cube, V(0.9f, 0.08f, 3.6f), "Thatch");
        ham.target.localPosition = V(0, 1.2f, 0);
        var car = RG(g, "The bamboo car", V(3484f, 0, 30f), 90f, "cas_car", "slide", PrimitiveType.Cube, V(1.4f, 0.15f, 2.2f), "DeskWood", 5f);
        foreach (float wx in new[] { -0.7f, 0.7f }) foreach (float wz in new[] { -0.8f, 0.8f }) Prim(PrimitiveType.Sphere, "Coconut wheel", car.target, V(wx, 0.25f, wz), Vector3.one * 0.5f, M("Trunk"));
        report.Add("Castaway Island: the wreck of the S.S. Sardine, four bamboo huts, eight palms, a lagoon, a signal fire; 4 voiced castaways, 4 gags (a coconut radio, a rescue plane, a hammock, a bamboo car)");
    }

    // ================================================================================================ 10 the Wishing Hills

    static void RoomWishing(Transform root)
    {
        float cx = 3575f;
        RM("PastelGrass", 0.55f, 0.85f, 0.5f); RM("PastelSky", 0.75f, 0.85f, 1f);
        var g = Room(root, "The Wishing Hills", 3550f, 3600f, 70f, 25f, "PastelGrass", "PastelSky", "Sky",
                     "THE WISHING HILLS", new Color(0.95f, 0.5f, 0.8f), "a storybook land of friendly dragons. touch the wishing stone and think of something nice.", "WISHING HILLS");
        foreach (var hp in new[] { V(3556f, 0, 64f), V(3594f, 0, 63f), V(3575f, 0, 69f) }) DPrim(PrimitiveType.Sphere, "Hill", g, hp, V(18f, 10f, 12f), RM("PastelHill", 0.45f, 0.78f, 0.45f));
        string[] petals = { "GemPink", "GlowYellow", "GemPurple", "GiftBlue" };
        for (int k = 0; k < 10; k++)
        {
            var fp = V(3553f + Mathf.Repeat(k * 0.618f, 1f) * 44f, 0, 30f + Mathf.Repeat(k * 0.382f + 0.2f, 1f) * 24f);
            if (Mathf.Abs(fp.x - cx) < 4f && fp.z < 34f) fp.x += 8f;
            DPrim(PrimitiveType.Cylinder, "Stem", g, fp + V(0, 1.2f, 0), V(0.15f, 1.2f, 0.15f), M("GiftGreen"));
            DPrim(PrimitiveType.Sphere, "Petals", g, fp + V(0, 2.6f, 0), V(1.4f, 0.5f, 1.4f), M(petals[k % 4]));
        }
        string[] rb = { "GlowRed", "GlowOrange", "GlowYellow", "GlowGreen", "GiftBlue" };
        for (int b = 0; b < rb.Length; b++) for (int k = 0; k <= 12; k++)
        {
            float a = k * 15f, r = 12f - b * 0.6f;
            DPrim(PrimitiveType.Cube, "Rainbow", g, V(cx + Mathf.Cos(a * Mathf.Deg2Rad) * r, Mathf.Sin(a * Mathf.Deg2Rad) * r, 66f), V(0.6f, 3.2f, 0.4f), M(rb[b]), Quaternion.Euler(0, 0, a));
        }
        for (int k = 0; k < 7; k++) DPrim(PrimitiveType.Sphere, "Cloud", g, V(3588f + Mathf.Sin(k) * 3f, 13f + (k % 2), 44f + Mathf.Cos(k) * 2f), V(4f, 2f, 3f), M("CaneWhite"));
        foreach (float tx in new[] { -1.5f, 1.5f }) DPrim(PrimitiveType.Cylinder, "Cloud castle tower", g, V(3588f + tx, 16f, 44f), V(1.2f, 2f, 1.2f), M("GemPink"));
        Box(g, "Wishing stone plinth", V(cx - 6f, 0.5f, 44f), V(1.4f, 1f, 1.4f), M("Stone"));
        Dragon(g, "Sleepy dragon", V(3592f, 0, 30f), -120f, 1.1f, "GiftBlue");
        RF(g, "Storykeeper Quill", "#ce93d8", 3, V(3565f, 0, 52f), 160f, "wis_quill");
        RF(g, "Pip the Pink Dragon", "#f48fb1", 21, V(3582f, 0, 48f), 200f, "wis_pip", 0.85f);
        if (rigs.TryGetValue("Pip the Pink Dragon", out var pr)) foreach (float wx in new[] { -1f, 1f }) Prim(PrimitiveType.Cube, "Wing", pr[1], V(wx * 0.35f, 0.1f, -0.15f), V(0.6f, 0.04f, 0.4f), M("GemPink")).transform.localRotation = Quaternion.Euler(0, 0, wx * -30f);
        RF(g, "Twiddle and Twaddle", "#9575cd", 19, V(3560f, 0, 36f), 120f, "wis_twins");
        if (rigs.TryGetValue("Twiddle and Twaddle", out var tr)) Prim(PrimitiveType.Sphere, "Second head", tr[2], V(0.3f, 0.05f, 0), V(0.26f, 0.26f, 0.26f), M("GemPurple"));
        RF(g, "Explorer Juno", "#ffcc80", 22, V(3576f, 0, 40f), 180f, "wis_juno", 0.75f);
        var stone = RG(g, "The wishing stone", V(cx - 6f, 0, 42.5f), 0f, "wis_stone", "grow", PrimitiveType.Sphere, V(0.7f, 0.9f, 0.7f), "GemPink");
        stone.target.localPosition = V(0, 1f, 1.5f);
        var fly = RG(g, "The dragon flight", V(3570f, 0, 58f), 180f, "wis_flight", "fly", PrimitiveType.Sphere, V(0.1f, 0.1f, 0.1f), "GiftGreen", 6f);
        Dragon(fly.target, "Flying dragon", Vector3.zero, 0f, 0.8f, "GiftGreen");
        var sneeze = RG(g, "The sneezing dragon", V(3592f, 0, 30f), -120f, "wis_sneeze", "shake", PrimitiveType.Sphere, V(0.6f, 0.6f, 0.6f), "GlowGold");
        sneeze.target.localPosition = V(0, 2.7f, 1.5f);
        report.Add("The Wishing Hills: a storybook land (pastel hills, giant flowers, a rainbow, a castle on a cloud, the wishing stone, a sleeping dragon); 4 voiced people, 3 gags");
    }

    // ================================================================================================ 11 the Kitchen Counter Theatre

    static void RoomCounter(Transform root)
    {
        float cx = 3638f;
        var g = Room(root, "The Kitchen Counter Theatre", 3620f, 3656f, 56f, 8f, "Rug", "WallDark", "Ceiling",
                     "THE KITCHEN COUNTER THEATRE", new Color(0.5f, 0.9f, 0.3f), "tonight: singing vegetables, live on the countertop. two shows nightly.", "KITCHEN COUNTER");
        Box(g, "Countertop", V(cx, 0.6f, 48f), V(24f, 1.2f, 10f), RM("Counter", 0.92f, 0.9f, 0.85f));
        Box(g, "Counter edge", V(cx, 1.17f, 43.05f), V(24f, 0.06f, 0.1f), M("Metal"), false);
        Box(g, "Backsplash", V(cx, 3f, 55.9f), V(24f, 3.6f, 0.1f), M("LabWhite"), false);
        Box(g, "Giant toaster", V(cx - 8f, 2.4f, 51f), V(3f, 2.4f, 2f), M("Metal"));
        Box(g, "Giant cereal box", V(cx + 8f, 3.4f, 52f), V(3f, 4.4f, 1.2f), M("CornYellow"));
        DPrim(PrimitiveType.Capsule, "Giant spoon", g, V(cx + 3f, 1.4f, 52f), V(0.4f, 2.4f, 0.4f), M("Metal"), Quaternion.Euler(0, 0, 90));
        for (int r = 0; r < 3; r++) foreach (float side in new[] { -1f, 1f })
            Box(g, "Theatre seats", V(cx + side * 9f, 0.3f, 29f + r * 3f), V(13f, 0.6f, 1f), M("Seat"));
        Box(g, "Curtain L", V(3626.3f, 4f, 43.5f), V(0.6f, 8f, 1f), M("Curtain"), false);
        Box(g, "Curtain R", V(3649.7f, 4f, 43.5f), V(0.6f, 8f, 1f), M("Curtain"), false);
        RF(g, "Stage Manager Sage", "#a5d6a7", 20, V(3623.5f, 0, 40f), 90f, "cou_sage");
        RF(g, "Usher Ulma", "#ef9a9a", 7, V(3654.5f, 0, 38f), 270f, "cou_usher");
        RF(g, "Critic Clive", "#b0bec5", 3, V(3634f, 0, 38f), 0f, "cou_critic");
        var radish = RG(g, "Rosie the Radish", V(cx - 5f, 1.2f, 44f), 180f, "cou_radish", "shake", PrimitiveType.Sphere, V(0.9f, 0.9f, 0.9f), "GlowRed");
        for (int k = 0; k < 3; k++) Prim(PrimitiveType.Cube, "Leaf", radish.target, V(-0.15f + k * 0.15f, 1.1f, 0), V(0.1f, 0.5f, 0.04f), M("GiftGreen"));
        foreach (float ex in new[] { -0.15f, 0.15f }) Prim(PrimitiveType.Sphere, "Eye", radish.target, V(ex, 0.55f, 0.42f), Vector3.one * 0.14f, M("CaneWhite"));
        var squash = RG(g, "Sir Squash", V(cx - 1.5f, 1.2f, 44f), 180f, "cou_squash", "slide", PrimitiveType.Capsule, V(0.6f, 1.2f, 0.6f), "GlowYellow", 1.2f);
        Prim(PrimitiveType.Cylinder, "Top hat", squash.target, V(0, 1.4f, 0), V(0.35f, 0.25f, 0.35f), M("Black"));
        var peas = RG(g, "The Pea Choir", V(cx + 2f, 1.2f, 44f), 180f, "cou_peas", "grow", PrimitiveType.Sphere, V(0.35f, 0.35f, 0.35f), "GiftGreen");
        for (int k = 1; k < 5; k++) Prim(PrimitiveType.Sphere, "Pea", peas.target, V(-0.6f + k * 0.3f - 0.15f, 0.18f + (k % 2) * 0.1f, 0), Vector3.one * 0.35f, M("GiftGreen"));
        var onion = RG(g, "Old Man Onion", V(cx + 5.5f, 1.2f, 44f), 180f, "cou_onion", "flip", PrimitiveType.Sphere, V(0.8f, 0.9f, 0.8f), "GemPurple");
        Prim(PrimitiveType.Cube, "Glasses", onion.target, V(0, 0.55f, 0.38f), V(0.4f, 0.08f, 0.04f), M("Black"));
        report.Add("The Kitchen Counter Theatre: a giant countertop stage (toaster, cereal box, spoon), curtains and theatre seats; 3 voiced people and 4 singing vegetables (gags)");
    }

    // ================================================================================================ 12 the Clubhouse on the Hill (with a toon-car ride)

    static void RoomClubhouse(Transform root)
    {
        float cx = 3705f, cz = 54f;
        var g = Room(root, "The Clubhouse on the Hill", 3680f, 3730f, 70f, 20f, "Grass", "PixelSky", "Sky",
                     "THE CLUBHOUSE ON THE HILL", new Color(1f, 0.25f, 0.2f), "a round red clubhouse on a hill, and a gadget for every problem", "CLUBHOUSE");
        DPrim(PrimitiveType.Sphere, "The hill", g, V(cx, -3f, 70f), V(40f, 12f, 6f), M("GiftGreen"));
        DPrim(PrimitiveType.Cylinder, "Clubhouse", g, V(cx, 3f, cz), V(11f, 3f, 11f), M("GlowYellow"));
        DPrim(PrimitiveType.Sphere, "Clubhouse roof", g, V(cx, 6.5f, cz), V(12f, 6f, 12f), M("CaneRed"));
        Box(g, "Clubhouse door", V(cx, 1.2f, cz - 5.45f), V(1.6f, 2.4f, 0.1f), M("GiftBlue"), false);
        foreach (float wx in new[] { -3f, 3f }) Box(g, "Round window", V(cx + wx, 3.5f, cz - 4.7f), V(1.2f, 1.2f, 0.1f), M("Glass"), false);
        DPrim(PrimitiveType.Cylinder, "Antenna", g, V(cx, 10.5f, cz), V(0.2f, 1.5f, 0.2f), M("GlowYellow"));
        DPrim(PrimitiveType.Sphere, "Antenna ball", g, V(cx, 12.2f, cz), Vector3.one * 0.6f, M("GlowRed"));
        Scatter(g, "Garden flower", 24, 3721f, 3729f, 26f, 40f, PrimitiveType.Sphere, V(0.4f, 0.4f, 0.4f), new[] { "GemPink", "GlowYellow", "GiftBlue" }, 0.6f);
        RF(g, "Clubhouse Host Clover", "#fff59d", 18, V(cx, 0, 35.5f), 180f, "clu_host");
        Hat("Clubhouse Host Clover", PrimitiveType.Cube, V(0, 0.14f, 0), V(0.36f, 0.12f, 0.08f), "GemPink");
        RF(g, "Handyman Hal", "#90a4ae", 16, V(3722f, 0, 42f), 220f, "clu_hal");
        RF(g, "Gardener Gwen", "#c5e1a5", 9, V(3726f, 0, 60f), 240f, "clu_gwen");
        RF(g, "Pickles the Pup Pal", "#ffcc80", 21, V(3686f, 0, 40f), 140f, "clu_pickles", 0.8f);
        var picker = RG(g, "The gizmo picker", V(3713f, 0, 34f), 180f, "clu_gizmo", "pop", PrimitiveType.Cube, V(1f, 0.6f, 0.6f), "GiftBlue", 2f);
        for (int k = 0; k < 3; k++) Prim(PrimitiveType.Sphere, "Gizmo", picker.target, V(-0.35f + k * 0.35f, 0.8f, 0), Vector3.one * 0.25f, M(new[] { "GlowRed", "GlowYellow", "GlowGreen" }[k]));
        var door = RG(g, "The magic word door", V(3697f, 0, 34f), 180f, "clu_door", "flip", PrimitiveType.Cube, V(1.2f, 2.2f, 0.1f), "GemPurple");
        RG(g, "The clubhouse slide", V(3690f, 0, 30f), 90f, "clu_slide", "slide", PrimitiveType.Capsule, V(0.5f, 0.5f, 0.5f), "GlowYellow", 4f);
        report.Add("The Clubhouse on the Hill: a round clubhouse, a hill, and a garden; 4 voiced people, 3 gags");
    }

    // ================================================================================================ 13 the Suspicious Starship

    static void RoomStarship(Transform root)
    {
        float cx = 3775f;
        var g = Room(root, "The Suspicious Starship", 3750f, 3800f, 70f, 5f, "LabFloor", "LabGrey", "LabWhite",
                     "THE SUSPICIOUS STARSHIP", new Color(1f, 0.3f, 0.3f), "a crew of six. some tasks to do. one of them is not really crew.", "STARSHIP");
        foreach (var tp in new[] { V(3765f, 0, 44f), V(3785f, 0, 44f), V(3765f, 0, 58f), V(3785f, 0, 58f), V(cx, 0, 51f) })
        {
            DPrim(PrimitiveType.Cylinder, "Table", g, tp + V(0, 0.45f, 0), V(3f, 0.05f, 3f), M("LabWhite"));
            DPrim(PrimitiveType.Cylinder, "Table leg", g, tp + V(0, 0.22f, 0), V(0.3f, 0.22f, 0.3f), M("Metal"));
        }
        Box(g, "Electrical wall", V(3760f, 2.5f, 35f), V(0.25f, 5f, 10f), M("LabGrey"));
        Box(g, "Reactor wall", V(3790f, 2.5f, 35f), V(0.25f, 5f, 10f), M("LabGrey"));
        Box(g, "Medbay wall", V(3756f, 2.5f, 62f), V(12f, 5f, 0.25f), M("LabGrey"));
        Box(g, "Window", V(cx, 2.4f, 69.88f), V(16f, 2.4f, 0.06f), M("NightSky"), false);
        for (int k = 0; k < 14; k++) Prim(PrimitiveType.Sphere, "Star", g, V(3768f + Mathf.Repeat(k * 0.618f, 1f) * 14f, 1.4f + Mathf.Repeat(k * 0.382f, 1f) * 2f, 69.84f), Vector3.one * 0.07f, M("GlowWarm"));
        DPrim(PrimitiveType.Cylinder, "Reactor core", g, V(3795f, 1.5f, 30f), V(2f, 1.5f, 2f), M("DarkGlowCyan"));
        string[] wires = { "GlowRed", "GiftBlue", "GlowYellow", "GemPink" };
        for (int k = 0; k < 4; k++) Box(g, "Wire", V(3750.1f, 1.2f + k * 0.2f, 42f), V(0.04f, 0.06f, 1.2f), M(wires[k]), false);
        foreach (var vp in new[] { V(3784f, 0, 64f), V(3756f, 0, 50f), V(3796f, 0, 52f) }) Box(g, "Vent", vp + V(0, 0.01f, 0), V(1f, 0.02f, 0.8f), M("Metal"), false);
        RF(g, "Crewmate Teal", "#4dd0e1", 10, V(3768f, 0, 38f), 0f, "shp_teal");
        RF(g, "Crewmate Lime", "#aed581", 20, V(3754f, 0, 38f), 90f, "shp_lime");
        RF(g, "Captain Coral", "#ff8a65", 16, V(cx, 0, 55.5f), 180f, "shp_coral");
        RF(g, "Engineer Ochre", "#ffb74d", 7, V(3794f, 0, 37f), 270f, "shp_ochre");
        foreach (var who in new[] { "Crewmate Teal", "Crewmate Lime", "Captain Coral", "Engineer Ochre" })
            if (rigs.TryGetValue(who, out var r)) { Prim(PrimitiveType.Sphere, "Helmet", r[2], V(0, 0.02f, 0), V(0.34f, 0.32f, 0.34f), M("Glass")); Prim(PrimitiveType.Cube, "Visor", r[2], V(0, 0.02f, 0.13f), V(0.24f, 0.1f, 0.08f), M("Visor")); Prim(PrimitiveType.Cube, "Backpack", r[1], V(0, 0, -0.18f), V(0.3f, 0.35f, 0.15f), M("SuitOrange")); }
        var button = RG(g, "The emergency button", V(cx, 0, 49.2f), 0f, "shp_button", "drop", PrimitiveType.Cylinder, V(0.5f, 0.12f, 0.5f), "GlowRed", 0.6f);
        button.target.localPosition = V(0, 1.15f, 1.8f);
        var vent = RG(g, "The vent", V(3784f, 0, 64f), 180f, "shp_vent", "pop", PrimitiveType.Capsule, V(0.7f, 1.6f, 0.7f), "GlowRed", 2f);
        Prim(PrimitiveType.Cube, "Visor", vent.target, V(0, 1.1f, 0.3f), V(0.4f, 0.2f, 0.15f), M("Visor"));
        var wire = RG(g, "The wiring task", V(3751.2f, 0, 45f), 90f, "shp_wires", "shake", PrimitiveType.Cube, V(0.8f, 0.8f, 0.1f), "Metal");
        wire.target.localPosition = V(0, 1.2f, 0);
        RG(g, "The medbay scan", V(3762f, 0, 65f), 180f, "shp_scan", "spin", PrimitiveType.Cylinder, V(1.4f, 0.05f, 1.4f), "GlowGreen");
        report.Add("The Suspicious Starship: a spaceship (a cafeteria with the emergency button, electrical, the reactor, medbay, vents, a window of stars) and a crew in spacesuits; 4 voiced crew, 4 gags");
    }

    // ================================================================================================ 14 the Meme Museum

    static void RoomMemes(Transform root)
    {
        float cx = 3840f;
        var g = Room(root, "The Meme Museum", 3820f, 3860f, 60f, 7f, "Floor2", "Wall", "Ceiling",
                     "THE MEME MUSEUM", new Color(1f, 0.85f, 0.3f), "the internet's finest jokes, preserved forever. please do not touch the memes.", "MEME MUSEUM");
        var ex = new[] {
            new { name = "The waiting skeleton", at = V(3822.5f, 0, 34f), yaw = 90f, clip = "mem_wait", plaque = "SKELETON WAITING FOR THE UPDATE\n(began waiting: 1997)" },
            new { name = "The trumpet skeleton", at = V(3822.5f, 0, 44f), yaw = 90f, clip = "mem_trumpet", plaque = "THE TRUMPET SKELETON\nbrass, bone, one note" },
            new { name = "The galaxy bone", at = V(3822.5f, 0, 54f), yaw = 90f, clip = "mem_galaxy", plaque = "GALAXY BONE\nfour skulls, each more enlightened" },
            new { name = "Two buttons", at = V(3857.5f, 0, 40f), yaw = 270f, clip = "mem_buttons", plaque = "TWO BUTTONS\none finger bone, no right answer" },
            new { name = "The surprised skeleton", at = V(3857.5f, 0, 52f), yaw = 270f, clip = "mem_surprised", plaque = "SURPRISED SKELETON\nhe has just seen his own X-ray" } };
        foreach (var e in ex)
        {
            var face = Quaternion.Euler(0, e.yaw, 0) * Vector3.forward;
            Box(g, "Frame", e.at - face * 2.3f + V(0, 2.6f, 0), V(3f, 2.2f, 0.08f), M("GlowGold"), false).transform.localRotation = Quaternion.Euler(0, e.yaw, 0);
            Box(g, "Plinth", e.at + V(0, 0.4f, 0), V(1.2f, 0.8f, 1.2f), M("CaneWhite"));
            Text("Plaque", g, e.at + face * 0.62f + V(0, 0.55f, 0), Face(face), V2(1.1f, 0.4f), 0.07f, e.plaque).color = new Color(0.15f, 0.1f, 0.05f);
            foreach (float s in new[] { -1f, 1f })
                DPrim(PrimitiveType.Cylinder, "Rope post", g, e.at + face * 1.8f + Quaternion.Euler(0, e.yaw, 0) * V(s * 1.3f, 0.5f, 0), V(0.1f, 0.5f, 0.1f), M("Brass"));
            Box(g, "Velvet rope", e.at + face * 1.8f + V(0, 0.85f, 0), V(2.6f, 0.06f, 0.06f), M("GlowRed"), false).transform.localRotation = Quaternion.Euler(0, e.yaw, 0);
        }
        Box(g, "Statue plinth", V(cx, 0.5f, 46f), V(2f, 1f, 2f), M("Stone"));
        Mini(g, "The first meme (a statue)", V(cx, 1f, 46f), 180f, 0.9f, "#cfd8dc");
        Text("StatuePlaque", g, V(cx, 0.6f, 44.98f), Face(Vector3.back), V2(1.8f, 0.5f), 0.09f, "THE FIRST MEME\na skeleton who said \"boo\", 1503. it killed.").color = new Color(0.15f, 0.1f, 0.05f);
        RF(g, "Curator Bonesworth", "#d7ccc8", 3, V(3830f, 0, 30f), 90f, "mem_curator");
        RF(g, "Tour Guide Tilly", "#f48fb1", 9, V(3850f, 0, 30f), 270f, "mem_guide");
        RF(g, "Guard Gordon", "#5c6bc0", 20, V(cx, 0, 57f), 180f, "mem_guard");
        Hat("Guard Gordon", PrimitiveType.Cylinder, V(0, 0.12f, 0), V(0.3f, 0.07f, 0.3f), "PoliceBlue");
        var gags = new List<FirGag>();
        var wait = RG(g, ex[0].name, ex[0].at, ex[0].yaw, ex[0].clip, "shake", PrimitiveType.Cube, V(0.8f, 0.3f, 0.5f), "DeskWood");
        wait.target.localPosition = V(0, 0.8f, 0);
        Mini(wait.target, "Waiting skeleton", V(0, 0.15f, 0), 0f, 0.5f, "#eceff1");
        Prim(PrimitiveType.Cube, "Cobweb", wait.target, V(0, 0.9f, 0), V(0.6f, 0.6f, 0.01f), M("Cobweb"));
        var trumpet = RG(g, ex[1].name, ex[1].at, ex[1].yaw, ex[1].clip, "shake", PrimitiveType.Cylinder, V(0.08f, 0.3f, 0.08f), "Brass");
        trumpet.target.localPosition = V(0, 1.3f, 0.2f);
        Mini(trumpet.target, "Trumpet skeleton", V(0, -0.5f, -0.2f), 0f, 0.6f, "#ffffff");
        var galaxy = RG(g, ex[2].name, ex[2].at, ex[2].yaw, ex[2].clip, "grow", PrimitiveType.Sphere, V(0.3f, 0.3f, 0.3f), "BoneWhite");
        galaxy.target.localPosition = V(0, 0.8f, 0);
        for (int k = 1; k < 4; k++) Prim(PrimitiveType.Sphere, "Skull", galaxy.target, V(0, 0.15f + k * 0.35f, 0), Vector3.one * 0.3f, M(new[] { "BoneWhite", "GlowYellow", "GemPink", "GlowGhost" }[k]));
        var buttons = RG(g, ex[3].name, ex[3].at, ex[3].yaw, ex[3].clip, "shake", PrimitiveType.Cylinder, V(0.25f, 0.08f, 0.25f), "GlowRed");
        buttons.target.localPosition = V(-0.25f, 0.8f, 0);
        Prim(PrimitiveType.Cylinder, "Other button", buttons.target, V(0.5f, 0.04f, 0), V(0.25f, 0.08f, 0.25f), M("GlowRed"));
        var surprised = RG(g, ex[4].name, ex[4].at, ex[4].yaw, ex[4].clip, "pop", PrimitiveType.Sphere, V(0.5f, 0.55f, 0.5f), "BoneWhite", 1f);
        surprised.target.localPosition = V(0, -0.2f, 0);
        foreach (float sx in new[] { -0.1f, 0.1f }) Prim(PrimitiveType.Sphere, "Wide eye", surprised.target, V(sx, 1.35f, 0.22f), Vector3.one * 0.16f, M("Black"));
        report.Add("The Meme Museum: 5 exhibits behind velvet ropes (the waiting skeleton, the trumpet skeleton, galaxy bone, two buttons, the surprised skeleton) and a statue of the first meme; 3 voiced people, 5 gags");
    }
}
#endif
