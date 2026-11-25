#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>The Bonetown Zoo: enclosures of animals made of shapes, keepers, a reptile house, and a night house wing.</summary>
public static partial class FirCheckBuilder
{
    /// <summary>A four-legged animal: a body, legs, a head and a tail. Returns it so trunks, manes, stripes and necks can be added.</summary>
    static Transform Quad(Transform g, string name, Vector3 at, float yaw, float len, float legH, float girth, string mat)
    {
        var a = Group(g, name, at);
        a.localRotation = Quaternion.Euler(0, yaw, 0);
        float by = legH + girth * 0.4f;
        Prim(PrimitiveType.Capsule, "Body", a, V(0, by, 0), V(girth, len / 2f, girth), M(mat)).transform.localRotation = Quaternion.Euler(90, 0, 0);
        foreach (float lx in new[] { -girth * 0.3f, girth * 0.3f }) foreach (float lz in new[] { -len * 0.32f, len * 0.32f })
                Prim(PrimitiveType.Cylinder, "Leg", a, V(lx, legH / 2f, lz), V(girth * 0.28f, legH / 2f, girth * 0.28f), M(mat));
        Prim(PrimitiveType.Sphere, "Head", a, V(0, by + girth * 0.35f, len * 0.55f), Vector3.one * girth * 0.7f, M(mat));
        foreach (float ex in new[] { -0.15f, 0.15f }) Prim(PrimitiveType.Sphere, "Eye", a, V(ex * girth, by + girth * 0.5f, len * 0.55f + girth * 0.32f), Vector3.one * girth * 0.1f, M("Black"));
        Prim(PrimitiveType.Capsule, "Tail", a, V(0, by, -len * 0.55f), V(girth * 0.12f, girth * 0.35f, girth * 0.12f), M(mat)).transform.localRotation = Quaternion.Euler(-30, 0, 0);
        return a;
    }

    static Transform Elephant(Transform g, string name, Vector3 at, float yaw, float s)
    {
        var e = Quad(g, name, at, yaw, 3f * s, 1.3f * s, 2f * s, "TurfGrey");
        Prim(PrimitiveType.Capsule, "Trunk", e, V(0, 1.7f * s, 2.4f * s), V(0.3f * s, 0.8f * s, 0.3f * s), M("TurfGrey")).transform.localRotation = Quaternion.Euler(20, 0, 0);
        foreach (float ex in new[] { -1f, 1f }) Prim(PrimitiveType.Cylinder, "Ear", e, V(ex * 0.75f * s, 2.6f * s, 1.5f * s), V(1.2f * s, 0.04f * s, 1f * s), M("TurfGrey")).transform.localRotation = Quaternion.Euler(0, 0, ex * 80f);
        foreach (float tx in new[] { -0.3f, 0.3f }) Prim(PrimitiveType.Capsule, "Tusk", e, V(tx * s, 1.8f * s, 2.3f * s), V(0.1f * s, 0.3f * s, 0.1f * s), M("CaneWhite")).transform.localRotation = Quaternion.Euler(60, 0, 0);
        return e;
    }

    static Transform Giraffe(Transform g, string name, Vector3 at, float yaw, float s)
    {
        var gi = Quad(g, name, at, yaw, 1.8f * s, 2.2f * s, 1.1f * s, "CornYellow");
        Prim(PrimitiveType.Capsule, "Neck", gi, V(0, 4f * s, 0.9f * s), V(0.35f * s, 1.3f * s, 0.35f * s), M("CornYellow")).transform.localRotation = Quaternion.Euler(20, 0, 0);
        Prim(PrimitiveType.Sphere, "Head", gi, V(0, 5.3f * s, 1.4f * s), V(0.45f * s, 0.4f * s, 0.7f * s), M("CornYellow"));
        foreach (float hx in new[] { -0.12f, 0.12f }) Prim(PrimitiveType.Cylinder, "Ossicone", gi, V(hx * s, 5.6f * s, 1.3f * s), V(0.06f * s, 0.15f * s, 0.06f * s), M("Trunk"));
        for (int k = 0; k < 8; k++) Prim(PrimitiveType.Sphere, "Spot", gi, V((k % 2 == 0 ? -0.5f : 0.5f) * s, 2.7f * s + (k % 3) * 0.12f * s, -0.6f * s + k * 0.17f * s), V(0.06f, 0.3f, 0.3f) * s, M("Trunk"));
        return gi;
    }

    static Transform Penguin(Transform g, string name, Vector3 at, float yaw, float s)
    {
        var p = Group(g, name, at);
        p.localRotation = Quaternion.Euler(0, yaw, 0);
        Prim(PrimitiveType.Capsule, "Body", p, V(0, 0.4f * s, 0), V(0.4f, 0.4f, 0.35f) * s, M("Black"));
        Prim(PrimitiveType.Sphere, "Belly", p, V(0, 0.38f * s, 0.1f * s), V(0.3f, 0.5f, 0.2f) * s, M("CaneWhite"));
        Prim(PrimitiveType.Cube, "Beak", p, V(0, 0.68f * s, 0.2f * s), V(0.06f, 0.05f, 0.12f) * s, M("GlowOrange"));
        return p;
    }

    static Transform Flamingo(Transform g, string name, Vector3 at, float yaw)
    {
        var f = Group(g, name, at);
        f.localRotation = Quaternion.Euler(0, yaw, 0);
        Prim(PrimitiveType.Cylinder, "Leg", f, V(0, 0.45f, 0), V(0.04f, 0.45f, 0.04f), M("GemPink"));
        Prim(PrimitiveType.Sphere, "Body", f, V(0, 1f, -0.05f), V(0.45f, 0.35f, 0.6f), M("GemPink"));
        Prim(PrimitiveType.Capsule, "Neck", f, V(0, 1.4f, 0.2f), V(0.07f, 0.3f, 0.07f), M("GemPink"));
        Prim(PrimitiveType.Sphere, "Head", f, V(0, 1.72f, 0.28f), Vector3.one * 0.14f, M("GemPink"));
        Prim(PrimitiveType.Cube, "Beak", f, V(0, 1.68f, 0.38f), V(0.04f, 0.04f, 0.12f), M("Black"));
        return f;
    }

    /// <summary>A low fence round an enclosure, so visitors can look over it.</summary>
    static void Fence(Transform g, float x0, float z0, float x1, float z1, string mat)
    {
        Box(g, "Fence S", V((x0 + x1) / 2f, 0.55f, z0), V(x1 - x0, 1.1f, 0.12f), M(mat));
        Box(g, "Fence N", V((x0 + x1) / 2f, 0.55f, z1), V(x1 - x0, 1.1f, 0.12f), M(mat));
        Box(g, "Fence W", V(x0, 0.55f, (z0 + z1) / 2f), V(0.12f, 1.1f, z1 - z0), M(mat));
        Box(g, "Fence E", V(x1, 0.55f, (z0 + z1) / 2f), V(0.12f, 1.1f, z1 - z0), M(mat));
    }

    static void ZooSign(Transform g, Vector3 at, string title, string sub)
    {
        var s = Group(g, "Enclosure sign", at);
        DPrim(PrimitiveType.Cylinder, "Post", s, V(0, 0.7f, 0), V(0.08f, 0.7f, 0.08f), M("DeskWood"));
        Box(s, "Board", V(0, 1.55f, 0), V(1.8f, 0.7f, 0.06f), M("DeskWood"), false);
        Text("Sign", s, V(0, 1.55f, -0.04f), Quaternion.identity, V2(1.7f, 0.6f), 0.16f, "<b>" + title + "</b>\n<size=70%>" + sub + "</size>").color = new Color(1f, 0.95f, 0.8f);
    }

    // ================================================================================================ 29 the Bonetown Zoo

    static void RoomZoo(Transform root)
    {
        float cx = 5030f;
        var g = Room(root, "The Bonetown Zoo", 4980f, 5080f, 100f, 25f, "Grass", "PixelSky", "Sky",
                     "THE BONETOWN ZOO", new Color(0.4f, 0.8f, 0.3f), "elephants, lions, giraffes, penguins, monkeys and a reptile house. please don't feed the skeletons.", "ZOO");
        // paths
        Box(g, "Main path", V(cx, 0.01f, 60f), V(10f, 0.02f, 80f), M("PixelSand"), false);
        Box(g, "Cross path", V(cx, 0.012f, 66f), V(96f, 0.02f, 5f), M("PixelSand"), false);
        // the entrance: a ticket booth, a map, an ice cream stall
        Stall(g, "TICKETS", V(4996f, 0, 27f), 90f, "GiftGreen", "CaneWhite");
        Stall(g, "ICE CREAM", V(5016f, 0, 30f), 90f, "BonePink", "CaneWhite");
        Box(g, "Map board", V(5040f, 1.4f, 27f), V(3f, 1.8f, 0.1f), M("DeskWood"));
        Text("Map", g, V(5040f, 1.4f, 26.94f), Face(Vector3.back), V2(2.8f, 1.6f), 0.15f, "<b>ZOO MAP</b>\nplains: north-west . giraffes: north-east\nlions: west . penguins: east\nflamingos and monkeys: the middle\nreptile house: by the gate . night house: through the far wall").color = Color.white;

        // the plains (north-west): elephants, zebras, a watering hole
        Fence(g, 4984f, 72f, 5012f, 96f, "DeskWood");
        Box(g, "Watering hole", V(4992f, 0.02f, 90f), V(8f, 0.04f, 6f), M("PoolBlue"), false);
        Elephant(g, "Elephant", V(5000f, 0, 82f), -120f, 1f);
        Elephant(g, "Baby elephant", V(5005f, 0, 78f), -100f, 0.55f);
        for (int k = 0; k < 2; k++)
        {
            var z = Quad(g, "Zebra " + k, V(4990f + k * 4f, 0, 77f + k * 2f), 60f + k * 40f, 1.8f, 1f, 0.9f, "CaneWhite");
            for (int st = 0; st < 5; st++) Prim(PrimitiveType.Cube, "Stripe", z, V(0, 1.4f, -0.7f + st * 0.35f), V(0.95f, 0.85f, 0.1f), M("Black"));
        }
        ZooSign(g, V(5013f, 0, 70.6f), "THE PLAINS", "elephants and zebras");
        // the giraffes (north-east): a tall tree
        Fence(g, 5048f, 72f, 5076f, 96f, "DeskWood");
        DPrim(PrimitiveType.Cylinder, "Acacia trunk", g, V(5068f, 3.5f, 90f), V(0.6f, 3.5f, 0.6f), M("Trunk"));
        DPrim(PrimitiveType.Cylinder, "Acacia canopy", g, V(5068f, 7.4f, 90f), V(8f, 0.5f, 6f), M("GiftGreen"));
        Giraffe(g, "Giraffe", V(5064f, 0, 86f), 30f, 1f);
        Giraffe(g, "Young giraffe", V(5056f, 0, 80f), 200f, 0.7f);
        ZooSign(g, V(5047f, 0, 70.6f), "GIRAFFES", "very tall. very calm. very judgemental");
        // the lions (west): a rock
        Fence(g, 4984f, 38f, 5008f, 62f, "Metal");
        DPrim(PrimitiveType.Sphere, "Pride rock", g, V(4994f, 1f, 54f), V(8f, 4f, 6f), M("Stone"));
        for (int k = 0; k < 3; k++)
        {
            var li = Quad(g, k == 0 ? "Lion" : "Lioness " + k, V(4990f + k * 5f, k == 0 ? 2.8f : 0f, k == 0 ? 54f : 44f + k * 2f), 90f + k * 50f, 1.9f, 0.7f, 0.95f, "CornYellow");
            if (k == 0) Prim(PrimitiveType.Sphere, "Mane", li, V(0, 1.5f, 1f), Vector3.one * 1.05f, M("Trunk"));
        }
        ZooSign(g, V(5009f, 0, 50f), "LIONS", "the king sleeps 20 hours a day. same.");
        // the penguins (east): an icy pool
        Fence(g, 5052f, 38f, 5076f, 62f, "Glass");
        Box(g, "Penguin pool", V(5066f, 0.02f, 52f), V(14f, 0.04f, 12f), M("PoolBlue"), false);
        for (int k = 0; k < 4; k++) DPrim(PrimitiveType.Cube, "Ice floe", g, V(5058f + k * 4f, 0.25f, 42f + (k % 2) * 3f), V(3f, 0.5f, 2.4f), M("CaneWhite"), Quaternion.Euler(0, k * 25f, 0));
        for (int k = 0; k < 7; k++) Penguin(g, "Penguin " + k, V(5056f + (k % 4) * 1.4f + (k / 4) * 8f, 0.5f, 41.5f + (k % 3) * 1f), 180f + k * 30f, 1f);
        ZooSign(g, V(5051f, 0, 50f), "PENGUINS", "they waddle. they dive. they don't fly. they've made their peace with it.");
        // the flamingos (middle south): a lagoon
        Box(g, "Flamingo lagoon", V(cx, 0.03f, 48f), V(12f, 0.04f, 8f), M("PoolGreen"), false);
        Fence(g, cx - 6.5f, 43.5f, cx + 6.5f, 52.5f, "CaneWhite");
        for (int k = 0; k < 6; k++) Flamingo(g, "Flamingo " + k, V(cx - 4.5f + k * 1.8f, 0, 46f + (k % 2) * 2.5f), k * 50f);
        // the monkeys (middle north): an island in a moat
        DPrim(PrimitiveType.Cylinder, "Moat", g, V(cx, 0.03f, 84f), V(18f, 0.03f, 18f), M("PoolBlue"));
        DPrim(PrimitiveType.Cylinder, "Monkey island", g, V(cx, 0.4f, 84f), V(12f, 0.4f, 12f), M("PixelGreen"));
        DPrim(PrimitiveType.Cylinder, "Monkey tree", g, V(cx, 4f, 84f), V(0.7f, 4f, 0.7f), M("Trunk"));
        DPrim(PrimitiveType.Sphere, "Monkey tree top", g, V(cx, 8.5f, 84f), V(6f, 3f, 6f), M("GiftGreen"));
        for (int k = 0; k < 4; k++) Mini(g, "Monkey " + k, V(cx + Mathf.Sin(k * 1.6f) * 3.5f, 0.8f + (k == 3 ? 3f : 0f), 84f + Mathf.Cos(k * 1.6f) * 3.5f), k * 90f, 0.45f, "#8d6e63");
        Box(g, "Moat wall", V(cx, 0.5f, 74.6f), V(10f, 1f, 0.2f), M("Stone"));
        // the reptile house (by the gate, east): a building with glass tanks
        var rh = Hollow(g, "Reptile house", 5058f, 5076f, 22f, 34f, 4f, false, 5067f, 2f, 2.6f, "PixelGreenDark", "Concrete");
        townRooms.RemoveAt(townRooms.Count - 1);   // it's the zoo's, not Bonetown's
        Text("ReptileSign", g, V(5067f, 3.3f, 34.12f), Face(Vector3.forward), V2(10f, 0.8f), 0.5f, "<b>REPTILE HOUSE</b>").color = new Color(0.8f, 1f, 0.6f);
        for (int k = 0; k < 4; k++)
        {
            float tz = 24.5f + k * 2.6f;
            Box(rh, "Tank", V(5059f, 1.1f, tz), V(1.4f, 1.2f, 2.2f), M("Glass"));
            Box(rh, "Tank glow", V(5058.4f, 1.1f, tz), V(0.05f, 1f, 2f), M(k % 2 == 0 ? "GlowGreen" : "GlowWarm"), false);
        }
        for (int k = 0; k < 6; k++) Prim(PrimitiveType.Sphere, "Snake coil", rh, V(5059f + Mathf.Sin(k) * 0.3f, 0.75f + k * 0.04f, 24.5f + Mathf.Cos(k) * 0.4f), Vector3.one * 0.18f, M("GiftGreen"));
        Prim(PrimitiveType.Capsule, "Lizard", rh, V(5059f, 0.65f, 27.1f), V(0.15f, 0.35f, 0.15f), M("GlowOrange")).transform.localRotation = Quaternion.Euler(90, 0, 0);
        Prim(PrimitiveType.Sphere, "Frog", rh, V(5059f, 0.65f, 29.7f), V(0.25f, 0.18f, 0.25f), M("GlowGreen"));
        PendantLamp(rh, V(5067f, 4f, 28f), 0.8f);
        // stickers to collect: one at every enclosure
        var stickers = new[] { V(5013f, 1f, 74f), V(5047f, 1f, 74f), V(5010f, 1f, 40f), V(5050f, 1f, 40f), V(cx + 8f, 1f, 48f), V(cx - 10f, 1f, 84f), V(cx + 10f, 1f, 84f), V(5067f, 1f, 30f), V(4996f, 1f, 33f), V(cx, 1f, 97f) };
        Collectables(g, "Animal stickers", stickers, (t, i) => { Prim(PrimitiveType.Cylinder, "Sticker", t, Vector3.zero, V(0.4f, 0.02f, 0.4f), M(new[] { "GlowYellow", "GemPink", "GiftBlue", "GiftGreen", "GlowOrange" }[i % 5])).transform.localRotation = Quaternion.Euler(90, 0, 0); return true; }, V(cx - 8f, 1.6f, 20.15f), Vector3.forward);
        // the keepers
        RF(g, "Head Keeper Hattie", "#a5d6a7", 16, V(5016f, 0, 66f), 90f, "zoo_hattie");
        Hat("Head Keeper Hattie", PrimitiveType.Cylinder, V(0, 0.12f, 0), V(0.42f, 0.05f, 0.42f), "PixelSand");
        RF(g, "Penguin Keeper Pablo", "#4fc3f7", 9, V(5049f, 0, 56f), 270f, "zoo_pablo");
        RF(g, "Dr Scales", "#c5e1a5", 3, V(5070f, 0, 28f), 270f, "zoo_scales");
        RF(g, "Ice Cream Seller Iggy", "#f8bbd0", 20, V(5015.4f, 0, 30f), 90f, "zoo_iggy");
        RF(g, "Tour Guide Tobias", "#ffcc80", 18, V(5024f, 0, 36f), 180f, "zoo_guide");
        // the animals' gags
        var ele = RG(g, "The elephant's spray", V(5012.8f, 0, 82f), 90f, "zoo_elephant", "fly", PrimitiveType.Sphere, V(0.5f, 0.5f, 0.5f), "PoolBlue", 3f);
        ele.target.localPosition = V(0, 2.4f, -2.5f);
        var roar = RG(g, "The lion's roar", V(5008.8f, 0, 54f), 90f, "zoo_lion", "grow", PrimitiveType.Sphere, V(0.6f, 0.6f, 0.6f), "Trunk");
        roar.target.localPosition = V(0, 3.6f, -3.4f);
        var neck = RG(g, "The giraffe's lunch", V(5047.2f, 0, 86f), 270f, "zoo_giraffe", "shake", PrimitiveType.Sphere, V(0.8f, 0.5f, 0.8f), "GiftGreen");
        neck.target.localPosition = V(0, 5.4f, -17.5f);
        var peng = RG(g, "The penguin slide", V(5051.2f, 0, 44f), 270f, "zoo_penguins", "slide", PrimitiveType.Sphere, V(0.05f, 0.05f, 0.05f), "Black", -3f);
        peng.target.localPosition = V(0, 0.5f, -2f);
        Penguin(peng.target, "Sliding penguin", Vector3.zero, 0f, 1.2f);
        var monk = RG(g, "The banana thief", V(cx + 4f, 0, 74f), 0f, "zoo_monkey", "fly", PrimitiveType.Capsule, V(0.15f, 0.4f, 0.15f), "GlowYellow", 3f);
        var fl = RG(g, "The flamingo's balance", V(cx + 6.8f, 0, 50f), 270f, "zoo_flamingo", "shake", PrimitiveType.Sphere, V(0.05f, 0.05f, 0.05f), "GemPink");
        fl.target.localPosition = V(0, 0, 1.5f);
        Flamingo(fl.target, "Wobbly flamingo", Vector3.zero, 0f);
        var snake = RG(g, "The snake", V(5061f, 0, 32f), 270f, "zoo_snake", "pop", PrimitiveType.Capsule, V(0.25f, 1.2f, 0.25f), "GiftGreen", 1.4f);
        var tort = RG(g, "The tortoise", V(5040f, 0, 40f), 0f, "zoo_tortoise", "slide", PrimitiveType.Sphere, V(0.9f, 0.5f, 1.1f), "PixelGreenDark", 0.3f);
        report.Add("The Bonetown Zoo: the plains (two elephants, two zebras, a watering hole), giraffes under an acacia, a lion pride on its rock, a penguin pool, a flamingo lagoon, a monkey island, a reptile house, an ice cream stall and 10 animal stickers; 5 voiced keepers, 8 animal gags");

        RoomWing(root, "The Bonetown Zoo", 4980f, 5080f, 100f, 26f, 8f, "Floor2", "NightSky", "Black", "THE NIGHT HOUSE", new Color(0.6f, 0.6f, 1f),
                 "the animals that come out at night. it's dark in here on purpose. let your eyes adjust.", float.NaN,
                 new[] { "Night Keeper Noor|#9fa8da|3|rzow_noor", "Bat Expert Bertram|#90a4ae|7|rzow_bertram", "Sleepy Visitor Sid|#ffe0b2|11|rzow_sid" },
                 new[] { "The bats|rzow_bats|shake|sph|1.2,0.4,1.2|Black|2|4", "The owl|rzow_owl|spin|sph|0.6,0.8,0.6|Trunk|2|2.4", "The glowing eyes|rzow_eyes|pop|sph|0.8,0.3,0.1|GlowYellow|1.5" },
                 (wg, wx, wz) => { for (int k = 0; k < 20; k++) Prim(PrimitiveType.Sphere, "Eyes in the dark", wg, V(wx - 45f + Mathf.Repeat(k * 0.618f, 1f) * 90f, 0.6f + Mathf.Repeat(k * 0.37f, 1f) * 4f, wz + 25.8f), V(0.25f, 0.1f, 0.02f), M("GlowYellow")); for (int k = 0; k < 4; k++) DPrim(PrimitiveType.Cylinder, "Night tree", wg, V(wx - 36f + k * 24f, 4f, wz + 22f), V(1.2f, 4f, 1.2f), M("TrimDark")); },
                 "pairs of eyes in the dark, and night trees");
    }
}
#endif
