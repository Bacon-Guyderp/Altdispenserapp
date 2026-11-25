#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Bonetown, inside: every Main Street shop is furnished, and the solid buildings (the town hall, the school, the
/// library, the fire station, the barn and the three houses on Clavicle Avenue) are hollowed out with a door and a
/// decorated interior.
/// </summary>
public static partial class FirCheckBuilder
{
    public static readonly List<KeyValuePair<string, float[]>> townRooms = new List<KeyValuePair<string, float[]>>();   // name, {x0, z0, x1, z1, doorX, doorZ (outside), insideZ}
    static int townProps;

    /// <summary>A building with walls, a ceiling, a floor and a doorway in its front (the south side if frontSouth, else the north).</summary>
    static Transform Hollow(Transform parent, string name, float x0, float x1, float z0, float z1, float h, bool frontSouth, float doorX, float doorW, float doorH, string wall, string floor)
    {
        var b = Group(parent, name + " (inside)", Vector3.zero);
        float cx = (x0 + x1) / 2f, cz = (z0 + z1) / 2f, t = 0.2f;
        float zf = frontSouth ? z0 + t / 2f : z1 - t / 2f, zb = frontSouth ? z1 - t / 2f : z0 + t / 2f;
        Box(b, "Back wall", V(cx, h / 2f, zb), V(x1 - x0, h, t), M(wall));
        Box(b, "Side wall W", V(x0 + t / 2f, h / 2f, cz), V(t, h, z1 - z0), M(wall));
        Box(b, "Side wall E", V(x1 - t / 2f, h / 2f, cz), V(t, h, z1 - z0), M(wall));
        float a = doorX - doorW / 2f, c = doorX + doorW / 2f;
        Box(b, "Front wall W", V((x0 + a) / 2f, h / 2f, zf), V(a - x0, h, t), M(wall));
        Box(b, "Front wall E", V((c + x1) / 2f, h / 2f, zf), V(x1 - c, h, t), M(wall));
        Box(b, "Lintel", V(doorX, (doorH + h) / 2f, zf), V(doorW, h - doorH, t), M(wall));
        Box(b, "Ceiling", V(cx, h + 0.1f, cz), V(x1 - x0, 0.2f, z1 - z0), M("Ceiling"));
        Box(b, "Inside floor", V(cx, 0.012f, cz), V(x1 - x0 - 0.4f, 0.02f, z1 - z0 - 0.4f), M(floor), false);
        float zOut = frontSouth ? z0 - 1.5f : z1 + 1.5f, zIn = frontSouth ? z0 + 1.5f : z1 - 1.5f;
        townRooms.Add(new KeyValuePair<string, float[]>(name, new[] { x0, z0, x1, z1, doorX, zOut, zIn }));
        return b;
    }

    static void Kill(Transform parent, params string[] names)
    {
        if (parent == null) return;
        foreach (var n in names)
        {
            var t = parent.Find(n);
            if (t == null) { report.Add("problem: Bonetown has no " + n + " to open up"); continue; }
            Object.DestroyImmediate(t.gameObject);
        }
    }

    static void P(Transform g, PrimitiveType t, string n, Vector3 at, Vector3 size, string mat, float yaw = 0f)
    {
        DPrim(t, n, g, at, size, M(mat), Quaternion.Euler(0, yaw, 0));
        townProps++;
    }

    static void PB(Transform g, string n, Vector3 at, Vector3 size, string mat)   // a furniture box you bump into
    {
        Box(g, n, at, size, M(mat));
        townProps++;
    }

    static void Chair(Transform g, Vector3 at, float yaw, string mat)
    {
        var c = Group(g, "Chair", at);
        c.localRotation = Quaternion.Euler(0, yaw, 0);
        Box(c, "Seat", V(0, 0.45f, 0), V(0.45f, 0.06f, 0.45f), M(mat), false);
        Box(c, "Back", V(0, 0.75f, -0.2f), V(0.45f, 0.55f, 0.05f), M(mat), false);
        foreach (float lx in new[] { -0.18f, 0.18f }) foreach (float lz in new[] { -0.18f, 0.18f }) Box(c, "Leg", V(lx, 0.22f, lz), V(0.04f, 0.44f, 0.04f), M("Black"), false);
        townProps++;
    }

    static void Table(Transform g, Vector3 at, float r, string mat)
    {
        P(g, PrimitiveType.Cylinder, "Table top", at + V(0, 0.75f, 0), V(r * 2f, 0.03f, r * 2f), mat);
        P(g, PrimitiveType.Cylinder, "Table leg", at + V(0, 0.37f, 0), V(0.1f, 0.37f, 0.1f), "Black");
    }

    /// <summary>Wall shelves: a unit `w` wide against a wall facing `normal`, with `rows` of items from `fill`.</summary>
    static void Shelves(Transform g, Vector3 at, Vector3 normal, float w, int rows, string[] fill, PrimitiveType item)
    {
        var s = Group(g, "Shelves", at);
        s.localRotation = Face(normal);
        Box(s, "Unit", V(0, rows * 0.45f + 0.1f, 0.18f), V(w, rows * 0.9f + 0.2f, 0.08f), M("DeskWood"), false);
        for (int r = 0; r < rows; r++)
        {
            Box(s, "Shelf", V(0, 0.4f + r * 0.85f, 0), V(w, 0.05f, 0.4f), M("DeskWood"), false);
            int n = Mathf.Max(2, (int)(w / 0.42f));
            for (int k = 0; k < n; k++)
            {
                var p = Prim(item, "Item", s, V(-w / 2f + 0.25f + k * (w - 0.5f) / Mathf.Max(1, n - 1), 0.56f + r * 0.85f, -0.02f), V(0.26f, 0.28f, 0.24f), M(fill[(k + r) % fill.Length]));
                GameObjectUtility.SetStaticEditorFlags(p, StaticEditorFlags.BatchingStatic);
            }
        }
        townProps++;
    }

    static void Frame(Transform g, Vector3 at, Vector3 normal, float w, float h, string text)
    {
        var f = Group(g, "Picture", at);
        f.localRotation = Face(normal);
        Box(f, "Frame", Vector3.zero, V(w + 0.1f, h + 0.1f, 0.04f), M("GlowGold"), false);
        Box(f, "Print", V(0, 0, -0.025f), V(w, h, 0.01f), M("Paper"), false);
        if (text != null) Text("Caption", f, V(0, 0, -0.035f), Quaternion.identity, V2(w * 0.9f, h * 0.9f), Mathf.Min(0.2f, h * 0.25f), text).color = new Color(0.2f, 0.12f, 0.06f);
        townProps++;
    }

    static void BuildTownInteriors(Transform root)
    {
        townRooms.Clear(); townProps = 0;
        var g = root.Find("Bonetown districts");
        if (g == null) { report.Add("problem: no Bonetown districts to furnish"); return; }
        var ig = Group(g, "Interiors", Vector3.zero);
        ShopInteriors(g, ig);
        TownHallInside(g, ig);
        SchoolInside(g, ig);
        LibraryInside(g, ig);
        FireStationInside(g, ig);
        BarnInside(g, ig);
        foreach (var hs in new[] { new { n = "House: the Femorellis", x = -18f, wall = "Wall2" }, new { n = "House: the Mowers", x = -90f, wall = "WallGreen" }, new { n = "House: Old Gruff", x = -102f, wall = "Wall" } })
            HouseInside(g, ig, hs.n, hs.x, hs.wall);
        report.Add("Bonetown interiors: 8 furnished shops on Main Street; the town hall, the school, the library, the fire station, the barn and three houses opened up with doors and rooms inside (" + townRooms.Count + " rooms, " + townProps + " pieces of furniture and decor)");
    }

    // ------------------------------------------------------------------------------------------------ the shops

    static void ShopInteriors(Transform g, Transform ig)
    {
        string[] shops = { "BAKERY", "SKULL CUTS", "TOYS", "POST OFFICE", "THE BONEPLEX", "CAFE CALCIUM", "FLOWERS", "FOR LEASE" };
        float[] xs = { -17f, -26.5f, -36f, -58f, -69f, -80f, -91f, -102f };
        for (int i = 0; i < shops.Length; i++)
        {
            float x = xs[i];
            var s = g.Find("Shop " + shops[i]);
            if (s == null) { report.Add("problem: no shop " + shops[i]); continue; }
            var d = Group(s, "Interior", Vector3.zero);
            // every shop: a ceiling, wainscot, two lamps, a doormat
            Box(d, "Shop ceiling", V(x, 3.98f, -40f), V(8.6f, 0.04f, 7.8f), M("Ceiling"), false);
            foreach (float sx in new[] { -4.37f, 4.37f }) Box(d, "Wainscot", V(x + sx, 0.5f, -40f), V(0.04f, 1f, 7.8f), M("Wainscot"), false);
            foreach (float lx in new[] { -2f, 2f }) PendantLamp(d, V(x + lx, 3.96f, -39.4f), 0.8f);
            Box(d, "Doormat", V(x, 0.025f, -36.7f), V(1.6f, 0.01f, 0.9f), M("TrimDark"), false);
            townRooms.Add(new KeyValuePair<string, float[]>("Shop " + shops[i], new[] { x - 4.5f, -44f, x + 4.5f, -36f, x, -34.5f, -38f }));
            switch (shops[i])
            {
                case "BAKERY":
                    Shelves(d, V(x, 0, -43.75f), Vector3.forward, 6.4f, 3, new[] { "Bun", "Bun", "Trunk" }, PrimitiveType.Capsule);
                    PB(d, "Oven", V(x + 3.6f, 0.8f, -42.9f), V(1.2f, 1.6f, 1.2f), "Metal");
                    Box(d, "Oven glow", V(x + 2.98f, 0.7f, -42.9f), V(0.02f, 0.5f, 0.8f), M("GlowFlame"), false);
                    PB(d, "Cake case", V(x - 3.7f, 0.5f, -38.8f), V(1.1f, 1f, 2.6f), "Glass");
                    for (int k = 0; k < 3; k++) P(d, PrimitiveType.Cylinder, "Cake", V(x - 3.7f, 1.12f, -39.8f + k * 1f), V(0.6f, 0.12f, 0.6f), k == 1 ? "GemPink" : "CaneWhite");
                    for (int k = 0; k < 3; k++) P(d, PrimitiveType.Capsule, "Flour sack", V(x - 3.4f + k * 0.6f, 0.35f, -43f), V(0.5f, 0.35f, 0.4f), "CaneWhite");
                    Table(d, V(x + 3f, 0, -37.6f), 0.45f, "CaneWhite"); Chair(d, V(x + 3f, 0, -38.5f), 0f, "Trunk"); Chair(d, V(x + 3.9f, 0, -37.2f), -90f, "Trunk");
                    Box(d, "Chalkboard", V(x + 4.36f, 2.3f, -39.2f), V(0.04f, 1f, 1.6f), M("Black"), false);
                    Text("Specials", d, V(x + 4.33f, 2.3f, -39.2f), Face(Vector3.left), V2(1.5f, 0.9f), 0.13f, "<b>TODAY</b>\nsourdough . rye\nthe femur loaf\ncrossbones buns").color = Color.white;
                    break;
                case "SKULL CUTS":
                    Shelves(d, V(x, 0, -43.75f), Vector3.forward, 6.4f, 2, new[] { "GiftBlue", "Glass", "GlowGreen" }, PrimitiveType.Cylinder);
                    foreach (float sd in new[] { -1f, 1f })
                    {
                        var ch = Group(d, "Barber chair", V(x + sd * 2.7f, 0, -38.6f));
                        ch.localRotation = Quaternion.Euler(0, sd * 90f, 0);
                        Prim(PrimitiveType.Cylinder, "Base", ch, V(0, 0.25f, 0), V(0.5f, 0.25f, 0.5f), M("Metal"));
                        Prim(PrimitiveType.Cube, "Seat", ch, V(0, 0.6f, 0), V(0.7f, 0.18f, 0.7f), M("CaneRed"));
                        Prim(PrimitiveType.Cube, "Chair back", ch, V(0, 1.1f, -0.32f), V(0.7f, 0.9f, 0.12f), M("CaneRed"));
                        Prim(PrimitiveType.Cube, "Footrest", ch, V(0, 0.25f, 0.45f), V(0.5f, 0.05f, 0.2f), M("Metal"));
                        ch.gameObject.AddComponent<BoxCollider>().size = V(0.8f, 1.4f, 0.8f);
                        townProps++;
                        Box(d, "Mirror", V(x + sd * 4.36f, 1.7f, -38.6f), V(0.04f, 1.3f, 1.4f), M("Glass"), false);
                        Box(d, "Mirror shelf", V(x + sd * 4.2f, 0.95f, -38.6f), V(0.3f, 0.05f, 1.4f), M("DeskWood"), false);
                        for (int k = 0; k < 3; k++) P(d, PrimitiveType.Cylinder, "Bottle", V(x + sd * 4.2f, 1.1f, -39.1f + k * 0.45f), V(0.12f, 0.12f, 0.12f), new[] { "GiftBlue", "GlowGreen", "GemPink" }[k]);
                        for (int k = 0; k < 6; k++) P(d, PrimitiveType.Sphere, "Clippings", V(x + sd * (2.4f + (k % 3) * 0.3f), 0.02f, -39.4f + (k / 3) * 1.4f), V(0.25f, 0.01f, 0.2f), "TrimDark");
                    }
                    PB(d, "Waiting bench", V(x - 3.9f, 0.25f, -36.7f), V(0.6f, 0.5f, 1.1f), "Seat");
                    P(d, PrimitiveType.Cylinder, "Comb jar", V(x + 2.2f, 1.15f, -41.6f), V(0.2f, 0.15f, 0.2f), "Glass");
                    Text("Styles", d, V(x, 3.2f, -43.85f), Face(Vector3.forward), V2(6f, 0.6f), 0.22f, "<b>STYLES</b>: the bald . the very bald . the polished . the extra shiny").color = Color.white;
                    break;
                case "TOYS":
                    Shelves(d, V(x, 0, -43.75f), Vector3.forward, 6.4f, 3, new[] { "GiftRed", "GiftBlue", "GlowYellow", "GiftGreen", "GemPurple" }, PrimitiveType.Cube);
                    Shelves(d, V(x - 4.25f, 0, -38.7f), Vector3.right, 3.6f, 3, new[] { "BonePink", "InkOrange", "GiftBlue" }, PrimitiveType.Sphere);
                    Shelves(d, V(x + 4.25f, 0, -38.7f), Vector3.left, 3.6f, 3, new[] { "GlowYellow", "GiftGreen", "CaneRed" }, PrimitiveType.Cube);
                    var horse = Group(d, "Rocking horse", V(x + 2.2f, 0, -37.4f));
                    Prim(PrimitiveType.Capsule, "Body", horse, V(0, 0.7f, 0), V(0.35f, 0.4f, 0.35f), M("DeskWood")).transform.localRotation = Quaternion.Euler(90, 0, 0);
                    Prim(PrimitiveType.Sphere, "Head", horse, V(0, 1.05f, 0.4f), V(0.25f, 0.3f, 0.35f), M("DeskWood"));
                    Prim(PrimitiveType.Cube, "Rocker", horse, V(0, 0.1f, 0), V(0.4f, 0.06f, 1.2f), M("CaneRed"));
                    townProps++;
                    for (int k = 0; k < 3; k++) P(d, PrimitiveType.Sphere, "Teddy", V(x - 2.4f + k * 0.35f, 0.25f + (k % 2) * 0.2f, -37.3f), V(0.35f, 0.4f, 0.3f), "Trunk");
                    for (int k = 0; k < 5; k++) P(d, PrimitiveType.Sphere, "Balloon", V(x - 1.6f + k * 0.8f, 3.3f, -40.2f + (k % 2) * 0.5f), V(0.35f, 0.42f, 0.35f), new[] { "CaneRed", "GiftBlue", "GlowYellow", "GiftGreen", "BonePink" }[k]);
                    P(d, PrimitiveType.Cube, "Kite", V(x, 3.25f, -43.84f), V(0.9f, 0.9f, 0.02f), "GemPurple", 0f);
                    break;
                case "POST OFFICE":
                    for (int r = 0; r < 5; r++) for (int c = 0; c < 6; c++)
                        Box(d, "PO box", V(x - 4.36f, 0.6f + r * 0.45f, -40.6f + c * 0.5f), V(0.04f, 0.4f, 0.45f), M("Brass"), false);
                    townProps += 30;
                    for (int k = 0; k < 8; k++) PB(d, "Parcel", V(x + 2.6f + (k % 2) * 0.7f, 0.25f + (k / 2) * 0.5f, -43.1f), V(0.6f, 0.45f, 0.6f), "Cardboard");
                    for (int k = 0; k < 3; k++) P(d, PrimitiveType.Capsule, "Mail sack", V(x - 3.2f + k * 0.7f, 0.4f, -43.1f), V(0.55f, 0.4f, 0.55f), k == 1 ? "TrimDark" : "CaneWhite");
                    P(d, PrimitiveType.Cube, "Scales", V(x - 2f, 1.08f, -41.6f), V(0.5f, 0.12f, 0.4f), "Metal");
                    foreach (float qz in new[] { -36.8f, -40.6f }) P(d, PrimitiveType.Cylinder, "Queue post", V(x + 1.6f, 0.5f, qz), V(0.1f, 0.5f, 0.1f), "Brass");
                    Box(d, "Queue rope", V(x + 1.6f, 0.9f, -38.7f), V(0.05f, 0.05f, 3.8f), M("PoliceBlue"), false);
                    Text("Poster", d, V(x + 4.36f, 2.2f, -38.7f), Face(Vector3.left), V2(2.4f, 1.2f), 0.2f, "<b>SEND A LETTER</b>\nit's like a text message, but it arrives next week").color = new Color(0.2f, 0.25f, 0.6f);
                    break;
                case "THE BONEPLEX":
                    Box(d, "Red carpet", V(x, 0.02f, -38.6f), V(2f, 0.01f, 5.2f), M("CaneRed"), false);
                    foreach (float sd in new[] { -2f, 2f }) Box(d, "Screen door", V(x + sd, 1.2f, -43.86f), V(1.6f, 2.4f, 0.06f), M("DoorWood"), false);
                    Text("Screen1", d, V(x - 2f, 2.7f, -43.82f), Face(Vector3.forward), V2(1.6f, 0.3f), 0.2f, "<b>SCREEN 1</b>").color = new Color(1f, 0.85f, 0.4f);
                    Text("Screen2", d, V(x + 2f, 2.7f, -43.82f), Face(Vector3.forward), V2(1.6f, 0.3f), 0.2f, "<b>SCREEN 2</b>").color = new Color(1f, 0.85f, 0.4f);
                    PB(d, "Popcorn machine", V(x - 3.6f, 0.9f, -38.8f), V(1f, 1.8f, 1f), "Glass");
                    for (int k = 0; k < 9; k++) P(d, PrimitiveType.Sphere, "Popcorn", V(x - 3.9f + (k % 3) * 0.3f, 0.6f + (k / 3) * 0.25f, -38.8f), Vector3.one * 0.22f, "CornYellow");
                    PB(d, "Candy case", V(x + 3.6f, 0.5f, -38.8f), V(1f, 1f, 2.4f), "Glass");
                    for (int k = 0; k < 8; k++) P(d, PrimitiveType.Sphere, "Candy", V(x + 3.6f + ((k % 2) - 0.5f) * 0.4f, 1.08f, -39.8f + (k / 2) * 0.6f), Vector3.one * 0.18f, new[] { "GlowRed", "GiftGreen", "GlowYellow", "GemPink" }[k % 4]);
                    Frame(d, V(x - 4.36f, 2.4f, -40.8f), Vector3.right, 1f, 1.4f, "<b>NIGHT OF THE LIVING FLESH</b>\nthey're squishy. they're warm. they're coming.");
                    Frame(d, V(x + 4.36f, 2.4f, -40.8f), Vector3.left, 1f, 1.4f, "<b>BONE AGAIN</b>\na romantic comedy about a skeleton who falls apart");
                    break;
                case "CAFE CALCIUM":
                    Shelves(d, V(x, 0, -43.75f), Vector3.forward, 6.4f, 2, new[] { "CaneWhite", "Trunk", "Glass" }, PrimitiveType.Cylinder);
                    P(d, PrimitiveType.Cube, "Espresso machine", V(x + 2.2f, 1.35f, -41.6f), V(1f, 0.7f, 0.55f), "Metal");
                    P(d, PrimitiveType.Cube, "Pastry case", V(x - 2f, 1.25f, -41.6f), V(1.6f, 0.5f, 0.5f), "Glass");
                    foreach (float sd in new[] { -1f, 1f })
                    {
                        Table(d, V(x + sd * 3f, 0, -38.4f), 0.45f, "CaneWhite");
                        Chair(d, V(x + sd * 3f, 0, -37.5f), 180f, "Trunk"); Chair(d, V(x + sd * 3f, 0, -39.3f), 0f, "Trunk");
                        P(d, PrimitiveType.Cylinder, "Cup", V(x + sd * 3f, 0.82f, -38.4f), V(0.12f, 0.06f, 0.12f), "CaneWhite");
                        P(d, PrimitiveType.Cylinder, "Plant pot", V(x + sd * 3.9f, 0.3f, -36.5f), V(0.5f, 0.3f, 0.5f), "Brick");
                        P(d, PrimitiveType.Sphere, "Plant", V(x + sd * 3.9f, 0.9f, -36.5f), V(0.7f, 0.8f, 0.7f), "GiftGreen");
                    }
                    Box(d, "Menu board", V(x, 3.1f, -43.85f), V(4f, 1f, 0.04f), M("Black"), false);
                    Text("Menu", d, V(x, 3.1f, -43.82f), Face(Vector3.forward), V2(3.8f, 0.9f), 0.17f, "<b>CAFE CALCIUM</b>\nmilk . more milk . a latte (mostly milk) . bone broth").color = Color.white;
                    break;
                case "FLOWERS":
                    Shelves(d, V(x, 0, -43.75f), Vector3.forward, 6.4f, 2, new[] { "GiftGreen", "PixelGreenDark" }, PrimitiveType.Sphere);
                    foreach (float sd in new[] { -1f, 1f }) for (int k = 0; k < 5; k++)
                        {
                            var bp = V(x + sd * 3.8f, 0, -37f - k * 0.9f);
                            P(d, PrimitiveType.Cylinder, "Bucket", bp + V(0, 0.25f, 0), V(0.5f, 0.25f, 0.5f), "Metal");
                            P(d, PrimitiveType.Sphere, "Bunch", bp + V(0, 0.75f, 0), V(0.55f, 0.45f, 0.55f), new[] { "BonePink", "GlowYellow", "GiftRed", "GemPurple", "CaneWhite" }[(k + (sd > 0 ? 2 : 0)) % 5]);
                        }
                    for (int k = 0; k < 3; k++) { P(d, PrimitiveType.Cylinder, "Hanger", V(x - 1.5f + k * 1.5f, 3.5f, -39f), V(0.02f, 0.45f, 0.02f), "Black"); P(d, PrimitiveType.Sphere, "Hanging basket", V(x - 1.5f + k * 1.5f, 3f, -39f), V(0.6f, 0.45f, 0.6f), "GiftGreen"); }
                    P(d, PrimitiveType.Cylinder, "Wreath", V(x, 2.9f, -43.84f), V(0.9f, 0.02f, 0.9f), "PixelGreenDark", 0f);
                    d.Find("Wreath").localRotation = Quaternion.Euler(90, 0, 0);
                    P(d, PrimitiveType.Cylinder, "Watering can", V(x + 1.2f, 0.2f, -37.2f), V(0.3f, 0.2f, 0.3f), "GiftGreen");
                    break;
                case "FOR LEASE":
                    for (int k = 0; k < 3; k++) PB(d, "Dust sheet", V(x - 2.5f + k * 2.5f, 0.45f, -43f), V(1.6f, 0.9f, 1f), "CaneWhite");
                    var ladder = Group(d, "Ladder", V(x + 3.5f, 0, -38.5f));
                    foreach (float lx in new[] { -0.25f, 0.25f }) Prim(PrimitiveType.Cube, "Rail", ladder, V(lx, 1.2f, 0), V(0.06f, 2.4f, 0.06f), M("DeskWood"));
                    for (int k = 0; k < 6; k++) Prim(PrimitiveType.Cube, "Rung", ladder, V(0, 0.3f + k * 0.38f, 0), V(0.5f, 0.04f, 0.04f), M("DeskWood"));
                    ladder.localRotation = Quaternion.Euler(-12, 0, 0);
                    townProps++;
                    for (int k = 0; k < 3; k++) P(d, PrimitiveType.Cylinder, "Paint can", V(x + 2.6f + k * 0.3f, 0.15f, -37.1f), V(0.25f, 0.15f, 0.25f), new[] { "CaneWhite", "GiftBlue", "Metal" }[k]);
                    Chair(d, V(x - 1f, 0, -39.4f), 20f, "DeskWood");
                    P(d, PrimitiveType.Sphere, "Bare bulb", V(x - 1f, 3.2f, -39.4f), Vector3.one * 0.15f, "GlowWarm");
                    for (int k = 0; k < 2; k++) Box(d, "Cobweb", V(x + (k == 0 ? -4.3f : 4.3f), 3.6f, -43.6f), V(0.02f, 0.6f, 0.6f), M("Cobweb"), false);
                    for (int k = 0; k < 4; k++) PB(d, "Box", V(x - 3.4f + (k % 2) * 0.6f, 0.2f + (k / 2) * 0.4f, -37.3f), V(0.5f, 0.4f, 0.5f), "Cardboard");
                    break;
            }
        }
    }

    // ------------------------------------------------------------------------------------------------ the civic buildings

    static void TownHallInside(Transform g, Transform ig)
    {
        Kill(g.Find("Town Hall"), "Hall");
        // the town clock's press-zone sat across the steps; move it to the side so the new doorway is clear
        var thg = g.Find("Town Hall"); var clock = thg != null ? thg.Find("The town clock") : null;
        if (clock != null) { var cc = clock.GetComponent<BoxCollider>(); if (cc != null) { cc.center = V(-3.4f, 0.6f, 0.6f); cc.size = V(1.4f, 1.2f, 1.4f); } }
        var hb = Hollow(ig, "Town Hall", -51f, -41f, -48f, -40f, 6f, false, -46f, 2f, 3.6f, "Concrete", "Floor2");
        // the hall stands on a plinth level with the top of its steps: a porch under the portico, a raised floor inside
        Box(hb, "Raised floor", V(-46f, 0.3f, -44f), V(9.6f, 0.6f, 7.6f), M("Floor2"));
        Box(hb, "Porch", V(-46f, 0.3f, -39.1f), V(10.4f, 0.6f, 1.6f), M("Concrete"));
        var kv = townRooms[townRooms.Count - 1]; townRooms[townRooms.Count - 1] = new KeyValuePair<string, float[]>(kv.Key, new[] { kv.Value[0], kv.Value[1], kv.Value[2], kv.Value[3], kv.Value[4], -38.6f, kv.Value[6], 0.6f });
        var b = Group(hb, "Furniture", V(0, 0.6f, 0));
        PB(b, "Dais", V(-46f, 0.25f, -46.6f), V(6f, 0.5f, 2.4f), "Stage");
        PB(b, "Mayor's desk", V(-46f, 0.95f, -46.4f), V(2.6f, 0.9f, 0.9f), "DoorWood");
        P(b, PrimitiveType.Cylinder, "Gavel", V(-45.4f, 1.45f, -46.4f), V(0.08f, 0.1f, 0.08f), "DeskWood");
        Chair(b, V(-46f, 0.5f, -47.3f), 0f, "CaneRed");
        foreach (float fx in new[] { -50.2f, -41.8f })
        {
            P(b, PrimitiveType.Cylinder, "Flag pole", V(fx, 1.5f, -47.3f), V(0.06f, 1.5f, 0.06f), "Brass");
            P(b, PrimitiveType.Cube, "Flag", V(fx + (fx < -46f ? 0.45f : -0.45f), 2.6f, -47.3f), V(0.8f, 0.55f, 0.02f), fx < -46f ? "PoliceBlue" : "GiftRed");
        }
        for (int r = 0; r < 3; r++) foreach (float bx in new[] { -48.8f, -43.2f }) PB(b, "Bench", V(bx, 0.25f, -44.6f + r * 1.4f), V(3.2f, 0.5f, 0.5f), "DeskWood");
        Frame(b, V(-50.86f, 3f, -45f), Vector3.right, 1.2f, 1.5f, "<b>MAYOR MARROWMONT</b>\nfirst term, 1952");
        Frame(b, V(-41.14f, 3f, -45f), Vector3.left, 1.2f, 1.5f, "<b>THE TOWN CHARTER</b>\nrule 1: no running. rule 2: no flesh.");
        PendantLamp(b, V(-46f, 5.4f, -44f), 1.4f);
        Text("HallInside", b, V(-46f, 4.2f, -47.88f), Face(Vector3.forward), V2(8f, 0.6f), 0.4f, "<b>COUNCIL CHAMBER</b>").color = new Color(0.3f, 0.25f, 0.2f);
    }

    static void SchoolInside(Transform g, Transform ig)
    {
        Kill(g, "School");
        var b = Hollow(ig, "School", -43f, -29f, 57f, 65f, 5f, true, -36.05f, 1.4f, 2.4f, "BrickWall", "Floor2");
        Box(b, "Chalkboard", V(-36f, 2.2f, 64.86f), V(6f, 1.6f, 0.04f), M("GiftGreen"), false);
        Text("Lesson", b, V(-36f, 2.2f, 64.83f), Face(Vector3.forward), V2(5.6f, 1.4f), 0.3f, "2 + 2 = 4\nbones in one hand: 27\nhomework: count your ribs").color = Color.white;
        Text("Alphabet", b, V(-36f, 3.6f, 64.85f), Face(Vector3.forward), V2(12f, 0.4f), 0.3f, "A  B  C  D  E  F  G  H  I  J  K  L  M  N  O  P  Q  R  S  T  U  V  W  X  Y  Z").color = new Color(0.9f, 0.3f, 0.2f);
        PB(b, "Teacher's desk", V(-36f, 0.4f, 63.2f), V(2f, 0.8f, 0.9f), "DoorWood");
        P(b, PrimitiveType.Sphere, "Globe", V(-35.4f, 1.05f, 63.2f), Vector3.one * 0.35f, "GiftBlue");
        P(b, PrimitiveType.Sphere, "Apple", V(-36.6f, 0.88f, 63.2f), Vector3.one * 0.15f, "GlowRed");
        foreach (float dx in new[] { -41f, -38.6f, -33.5f, -31.1f }) foreach (float dz in new[] { 59.2f, 60.8f, 62.2f })
            {
                PB(b, "Pupil's desk", V(dx, 0.35f, dz), V(0.9f, 0.7f, 0.6f), "DeskWood");
                Chair(b, V(dx, 0, dz - 0.55f), 0f, "GiftBlue");
            }
        for (int k = 0; k < 10; k++) P(b, PrimitiveType.Cube, "Cubby", V(-42.75f, 0.4f + (k % 2) * 0.5f, 58f + (k / 2) * 0.55f), V(0.3f, 0.45f, 0.5f), new[] { "GiftRed", "GlowYellow", "GiftGreen", "GiftBlue", "GemPurple" }[k % 5]);
        Shelves(b, V(-29.25f, 0, 61f), Vector3.left, 3f, 3, new[] { "Book1", "Book2", "Book3" }, PrimitiveType.Cube);
        Mini(b, "The classroom skeleton", V(-41.8f, 0, 63.8f), 135f, 0.9f, "#eeeeee");
        townProps++;
        PendantLamp(b, V(-39f, 5f, 61f), 1f); PendantLamp(b, V(-33f, 5f, 61f), 1f);
    }

    static void LibraryInside(Transform g, Transform ig)
    {
        Kill(g, "Library");
        var b = Hollow(ig, "Library", -61f, -51f, 57f, 65f, 5f, true, -57.5f, 1.6f, 2.4f, "Concrete", "Rug");
        Shelves(b, V(-56f, 0, 64.75f), Vector3.back, 9f, 4, new[] { "Book1", "Book2", "Book3", "GiftRed" }, PrimitiveType.Cube);
        Shelves(b, V(-60.75f, 0, 61f), Vector3.right, 5f, 4, new[] { "Book2", "Book3", "Book1" }, PrimitiveType.Cube);
        foreach (float sx in new[] { -54.6f, -52.4f })
        {
            PB(b, "Bookcase", V(sx, 1.1f, 61.3f), V(0.5f, 2.2f, 4f), "DeskWood");
            for (int r = 0; r < 4; r++) for (int k = 0; k < 8; k++) foreach (float side in new[] { -0.28f, 0.28f })
                    P(b, PrimitiveType.Cube, "Book", V(sx + side, 0.3f + r * 0.5f, 59.6f + k * 0.45f), V(0.08f, 0.38f, 0.3f), new[] { "Book1", "Book2", "Book3" }[(k + r) % 3]);
        }
        PB(b, "Reading table", V(-58.6f, 0.38f, 61.3f), V(1.6f, 0.76f, 2.4f), "DoorWood");
        foreach (float cz in new[] { 60.6f, 62f }) { Chair(b, V(-59.7f, 0, cz), 90f, "CaneRed"); Chair(b, V(-57.5f, 0, cz), -90f, "CaneRed"); }
        P(b, PrimitiveType.Cylinder, "Reading lamp", V(-58.6f, 1f, 61.3f), V(0.25f, 0.25f, 0.25f), "GlowWarm");
        PB(b, "Returns desk", V(-53.5f, 0.45f, 58.4f), V(2f, 0.9f, 0.8f), "DoorWood");
        Text("Shh", b, V(-56f, 4.2f, 64.6f), Face(Vector3.back), V2(4f, 0.6f), 0.45f, "<b>SHHH.</b>").color = new Color(0.8f, 0.2f, 0.2f);
        PendantLamp(b, V(-56f, 5f, 61f), 1.2f);
    }

    static void FireStationInside(Transform g, Transform ig)
    {
        Kill(g, "FireStation", "FireDoor");
        var b = Hollow(ig, "Fire Station", -79f, -67f, 57.5f, 66.5f, 6f, true, -73f, 5f, 4f, "BrickWall", "Concrete");
        Box(b, "Outside red", V(-73f, 5f, 57.38f), V(12f, 2f, 0.04f), M("GiftRed"), false);
        var eng = Group(b, "Spare fire engine", V(-73f, 0, 62.5f));
        Box(eng, "Body", V(0, 1.1f, 0), V(2.2f, 1.6f, 6f), M("GiftRed"));
        Box(eng, "Cab", V(0, 1.6f, -2.4f), V(2.2f, 1.2f, 1.2f), M("Glass"), false);
        foreach (float wz in new[] { -2f, 2f }) foreach (float wx in new[] { -1.1f, 1.1f }) Prim(PrimitiveType.Cylinder, "Wheel", eng, V(wx, 0.45f, wz), V(0.9f, 0.12f, 0.9f), M("Black")).transform.localRotation = Quaternion.Euler(0, 0, 90);
        Box(eng, "Hose reel", V(0, 2.1f, 1.5f), V(1.6f, 0.5f, 1.2f), M("CaneWhite"), false);
        townProps++;
        P(b, PrimitiveType.Cylinder, "Fire pole", V(-68.5f, 3f, 64.8f), V(0.12f, 3f, 0.12f), "Brass");
        P(b, PrimitiveType.Cylinder, "Pole hole", V(-68.5f, 5.98f, 64.8f), V(1f, 0.02f, 1f), "Black");
        for (int k = 0; k < 6; k++) PB(b, "Locker", V(-78.6f, 1f, 59f + k * 0.8f), V(0.6f, 2f, 0.7f), k % 2 == 0 ? "Metal" : "LabGrey");
        for (int k = 0; k < 4; k++)
        {
            P(b, PrimitiveType.Cube, "Coat", V(-67.35f, 1.4f, 59f + k * 0.9f), V(0.2f, 1f, 0.6f), "GlowYellow");
            P(b, PrimitiveType.Sphere, "Helmet", V(-67.4f, 2.1f, 59f + k * 0.9f), V(0.4f, 0.25f, 0.45f), "GiftRed");
        }
        PB(b, "Kitchen table", V(-76.5f, 0.4f, 65.2f), V(2f, 0.8f, 1f), "DeskWood");
        P(b, PrimitiveType.Cylinder, "Coffee pot", V(-76f, 0.95f, 65.2f), V(0.2f, 0.15f, 0.2f), "Black");
        Text("Board", b, V(-73f, 4.5f, 66.35f), Face(Vector3.back), V2(6f, 0.8f), 0.35f, "<b>DAYS WITHOUT A FIRE: 2</b>\n<size=60%>(the toaster again)</size>").color = Color.white;
        PendantLamp(b, V(-76f, 6f, 61f), 1.5f); PendantLamp(b, V(-70f, 6f, 61f), 1.5f);
    }

    static void BarnInside(Transform g, Transform ig)
    {
        var fm = g.Find("The farm");
        Kill(fm, "Barn", "BarnDoor");
        var b = Hollow(ig, "Barn", -24f, -16f, 37f, 43f, 5f, true, -20f, 3f, 3.2f, "GiftRed", "PixelSand");
        foreach (float sd in new[] { -1f, 1f })
        {
            var dr = Box(b, "Barn door (open)", V(-20f + sd * 2.6f, 1.6f, 36.5f), V(1.5f, 3.2f, 0.06f), M("CaneWhite"), false);
            dr.transform.localRotation = Quaternion.Euler(0, sd * -60f, 0);
        }
        for (int k = 0; k < 6; k++) PB(b, "Hay bale", V(-23.1f + (k % 3) * 1.1f, 0.3f + (k / 3) * 0.6f, 42.2f), V(1f, 0.6f, 0.8f), "CornYellow");
        foreach (float sx in new[] { -18.5f, -16.9f }) Box(b, "Stall divider", V(sx, 0.6f, 41.3f), V(0.08f, 1.2f, 2.8f), M("DeskWood"));
        P(b, PrimitiveType.Cube, "Trough", V(-17.7f, 0.3f, 42.4f), V(1.2f, 0.4f, 0.4f), "Metal");
        var fork = Group(b, "Pitchfork", V(-23.6f, 0, 39f));
        Prim(PrimitiveType.Cylinder, "Handle", fork, V(0, 0.9f, 0), V(0.04f, 0.9f, 0.04f), M("DeskWood"));
        for (int k = 0; k < 3; k++) Prim(PrimitiveType.Cube, "Tine", fork, V(-0.08f + k * 0.08f, 1.95f, 0), V(0.02f, 0.3f, 0.02f), M("Metal"));
        fork.localRotation = Quaternion.Euler(0, 0, 10);
        townProps++;
        for (int k = 0; k < 4; k++) P(b, PrimitiveType.Sphere, "Chicken", V(-21.5f + k * 0.6f, 0.2f, 40.2f + (k % 2) * 0.5f), V(0.3f, 0.3f, 0.35f), "CaneWhite");
        P(b, PrimitiveType.Sphere, "Lantern", V(-20f, 3.6f, 40f), Vector3.one * 0.3f, "GlowWarm");
    }

    static void HouseInside(Transform g, Transform ig, string name, float x, string wall)
    {
        var h = g.Find(name);
        Kill(h, "Body", "Door");
        var b = Hollow(ig, name, x - 4.5f, x + 4.5f, 57f, 64f, 3.5f, true, x, 1.2f, 2.2f, wall, "Floor2");
        Box(b, "Doormat", V(x, 0.03f, 57.8f), V(1.2f, 0.01f, 0.7f), M("TrimDark"), false);
        // the living room (west half)
        Box(b, "Rug", V(x - 2.3f, 0.025f, 60.5f), V(3f, 0.01f, 2.4f), M("Rug"), false);
        PB(b, "Sofa", V(x - 2.3f, 0.35f, 62.9f), V(2.6f, 0.7f, 0.9f), "Seat");
        Box(b, "Sofa back", V(x - 2.3f, 0.85f, 63.4f), V(2.6f, 0.8f, 0.2f), M("Seat"), false);
        PB(b, "TV cabinet", V(x - 2.3f, 0.3f, 58.4f), V(1.6f, 0.6f, 0.5f), "DoorWood");
        Box(b, "TV", V(x - 2.3f, 0.95f, 58.4f), V(1.2f, 0.7f, 0.08f), M("Black"), false);
        P(b, PrimitiveType.Cylinder, "Floor lamp", V(x - 4f, 0.8f, 63.4f), V(0.05f, 0.8f, 0.05f), "Black");
        P(b, PrimitiveType.Cylinder, "Lamp shade", V(x - 4f, 1.65f, 63.4f), V(0.45f, 0.2f, 0.45f), "GlowWarm");
        Frame(b, V(x - 2.3f, 2.1f, 63.88f), Vector3.back, 1.2f, 0.8f, null);
        // the kitchen (east half)
        PB(b, "Kitchen counter", V(x + 3.9f, 0.45f, 61f), V(0.6f, 0.9f, 3.6f), "LabWhite");
        PB(b, "Fridge", V(x + 3.9f, 0.95f, 63.3f), V(0.7f, 1.9f, 0.7f), "CaneWhite");
        P(b, PrimitiveType.Cylinder, "Kettle", V(x + 3.9f, 1.05f, 60.4f), V(0.2f, 0.15f, 0.2f), "Metal");
        Table(b, V(x + 1.8f, 0, 61f), 0.5f, "DeskWood");
        Chair(b, V(x + 1.8f, 0, 60.1f), 0f, "DeskWood"); Chair(b, V(x + 1.8f, 0, 61.9f), 180f, "DeskWood");
        P(b, PrimitiveType.Sphere, "Fruit bowl", V(x + 1.8f, 0.82f, 61f), V(0.35f, 0.12f, 0.35f), "GlowOrange");
        PendantLamp(b, V(x, 3.5f, 61f), 0.6f);
    }
}
#endif
