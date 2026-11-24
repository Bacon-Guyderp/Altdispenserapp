#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// The new rooms, part 3: a cafe, and eight more original takes on a kind of game or show (a puffball's dreamy
/// hills, a brawling arena, a cartoon show eaten by a glitch, a rhythm battle stage, a pony meadow, four element
/// isles, a gorilla chase forest, a sunny crime-caper city), each with its own wing.
/// </summary>
public static partial class FirCheckBuilder
{
    /// <summary>A cartoon pony made of shapes (body, head, mane, tail, legs).</summary>
    static Transform Pony(Transform g, string name, Vector3 at, float yaw, string coat, string mane, bool horn = false, bool wings = false)
    {
        var p = Group(g, name, at);
        p.localRotation = Quaternion.Euler(0, yaw, 0);
        Prim(PrimitiveType.Capsule, "Body", p, V(0, 1.0f, 0), V(0.7f, 0.65f, 0.7f), M(coat)).transform.localRotation = Quaternion.Euler(90, 0, 0);
        Prim(PrimitiveType.Sphere, "Head", p, V(0, 1.6f, 0.65f), V(0.6f, 0.6f, 0.65f), M(coat));
        Prim(PrimitiveType.Sphere, "Snout", p, V(0, 1.48f, 0.95f), V(0.38f, 0.3f, 0.3f), M(coat));
        foreach (float ex in new[] { -0.15f, 0.15f }) Prim(PrimitiveType.Sphere, "Eye", p, V(ex, 1.68f, 0.93f), V(0.16f, 0.2f, 0.06f), M("Black"));
        Prim(PrimitiveType.Capsule, "Mane", p, V(0, 1.85f, 0.45f), V(0.3f, 0.45f, 0.3f), M(mane)).transform.localRotation = Quaternion.Euler(-40, 0, 0);
        Prim(PrimitiveType.Capsule, "Tail", p, V(0, 0.9f, -0.75f), V(0.25f, 0.5f, 0.25f), M(mane)).transform.localRotation = Quaternion.Euler(-30, 0, 0);
        foreach (float lx in new[] { -0.2f, 0.2f }) foreach (float lz in new[] { -0.35f, 0.35f }) Prim(PrimitiveType.Cylinder, "Leg", p, V(lx, 0.35f, lz), V(0.16f, 0.35f, 0.16f), M(coat));
        if (horn) Prim(PrimitiveType.Capsule, "Horn", p, V(0, 2.0f, 0.8f), V(0.08f, 0.2f, 0.08f), M("GlowGold")).transform.localRotation = Quaternion.Euler(30, 0, 0);
        if (wings) foreach (float wx in new[] { -1f, 1f }) Prim(PrimitiveType.Cube, "Wing", p, V(wx * 0.45f, 1.3f, 0), V(0.6f, 0.05f, 0.4f), M(coat)).transform.localRotation = Quaternion.Euler(0, 0, wx * -25f);
        return p;
    }

    // ================================================================================================ 20 the Bone Dry Cafe

    static void RoomCafe(Transform root)
    {
        var g = Room(root, "The Bone Dry Cafe", 4350f, 4390f, 56f, 6f, "Floor2", "BrickWall", "Ceiling",
                     "THE BONE DRY CAFE", new Color(0.85f, 0.6f, 0.35f), "coffee, cake and a quiet corner. we don't do decaf. we don't do anything without caffeine.", "CAFE");
        Box(g, "Counter", V(4381f, 0.55f, 44f), V(10f, 1.1f, 1.2f), M("DeskWood"));
        Box(g, "Espresso machine", V(4384f, 1.45f, 44f), V(1.4f, 0.7f, 0.7f), M("Metal"), false);
        Box(g, "Pastry case", V(4378f, 1.35f, 44f), V(2.4f, 0.5f, 0.9f), M("Glass"), false);
        for (int k = 0; k < 6; k++) Prim(PrimitiveType.Sphere, "Pastry", g, V(4377.2f + (k % 3) * 0.8f, 1.2f, 43.8f + (k / 3) * 0.35f), V(0.3f, 0.15f, 0.3f), M(k % 2 == 0 ? "Bun" : "GemPink"));
        Box(g, "Menu board", V(4389.9f, 3.2f, 44f), V(0.06f, 1.6f, 4f), M("Black"), false);
        Text("Menu", g, V(4389.85f, 3.2f, 44f), Face(Vector3.left), V2(3.8f, 1.5f), 0.2f, "<b>MENU</b>\nespresso . flat white . the marrow mocha\ncake of the day . a scone the size of your skull").color = Color.white;
        for (int k = 0; k < 6; k++)
        {
            var tp = V(4355f + (k % 2) * 7f, 0, 30f + (k / 2) * 8f);
            DPrim(PrimitiveType.Cylinder, "Table", g, tp + V(0, 0.38f, 0), V(1.2f, 0.04f, 1.2f), M("DeskWood"));
            DPrim(PrimitiveType.Cylinder, "Table leg", g, tp + V(0, 0.19f, 0), V(0.12f, 0.19f, 0.12f), M("Black"));
            foreach (float cz in new[] { -1f, 1f }) Box(g, "Chair", tp + V(0, 0.25f, cz), V(0.5f, 0.5f, 0.5f), M("Trunk"));
            Prim(PrimitiveType.Cylinder, "Cup", g, tp + V(0.2f, 0.47f, 0), V(0.12f, 0.06f, 0.12f), M("CaneWhite"));
        }
        Box(g, "Sofa", V(4386f, 0.35f, 30f), V(1f, 0.7f, 4f), M("Curtain"));
        for (int k = 0; k < 3; k++) PendantLamp(g, V(4376f + k * 4f, 6f, 44f), 1.4f);
        RF(g, "Barista Bex", "#ffccbc", 9, V(4381f, 0, 45.6f), 180f, "caf_barista");
        RF(g, "Poet Percival", "#b39ddb", 3, V(4358.5f, 0, 38f), 270f, "caf_poet");
        RF(g, "Regular Rhoda", "#c5e1a5", 16, V(4365f, 0, 46f), 270f, "caf_regular");
        var esp = RG(g, "The espresso machine", V(4385f, 0, 42.5f), 0f, "caf_espresso", "shake", PrimitiveType.Cube, V(0.3f, 0.3f, 0.3f), "Metal");
        esp.target.localPosition = V(0, 1.9f, 1.5f);
        RG(g, "The latte art", V(4372f, 0, 42f), 180f, "caf_latte", "spin", PrimitiveType.Cylinder, V(0.4f, 0.1f, 0.4f), "CaneWhite");
        RG(g, "The tip jar", V(4376f, 0, 42.5f), 180f, "caf_tips", "drop", PrimitiveType.Cylinder, V(0.3f, 0.4f, 0.3f), "Glass", 2f);
        report.Add("The Bone Dry Cafe: a counter with an espresso machine and a pastry case, six tables, a sofa, pendant lamps and a menu board; 3 voiced people, 3 gags");
    }

    // ================================================================================================ 21 the Dreamy Star Hills

    static void RoomStarHills(Transform root)
    {
        float cx = 4435f;
        var g = Room(root, "The Dreamy Star Hills", 4410f, 4460f, 70f, 20f, "PastelGrass", "PastelSky", "Sky",
                     "THE DREAMY STAR HILLS", new Color(1f, 0.85f, 0.3f), "round hills, round clouds, and a very round hero who can swallow almost anything", "STAR HILLS");
        foreach (var hp in new[] { V(4416f, 0, 64f), V(4454f, 0, 62f) }) DPrim(PrimitiveType.Sphere, "Round hill", g, hp, V(14f, 9f, 10f), RM("PastelHill", 0.45f, 0.78f, 0.45f));
        for (int k = 0; k < 8; k++) DPrim(PrimitiveType.Sphere, "Round cloud", g, V(4414f + k * 6f, 12f + (k % 3), 60f + (k % 2) * 4f), V(4f, 2.4f, 3f), M("CaneWhite"));
        for (int k = 0; k < 5; k++) Prim(PrimitiveType.Sphere, "Star", g, V(4414f + k * 10f, 8f + (k % 2) * 2f, 66f), V(0.8f, 0.8f, 0.2f), M("GlowYellow"));
        DPrim(PrimitiveType.Cylinder, "Warp pad", g, V(4450f, 0.05f, 38f), V(3f, 0.05f, 3f), M("GlowGold"));
        Box(g, "Castle", V(4418f, 3f, 48f), V(6f, 6f, 6f), M("GiftBlue"));
        foreach (float tz in new[] { 45f, 51f }) DPrim(PrimitiveType.Cylinder, "Castle tower", g, V(4418f, 4f, tz), V(1.6f, 4f, 1.6f), M("GiftBlue"));
        RF(g, "Pom the Puffball", "#b3e5fc", 21, V(cx, 0, 40f), 180f, "kir_pom", 0.7f);
        if (rigs.TryGetValue("Pom the Puffball", out var pr)) Prim(PrimitiveType.Sphere, "Puff", pr[1], V(0, 0.05f, 0), V(0.75f, 0.75f, 0.7f), M("GiftBlue"));
        RF(g, "Chef Brulee", "#fff3e0", 16, V(4448f, 0, 48f), 200f, "kir_chef");
        Hat("Chef Brulee", PrimitiveType.Cylinder, V(0, 0.2f, 0), V(0.25f, 0.2f, 0.25f), "CaneWhite");
        RF(g, "Bumble the Round Guard", "#ffb74d", 20, V(4424f, 0, 40f), 160f, "kir_guard", 0.8f);
        var star = RG(g, "The warp star", V(4450f, 0, 41f), 180f, "kir_warp", "fly", PrimitiveType.Cube, V(1.2f, 1.2f, 0.25f), "GlowYellow", 6f);
        star.target.localRotation = Quaternion.Euler(0, 0, 45);
        RG(g, "The big inhale", V(4430f, 0, 46f), 180f, "kir_inhale", "grow", PrimitiveType.Sphere, V(1.2f, 1.2f, 1.2f), "GiftBlue");
        var cake = RG(g, "The strawberry cake", V(4442f, 0, 34f), 180f, "kir_cake", "pop", PrimitiveType.Cylinder, V(1f, 0.6f, 1f), "CaneWhite", 1.2f);
        Prim(PrimitiveType.Sphere, "Strawberry", cake.target, V(0, 0.7f, 0), Vector3.one * 0.25f, M("GlowRed"));
        report.Add("The Dreamy Star Hills: round pastel hills, clouds and stars, a little castle, a warp pad, and Pom the Puffball; 3 voiced people, 3 gags");
    }

    // ================================================================================================ 22 the Brawl Arena

    static void RoomBrawl(Transform root)
    {
        float cx = 4505f;
        var g = Room(root, "The Brawl Arena", 4480f, 4530f, 70f, 25f, "Stone", "NightSky", "NightSky",
                     "THE BRAWL ARENA", new Color(1f, 0.4f, 0.2f), "every fighter from every world, one floating stage, and a lot of items falling from the sky", "BRAWL ARENA");
        Box(g, "Main stage", V(cx, 1f, 54f), V(24f, 2f, 8f), M("LabGrey"));
        Box(g, "Stage edge", V(cx, 2.02f, 54f), V(24.2f, 0.04f, 8.2f), M("GlowGold"), false);
        for (int k = 0; k < 4; k++) Box(g, "Stage steps", V(cx - 8f + k * 0.01f, 0.25f + k * 0.5f, 49.5f - k * 0.01f), V(3f, 0.5f, 1f), M("LabGrey"));
        foreach (var pp in new[] { V(cx - 7f, 5.5f, 54f), V(cx + 7f, 5.5f, 54f), V(cx, 8.5f, 54f) }) Box(g, "Floating platform", pp, V(5f, 0.3f, 2f), M("GiftBlue"));
        for (int r = 0; r < 3; r++) foreach (float side in new[] { -1f, 1f }) Box(g, "Stands", V(cx + side * 20f, 0.5f + r, 40f + r * 0.01f), V(6f, 1f + r * 2f, 14f - r * 4f), M("Seat"));
        Text("BlastZone", g, V(cx, 15f, 69.85f), Face(Vector3.back), V2(30f, 1.4f), 1f, "<b>3 . 2 . 1 . BRAWL!</b>").color = new Color(1f, 0.5f, 0.2f);
        RF(g, "Announcer Boomer", "#90a4ae", 18, V(4486f, 0, 30f), 120f, "brw_announcer");
        RF(g, "Brawler Bruno", "#ef5350", 10, V(cx - 4f, 2f, 54f), 90f, "brw_bruno");
        RF(g, "Brawler Kiki", "#4fc3f7", 21, V(cx + 4f, 2f, 54f), 270f, "brw_kiki");
        RF(g, "Trophy Keeper Tova", "#ffe082", 9, V(4524f, 0, 30f), 240f, "brw_tova");
        var orb = RG(g, "The shine orb", V(cx, 0, 44f), 180f, "brw_orb", "spin", PrimitiveType.Sphere, V(0.8f, 0.8f, 0.8f), "GlowGold");
        orb.target.localPosition = V(0, 1.8f, 0);
        RG(g, "The giant hammer", V(4494f, 0, 40f), 180f, "brw_hammer", "shake", PrimitiveType.Cube, V(0.8f, 0.6f, 0.5f), "GlowRed").target.localPosition = V(0, 1.4f, 0);
        RG(g, "The home-run bat", V(4516f, 0, 40f), 180f, "brw_bat", "flip", PrimitiveType.Capsule, V(0.2f, 0.9f, 0.2f), "DeskWood");
        report.Add("The Brawl Arena: a floating fighting stage with three platforms, stands on both sides, and a countdown in the sky; 4 voiced people (two brawlers on the stage), 3 gags (the shine orb, a giant hammer, a home-run bat)");
    }

    // ================================================================================================ 23 the Glitch Show

    static void RoomGlitch(Transform root)
    {
        float cx = 4575f;
        var g = Room(root, "The Glitch Show", 4550f, 4600f, 70f, 10f, "Rug", "GiftBlue", "Ceiling",
                     "THE GLITCH SHOW", new Color(0.2f, 1f, 1f), "a happy little cartoon show. something is eating it from the edges.", "GLITCH SHOW");
        Box(g, "Cartoon house", V(4562f, 2f, 56f), V(8f, 4f, 6f), M("GlowYellow"));
        Box(g, "Cartoon roof", V(4562f, 4.4f, 56f), V(9f, 0.8f, 7f), M("CaneRed"), false);
        for (int k = 0; k < 4; k++) DPrim(PrimitiveType.Sphere, "Cartoon cloud", g, V(4556f + k * 12f, 7.6f, 68f), V(4f, 2f, 1f), M("CaneWhite"));
        DPrim(PrimitiveType.Sphere, "Cartoon sun", g, V(4594f, 7.8f, 69.5f), V(2.4f, 2.4f, 0.3f), M("GlowYellow"));
        string[] glitch = { "Black", "DarkGlowCyan", "DarkGlowPink" };
        for (int k = 0; k < 40; k++)
        {
            float fx = Mathf.Repeat(k * 0.618f, 1f), fz = Mathf.Repeat(k * 0.382f + 0.05f, 1f);
            var gp = V(4586f + fx * 13f, 0.2f + Mathf.Repeat(k * 0.27f, 1f) * 6f, 34f + fz * 34f);
            DPrim(PrimitiveType.Cube, "Glitch block", g, gp, Vector3.one * (0.3f + Mathf.Repeat(k * 0.13f, 1f) * 0.8f), M(glitch[k % 3]), Quaternion.Euler(0, k * 23f, 0));
        }
        Box(g, "Corrupted wall", V(4599.9f, 5f, 50f), V(0.05f, 10f, 30f), M("DarkGlowPink"), false);
        RF(g, "Little Lumi", "#ffe0b2", 21, V(4570f, 0, 42f), 160f, "gli_lumi", 0.75f);
        if (rigs.TryGetValue("Little Lumi", out var lr)) Prim(PrimitiveType.Capsule, "Sock puppet", lr[6], V(0, -0.15f, 0.05f), V(0.12f, 0.2f, 0.12f), M("GlowOrange"));
        RF(g, "Show Host Hoppy", "#81d4fa", 18, V(4562f, 0, 50f), 180f, "gli_host");
        RF(g, "Cartoon Cat Cleo", "#ce93d8", 11, V(4584f, 0, 46f), 220f, "gli_cleo");
        RG(g, "The glitch", V(4590f, 0, 40f), 220f, "gli_glitch", "pop", PrimitiveType.Capsule, V(0.8f, 2f, 0.8f), "DarkGlowPink", 2.2f);
        RG(g, "The rescue hug", V(4566f, 0, 36f), 180f, "gli_hug", "grow", PrimitiveType.Sphere, V(0.8f, 0.8f, 0.8f), "GlowOrange");
        var tv = RG(g, "The broken TV", V(4578f, 0, 58f), 180f, "gli_tv", "shake", PrimitiveType.Cube, V(1.6f, 1.1f, 0.6f), "Black");
        tv.target.localPosition = V(0, 0.6f, 0);
        Prim(PrimitiveType.Cube, "Static", tv.target, V(0, 0.55f, 0.31f), V(1.4f, 0.9f, 0.02f), M("Mist"));
        report.Add("The Glitch Show: a bright cartoon set (a little house, clouds, a smiling sun) being eaten from the east by glitch blocks; a small hero and her sock puppet; 3 voiced people, 3 gags");
    }

    // ================================================================================================ 24 the Rhythm Battle Stage

    static void RoomRhythm(Transform root)
    {
        float cx = 4640f;
        var g = Room(root, "The Rhythm Battle Stage", 4620f, 4660f, 60f, 10f, "Black", "GemPurple", "Black",
                     "THE RHYTHM BATTLE STAGE", new Color(1f, 0.3f, 0.8f), "hit the arrows on the beat. win the battle. get the date.", "RHYTHM STAGE");
        Box(g, "Stage", V(cx, 0.4f, 50f), V(20f, 0.8f, 8f), M("Stage"));
        foreach (float sx in new[] { -11.5f, 11.5f }) { Box(g, "Big speaker", V(cx + sx, 1.6f, 52f), V(2.4f, 3.2f, 2f), M("Black")); DPrim(PrimitiveType.Cylinder, "Woofer", g, V(cx + sx, 1.2f, 50.95f), V(1.6f, 0.05f, 1.6f), M("LabGrey"), Quaternion.Euler(90, 0, 0)); }
        string[] arrowCol = { "GemPurple", "GiftBlue", "GlowGreen", "GlowRed" };
        float[] arrowRot = { 90f, 180f, 0f, -90f };
        for (int k = 0; k < 4; k++) DPrim(PrimitiveType.Cube, "Arrow", g, V(cx - 4.5f + k * 3f, 6.5f, 59.85f), V(1.6f, 1.6f, 0.1f), M(arrowCol[k]), Quaternion.Euler(0, 0, 45f + arrowRot[k]));
        DPrim(PrimitiveType.Cylinder, "Mic stand", g, V(cx - 3f, 1.6f, 50f), V(0.05f, 0.8f, 0.05f), M("Metal"));
        for (int k = 0; k < 10; k++) Prim(PrimitiveType.Sphere, "Stage light", g, V(4622f + k * 4f, 9.6f, 46f), Vector3.one * 0.35f, M(arrowCol[k % 4]));
        RF(g, "Little Bop", "#80deea", 21, V(cx - 4f, 0.8f, 50f), 180f, "rhy_bop", 0.8f);
        Hat("Little Bop", PrimitiveType.Cylinder, V(0, 0.12f, 0.03f), V(0.32f, 0.06f, 0.36f), "GiftBlue");
        RF(g, "Big Dad Dynamo", "#9575cd", 18, V(cx + 4f, 0.8f, 50f), 180f, "rhy_dad", 1.2f);
        RF(g, "Speaker Sitter Lola", "#f48fb1", 9, V(cx - 11.5f, 3.2f, 52f), 160f, "rhy_lola");
        RG(g, "The arrow board", V(cx, 0, 42f), 180f, "rhy_arrows", "shake", PrimitiveType.Cube, V(3f, 0.8f, 0.1f), "GemPurple").target.localPosition = V(0, 1.2f, 0);
        RG(g, "The mic drop", V(4628f, 0, 40f), 180f, "rhy_mic", "drop", PrimitiveType.Capsule, V(0.12f, 0.25f, 0.12f), "Metal", 1.8f);
        RG(g, "The bass speaker", V(4652f, 0, 40f), 180f, "rhy_bass", "grow", PrimitiveType.Cube, V(1f, 1.2f, 0.8f), "Black");
        report.Add("The Rhythm Battle Stage: a stage between two giant speakers, four arrows on the wall, coloured lights, a little rapper versus a rock-star dad; 3 voiced people, 3 gags");
    }

    // ================================================================================================ 25 the Pony Meadow

    static void RoomPonies(Transform root)
    {
        float cx = 4705f;
        RM("PonyYellow", 1f, 0.9f, 0.5f); RM("PonyBlue", 0.55f, 0.75f, 1f); RM("PonyLilac", 0.8f, 0.7f, 1f); RM("PonyMint", 0.6f, 1f, 0.8f);
        var g = Room(root, "The Pony Meadow", 4680f, 4730f, 70f, 20f, "PastelGrass", "PastelSky", "Sky",
                     "THE PONY MEADOW", new Color(1f, 0.55f, 0.85f), "a little town of talking ponies. everybody's friends. mostly. there was the cupcake incident.", "PONY MEADOW");
        DPrim(PrimitiveType.Cylinder, "Library tree", g, V(4690f, 3f, 58f), V(4f, 3f, 4f), M("Trunk"));
        DPrim(PrimitiveType.Sphere, "Library tree leaves", g, V(4690f, 8f, 58f), V(9f, 6f, 9f), M("GiftGreen"));
        Box(g, "Library door", V(4690f, 1f, 55.95f), V(1f, 2f, 0.1f), M("DoorWood"), false);
        for (int k = 0; k < 5; k++) DPrim(PrimitiveType.Sphere, "Cloud house", g, V(4720f + Mathf.Sin(k) * 2f, 11f + (k % 2), 58f + Mathf.Cos(k) * 2f), V(4f, 2.4f, 3f), M("CaneWhite"));
        string[] rb = { "GlowRed", "GlowOrange", "GlowYellow", "GlowGreen", "GiftBlue", "GemPurple" };
        for (int b = 0; b < rb.Length; b++) Box(g, "Rainbow stripe", V(4705f, 0.02f + b * 0.001f, 64f - b * 0.6f), V(24f, 0.02f, 0.6f), M(rb[b]), false);
        Scatter(g, "Flower", 30, 4683f, 4727f, 26f, 54f, PrimitiveType.Sphere, V(0.3f, 0.3f, 0.3f), new[] { "GemPink", "GlowYellow", "PonyLilac" }, 0.2f);
        Pony(g, "Grazing pony", V(4724f, 0, 32f), -60f, "PonyMint", "GiftBlue");
        RF(g, "Meadow Keeper Posy", "#f8bbd0", 9, V(4700f, 0, 46f), 180f, "pon_posy");
        RF(g, "Cloud Weaver Wren", "#e1f5fe", 7, V(4716f, 0, 50f), 220f, "pon_wren");
        RF(g, "Apple Farmer Abner", "#ffcc80", 16, V(4686f, 0, 40f), 120f, "pon_abner");
        Hat("Apple Farmer Abner", PrimitiveType.Cylinder, V(0, 0.12f, 0), V(0.42f, 0.05f, 0.42f), "CornYellow");
        var sunny = RG(g, "Sunny Petal the pony", V(4696f, 0, 34f), 180f, "pon_sunny", "shake", PrimitiveType.Sphere, V(0.05f, 0.05f, 0.05f), "Black");
        Pony(sunny.target, "Sunny Petal", Vector3.zero, 0f, "PonyYellow", "GlowOrange", false, true);
        var quill = RG(g, "Midnight Quill the pony", V(4712f, 0, 38f), 180f, "pon_quill", "spin", PrimitiveType.Sphere, V(0.05f, 0.05f, 0.05f), "Black");
        quill.target.localPosition = Vector3.zero;
        Pony(quill.target, "Midnight Quill", Vector3.zero, 0f, "PonyLilac", "GemPurple", true);
        var biscuit = RG(g, "Biscuit Breeze the pony", V(4704f, 0, 54f), 90f, "pon_biscuit", "slide", PrimitiveType.Sphere, V(0.05f, 0.05f, 0.05f), "Black", 3f);
        Pony(biscuit.target, "Biscuit Breeze", Vector3.zero, 0f, "PonyBlue", "CaneWhite");
        report.Add("The Pony Meadow: a library in a tree, a house on a cloud, a rainbow path, flowers, and three talking ponies (Sunny Petal, Midnight Quill, Biscuit Breeze); 3 voiced people, 3 gags");
    }

    // ================================================================================================ 26 the Four Element Isles

    static void RoomElements(Transform root)
    {
        float cx = 4780f;
        var g = Room(root, "The Four Element Isles", 4750f, 4810f, 80f, 25f, "PixelGreen", "PixelSky", "Sky",
                     "THE FOUR ELEMENT ISLES", new Color(0.9f, 0.75f, 0.3f), "water, earth, fire and air: four islands, four schools, one very long walk between them", "ELEMENT ISLES");
        // water (north-west): a pool with ice
        Box(g, "Water isle", V(4762f, 0.02f, 66f), V(18f, 0.04f, 14f), M("PoolBlue"), false);
        for (int k = 0; k < 4; k++) DPrim(PrimitiveType.Cube, "Ice", g, V(4756f + k * 4f, 0.6f, 70f + (k % 2) * 3f), V(1.6f, 1.2f, 1.6f), M("Glass"), Quaternion.Euler(0, k * 20f, 0));
        // earth (south-west): stone pillars
        for (int k = 0; k < 6; k++) Box(g, "Earth pillar", V(4756f + (k % 3) * 4f, 1f + (k % 2), 36f + (k / 3) * 6f), V(1.6f, 2f + 2f * (k % 2), 1.6f), M("Stone"));
        // fire (north-east): a red pagoda and braziers
        Box(g, "Fire pagoda", V(4798f, 3f, 68f), V(10f, 6f, 8f), M("CaneRed"));
        Box(g, "Fire pagoda roof", V(4798f, 6.4f, 68f), V(12f, 0.8f, 10f), M("Black"), false);
        foreach (float bx in new[] { 4792f, 4804f }) { DPrim(PrimitiveType.Cylinder, "Brazier", g, V(bx, 0.6f, 62f), V(0.8f, 0.6f, 0.8f), M("Metal")); Prim(PrimitiveType.Sphere, "Flame", g, V(bx, 1.5f, 62f), V(0.7f, 0.9f, 0.7f), M("GlowFlame")); }
        // air (south-east): a white temple on a rock, chimes
        Box(g, "Air rock", V(4800f, 1.5f, 38f), V(10f, 3f, 8f), M("Stone"));
        Box(g, "Air temple", V(4800f, 5f, 38f), V(6f, 4f, 5f), M("CaneWhite"), false);
        DPrim(PrimitiveType.Cylinder, "Air spire", g, V(4800f, 9f, 38f), V(1.5f, 2f, 1.5f), M("GiftBlue"));
        for (int k = 0; k < 6; k++) DPrim(PrimitiveType.Cylinder, "Wind chime", g, V(4793f + k * 0.4f, 2.2f, 33.8f), V(0.06f, 0.4f, 0.06f), M("Brass"));
        Text("Isles", g, V(cx, 1.6f, 52f), Quaternion.Euler(0, 180, 0), V2(8f, 1f), 0.25f, "<b>WATER</b>  north-west   <b>EARTH</b>  south-west\n<b>FIRE</b>  north-east   <b>AIR</b>  south-east").color = Color.white;
        RF(g, "Water Master Nalu", "#4fc3f7", 9, V(4766f, 0, 58f), 160f, "ele_water");
        RF(g, "Earth Master Bodo", "#8d6e63", 10, V(4766f, 0, 40f), 120f, "ele_earth");
        RF(g, "Fire Master Ember", "#ff7043", 16, V(4794f, 0, 58f), 200f, "ele_fire");
        RF(g, "Air Monk Soren", "#ffcc80", 3, V(4792f, 0, 46f), 240f, "ele_air");
        if (rigs.TryGetValue("Air Monk Soren", out var ar)) Prim(PrimitiveType.Cube, "Arrow mark", ar[2], V(0, 0.15f, 0.02f), V(0.05f, 0.02f, 0.22f), M("GiftBlue"));
        RG(g, "The water whip", V(4770f, 0, 64f), 180f, "ele_whip", "spin", PrimitiveType.Capsule, V(0.3f, 1.2f, 0.3f), "PoolBlue");
        RG(g, "The earth pillar", V(4772f, 0, 36f), 180f, "ele_pillar", "pop", PrimitiveType.Cube, V(1.2f, 2.4f, 1.2f), "Stone", 2.4f);
        RG(g, "The fire puff", V(4788f, 0, 64f), 180f, "ele_puff", "grow", PrimitiveType.Sphere, V(0.8f, 0.8f, 0.8f), "GlowFlame").target.localPosition = V(0, 1.2f, 0);
        RG(g, "The air ball", V(4788f, 0, 34f), 180f, "ele_airball", "fly", PrimitiveType.Sphere, V(1f, 1f, 1f), "Mist", 5f);
        report.Add("The Four Element Isles: a water isle with ice, an earth isle of stone pillars, a fire pagoda with braziers, an air temple on a rock with wind chimes; 4 element masters, 4 gags");
    }

    // ================================================================================================ 27 the Gorilla Chase Forest

    static void RoomGorilla(Transform root)
    {
        float cx = 4855f;
        var g = Room(root, "The Gorilla Chase Forest", 4830f, 4880f, 70f, 25f, "Dirt", "PixelGreenDark", "Sky",
                     "THE GORILLA CHASE FOREST", new Color(1f, 0.45f, 0.1f), "no legs, just arms. run on your hands. don't get tagged. if you're tagged, you're lava.", "GORILLA FOREST");
        for (int k = 0; k < 8; k++)
        {
            var tp = V(4836f + (k % 4) * 12.5f, 0, 36f + (k / 4) * 20f + (k % 2) * 3f);
            if (Mathf.Abs(tp.x - cx) < 4f && tp.z < 40f) tp.x += 6f;
            DPrim(PrimitiveType.Cylinder, "Big trunk", g, tp + V(0, 9f, 0), V(2f, 9f, 2f), M("Trunk"));
            DPrim(PrimitiveType.Sphere, "Canopy", g, tp + V(0, 19f, 0), V(10f, 5f, 10f), M("PixelGreenDark"));
            Box(g, "Tree platform", tp + V(1.6f, 4f + (k % 3) * 3f, 0), V(3f, 0.3f, 3f), M("DeskWood"));
        }
        Box(g, "Stump hub", V(cx, 0.5f, 48f), V(5f, 1f, 5f), M("Trunk"));
        Text("TagBoard", g, V(cx, 2.4f, 45.4f), Face(Vector3.back), V2(4.5f, 1.2f), 0.3f, "<b>TAG RULES</b>\nlava tags you. you become lava. run.").color = new Color(1f, 0.6f, 0.2f);
        Box(g, "Tag board", V(cx, 2.4f, 45.45f), V(4.8f, 1.4f, 0.05f), M("Black"), false);
        RF(g, "Gorilla Grunt", "#5d4037", 19, V(4846f, 0, 44f), 120f, "gor_grunt");
        RF(g, "Gorilla Bongo", "#4e342e", 21, V(4866f, 0, 52f), 220f, "gor_bongo");
        RF(g, "Lava Gorilla Blaze", "#ff7043", 10, V(4870f, 0, 40f), 220f, "gor_blaze");
        foreach (var who in new[] { "Gorilla Grunt", "Gorilla Bongo", "Lava Gorilla Blaze" })
            if (rigs.TryGetValue(who, out var r)) { Prim(PrimitiveType.Sphere, "Gorilla head", r[2], V(0, 0, 0.02f), V(0.36f, 0.32f, 0.34f), M(who.StartsWith("Lava") ? "GlowOrange" : "TrimDark")); }
        RG(g, "The lava tag", V(4850f, 0, 36f), 180f, "gor_tag", "grow", PrimitiveType.Sphere, V(0.8f, 0.8f, 0.8f), "GlowFlame").target.localPosition = V(0, 1f, 0);
        RG(g, "The tree climb", V(4862f, 0, 60f), 180f, "gor_climb", "fly", PrimitiveType.Capsule, V(0.6f, 1.2f, 0.6f), "TrimDark", 6f);
        RG(g, "The banana", V(4840f, 0, 56f), 180f, "gor_banana", "spin", PrimitiveType.Capsule, V(0.15f, 0.4f, 0.15f), "GlowYellow");
        report.Add("The Gorilla Chase Forest: eight giant trees with platforms, a stump hub with the tag rules, two gorillas and a lava gorilla; 3 voiced gorillas, 3 gags");
    }

    // ================================================================================================ 28 Sunny Bay City

    static void RoomSunnyBay(Transform root)
    {
        float cx = 4930f;
        var g = Room(root, "Sunny Bay City", 4900f, 4960f, 80f, 25f, "Concrete", "PixelSky", "Sky",
                     "SUNNY BAY CITY", new Color(0.3f, 0.85f, 0.4f), "palm trees, sports cars, a bank that's asking for it, and the police are already on their way", "SUNNY BAY CITY");
        Box(g, "Boulevard", V(cx, 0.01f, 40f), V(60f, 0.02f, 8f), M("TurfGrey"), false);
        for (int k = 0; k < 10; k++) Box(g, "Lane line", V(4903f + k * 6f, 0.025f, 40f), V(2.4f, 0.01f, 0.15f), M("GlowYellow"), false);
        for (int k = 0; k < 6; k++) Palm(g, V(4904f + k * 10f + (k >= 3 ? 2f : 0f), 0, 35f), 8f, k);
        House(g, "The bank", V(4910f, 0, 58f), V(14f, 8f, 10f), "LabWhite", "TrimDark", "SUNNY BAY SAVINGS & LOAN", Vector3.back);
        House(g, "The car dealership", V(4950f, 0, 58f), V(14f, 5f, 10f), "Glass", "CaneWhite", "SUNNY BAY MOTORS", Vector3.back);
        House(g, "The corner store", V(4908f, 0, 26.5f), V(10f, 4f, 5f), "CaneRed", "TrimDark", "24/7", Vector3.forward);
        string[] carCol = { "GlowRed", "GlowYellow", "Black", "GiftBlue" };
        for (int k = 0; k < 4; k++) { Box(g, "Sports car", V(4946f + (k % 2) * 6f, 0.5f, 48f + (k / 2) * 3f), V(4.4f, 0.8f, 2f), M(carCol[k]), false); Box(g, "Car cabin", V(4946f + (k % 2) * 6f, 1.1f, 48f + (k / 2) * 3f), V(2f, 0.5f, 1.8f), M("Glass"), false); }
        Box(g, "Police car", V(4924f, 0.6f, 44f), V(4.6f, 1f, 2f), M("CaneWhite"), false);
        Box(g, "Siren", V(4924f, 1.2f, 44f), V(1f, 0.2f, 0.4f), M("GlowRed"), false);
        foreach (float hx in new[] { -16.5f, 16.5f }) Box(g, "Hill", V(cx + hx, 6f, 79f), V(26f, 12f, 1.5f), M("PixelGreenDark"), false);
        Text("HillSign", g, V(cx, 9f, 78.2f), Face(Vector3.back), V2(40f, 3f), 2.4f, "<b>SUNNY BAY</b>").color = Color.white;
        RF(g, "Car Dealer Duke", "#ffcc80", 16, V(4942f, 0, 52f), 200f, "gta_dealer");
        RF(g, "Retired Robber Reggie", "#b0bec5", 19, V(4916f, 0, 50f), 160f, "gta_reggie");
        RF(g, "Hiker Hamish", "#a5d6a7", 7, V(4954f, 0, 30f), 220f, "gta_hiker");
        RF(g, "Officer Sully", "#90caf9", 20, V(4921f, 0, 46.5f), 180f, "gta_cop");
        Hat("Officer Sully", PrimitiveType.Cylinder, V(0, 0.12f, 0), V(0.3f, 0.07f, 0.3f), "PoliceBlue");
        var stars = RG(g, "The wanted stars", V(cx + 4f, 0, 50f), 180f, "gta_stars", "spin", PrimitiveType.Cube, V(0.5f, 0.5f, 0.1f), "GlowYellow");
        stars.target.localPosition = V(0, 3f, 0);
        for (int k = 1; k < 5; k++) Prim(PrimitiveType.Cube, "Star", stars.target, V(-1.2f + k * 0.6f - 0.6f, 0.25f, 0), V(0.5f, 0.5f, 0.1f), M("GlowYellow")).transform.localRotation = Quaternion.Euler(0, 0, 45);
        RG(g, "The getaway car", V(4936f, 0, 30f), 90f, "gta_getaway", "slide", PrimitiveType.Cube, V(2f, 0.9f, 4.4f), "GlowRed", 8f);
        RG(g, "The bank vault", V(4910f, 0, 51.5f), 180f, "gta_vault", "flip", PrimitiveType.Cylinder, V(1.6f, 0.15f, 1.6f), "Metal").target.localPosition = V(0, 1.2f, 0);
        report.Add("Sunny Bay City: a palm-lined boulevard, the bank, a car dealership with four sports cars, the corner store, a police car and a big sign on the hill; 4 voiced people, 3 gags (the wanted stars, the getaway car, the bank vault)");
    }

    // ================================================================================================ their wings

    static void RoomWings3(Transform root)
    {
        RoomWing(root, "The Bone Dry Cafe", 4350f, 4390f, 56f, 22f, 6f, "Concrete", "BrickWall", "Ceiling", "THE ROASTERY", new Color(0.85f, 0.6f, 0.35f),
                 "where the beans are roasted. it smells wonderful. it is also forty degrees.", float.NaN,
                 new[] { "Roaster Rafa|#a1887f|16|rcfw_rafa", "Bean Taster Tamsin|#ffccbc|9|rcfw_tamsin", "Delivery Cyclist Cy|#80cbc4|21|rcfw_cy" },
                 new[] { "The roasting drum|rcfw_drum|spin|cyl|1.4,0.8,1.4|Metal|2|0.5", "The bean sack|rcfw_sack|drop|cap|0.8,1,0.8|Cardboard|2", "The grinder|rcfw_grinder|shake|cube|0.6,1,0.6|Black|2" },
                 (g, cx, z) => { for (int k = 0; k < 8; k++) DPrim(PrimitiveType.Capsule, "Coffee sack", g, V(cx - 16f + k * 1.2f, 0.5f, z + 21f), V(0.9f, 0.5f, 0.7f), M("Cardboard")); },
                 "sacks of beans and the roasting drum");
        RoomWing(root, "The Dreamy Star Hills", 4410f, 4460f, 70f, 24f, 18f, "CaneWhite", "PastelSky", "Sky", "THE CLOUD KITCHEN", new Color(1f, 0.85f, 0.3f),
                 "the biggest cakes in the hills, baked on a cloud. the round hero has been banned twice.", float.NaN,
                 new[] { "Baker Birdie|#fff59d|9|rkiw_birdie", "Taste Tester Tumble|#ffab91|21|rkiw_tumble", "Cloud Waiter Wisp|#e1f5fe|20|rkiw_wisp" },
                 new[] { "The giant cake|rkiw_cake|grow|cyl|2,1.2,2|GemPink|2", "The flying pie|rkiw_pie|fly|cyl|0.8,0.15,0.8|Bun|4|1", "The cookie tower|rkiw_cookies|shake|cyl|0.6,1.6,0.6|Trunk|2" },
                 (g, cx, z) => { for (int k = 0; k < 3; k++) DPrim(PrimitiveType.Cylinder, "Cloud oven", g, V(cx - 15f + k * 15f, 1.2f, z + 22f), V(3f, 1.2f, 3f), M("LabWhite")); },
                 "three cloud ovens");
        RoomWing(root, "The Brawl Arena", 4480f, 4530f, 70f, 24f, 12f, "Floor2", "NightSky", "NightSky", "THE TROPHY HALL", new Color(1f, 0.4f, 0.2f),
                 "every fighter who ever brawled, frozen as a little gold statue. some of them are still a bit cross.", float.NaN,
                 new[] { "Collector Cosmo|#ce93d8|3|rbrw_cosmo", "Stagehand Stu|#90a4ae|20|rbrw_stu", "Retired Champ Rex|#ffcc80|10|rbrw_rex" },
                 new[] { "The golden trophy|rbrw_trophy|spin|cap|0.6,1,0.6|GlowGold|2|1", "The assist capsule|rbrw_capsule|pop|sph|0.6,0.6,0.6|GlowRed|1.4", "The sandbag|rbrw_sandbag|shake|cap|0.9,1.6,0.9|Cardboard|2" },
                 (g, cx, z) => { for (int k = 0; k < 8; k++) { var p = V(cx - 21f + k * 6f, 0, z + 22f); DPrim(PrimitiveType.Cylinder, "Pedestal", g, p + V(0, 0.5f, 0), V(1f, 0.5f, 1f), M("Stone")); DPrim(PrimitiveType.Capsule, "Trophy figure", g, p + V(0, 1.6f, 0), V(0.4f, 0.6f, 0.4f), M("GlowGold")); } },
                 "eight trophies on pedestals");
        RoomWing(root, "The Glitch Show", 4550f, 4600f, 70f, 24f, 8f, "Black", "DarkGlowPink", "Black", "THE CORRUPTED SET", new Color(0.2f, 1f, 1f),
                 "the back lot, where the glitch got in first. step carefully. the floor is only mostly there.", float.NaN,
                 new[] { "Stage Mouse Morris|#bdbdbd|19|rglw_morris", "Puppet Maker Prue|#ffccbc|9|rglw_prue", "Lost Cartoon Duck Dabble|#fff176|11|rglw_dabble" },
                 new[] { "The glitch wave|rglw_wave|slide|cube|4,2,0.3|DarkGlowCyan|4", "The flickering cartoon|rglw_flicker|shake|cap|0.6,1.4,0.6|GiftBlue|2", "The reboot button|rglw_reboot|drop|cyl|0.6,0.2,0.6|GlowGreen|1.5" },
                 (g, cx, z) => { for (int k = 0; k < 30; k++) DPrim(PrimitiveType.Cube, "Glitch block", g, V(cx - 22f + Mathf.Repeat(k * 0.618f, 1f) * 44f, 0.3f + Mathf.Repeat(k * 0.29f, 1f) * 5f, z + 21f + Mathf.Repeat(k * 0.41f, 1f) * 2.5f), Vector3.one * 0.6f, M(k % 2 == 0 ? "DarkGlowCyan" : "Black")); },
                 "glitch blocks hanging in the air");
        RoomWing(root, "The Rhythm Battle Stage", 4620f, 4660f, 60f, 22f, 8f, "Concrete", "BrickWall", "Black", "THE BACK ALLEY STAGE", new Color(1f, 0.3f, 0.8f),
                 "behind the club, where the real battles happen. bring your own boombox.", float.NaN,
                 new[] { "DJ Scratch|#80cbc4|18|rrhw_dj", "Rival Rapper Rook|#b0bec5|10|rrhw_rook", "Bouncer Boris|#8d6e63|20|rrhw_boris" },
                 new[] { "The boombox|rrhw_boombox|shake|cube|1.2,0.6,0.4|Black|2|0.6", "The spotlight|rrhw_spot|spin|cyl|0.6,0.6,0.6|GlowYellow|2|2.5", "The beat drop|rrhw_drop|drop|cube|1,1,1|GemPurple|3" },
                 (g, cx, z) => { Box(g, "Dumpster", V(cx - 14f, 0.8f, z + 20.5f), V(3f, 1.6f, 1.6f), M("GiftGreen")); Box(g, "Graffiti wall", V(cx + 8f, 2.5f, z + 21.9f), V(12f, 5f, 0.05f), M("GemPink"), false); },
                 "a dumpster and a graffiti wall");
        RoomWing(root, "The Pony Meadow", 4680f, 4730f, 70f, 24f, 18f, "PastelGrass", "PastelSky", "Sky", "THE SPARKLE CASTLE", new Color(1f, 0.55f, 0.85f),
                 "a castle made of crystal, for the friendliest ponies in the meadow. please don't lick the walls.", float.NaN,
                 new[] { "Royal Herald Hettie|#ce93d8|18|rpow_hettie", "Gem Polisher Garnet|#ef9a9a|7|rpow_garnet", "Pony Fan Fifi|#ffccbc|21|rpow_fifi" },
                 new[] { "The friendship crystal|rpow_crystal|grow|cube|0.8,1.6,0.8|GemPink|2", "The cupcake cannon|rpow_cupcake|fly|cyl|0.5,0.4,0.5|GemPink|5|0.5", "The rainbow fountain|rpow_fountain|spin|cyl|1.6,0.3,1.6|PonyBlue|2" },
                 (g, cx, z) => { for (int k = 0; k < 5; k++) DPrim(PrimitiveType.Cube, "Crystal spire", g, V(cx - 16f + k * 8f, 4f + (k % 2) * 2f, z + 22f), V(2f, 8f + (k % 2) * 4f, 2f), M(k % 2 == 0 ? "GemPink" : "PonyLilac"), Quaternion.Euler(0, 45, 0)); },
                 "five crystal spires");
        RoomWing(root, "The Four Element Isles", 4750f, 4810f, 80f, 26f, 20f, "PixelGreenDark", "NightSky", "NightSky", "THE SPIRIT FOREST", new Color(0.6f, 1f, 0.8f),
                 "at night, under the moon, the spirits come out of the trees. be polite. offer tea.", float.NaN,
                 new[] { "Spirit Guide Yara|#b2dfdb|3|relw_yara", "Tea Seller Old Mun|#d7ccc8|16|relw_mun", "Lantern Kid Lio|#ffcc80|21|relw_lio" },
                 new[] { "The spirit lantern|relw_lantern|spin|sph|0.6,0.8,0.6|GlowGhost|2|2", "The wandering spirit|relw_spirit|pop|cap|0.8,1.8,0.8|GlowGhost|2", "The tea kettle|relw_kettle|shake|sph|0.5,0.45,0.5|Brass|2|0.9" },
                 (g, cx, z) => { for (int k = 0; k < 7; k++) DPrim(PrimitiveType.Cylinder, "Spirit tree", g, V(cx - 27f + k * 9f, 6f, z + 23f), V(2f, 6f, 2f), M("TrimDark")); DPrim(PrimitiveType.Sphere, "Moon", g, V(cx, 16f, z + 25.8f), V(6f, 6f, 0.2f), M("GemWhite")); },
                 "dark trees and a big moon");
        RoomWing(root, "The Gorilla Chase Forest", 4830f, 4880f, 70f, 24f, 10f, "CaveRock", "CaveRock", "CaveRock", "THE CAVE", new Color(1f, 0.45f, 0.1f),
                 "the cave under the forest. it's dark, it echoes, and the cosmetics shop is down here for some reason.", float.NaN,
                 new[] { "Cave Gorilla Mossy|#689f38|19|rgow_mossy", "Shop Gorilla Chips|#795548|9|rgow_chips", "Newbie Nibbles|#bcaaa4|21|rgow_nibbles" },
                 new[] { "The cosmetics stand|rgow_cosmetics|spin|cyl|0.8,0.4,0.8|GemPurple|2|1", "The echo|rgow_echo|shake|sph|0.4,0.4,0.4|Mist|2|2", "The ceiling drop|rgow_drop|drop|sph|0.8,0.6,0.8|CaveRock|4" },
                 (g, cx, z) => { for (int k = 0; k < 6; k++) DPrim(PrimitiveType.Capsule, "Stalactite", g, V(cx - 20f + k * 8f, 8.5f, z + 20f + (k % 2) * 2f), V(0.6f, 1.5f, 0.6f), M("CaveRock")); for (int k = 0; k < 4; k++) Prim(PrimitiveType.Sphere, "Glow crystal", g, V(cx - 18f + k * 12f, 0.4f, z + 23f), Vector3.one * 0.6f, M("DarkGlowCyan")); },
                 "stalactites and glowing crystals");
        RoomWing(root, "Sunny Bay City", 4900f, 4960f, 80f, 26f, 20f, "PixelSand", "PixelSky", "Sky", "THE BEACH PIER", new Color(0.3f, 0.85f, 0.4f),
                 "the pier, the beach and the muscle gym. everybody here is either fishing, lifting or being chased.", float.NaN,
                 new[] { "Pier Fisher Flo|#80deea|16|rgtw_flo", "Muscle Beach Max|#ffab91|10|rgtw_max", "Tourist Teddy|#fff59d|21|rgtw_teddy" },
                 new[] { "The jet ski|rgtw_jetski|slide|cube|1,0.6,2.4|GlowYellow|6", "The muscle weights|rgtw_weights|shake|cyl|1.6,0.15,0.15|Metal|2|1.2", "The seagull thief|rgtw_gull|fly|sph|0.5,0.4,0.6|CaneWhite|5|1" },
                 (g, cx, z) => { Box(g, "Pier", V(cx + 16f, 0.5f, z + 20f), V(4f, 1f, 12f), M("DeskWood")); Box(g, "Sea", V(cx, 0.02f, z + 24.5f), V(60f, 0.04f, 3f), M("PoolBlue"), false); Box(g, "Weight bench", V(cx - 16f, 0.4f, z + 20f), V(1f, 0.8f, 2f), M("Black")); },
                 "the pier, the sea and a weight bench");
    }
}
#endif
