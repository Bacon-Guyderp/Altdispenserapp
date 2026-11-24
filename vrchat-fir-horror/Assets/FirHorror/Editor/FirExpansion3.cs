#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>The expansion, batch 3: new wings for the original Station areas.</summary>
public static partial class FirCheckBuilder
{
    static void BuildExpansions3(Transform root)
    {
        WingPark(root); WingPrecinct(root); WingPizzeria(root); WingPixel(root); WingAnnex(root); WingBlocklands(root); WingNorth(root); WingMystery(root);
    }

    // ------------------------------------------------------------------------------------------------ Boneanza Park

    static void WingPark(Transform root)
    {
        float cx = 90f, z = 80f;
        var g = Wing(root, "Boneanza Park", 60f, 120f, z, 30f, 22f, "Dirt", "Sky", "Sky",
                     "THE MIDWAY", new Color(1f, 0.85f, 0.2f), "step right up! every game is fair! (no game is fair)", 100f);
        string[] stripe = { "CaneRed", "CaneWhite" };
        for (int k = 0; k < 6; k++)
        {
            var at = V(70f + k * 8.4f, 0, z + 26f);
            Box(g, "Booth counter", at + V(0, 0.55f, -1.4f), V(6f, 1.1f, 0.8f), M("DeskWood"));
            for (int s = 0; s < 6; s++) DPrim(PrimitiveType.Cube, "Awning stripe", g, at + V(-2.5f + s, 3.2f, -1.2f), V(1f, 0.1f, 2.4f), M(stripe[s % 2]), Quaternion.Euler(-15f, 0, 0));
            DPrim(PrimitiveType.Cube, "Booth back", g, at + V(0, 1.6f, 0.6f), V(6f, 3.2f, 0.2f), M(stripe[k % 2]));
        }
        Scatter(g, "Balloon", 20, 64f, 116f, z + 25.4f, z + 25.6f, PrimitiveType.Sphere, V(0.45f, 0.55f, 0.45f), new[] { "GlowRed", "GlowYellow", "GiftBlue", "GemPink" }, 2.2f);
        var tokens = new List<Vector3>(); for (int k = 0; k < 10; k++) tokens.Add(V(66f + k * 5.4f, 0.8f, z + 12f + (k % 3) * 2f));
        Collectables(g, "Bone tokens", tokens.ToArray(), (t, i) => { DPrim(PrimitiveType.Cylinder, "Token", t, Vector3.zero, V(0.45f, 0.04f, 0.45f), M("GlowGold"), Quaternion.Euler(90, 0, 0)); return true; }, V(cx - 7f, 1.6f, z + 0.35f), Vector3.forward);
        Folk(g, "Barker Bartholomew", "#ffb300", 16, V(82f, 0, z + 20f), 180f, "More/mo_prk_barker");
        Folk(g, "The Strongman", "#d84315", 11, V(112f, 0, z + 14f), 230f, "More/mo_prk_strong");
        Folk(g, "Cotton Candy Connie", "#f8bbd0", 9, V(66f, 0, z + 6f), 120f, "More/mo_prk_candy");
        var bell = Gk(g, "The strongman bell", V(112f, 0, z + 10f), 180f, "More/mo_prk_bell", "pop", PrimitiveType.Cylinder, V(0.4f, 0.2f, 0.4f), "GlowGold", 5f);
        DPrim(PrimitiveType.Cube, "Bell tower", g, V(112f, 3f, z + 10.4f), V(0.4f, 6f, 0.2f), M("CaneWhite"));
        bell.target.localPosition = V(0, 0.2f, 0); bell.hideWhenIdle = false;
        Gk(g, "The ring toss", V(78.4f, 0, z + 22f), 180f, "More/mo_prk_rings", "fly", PrimitiveType.Cylinder, V(0.5f, 0.04f, 0.5f), "GlowRed", 2f).target.localPosition = V(0, 1.2f, 0);
        Gk(g, "The balloon darts", V(95.2f, 0, z + 22f), 180f, "More/mo_prk_darts", "grow", PrimitiveType.Sphere, V(0.6f, 0.7f, 0.6f), "GlowRed").target.localPosition = V(0, 1.4f, 0);
        report.Add("The Midway (Boneanza Park's new wing): 6 striped game booths, balloons and 10 bone tokens; 3 voiced people, 3 gags");
    }

    // ------------------------------------------------------------------------------------------------ Precinct 206

    static void WingPrecinct(Transform root)
    {
        float cx = 158f, z = 56f;
        var g = Wing(root, "Precinct 206", 140f, 176f, z, 20f, 4.2f, "Floor2", "Wall2", "Ceiling",
                     "EVIDENCE AND INTERROGATION", new Color(0.9f, 0.9f, 0.6f), "everything here is evidence. including you. please don't touch yourself.", 166f);
        for (int k = 0; k < 4; k++)
        {
            Box(g, "Evidence shelf", V(143f, 1.2f, z + 3f + k * 4.2f), V(1.2f, 2.4f, 3.6f), M("Metal"));
            for (int lv = 0; lv < 3; lv++) Box(g, "Evidence box", V(143.7f, 0.4f + lv * 0.75f, z + 3f + k * 4.2f), V(0.2f, 0.5f, 3.2f), M("Cardboard"), false);
        }
        Box(g, "Interrogation table", V(166f, 0.75f, z + 14f), V(2.4f, 0.08f, 1.4f), M("Metal"));
        Box(g, "One-way mirror", V(175.9f, 1.8f, z + 12f), V(0.05f, 1.6f, 4f), M("Glass"), false);
        Folk(g, "Evidence Clerk Ethel", "#bcaaa4", 9, V(147f, 0, z + 2f), 90f, "More/mo_pct_clerk");
        Folk(g, "Detective Dorsal", "#8d6e63", 15, V(163.5f, 0, z + 14f), 90f, "More/mo_pct_detective");
        Folk(g, "The Usual Suspect", "#90a4ae", 20, V(168.5f, 0, z + 14f), 270f, "More/mo_pct_suspect");
        var lamp = Gk(g, "The interrogation lamp", V(166f, 0, z + 16f), 180f, "More/mo_pct_lamp", "shake", PrimitiveType.Cylinder, V(0.5f, 0.3f, 0.5f), "GlowWarm");
        lamp.target.localPosition = V(0, 3.2f, -2f); lamp.wobble = 35f;
        Gk(g, "Exhibit A", V(152f, 0, z + 10f), 180f, "More/mo_pct_exhibit", "pop", PrimitiveType.Capsule, V(0.3f, 0.5f, 0.3f), "GlowYellow", 0.9f);
        Box(g, "Exhibit table", V(152f, 0.45f, z + 10f), V(1.4f, 0.9f, 1f), M("DeskWood"));
        Gk(g, "The one-way mirror", V(174f, 0, z + 12f), 270f, "More/mo_pct_mirror", "shake", PrimitiveType.Sphere, V(0.4f, 0.4f, 0.4f), "BoneWhite").target.localPosition = V(0, 1.8f, -1.6f);
        report.Add("Evidence and Interrogation (Precinct 206's new wing): evidence shelves, an interrogation table and a one-way mirror; 3 voiced people, 3 gags");
    }

    // ------------------------------------------------------------------------------------------------ Pine Pete's Pizza

    static void WingPizzeria(Transform root)
    {
        float cx = 220f, z = 60f;
        var g = Wing(root, "Pine Petes", 200f, 240f, z, 20f, 4f, "LabFloor", "WallDark", "Ceiling",
                     "THE KITCHEN", new Color(1f, 0.5f, 0.2f), "employees only. the ovens never go out. nobody remembers lighting them.", 210f);
        for (int k = 0; k < 3; k++)
        {
            Box(g, "Pizza oven", V(206f + k * 7f, 1f, z + 18.6f), V(3.2f, 2f, 2f), M("Brick"));
            Box(g, "Oven mouth", V(206f + k * 7f, 0.9f, z + 17.58f), V(1.6f, 0.7f, 0.04f), M("GlowFlame"), false);
        }
        Box(g, "Prep counter", V(cx, 0.45f, z + 10f), V(10f, 0.9f, 1.6f), M("Metal"));
        Box(g, "Sink", V(236f, 0.45f, z + 4f), V(2f, 0.9f, 1.2f), M("Porcelain"));
        for (int k = 0; k < 6; k++) DPrim(PrimitiveType.Cylinder, "Pizza", g, V(216f + k * 1.6f, 0.93f, z + 10f), V(0.9f, 0.02f, 0.9f), M("GlowOrange"));
        Folk(g, "Head Chef Basilica", "#ffffff", 9, V(cx, 0, z + 12f), 180f, "More/mo_pzk_chef");
        Folk(g, "Delivery Driver Dash", "#ef5350", 2, V(213f, 0, z + 3f), 160f, "More/mo_pzk_driver");
        Folk(g, "Dishwasher Dot", "#80deea", 7, V(236f, 0, z + 5.8f), 180f, "More/mo_pzk_dishes");
        Gk(g, "The oven flare", V(213f, 0, z + 17f), 180f, "More/mo_pzk_oven", "grow", PrimitiveType.Sphere, V(1f, 0.6f, 0.4f), "GlowFlame").target.localPosition = V(0, 0.6f, -0.3f);
        Gk(g, "The spinning dough", V(223f, 0, z + 10f), 180f, "More/mo_pzk_dough", "spin", PrimitiveType.Cylinder, V(0.9f, 0.02f, 0.9f), "Milk");
        var box = Gk(g, "The pizza box", V(230f, 0, z + 14f), 180f, "More/mo_pzk_box", "flip", PrimitiveType.Cube, V(0.7f, 0.04f, 0.7f), "Cardboard");
        box.target.localPosition = V(0, 0.95f, 0); box.rotateBy = V(-100f, 0, 0);
        Box(g, "Box bottom", V(230f, 0.9f, z + 14f), V(0.7f, 0.1f, 0.7f), M("Cardboard"));
        Prim(PrimitiveType.Sphere, "Eyes in the box", box.transform, V(0, 0.98f, 0), V(0.3f, 0.06f, 0.1f), M("GlowRedEye"));
        Box(g, "Box stand", V(230f, 0.45f, z + 14f), V(0.8f, 0.9f, 0.8f), M("Metal"));
        report.Add("The Kitchen (Pine Pete's new wing): 3 pizza ovens, a prep counter, a sink; 3 voiced staff, 3 gags");
    }

    // ------------------------------------------------------------------------------------------------ Pixel Hollow

    static void WingPixel(Transform root)
    {
        float cx = 280f, z = 84f;
        var g = Wing(root, "Pixel Hollow", 260f, 300f, z, 26f, 10f, "GemPurple", "PixelSky", "NightSky",
                     "THE OLD RUINS", new Color(0.8f, 0.6f, 1f), "purple stone, old riddles, and somebody who really wants you to solve them");
        for (int k = 0; k < 10; k++) Box(g, "Ruin pillar", V(263f + (k % 2) * 34f, 2.5f, z + 3f + (k / 2) * 5f), V(1.4f, 5f, 1.4f), M("GemPurple"));
        for (int i = 0; i < 5; i++) for (int j = 0; j < 3; j++) Box(g, "Floor tile", V(274f + i * 3f, 0.015f, z + 12f + j * 3f), V(2.8f, 0.03f, 2.8f), M((i + j) % 2 == 0 ? "PixelRoof" : "GemPurple"), false);
        DPrim(PrimitiveType.Sphere, "Leaf pile", g, V(266f, 0.2f, z + 22f), V(3f, 0.5f, 2.4f), M("FoliageRed"));
        var stars = new List<Vector3>(); for (int k = 0; k < 6; k++) stars.Add(V(268f + k * 5f, 1.2f, z + 20f + (k % 2) * 2f));
        Collectables(g, "Little stars", stars.ToArray(), (t, i) => { DPrim(PrimitiveType.Cube, "Star", t, Vector3.zero, V(0.35f, 0.35f, 0.08f), M("GlowYellow"), Quaternion.Euler(0, 0, 45)); return true; }, V(cx - 7f, 1.6f, z + 0.35f), Vector3.forward);
        Folk(g, "Riddle Rook", "#b39ddb", 9, V(288f, 0, z + 10f), 200f, "More/mo_pxr_riddle");
        Folk(g, "The Gatekeeper", "#90a4ae", 11, V(280f, 0, z + 24f), 180f, "More/mo_pxr_gate");
        Folk(g, "Pixel Postman Pip", "#ffcc80", 2, V(268f, 0, z + 6f), 150f, "More/mo_pxr_post");
        var spikes = Gk(g, "The spike tiles", V(280f, 0, z + 15f), 180f, "More/mo_pxr_spikes", "pop", PrimitiveType.Cube, V(2.6f, 0.3f, 2.6f), "Metal", 0.3f);
        Gk(g, "The glowing save star", V(292f, 0, z + 20f), 180f, "More/mo_pxr_star", "spin", PrimitiveType.Cube, V(0.5f, 0.5f, 0.1f), "GlowYellow");
        Gk(g, "The rock that won't move", V(270f, 0, z + 14f), 90f, "More/mo_pxr_rock", "shake", PrimitiveType.Sphere, V(1.2f, 0.9f, 1.1f), "Stone");
        report.Add("The Old Ruins (Pixel Hollow's new wing): purple pillars, a tile puzzle floor, a leaf pile and 6 little stars; 3 voiced people, 3 gags");
    }

    // ------------------------------------------------------------------------------------------------ the Test Annex

    static void WingAnnex(Transform root)
    {
        float cx = 338f, z = 64f;
        var g = Wing(root, "Test Annex", 320f, 356f, z, 22f, 6f, "LabFloor", "LabWhite", "LabWhite",
                     "THE OBSERVATION DECK", new Color(0.3f, 0.6f, 1f), "where the testers watch the tests. and the testers are tested. nobody watches us.", 344f);
        Box(g, "Observation window", V(cx, 2.6f, z + 21.85f), V(30f, 2.4f, 0.1f), M("Glass"), false);
        Box(g, "Console desk", V(cx, 0.5f, z + 18f), V(20f, 1f, 1.4f), M("LabGrey"));
        for (int k = 0; k < 6; k++) Box(g, "Monitor", V(cx - 8.5f + k * 3.4f, 1.4f, z + 18.4f), V(1.2f, 0.8f, 0.08f), M(k % 2 == 0 ? "DarkGlowCyan" : "PortalGreen"), false);
        Box(g, "Cube dispenser", V(324f, 4.5f, z + 8f), V(1.6f, 3f, 1.6f), M("LabWhite"));
        Folk(g, "Test Supervisor Sinew", "#e0e0e0", 9, V(cx - 4f, 0, z + 16.5f), 0f, "More/mo_tst_super");
        Folk(g, "Janitor Joint", "#a1887f", 7, V(350f, 0, z + 6f), 220f, "More/mo_tst_janitor");
        Folk(g, "Test Subject Thirty-Seven", "#ffab91", 16, V(cx - 10f, 0, z + 8f), 150f, "More/mo_tst_subject");
        Gk(g, "The cube dispenser", V(324f, 0, z + 8f), 90f, "More/mo_tst_cube", "drop", PrimitiveType.Cube, V(0.8f, 0.8f, 0.8f), "LabGrey", 3f);
        var arch = Gk(g, "The scanner arch", V(334f, 0, z + 10f), 180f, "More/mo_tst_scan", "slide", PrimitiveType.Cube, V(3f, 0.1f, 0.1f), "PortalPurple", 0f);
        arch.target.localPosition = V(0, 0.2f, 0); arch.moveBy = V(0, 2.6f, 0); arch.inTime = 1.2f;
        DPrim(PrimitiveType.Cube, "Arch post", g, V(332.4f, 1.5f, z + 10f), V(0.2f, 3f, 0.2f), M("LabGrey"));
        DPrim(PrimitiveType.Cube, "Arch post", g, V(335.6f, 1.5f, z + 10f), V(0.2f, 3f, 0.2f), M("LabGrey"));
        Gk(g, "The safety drone", V(348f, 0, z + 14f), 180f, "More/mo_tst_drone", "fly", PrimitiveType.Sphere, V(0.6f, 0.4f, 0.6f), "LabWhite", 2f).target.localPosition = V(0, 2f, 0);
        report.Add("The Observation Deck (the Test Annex's new wing): a long window over the tests, a console of monitors, a cube dispenser and a scanner arch; 3 voiced people, 3 gags");
    }

    // ------------------------------------------------------------------------------------------------ Blocklands

    static void WingBlocklands(Transform root)
    {
        float cx = 400f, z = 60f;
        var g = Wing(root, "Blocklands", 380f, 420f, z, 26f, 14f, "Stone", "Stone", "Stone",
                     "THE MINES", new Color(0.6f, 0.8f, 1f), "dig down. (don't dig straight down.)");
        for (int i = 0; i < 20; i++) for (int j = 0; j < 4; j++)
        {
            int ore = (i * 7 + j * 3) % 11;
            Box(g, "Rock block", V(381f + i * 2f, 1f + j * 2f, z + 25f), V(2f, 2f, 2f), M(ore == 0 ? "OreBlue" : ore == 5 ? "GlowGold" : (i + j) % 2 == 0 ? "Stone" : "Dirt"));
        }
        for (int k = 0; k < 10; k++) Box(g, "Rail", V(cx - 18f + k * 4f, 0.05f, z + 12f), V(4f, 0.1f, 1.2f), M("TrimDark"), false);
        for (int k = 0; k < 4; k++) DPrim(PrimitiveType.Cube, "Torch", g, V(384f + k * 10f, 2.5f, z + 23.9f), V(0.15f, 0.6f, 0.15f), M("GlowFlame"));
        var ores = new List<Vector3>(); for (int k = 0; k < 8; k++) ores.Add(V(384f + k * 4.4f, 0.5f, z + 18f));
        Collectables(g, "Ore chunks", ores.ToArray(), (t, i) => { DPrim(PrimitiveType.Cube, "Ore", t, Vector3.zero, Vector3.one * 0.5f, M(i % 2 == 0 ? "OreBlue" : "GlowGold")); return true; }, V(cx - 7f, 1.6f, z + 0.35f), Vector3.forward);
        Folk(g, "Miner Mandible", "#8d6e63", 7, V(390f, 0, z + 20f), 0f, "More/mo_blk_miner");
        Folk(g, "Trader Tarsal", "#a5d6a7", 9, V(410f, 0, z + 6f), 200f, "More/mo_blk_trader");
        Folk(g, "Blacksmith Brisket", "#616161", 11, V(386f, 0, z + 6f), 160f, "More/mo_blk_smith");
        var cart = Gk(g, "The minecart", V(cx - 16f, 0, z + 12f), 90f, "More/mo_blk_cart", "slide", PrimitiveType.Cube, V(1.2f, 0.8f, 1.6f), "Metal", 30f);
        cart.inTime = 4f;
        Gk(g, "The red block", V(404f, 0, z + 18f), 180f, "More/mo_blk_boom", "grow", PrimitiveType.Cube, V(1f, 1f, 1f), "GlowRed");
        Gk(g, "The shiny ore", V(396f, 0, z + 23.5f), 180f, "More/mo_blk_ore", "spin", PrimitiveType.Cube, V(0.6f, 0.6f, 0.6f), "OreBlue");
        report.Add("The Mines (Blocklands' new wing): a rock face of 80 blocks with ore, a minecart track, torches and 8 ore chunks; 3 voiced people, 3 gags");
    }

    // ------------------------------------------------------------------------------------------------ the Cold North

    static void WingNorth(Transform root)
    {
        float cx = 458f, z = 60f;
        // the mountains run the whole length of the north wall: tunnel through the one at x 467
        var ng = root.Find("The Cold North");
        if (ng != null)
            for (int i = ng.childCount - 1; i >= 0; i--)
            {
                var c = ng.GetChild(i);
                if ((c.name == "Mountain" || c.name == "Snowcap") && Mathf.Abs(c.localPosition.x - 467f) < 0.1f) Object.DestroyImmediate(c.gameObject);
            }
        var g = Wing(root, "The Cold North", 440f, 476f, z, 26f, 16f, "Stone", "TrimDark", "TrimDark",
                     "THE MEAD HALL", new Color(1f, 0.8f, 0.5f), "a long hall, a long fire, and a long story about a knee", 467f);
        if (ng != null)
        {
            Box(ng, "Tunnel wall W", V(464.6f, 2.5f, 55.5f), V(1.2f, 5f, 9f), M("Stone"));
            Box(ng, "Tunnel wall E", V(469.4f, 2.5f, 55.5f), V(1.2f, 5f, 9f), M("Stone"));
            Box(ng, "Tunnel roof", V(467f, 6f, 55.5f), V(6.2f, 2f, 9f), M("Stone"));
            Box(ng, "Tunnel snowcap", V(467f, 7.3f, 55.5f), V(5f, 0.6f, 7f), M("Snow"), false);
        }
        for (int k = 0; k < 2; k++) Box(g, "Long table", V(452f + k * 12f, 0.75f, z + 13f), V(2f, 0.1f, 16f), M("DeskWood"));
        Box(g, "Fire pit", V(458f, 0.3f, z + 13f), V(2f, 0.6f, 12f), M("Stone"));
        Box(g, "Fire", V(458f, 0.75f, z + 13f), V(1.4f, 0.4f, 11f), M("GlowFlame"), false);
        for (int k = 0; k < 8; k++) DPrim(PrimitiveType.Cylinder, "Round shield", g, V(440.1f + (k % 2) * 35.8f, 3f, z + 4f + (k / 2) * 5f), V(1.2f, 0.05f, 1.2f), M(k % 3 == 0 ? "GiftRed" : k % 3 == 1 ? "GiftBlue" : "GlowGold"), Quaternion.Euler(0, 0, 90));
        Box(g, "High seat", V(458f, 1f, z + 24f), V(3f, 2f, 1.4f), M("TrimDark"));
        Folk(g, "Bard Brannoc", "#ffcc80", 16, V(446f, 0, z + 20f), 150f, "More/mo_nth_bard");
        Folk(g, "Chieftain Grimtooth", "#8d6e63", 11, V(458f, 0, z + 22.5f), 180f, "More/mo_nth_chief");
        Folk(g, "Hall Guard Halvard", "#b0bec5", 15, V(470f, 0, z + 4f), 220f, "More/mo_nth_guard");
        Gk(g, "The roaring fire", V(458f, 0, z + 7f), 0f, "More/mo_nth_fire", "grow", PrimitiveType.Sphere, V(1f, 1.4f, 1f), "GlowFlame");
        Gk(g, "The tossed mug", V(452f, 0, z + 10f), 90f, "More/mo_nth_mug", "fly", PrimitiveType.Cylinder, V(0.25f, 0.3f, 0.25f), "DeskWood", 2f).target.localPosition = V(0, 0.85f, 0);
        Gk(g, "The shield wall", V(470f, 0, z + 14f), 270f, "More/mo_nth_shields", "pop", PrimitiveType.Cylinder, V(1.2f, 0.05f, 1.2f), "GiftRed", 1.2f);
        report.Add("The Mead Hall (the Cold North's new wing, through a tunnel in the mountain): long tables, a fire pit, shields on the walls and a high seat; 3 voiced people, 3 gags");
    }

    // ------------------------------------------------------------------------------------------------ the Mystery Hut

    static void WingMystery(Transform root)
    {
        float cx = 520f, z = 64f;
        var g = Wing(root, "The Mystery Hut", 500f, 540f, z, 26f, 12f, "PixelGreenDark", "NightSky", "NightSky",
                     "THE DEEP WOODS", new Color(0.6f, 1f, 0.6f), "past the hut, where the strange things live. bring a journal.", 522.5f);
        Scatter(g, "Boulder", 10, 503f, 537f, z + 4f, z + 24f, PrimitiveType.Sphere, V(1.6f, 1.2f, 1.6f), new[] { "Stone", "CaveRock" });
        Scatter(g, "Mushroom", 16, 503f, 537f, z + 4f, z + 24f, PrimitiveType.Sphere, V(0.5f, 0.3f, 0.5f), new[] { "GlowRed", "GlowYellow" }, 0.4f);
        Box(g, "Bunker hatch frame", V(530f, 0.05f, z + 16f), V(2.2f, 0.1f, 2.2f), M("Metal"), false);
        DPrim(PrimitiveType.Cylinder, "Journal pedestal", g, V(512f, 0.5f, z + 18f), V(0.6f, 0.5f, 0.6f), M("Stone"));
        var pages = new List<Vector3>(); for (int k = 0; k < 7; k++) pages.Add(V(504f + k * 5f, 1f, z + 22f - (k % 2) * 3f));
        Collectables(g, "Journal pages", pages.ToArray(), (t, i) => { DPrim(PrimitiveType.Cube, "Page", t, Vector3.zero, V(0.4f, 0.5f, 0.02f), M("Paper")); return true; }, V(cx - 7f, 1.6f, z + 0.35f), Vector3.forward);
        Folk(g, "Lumberjack Lars", "#e53935", 7, V(508f, 0, z + 8f), 150f, "More/mo_mys_lumber");
        Folk(g, "Cryptid Hunter Cass", "#7cb342", 15, V(528f, 0, z + 6f), 200f, "More/mo_mys_hunter");
        Folk(g, "The Conspiracy Theorist", "#9e9e9e", 20, V(534f, 0, z + 20f), 220f, "More/mo_mys_conspiracy");
        var hatch = Gk(g, "The bunker hatch", V(530f, 0, z + 16f), 0f, "More/mo_mys_hatch", "flip", PrimitiveType.Cube, V(2f, 0.08f, 2f), "Metal");
        hatch.target.localPosition = V(0, 0.08f, -1f); hatch.rotateBy = V(-110f, 0, 0);
        var eye = Gk(g, "The one-eyed thing", V(516f, 0, z + 24f), 180f, "More/mo_mys_eye", "pop", PrimitiveType.Sphere, V(1f, 1f, 1f), "GemWhite", 1.6f);
        Prim(PrimitiveType.Sphere, "Pupil", eye.target, V(0, 0.55f, 0.45f), Vector3.one * 0.35f, M("Black"));
        Gk(g, "The floating journal", V(512f, 0, z + 18f), 180f, "More/mo_mys_journal", "spin", PrimitiveType.Cube, V(0.6f, 0.1f, 0.45f), "GiftRed").target.localPosition = V(0, 1.4f, 0);
        report.Add("The Deep Woods (the Mystery Hut's new wing): boulders, glowing mushrooms, a bunker hatch, a journal pedestal and 7 journal pages; 3 voiced people, 3 gags");
    }
}
#endif
