#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>The expansion, batch 2: new wings for the rest of the Station areas.</summary>
public static partial class FirCheckBuilder
{
    /// <summary>
    /// A gag with one shaped prop and a stock motion: pop (rises from below, hidden when idle), spin, drop, shake,
    /// slide (along the gag's forward), fly (up and away), grow, flip. Returns the gag; add more parts to gag.target.
    /// </summary>
    static FirGag Gk(Transform g, string name, Vector3 at, float yaw, string clip, string kind, PrimitiveType shape, Vector3 size, string mat, float amount = 2f)
    {
        var gag = Gg(g, name, at, yaw, clip, Mathf.Max(1.2f, Mathf.Max(size.x, size.z) + 0.4f), Mathf.Max(1.6f, size.y + 0.6f));
        var prop = Group(gag.transform, name + " prop", Vector3.zero);
        float y = size.y / 2f;
        if (kind == "pop") { prop.localPosition = V(0, -amount, 0); gag.hideWhenIdle = true; Move(gag, prop, V(0, amount, 0), Vector3.zero, 0f, 8f); }
        else if (kind == "spin") { prop.localPosition = V(0, 0.8f, 0); Move(gag, prop, V(0, 0.4f, 0), Vector3.zero, 0.1f, 0f, 300f); }
        else if (kind == "drop") { prop.localPosition = V(0, amount, 0); Move(gag, prop, V(0, -amount + 0.05f, 0), Vector3.zero, 0f, 6f).inTime = 0.2f; }
        else if (kind == "shake") { Move(gag, prop, V(0, 0.05f, 0), Vector3.zero, 0.08f, 22f); }
        else if (kind == "slide") { Move(gag, prop, V(0, 0, amount), Vector3.zero, 0.05f).inTime = 1.5f; }
        else if (kind == "fly") { Move(gag, prop, V(0, amount, amount * 0.5f), V(0, 360f, 0), 0.1f).inTime = 1.2f; }
        else if (kind == "grow") { Move(gag, prop, Vector3.zero, Vector3.zero, 0f, 8f, 0f, 1.8f); }
        else { Move(gag, prop, V(0, 0.3f, 0), V(180f, 0, 0)); }
        var p = Prim(shape, name + " shape", prop, V(0, y, 0), size, M(mat));
        return gag;
    }

    /// <summary>Scatters n static props of one shape round a rectangle (deterministic, no Random).</summary>
    static void Scatter(Transform g, string name, int n, float x0, float x1, float z0, float z1, PrimitiveType shape, Vector3 size, string[] mats, float y = -1f)
    {
        for (int k = 0; k < n; k++)
        {
            float fx = Mathf.Repeat(k * 0.6180339f + 0.11f, 1f), fz = Mathf.Repeat(k * 0.3819661f + 0.37f, 1f);
            DPrim(shape, name, g, V(Mathf.Lerp(x0, x1, fx), y < 0f ? size.y / 2f : y, Mathf.Lerp(z0, z1, fz)), size, M(mats[k % mats.Length]), Quaternion.Euler(0, k * 47f, 0));
        }
    }

    static void BuildExpansions2(Transform root)
    {
        WingGlade(root); WingHoney(root); WingDragon(root); WingHeroTower(root); WingGloom(root); WingMyrtle(root); WingBoone(root);
        WingDoxie(root); WingXmas(root); WingCellbound(root); WingMark(root); WingFlamingo(root); WingTrahan(root); WingScream(root);
        WingInventors(root); WingNotebook(root); WingSeabed(root); WingBattle(root); WingScp(root); WingGarage(root); WingWater(root); WingCT(root);
    }

    // ------------------------------------------------------------------------------------------------ Hero's Glade

    static void WingGlade(Transform root)
    {
        float cx = 1445f, z = 70f;
        var g = Wing(root, "Hero's Glade", 1420f, 1470f, z, 30f, 20f, "Stone", "CaveRock", "Black",
                     "THE SUNKEN SHRINE", new Color(0.4f, 0.9f, 0.6f), "an old shrine under the glade. bring a lantern. bring two.");
        for (int k = 0; k < 8; k++) DPrim(PrimitiveType.Cube, "Shrine block", g, V(cx - 21f + k * 6f, 1.5f, z + 28f), V(4f, 3f, 2f), M(k % 2 == 0 ? "Stone" : "GloomStone"));
        for (int k = 0; k < 6; k++) DPrim(PrimitiveType.Cylinder, "Torch", g, V(cx - 20f + k * 8f, 1.2f, z + 4f + (k % 2) * 20f), V(0.3f, 1.2f, 0.3f), M("TrimDark"));
        for (int k = 0; k < 6; k++) DPrim(PrimitiveType.Sphere, "Flame", g, V(cx - 20f + k * 8f, 2.6f, z + 4f + (k % 2) * 20f), V(0.4f, 0.6f, 0.4f), M("GlowFlame"));
        Box(g, "Sunken pool", V(cx + 12f, 0.02f, z + 14f), V(10f, 0.04f, 8f), M("Water"), false);
        Scatter(g, "Clay pot", 10, cx - 22f, cx - 8f, z + 6f, z + 24f, PrimitiveType.Sphere, V(0.7f, 0.8f, 0.7f), new[] { "Brick", "Dirt" });
        var gems = new List<Vector3>(); for (int k = 0; k < 10; k++) gems.Add(V(cx - 18f + k * 4f, 1f, z + 10f + (k % 3) * 4f));
        Collectables(g, "Green gems", gems.ToArray(), (t, i) => { DPrim(PrimitiveType.Cube, "Gem", t, Vector3.zero, V(0.25f, 0.45f, 0.25f), M("GlowGreen"), Quaternion.Euler(0, 45, 0)); return true; }, V(cx - 7f, 1.6f, z + 0.35f), Vector3.forward);
        Folk(g, "Old Sage Sternum", "#d7ccc8", 9, V(cx, 0, z + 24f), 180f, "More/mo_gld_sage");
        Folk(g, "Fountain Keeper Fae", "#f8bbd0", 5, V(cx + 12f, 0, z + 9f), 180f, "More/mo_gld_fairy");
        Folk(g, "Goblin Shopkeep Gristle", "#9ccc65", 16, V(cx - 18f, 0, z + 26f), 150f, "More/mo_gld_shop");
        var chest = Gk(g, "The treasure chest", V(cx + 4f, 0, z + 18f), 180f, "More/mo_gld_chest", "flip", PrimitiveType.Cube, V(1.2f, 0.15f, 0.8f), "GlowGold");
        Box(g, "Chest", V(cx + 4f, 0.4f, z + 18f), V(1.2f, 0.8f, 0.8f), M("DeskWood"));
        chest.target.localPosition = V(0, 0.8f, 0);
        var sword = Gk(g, "The sword in the stone", V(cx - 4f, 0, z + 14f), 180f, "More/mo_gld_sword", "fly", PrimitiveType.Cube, V(0.12f, 1.6f, 0.3f), "Metal", 1.5f);
        Box(g, "The stone", V(cx - 4f, 0.4f, z + 14f), V(1.4f, 0.8f, 1.4f), M("Stone"));
        sword.target.localPosition = V(0, 0.6f, 0);
        Gk(g, "The pot that bursts", V(cx - 14f, 0, z + 4f), 180f, "More/mo_gld_pot", "grow", PrimitiveType.Sphere, V(0.8f, 0.9f, 0.8f), "Brick");
        report.Add("The Sunken Shrine (Hero's Glade's new wing): shrine blocks, torches, a pool, clay pots and 10 gems; 3 voiced people, 3 gags");
    }

    // ------------------------------------------------------------------------------------------------ Honeycomb Hollow

    static void WingHoney(Transform root)
    {
        float cx = 1515f, z = 70f;
        var g = Wing(root, "Honeycomb Hollow", 1490f, 1540f, z, 30f, 20f, "Honey", "GlowGold", "Honey",
                     "THE GREAT HIVE", new Color(1f, 0.8f, 0.2f), "inside the biggest hive in the hollow. it's sticky in here.");
        for (int k = 0; k < 24; k++)
            DPrim(PrimitiveType.Cylinder, "Comb cell", g, V(cx - 22f + (k % 8) * 6f + ((k / 8) % 2) * 3f, 0.4f + (k / 8) * 0.01f, z + 6f + (k / 8) * 8f), V(5f, 0.4f, 5f), M(k % 3 == 0 ? "GlowGold" : "Honey"));
        for (int k = 0; k < 6; k++) DPrim(PrimitiveType.Sphere, "Honey blob", g, V(cx - 18f + k * 7f, 12f, z + 10f + (k % 2) * 10f), V(1.6f, 2.4f, 1.6f), M("Honey"));
        var pieces = new List<Vector3>(); for (int k = 0; k < 8; k++) pieces.Add(V(cx - 16f + k * 4.5f, 1.4f + (k % 2), z + 8f + (k % 4) * 5f));
        Collectables(g, "Golden puzzle pieces", pieces.ToArray(), (t, i) => { DPrim(PrimitiveType.Cube, "Piece", t, Vector3.zero, V(0.6f, 0.6f, 0.1f), M("GlowGold")); DPrim(PrimitiveType.Sphere, "Knob", t, V(0.32f, 0, 0), Vector3.one * 0.22f, M("GlowGold")); return true; },
                     V(cx - 7f, 1.6f, z + 0.35f), Vector3.forward);
        Folk(g, "Queen Beatrix", "#ffd600", 11, V(cx, 1f, z + 26f), 180f, "More/mo_hny_queen");
        Folk(g, "Beekeeper Barnaby", "#f5f5f5", 7, V(cx - 14f, 0, z + 18f), 120f, "More/mo_hny_keeper");
        Folk(g, "Jarvis the Jar Collector", "#bcaaa4", 9, V(cx + 14f, 0, z + 6f), 220f, "More/mo_hny_jars");
        Box(g, "Throne", V(cx, 0.5f, z + 27f), V(3f, 1f, 2f), M("GlowGold"));
        var drip = Gk(g, "The honey drip", V(cx + 6f, 0, z + 14f), 0f, "More/mo_hny_drip", "drop", PrimitiveType.Sphere, V(0.5f, 0.7f, 0.5f), "Honey", 8f);
        var swarm = Gk(g, "The bee swarm", V(cx - 6f, 0, z + 12f), 0f, "More/mo_hny_swarm", "spin", PrimitiveType.Sphere, V(0.2f, 0.15f, 0.3f), "GlowYellow");
        for (int k = 0; k < 7; k++) Prim(PrimitiveType.Sphere, "Bee", swarm.target, V(Mathf.Cos(k) * 0.9f, 0.4f + (k % 3) * 0.3f, Mathf.Sin(k) * 0.9f), V(0.2f, 0.15f, 0.3f), M(k % 2 == 0 ? "GlowYellow" : "Black"));
        Gk(g, "The talking honey jar", V(cx + 14f, 0, z + 9f), 180f, "More/mo_hny_jar", "shake", PrimitiveType.Cylinder, V(0.6f, 0.8f, 0.6f), "Honey");
        report.Add("The Great Hive (Honeycomb Hollow's new wing): honeycomb floor, honey blobs, a queen's throne and 8 golden puzzle pieces; 3 voiced people, 3 gags");
    }

    // ------------------------------------------------------------------------------------------------ Dragon Shore

    static void WingDragon(Transform root)
    {
        float cx = 1585f, z = 70f;
        var g = Wing(root, "Dragon Shore", 1560f, 1610f, z, 30f, 20f, "PixelSand", "PixelSky", "Sky",
                     "THE DRAGON HATCHERY", new Color(0.7f, 0.4f, 1f), "where little dragons come from. eggs everywhere. step carefully.");
        for (int k = 0; k < 8; k++)
        {
            var n = V(cx - 18f + (k % 4) * 12f, 0, z + 8f + (k / 4) * 12f);
            DPrim(PrimitiveType.Cylinder, "Nest", g, n + V(0, 0.2f, 0), V(3f, 0.2f, 3f), M("Dirt"));
            for (int e = 0; e < 3; e++) DPrim(PrimitiveType.Sphere, "Egg", g, n + V(-0.6f + e * 0.6f, 0.65f, (e % 2) * 0.3f), V(0.5f, 0.7f, 0.5f), M(new[] { "GemPurple", "GemPink", "GlowGold" }[(k + e) % 3]));
        }
        var eggs = new List<Vector3>(); for (int k = 0; k < 8; k++) eggs.Add(V(cx - 20f + k * 5.5f, 0.6f, z + 26f));
        Collectables(g, "Lost eggs", eggs.ToArray(), (t, i) => { DPrim(PrimitiveType.Sphere, "Egg", t, Vector3.zero, V(0.45f, 0.6f, 0.45f), M(i % 2 == 0 ? "GemPink" : "GemWhite")); return true; }, V(cx - 7f, 1.6f, z + 0.35f), Vector3.forward);
        Folk(g, "Egg-Sitter Edna", "#ce93d8", 9, V(cx - 6f, 0, z + 14f), 180f, "More/mo_drg_sitter");
        Folk(g, "Sly the Egg Thief", "#78909c", 15, V(cx + 18f, 0, z + 26f), 200f, "More/mo_drg_thief");
        Folk(g, "Professor Scales", "#a5d6a7", 7, V(cx + 6f, 0, z + 4f), 180f, "More/mo_drg_prof");
        var hatch = Gk(g, "The hatching egg", V(cx + 6f, 0, z + 14f), 180f, "More/mo_drg_hatch", "pop", PrimitiveType.Sphere, V(0.6f, 0.6f, 0.7f), "GemPurple", 0.8f);
        Prim(PrimitiveType.Cube, "Tiny wing", hatch.target, V(0.4f, 0.4f, 0), V(0.5f, 0.05f, 0.3f), M("GlowGold"));
        DPrim(PrimitiveType.Sphere, "Cracked shell", g, V(cx + 6f, 0.3f, z + 14f), V(0.8f, 0.5f, 0.8f), M("GemWhite"));
        var breath = Gk(g, "The dragon's sneeze", V(cx - 14f, 0, z + 4f), 90f, "More/mo_drg_sneeze", "slide", PrimitiveType.Sphere, V(0.8f, 0.8f, 0.8f), "GlowFlame", 6f);
        breath.hideWhenIdle = true; breath.target.localPosition = V(0, 1f, 0);
        Gk(g, "The gem chest", V(cx - 18f, 0, z + 20f), 90f, "More/mo_drg_gems", "spin", PrimitiveType.Cube, V(0.4f, 0.6f, 0.4f), "GemPink");
        report.Add("The Dragon Hatchery (Dragon Shore's new wing): 8 nests of eggs and 8 lost eggs to find; 3 voiced people, 3 gags");
    }

    // ------------------------------------------------------------------------------------------------ Hero Tower

    static void WingHeroTower(Transform root)
    {
        float cx = 1650f, z = 60f;
        var g = Wing(root, "Hero Tower", 1630f, 1670f, z, 26f, 10f, "LabFloor", "LabGrey", "LabWhite",
                     "THE TRAINING ROOM", new Color(1f, 0.3f, 0.3f), "where heroes train. and sidekicks fetch the towels.");
        for (int k = 0; k < 6; k++) Box(g, "Hurdle", V(cx - 15f + k * 3f, 0.5f, z + 8f), V(0.2f, 1f, 3f), M(k % 2 == 0 ? "HeroRed" : "HeroBlue"));
        for (int k = 0; k < 4; k++) { DPrim(PrimitiveType.Capsule, "Target dummy", g, V(cx + 6f + k * 3f, 1f, z + 20f), V(0.7f, 1f, 0.7f), M("Cardboard")); DPrim(PrimitiveType.Cylinder, "Bullseye", g, V(cx + 6f + k * 3f, 1.4f, z + 19.62f), V(0.5f, 0.02f, 0.5f), M("GlowRed"), Quaternion.Euler(90, 0, 0)); }
        Box(g, "Climbing wall", V(cx - 19.8f, 4f, z + 16f), V(0.3f, 8f, 8f), M("Pegboard"));
        Box(g, "Gadget bench", V(cx + 15f, 0.5f, z + 6f), V(4f, 1f, 1.6f), M("Metal"));
        Folk(g, "Drill Sergeant Scapula", "#ef5350", 11, V(cx - 4f, 0, z + 4f), 180f, "More/mo_her_drill");
        Folk(g, "Gadget Gwen", "#4dd0e1", 7, V(cx + 15f, 0, z + 4.5f), 0f, "More/mo_her_gadget");
        Folk(g, "Retired Sidekick Stu", "#ffcc80", 20, V(cx - 15f, 0, z + 22f), 150f, "More/mo_her_sidekick");
        Gk(g, "The punching robot", V(cx + 2f, 0, z + 18f), 180f, "More/mo_her_robot", "shake", PrimitiveType.Cube, V(1f, 2f, 0.8f), "Metal");
        Gk(g, "The bouncing shield", V(cx - 8f, 0, z + 14f), 180f, "More/mo_her_shield", "fly", PrimitiveType.Cylinder, V(1f, 0.08f, 1f), "HeroBlue", 3f);
        Gk(g, "The lab experiment", V(cx + 15f, 0, z + 8.5f), 180f, "More/mo_her_lab", "grow", PrimitiveType.Sphere, V(0.6f, 0.6f, 0.6f), "AlienGlow");
        report.Add("The Training Room (Hero Tower's new wing): hurdles, target dummies, a climbing wall and a gadget bench; 3 voiced people, 3 gags");
    }

    // ------------------------------------------------------------------------------------------------ Gloom City

    static void WingGloom(Transform root)
    {
        float cx = 1715f, z = 70f;
        var g = Wing(root, "Gloom City", 1690f, 1740f, z, 30f, 25f, "GloomStone", "NightSky", "NightSky",
                     "THE ROOFTOPS", new Color(0.7f, 0.7f, 0.9f), "above the city. the gargoyles are watching. one of them is new.");
        for (int k = 0; k < 6; k++)
        {
            var at = V(cx + new[] { -18f, -8f, 14f }[k % 3], 0, z + 6f + (k / 3) * 14f);
            Box(g, "Roof block", at + V(0, 1f + (k % 2), 0), V(8f, 2f + (k % 2) * 2f, 8f), M("GloomStone"));
            DPrim(PrimitiveType.Cube, "Chimney", g, at + V(2f, 3.5f + (k % 2) * 2f, 2f), V(1f, 1.5f, 1f), M("Brick"));
        }
        for (int k = 0; k < 4; k++) DPrim(PrimitiveType.Cube, "Gargoyle", g, V(cx - 15f + k * 10f, 1.2f, z + 28.5f), V(1.2f, 1.6f, 1f), M("Stone"));
        Folk(g, "Night Watchman Nate", "#90a4ae", 15, V(cx, 0, z + 12f), 180f, "More/mo_glm_watch");
        Folk(g, "Whispers the Informant", "#616161", 20, V(cx + 10f, 0, z + 24f), 200f, "More/mo_glm_inform");
        Folk(g, "Scoop the Reporter", "#fff176", 2, V(cx - 10f, 0, z + 14f), 160f, "More/mo_glm_reporter");
        var sig = Gk(g, "The skull signal", V(cx, 0, z + 26f), 180f, "More/mo_glm_signal", "pop", PrimitiveType.Sphere, V(3f, 3f, 0.2f), "BeamWhite", 4f);
        sig.target.localPosition = V(0, 10f, 0); sig.moveBy = V(0, 6f, 0);
        Gk(g, "The swinging stranger", V(cx - 18f, 0, z + 20f), 90f, "More/mo_glm_swing", "fly", PrimitiveType.Capsule, V(0.6f, 1f, 0.6f), "Black", 6f).hideWhenIdle = true;
        var garg = Gk(g, "The new gargoyle", V(cx + 15f, 0, z + 27f), 180f, "More/mo_glm_gargoyle", "flip", PrimitiveType.Cube, V(1.2f, 1.6f, 1f), "Stone");
        garg.rotateBy = V(0, 120f, 0);
        report.Add("The Rooftops (Gloom City's new wing): rooftops, chimneys, gargoyles and the skull signal; 3 voiced people, 3 gags");
    }

    // ------------------------------------------------------------------------------------------------ Myrtle Beach

    static void WingMyrtle(Transform root)
    {
        float cx = 1790f, z = 80f;
        var g = Wing(root, "Myrtle Beach", 1760f, 1820f, z, 28f, 25f, "DeskWood", "Sky", "Sky",
                     "THE BOARDWALK ARCADE", new Color(1f, 0.4f, 0.8f), "ten thousand tickets for a pencil. worth it.");
        for (int k = 0; k < 10; k++)
        {
            var at = V(cx - 25f + k * 5.5f, 0, z + 24f);
            Box(g, "Arcade cabinet", at + V(0, 1f, 0), V(1.4f, 2f, 1.2f), M(new[] { "HeroBlue", "GiftRed", "GemPurple", "GlowGreen" }[k % 4]));
            Box(g, "Screen", at + V(0, 1.4f, -0.62f), V(1f, 0.7f, 0.04f), M(k % 2 == 0 ? "DarkGlowCyan" : "DarkGlowPink"), false);
        }
        for (int k = 0; k < 4; k++) Box(g, "Skee lane", V(cx - 18f + k * 3f, 0.5f, z + 10f), V(1.6f, 1f, 6f), M("DeskWood"));
        Box(g, "Prize counter", V(cx + 18f, 0.6f, z + 10f), V(8f, 1.2f, 1.4f), M("Glass"));
        Scatter(g, "Plush prize", 12, cx + 14.5f, cx + 21.5f, z + 9.7f, z + 10.3f, PrimitiveType.Sphere, V(0.4f, 0.4f, 0.4f), new[] { "GemPink", "GlowYellow", "GiftBlue" }, 1.4f);
        var tix = new List<Vector3>(); for (int k = 0; k < 10; k++) tix.Add(V(cx - 22f + k * 5f, 0.9f, z + 18f));
        Collectables(g, "Tickets", tix.ToArray(), (t, i) => { DPrim(PrimitiveType.Cube, "Ticket", t, Vector3.zero, V(0.5f, 0.25f, 0.03f), M("GiftRed")); return true; }, V(cx - 7f, 1.6f, z + 0.35f), Vector3.forward);
        Folk(g, "Token Tammy", "#f48fb1", 9, V(cx - 6f, 0, z + 4f), 180f, "More/mo_myr_tokens");
        Folk(g, "Skee-Ball Champ Chuck", "#81d4fa", 16, V(cx - 18f, 0, z + 5f), 180f, "More/mo_myr_skee");
        Folk(g, "Prize Counter Priya", "#ffe082", 2, V(cx + 18f, 0, z + 11.6f), 180f, "More/mo_myr_prize");
        var claw = Gk(g, "The claw machine", V(cx + 6f, 0, z + 14f), 180f, "More/mo_myr_claw", "drop", PrimitiveType.Cube, V(0.3f, 0.4f, 0.3f), "Metal", 1.6f);
        Box(g, "Claw cabinet", V(cx + 6f, 0.5f, z + 14f), V(1.6f, 1f, 1.6f), M("GemPink"));
        claw.target.localPosition = V(0, 2.4f, 0);
        Gk(g, "The whack-a-skull", V(cx, 0, z + 14f), 180f, "More/mo_myr_whack", "pop", PrimitiveType.Sphere, V(0.5f, 0.5f, 0.5f), "BoneWhite", 0.7f);
        Box(g, "Whack table", V(cx, 0.45f, z + 14f), V(1.6f, 0.9f, 1.6f), M("GiftGreen"));
        Gk(g, "The skee-ball", V(cx - 18f, 0, z + 8f), 0f, "More/mo_myr_ball", "slide", PrimitiveType.Sphere, V(0.3f, 0.3f, 0.3f), "GlowOrange", 4f).target.localPosition = V(0, 1f, 0);
        report.Add("The Boardwalk Arcade (Myrtle Beach's new wing): 10 cabinets, skee-ball lanes, a prize counter and 10 tickets; 3 voiced people, 3 gags");
    }

    // ------------------------------------------------------------------------------------------------ Boone

    static void WingBoone(Transform root)
    {
        float cx = 1870f, z = 80f;
        var g = Wing(root, "Boone", 1840f, 1900f, z, 30f, 25f, "PixelGreen", "RidgeBlue2", "Sky",
                     "THE MOUNTAIN TRAIL", new Color(0.6f, 0.9f, 1f), "a trail up through the rhododendrons to the falls");
        for (int k = 0; k < 7; k++) Box(g, "Ridge", V(cx - 27f + k * 9f, 4f + (k % 3) * 2f, z + 28.5f), V(9.5f, 8f + (k % 3) * 4f, 2f), M(k % 2 == 0 ? "RidgeBlue1" : "RidgeBlue3"));
        Scatter(g, "Rhododendron", 18, cx - 26f, cx + 26f, z + 4f, z + 24f, PrimitiveType.Sphere, V(1.8f, 1.4f, 1.8f), new[] { "GiftGreen", "PixelGreenDark" });
        Scatter(g, "Blossom", 18, cx - 26f, cx + 26f, z + 4f, z + 24f, PrimitiveType.Sphere, V(0.5f, 0.4f, 0.5f), new[] { "GemPink", "GemPurple" }, 1.4f);
        Box(g, "Falls", V(cx + 18f, 6f, z + 27f), V(4f, 12f, 0.4f), M("Water"), false);
        Box(g, "Plunge pool", V(cx + 18f, 0.02f, z + 23f), V(8f, 0.04f, 6f), M("Water"), false);
        DPrim(PrimitiveType.Cylinder, "Campfire ring", g, V(cx - 12f, 0.15f, z + 16f), V(2f, 0.15f, 2f), M("Stone"));
        Folk(g, "Thru-Hiker Thelma", "#a1887f", 2, V(cx - 4f, 0, z + 6f), 170f, "More/mo_bne_hiker");
        Folk(g, "Banjo Bill", "#ffcc80", 20, V(cx - 14f, 0, z + 18f), 120f, "More/mo_bne_banjo");
        Folk(g, "Ranger Radius", "#689f38", 11, V(cx + 12f, 0, z + 8f), 200f, "More/mo_bne_ranger");
        var fire = Gk(g, "The campfire", V(cx - 12f, 0, z + 16f), 180f, "More/mo_bne_fire", "grow", PrimitiveType.Sphere, V(0.6f, 0.9f, 0.6f), "GlowFlame");
        Gk(g, "The bear in the bushes", V(cx + 4f, 0, z + 20f), 180f, "More/mo_bne_bear", "pop", PrimitiveType.Sphere, V(1.2f, 1.4f, 1f), "BoneWhite", 1.4f);
        Gk(g, "The falling leaf", V(cx + 18f, 0, z + 20f), 180f, "More/mo_bne_leaf", "drop", PrimitiveType.Cube, V(0.4f, 0.04f, 0.3f), "FoliageOrange", 6f);
        report.Add("The Mountain Trail (Boone's new wing): blue ridges, rhododendrons in bloom, a waterfall and a campfire; 3 voiced people, 3 gags");
    }

    // ------------------------------------------------------------------------------------------------ the Dachshund Room

    static void WingDoxie(Transform root)
    {
        float cx = 1935f, z = 50f;
        var g = Wing(root, "The Dachshund Room", 1920f, 1950f, z, 24f, 5f, "Grass", "WallGreen", "Ceiling",
                     "THE DOG PARK", new Color(0.6f, 0.4f, 0.1f), "off-leash area. mind the low-flying dogs.");
        for (int k = 0; k < 4; k++) DPrim(PrimitiveType.Cylinder, "Fire hydrant", g, V(cx - 12f + k * 8f, 0.4f, z + 3f), V(0.4f, 0.4f, 0.4f), M("GlowRed"));
        for (int k = 0; k < 5; k++) Box(g, "Agility hurdle", V(cx - 8f + k * 4f, 0.3f, z + 14f), V(0.1f, 0.6f, 1.6f), M(k % 2 == 0 ? "CaneRed" : "CaneWhite"));
        DPrim(PrimitiveType.Cylinder, "Agility tunnel", g, V(cx + 10f, 0.5f, z + 20f), V(1f, 2.5f, 1f), M("GiftBlue"), Quaternion.Euler(0, 0, 90));
        string[] coats = { "DoxieBrown", "DoxieBlack" };
        for (int k = 0; k < 5; k++) Doxie(g, "Park doxie " + k, V(cx - 10f + k * 5f, 0, z + 9f + (k % 2) * 8f), k * 70f, 0.9f + (k % 3) * 0.3f, coats[k % 2]);
        var balls = new List<Vector3>(); for (int k = 0; k < 8; k++) balls.Add(V(cx - 12f + k * 3.4f, 0.3f, z + 18f + (k % 2) * 2f));
        Collectables(g, "Tennis balls", balls.ToArray(), (t, i) => { DPrim(PrimitiveType.Sphere, "Ball", t, Vector3.zero, Vector3.one * 0.25f, M("GlowYellow")); return true; }, V(cx - 7f, 1.6f, z + 0.35f), Vector3.forward);
        Folk(g, "Dog Walker Dolores", "#80cbc4", 2, V(cx - 4f, 0, z + 6f), 160f, "More/mo_dox_walker");
        Folk(g, "Groomer Gus", "#f48fb1", 9, V(cx + 12f, 0, z + 6f), 220f, "More/mo_dox_groomer");
        Folk(g, "Agility Judge Agnes", "#ffd54f", 11, V(cx, 0, z + 20f), 180f, "More/mo_dox_judge");
        Gk(g, "The ball launcher", V(cx - 12f, 0, z + 20f), 0f, "More/mo_dox_launcher", "fly", PrimitiveType.Sphere, V(0.25f, 0.25f, 0.25f), "GlowYellow", 3f);
        var zoom = Gg(g, "The zoomies", V(cx - 12f, 0, z + 11f), 90f, "More/mo_dox_zoomies", 1.6f, 1.2f);
        var zd = Doxie(zoom.transform, "Zooming doxie", Vector3.zero, 0f, 1.1f, "DoxieBrown");
        Move(zoom, zd, V(0, 0, 20f), V(0, 720f, 0), 0.05f).inTime = 1.4f;
        var tun = Gg(g, "The tunnel", V(cx + 10f, 0, z + 18.5f), 180f, "More/mo_dox_tunnel", 1.6f, 1.2f);
        var td = Doxie(tun.transform, "Tunnel doxie", V(0, 0, -1f), 90f, 1.2f, "DoxieBlack");
        Move(tun, td, V(-2.4f, 0, 0), Vector3.zero, 0.05f).hideWhenIdle = true;
        report.Add("The Dog Park (the Dachshund Room's new wing): hydrants, an agility course, a tunnel, 5 doxies and 8 tennis balls; 3 voiced people, 3 gags");
    }

    // ------------------------------------------------------------------------------------------------ Extremely Christmas

    static void WingXmas(Transform root)
    {
        float cx = 2000f, z = 80f;
        var g = Wing(root, "Extremely Christmas", 1970f, 2030f, z, 30f, 25f, "DeskWood", "GiftRed", "NightSky",
                     "THE TOY WORKSHOP", new Color(0.3f, 1f, 0.4f), "where every toy is made. by skeletons. it's fine. they're very careful.");
        Box(g, "Conveyor", V(cx, 0.6f, z + 14f), V(40f, 0.2f, 2f), M("Black"));
        for (int k = 0; k < 14; k++) Box(g, "Toy on the belt", V(cx - 19f + k * 2.9f, 0.95f, z + 14f), V(0.5f, 0.5f, 0.5f), M(new[] { "GiftRed", "GiftGreen", "GiftBlue", "GiftGold" }[k % 4]), false);
        for (int k = 0; k < 6; k++) Box(g, "Workbench", V(cx - 20f + k * 8f, 0.5f, z + 22f), V(4f, 1f, 1.6f), M("DeskWood"));
        Scatter(g, "Wrapped present", 20, cx - 26f, cx + 26f, z + 3f, z + 9f, PrimitiveType.Cube, V(0.7f, 0.6f, 0.7f), new[] { "GiftRed", "GiftGreen", "GiftBlue", "GiftGold" });
        Folk(g, "Head Elf Ernestine", "#66bb6a", 11, V(cx - 8f, 0, z + 20.5f), 180f, "More/mo_xms_headelf");
        Folk(g, "Toy Tester Tobias", "#ef5350", 16, V(cx + 8f, 0, z + 20.5f), 180f, "More/mo_xms_tester");
        Folk(g, "Wrapping Champion Winnie", "#fff59d", 7, V(cx + 18f, 0, z + 10f), 200f, "More/mo_xms_wrap");
        var box = Gk(g, "The confetti present", V(cx - 18f, 0, z + 18f), 180f, "More/mo_xms_confetti", "grow", PrimitiveType.Cube, V(0.9f, 0.9f, 0.9f), "GiftGold");
        var bot = Gk(g, "The wind-up robot", V(cx, 0, z + 18.5f), 90f, "More/mo_xms_robot", "slide", PrimitiveType.Cube, V(0.5f, 0.8f, 0.4f), "Metal", 4f);
        Prim(PrimitiveType.Cylinder, "Key", bot.target, V(0, 0.5f, -0.3f), V(0.3f, 0.03f, 0.3f), M("GlowGold")).transform.localRotation = Quaternion.Euler(90, 0, 0);
        var sleigh = Gk(g, "The sleigh test", V(cx + 20f, 0, z + 26f), 270f, "More/mo_xms_sleigh", "fly", PrimitiveType.Cube, V(2f, 0.8f, 1.2f), "GiftRed", 10f);
        report.Add("The Toy Workshop (Extremely Christmas's new wing): a conveyor of toys, 6 workbenches, piles of presents; 3 voiced people, 3 gags");
    }

    // ------------------------------------------------------------------------------------------------ Cellbound

    static void WingCellbound(Transform root)
    {
        float cx = 2070f, z = 50f;
        var g = Wing(root, "Cellbound", 2050f, 2090f, z, 24f, 5f, "Floor2", "WallDark", "Ceiling",
                     "THE WARDEN'S OFFICE", new Color(0.9f, 0.85f, 0.7f), "the office upstairs from the tunnel. mind the floor. it's thinner than it looks.");
        Box(g, "Warden's desk", V(cx, 0.75f, z + 16f), V(4f, 1.5f, 1.8f), M("DeskWood"));
        Box(g, "Big old TV", V(cx + 12f, 1f, z + 20f), V(2f, 1.6f, 1.4f), M("DeskWood"));
        Box(g, "TV screen", V(cx + 12f, 1.1f, z + 19.28f), V(1.4f, 1f, 0.04f), M("TurfGrey"), false);
        for (int k = 0; k < 4; k++) Box(g, "Filing cabinet", V(cx - 18f, 0.75f, z + 4f + k * 1.2f), V(1f, 1.5f, 1.1f), M("Metal"));
        for (int k = 0; k < 6; k++) Box(g, "Window bar", V(cx - 6f + k * 0.4f, 2.6f, z + 23.85f), V(0.06f, 1.6f, 0.06f), M("Black"), false);
        Folk(g, "Guard Gus Gristlebone", "#78909c", 11, V(cx - 8f, 0, z + 6f), 180f, "More/mo_cel_guard");
        Folk(g, "Cookie the Prison Cook", "#f5f5f5", 9, V(cx + 14f, 0, z + 6f), 210f, "More/mo_cel_cook");
        Folk(g, "Inmate Number Nine", "#ffab91", 20, V(cx - 14f, 0, z + 16f), 120f, "More/mo_cel_inmate");
        var dig = Gk(g, "The hole in the floor", V(cx - 4f, 0, z + 10f), 180f, "More/mo_cel_hole", "pop", PrimitiveType.Sphere, V(0.5f, 0.5f, 0.5f), "BoneWhite", 0.8f);
        Box(g, "Hole", V(cx - 4f, 0.01f, z + 10f), V(1f, 0.02f, 1f), M("Black"), false);
        Gk(g, "The TV turns itself on", V(cx + 12f, 0, z + 18.5f), 180f, "More/mo_cel_tv", "shake", PrimitiveType.Cube, V(1.3f, 0.9f, 0.04f), "GlowWarm").target.localPosition = V(0, 0.65f, -0.7f);
        Gk(g, "The spoon", V(cx, 0, z + 16f), 180f, "More/mo_cel_spoon", "spin", PrimitiveType.Capsule, V(0.06f, 0.2f, 0.06f), "Metal").target.localPosition = V(0, 1.6f, 0);
        report.Add("The Warden's Office (Cellbound's new wing): the warden's desk, the old TV, filing cabinets, a hole in the floor; 3 voiced people, 3 gags");
    }

    // ------------------------------------------------------------------------------------------------ the fan rooms

    static void WingMark(Transform root)
    {
        float cx = 2128f, z = 50f;
        var g = Wing(root, "Markiplier Room", 2110f, 2146f, z, 22f, 6f, "Rug", "WallDark", "Ceiling",
                     "THE FAN ART GALLERY", new Color(1f, 0.3f, 0.4f), "unofficial fan tribute. not affiliated. every picture drawn by a skeleton.");
        for (int k = 0; k < 8; k++)
        {
            float fz = z + 3f + k * 2.4f; bool west = k % 2 == 0;
            float fx = west ? 2110.06f : 2145.94f;
            Box(g, "Frame", V(fx, 2.4f, fz), V(0.05f, 1.4f, 1.8f), M("GlowGold"), false);
            Box(g, "Canvas", V(fx + (west ? 0.04f : -0.04f), 2.4f, fz), V(0.02f, 1.2f, 1.6f), M(new[] { "GemPink", "HeroBlue", "PixelGreen", "GlowOrange" }[k % 4]), false);
        }
        Plaque(g, V(cx, 0, z + 20f), "<b>THE FAN ART GALLERY</b>\nAn unofficial fan tribute. Not affiliated with or endorsed by anyone it celebrates.", "More/mo_mrk_plaque");
        Folk(g, "Curator Coccyx", "#b0bec5", 9, V(cx - 6f, 0, z + 12f), 90f, "More/mo_mrk_curator");
        Folk(g, "Fan With A Sign", "#ff8a80", 16, V(cx + 6f, 0, z + 8f), 200f, "More/mo_mrk_fan");
        Folk(g, "Night Editor Ned", "#9fa8da", 20, V(cx + 8f, 0, z + 18f), 180f, "More/mo_mrk_editor");
        Gk(g, "The jump-scare painting", V(cx - 14f, 0, z + 18f), 90f, "More/mo_mrk_scare", "pop", PrimitiveType.Sphere, V(0.8f, 0.9f, 0.6f), "BoneWhite", 1.2f);
        Gk(g, "The confetti cannon", V(cx + 14f, 0, z + 4f), 270f, "More/mo_mrk_confetti", "fly", PrimitiveType.Cube, V(0.3f, 0.3f, 0.3f), "GemPink", 3f);
        Gk(g, "The editing desk", V(cx + 12f, 0, z + 18f), 180f, "More/mo_mrk_desk", "shake", PrimitiveType.Cube, V(1.6f, 0.9f, 0.8f), "Black");
        report.Add("The Fan Art Gallery (the Markiplier room's new wing): 8 framed fan paintings and a tribute plaque; 3 voiced fans, 3 gags (plus the plaque)");
    }

    static void WingFlamingo(Transform root)
    {
        float cx = 2178f, z = 50f;
        var g = Wing(root, "Flamingo Room", 2160f, 2196f, z, 24f, 8f, "PoolBlue", "GemPink", "Ceiling",
                     "THE POOL PARTY", new Color(1f, 0.5f, 0.7f), "unofficial fan tribute. not affiliated. pink everything.");
        Box(g, "Pool deck", V(cx, 0.01f, z + 12f), V(36f, 0.02f, 24f), M("PixelSand"), false);
        Box(g, "Pool", V(cx, 0.03f, z + 14f), V(16f, 0.04f, 10f), M("PoolBlue"), false);
        for (int k = 0; k < 6; k++) LawnFlamingo(g, V(cx - 15f + k * 6f, 0, z + 22f), 180f + k * 25f);
        for (int k = 0; k < 4; k++) Box(g, "Sun lounger", V(cx - 14f, 0.3f, z + 5f + k * 3f), V(0.9f, 0.3f, 2f), M("CaneWhite"));
        Plaque(g, V(cx + 14f, 0, z + 3f), "<b>THE POOL PARTY</b>\nAn unofficial fan tribute. Not affiliated with or endorsed by anyone it celebrates.", "More/mo_flm_plaque");
        Folk(g, "Lifeguard Lumbar", "#ff5252", 11, V(cx + 10f, 0, z + 8f), 180f, "More/mo_flm_guard");
        Folk(g, "DJ Pinkbones", "#f48fb1", 16, V(cx, 0, z + 21f), 180f, "More/mo_flm_dj");
        Folk(g, "Party Guest Pam", "#ffd180", 20, V(cx - 10f, 0, z + 18f), 120f, "More/mo_flm_guest");
        Gk(g, "The inflatable flamingo", V(cx + 3f, 0, z + 14f), 0f, "More/mo_flm_float", "spin", PrimitiveType.Capsule, V(0.8f, 0.6f, 1.4f), "GemPink");
        Gk(g, "The cannonball", V(cx - 4f, 0, z + 12f), 0f, "More/mo_flm_splash", "drop", PrimitiveType.Sphere, V(0.7f, 0.7f, 0.7f), "BoneWhite", 5f);
        Gk(g, "The dancing lawn flamingo", V(cx + 6f, 0, z + 22f), 180f, "More/mo_flm_dance", "shake", PrimitiveType.Capsule, V(0.4f, 0.5f, 0.6f), "GemPink");
        report.Add("The Pool Party (the Flamingo room's new wing): a pool, sun loungers, 6 lawn flamingos and a tribute plaque; 3 voiced guests, 3 gags (plus the plaque)");
    }

    static void WingTrahan(Transform root)
    {
        float cx = 2228f, z = 50f;
        var g = Wing(root, "Ryan Trahan Room", 2210f, 2246f, z, 24f, 6f, "Concrete", "Wall2", "Ceiling",
                     "THE ROAD TRIP", new Color(0.3f, 0.7f, 1f), "unofficial fan tribute. not affiliated. a map, a car, and a jar of pennies.");
        Box(g, "Road", V(cx, 0.01f, z + 12f), V(6f, 0.02f, 24f), M("Black"), false);
        for (int k = 0; k < 6; k++) Box(g, "Road line", V(cx, 0.025f, z + 2f + k * 4f), V(0.2f, 0.01f, 2f), M("GlowYellow"), false);
        Box(g, "Diner counter", V(cx - 13f, 0.55f, z + 12f), V(1.2f, 1.1f, 8f), M("CaneRed"));
        Box(g, "Gas pump", V(cx + 12f, 0.9f, z + 6f), V(0.8f, 1.8f, 0.6f), M("GiftRed"));
        Box(g, "Big map", V(cx, 2.6f, z + 23.85f), V(10f, 2.6f, 0.05f), M("Paper"), false);
        var pennies = new List<Vector3>(); for (int k = 0; k < 10; k++) pennies.Add(V(cx - 2f + (k % 2) * 4f, 0.3f, z + 3f + k * 2f));
        Collectables(g, "Pennies", pennies.ToArray(), (t, i) => { DPrim(PrimitiveType.Cylinder, "Penny", t, Vector3.zero, V(0.3f, 0.02f, 0.3f), M("Brass"), Quaternion.Euler(90, 0, 0)); return true; }, V(cx - 7f, 1.6f, z + 0.35f), Vector3.forward);
        Plaque(g, V(cx + 14f, 0, z + 20f), "<b>THE ROAD TRIP</b>\nAn unofficial fan tribute. Not affiliated with or endorsed by anyone it celebrates.", "More/mo_trh_plaque");
        Folk(g, "Waitress Wanda", "#ffab91", 9, V(cx - 14.5f, 0, z + 12f), 90f, "More/mo_trh_waitress");
        Folk(g, "Hitchhiker Hal", "#c5e1a5", 16, V(cx + 5f, 0, z + 16f), 270f, "More/mo_trh_hitch");
        Folk(g, "Gas Station Gil", "#90caf9", 7, V(cx + 13.5f, 0, z + 6f), 270f, "More/mo_trh_gas");
        var car = Gg(g, "The car honks", V(cx, 0, z + 8f), 0f, "More/mo_trh_car", 2.4f, 1.6f);
        var body = Group(car.transform, "Car", Vector3.zero);
        Prim(PrimitiveType.Cube, "Body", body, V(0, 0.6f, 0), V(1.8f, 0.8f, 3.6f), M("GiftBlue"));
        Prim(PrimitiveType.Cube, "Cabin", body, V(0, 1.2f, -0.2f), V(1.6f, 0.6f, 1.8f), M("Glass"));
        Move(car, body, V(0, 0.05f, 0.3f), Vector3.zero, 0.06f, 3f);
        Gk(g, "The penny jar", V(cx - 13f, 0, z + 15f), 90f, "More/mo_trh_jar", "shake", PrimitiveType.Cylinder, V(0.4f, 0.5f, 0.4f), "Glass").target.localPosition = V(0, 1.1f, 0);
        Gk(g, "The map unfolds", V(cx - 6f, 0, z + 22f), 180f, "More/mo_trh_map", "grow", PrimitiveType.Cube, V(1f, 0.7f, 0.04f), "Paper").target.localPosition = V(0, 1f, 0);
        report.Add("The Road Trip (the Ryan Trahan room's new wing): a road, a diner counter, a gas pump, a big map, 10 pennies and a tribute plaque; 3 voiced people, 3 gags (plus the plaque)");
    }

    // ------------------------------------------------------------------------------------------------ the Scream Room

    static void WingScream(Transform root)
    {
        float cx = 2278f, z = 50f;
        var g = Wing(root, "The Scream Room", 2260f, 2296f, z, 22f, 7f, "Rug", "WallGreen", "Ceiling",
                     "THE WHISPER ROOM", new Color(0.6f, 0.8f, 0.7f), "the scream room's quiet neighbour. please use your indoor bones.", 2290f);
        for (int k = 0; k < 8; k++) Box(g, "Soft cushion", V(cx - 14f + k * 4f, 0.25f, z + 10f + (k % 2) * 4f), V(2f, 0.5f, 2f), M(k % 2 == 0 ? "GemWhite" : "WallGreen"));
        for (int k = 0; k < 12; k++) Box(g, "Foam panel", V(cx - 16.5f + k * 3f, 3.5f, z + 21.85f), V(2.6f, 2.6f, 0.15f), M("TurfGrey"), false);
        Folk(g, "Whispering Wilma", "#c8e6c9", 9, V(cx - 6f, 0, z + 6f), 180f, "More/mo_wsp_wilma");
        Folk(g, "Meditating Mort", "#e1bee7", 20, V(cx + 6f, 0.5f, z + 14f), 180f, "More/mo_wsp_mort");
        Folk(g, "The Librarian's Cousin", "#b3e5fc", 11, V(cx + 14f, 0, z + 4f), 220f, "More/mo_wsp_cousin");
        Gk(g, "The pin drop", V(cx, 0, z + 16f), 180f, "More/mo_wsp_pin", "drop", PrimitiveType.Capsule, V(0.03f, 0.08f, 0.03f), "Metal", 2f);
        Gk(g, "The sneeze", V(cx - 12f, 0, z + 18f), 180f, "More/mo_wsp_sneeze", "shake", PrimitiveType.Sphere, V(0.5f, 0.5f, 0.5f), "BoneWhite").target.localPosition = V(0, 1.5f, 0);
        Gk(g, "The shushing sign", V(cx + 12f, 0, z + 18f), 180f, "More/mo_wsp_shush", "flip", PrimitiveType.Cube, V(1.2f, 0.6f, 0.05f), "GlowGreen").target.localPosition = V(0, 1.6f, 0);
        report.Add("The Whisper Room (the Scream Room's new wing): cushions, foam panels, and silence; 3 voiced (quietly) people, 3 gags");
    }

    // ------------------------------------------------------------------------------------------------ the Inventors' Backyard

    static void WingInventors(Transform root)
    {
        float cx = 2330f, z = 56f;
        var g = Wing(root, "The Inventors' Backyard", 2310f, 2350f, z, 26f, 18f, "Concrete", "PixelSky", "Sky",
                     "THE ROCKET GARAGE", new Color(1f, 0.6f, 0.1f), "today's project: space. tomorrow's project: getting back.", 2344f);
        var rocket = V(cx + 10f, 0, z + 16f);
        DPrim(PrimitiveType.Cylinder, "Rocket body", g, rocket + V(0, 4f, 0), V(2f, 4f, 2f), M("CaneWhite"));
        DPrim(PrimitiveType.Sphere, "Nose cone", g, rocket + V(0, 8.5f, 0), V(2f, 2.2f, 2f), M("GlowRed"));
        for (int k = 0; k < 3; k++) DPrim(PrimitiveType.Cube, "Fin", g, rocket + V(Mathf.Cos(k * 2.1f) * 1.2f, 1f, Mathf.Sin(k * 2.1f) * 1.2f), V(0.15f, 2f, 1.2f), M("GlowRed"), Quaternion.Euler(0, -k * 120f, 0));
        Box(g, "Blueprint table", V(cx - 10f, 0.75f, z + 8f), V(4f, 0.1f, 2.4f), M("HeroBlue"));
        Scatter(g, "Spare part", 16, cx - 18f, cx - 2f, z + 14f, z + 24f, PrimitiveType.Cube, V(0.5f, 0.3f, 0.4f), new[] { "Metal", "GlowYellow", "GiftBlue" });
        Folk(g, "Neighbour Nora", "#ffcc80", 2, V(cx - 14f, 0, z + 4f), 160f, "More/mo_inv_neighbour");
        Folk(g, "Mail Carrier Mo", "#64b5f6", 7, V(cx + 2f, 0, z + 4f), 200f, "More/mo_inv_mail");
        Folk(g, "Scout Leader Scapula", "#aed581", 11, V(cx - 4f, 0, z + 20f), 150f, "More/mo_inv_scout");
        var launch = Gk(g, "The rocket test", V(cx + 4f, 0, z + 16f), 90f, "More/mo_inv_rocket", "fly", PrimitiveType.Capsule, V(0.6f, 1f, 0.6f), "GlowRed", 12f);
        Gk(g, "The robot arm", V(cx - 10f, 0, z + 10f), 180f, "More/mo_inv_arm", "flip", PrimitiveType.Cube, V(0.25f, 1.6f, 0.25f), "Metal").target.localPosition = V(0, 0.8f, 0);
        Gk(g, "The blueprint", V(cx - 10f, 0, z + 6f), 0f, "More/mo_inv_plan", "grow", PrimitiveType.Cube, V(1f, 0.02f, 0.7f), "HeroBlue").target.localPosition = V(0, 0.85f, 0);
        report.Add("The Rocket Garage (the Inventors' Backyard's new wing): a rocket on its pad, a blueprint table and spare parts; 3 voiced people, 3 gags");
    }

    // ------------------------------------------------------------------------------------------------ the Notebook School

    static void WingNotebook(Transform root)
    {
        float cx = 2378f, z = 50f;
        var g = Wing(root, "The Notebook School", 2360f, 2396f, z, 22f, 6f, "Paper", "Paper", "Paper",
                     "THE CAFETERIA", new Color(0.2f, 0.2f, 0.6f), "where social standing is decided by where you sit. sit anywhere. it's fine.");
        for (int k = 0; k < 6; k++)
        {
            var t = V(cx - 10f + (k % 3) * 10f, 0, z + 6f + (k / 3) * 8f);
            Box(g, "Lunch table", t + V(0, 0.75f, 0), V(6f, 0.08f, 1.6f), M("Paper"));
            foreach (float bz in new[] { -1.2f, 1.2f }) Box(g, "Bench", t + V(0, 0.45f, bz), V(6f, 0.08f, 0.4f), M("Paper"));
        }
        Box(g, "Lunch line", V(cx, 0.55f, z + 20.5f), V(14f, 1.1f, 1f), M("Metal"));
        Folk(g, "Lunch Lady Lorraine", "#e0e0e0", 9, V(cx - 3f, 0, z + 21.5f), 180f, "More/mo_nbk_lunch");
        Folk(g, "Class Clown Kev", "#ffab40", 16, V(cx + 10f, 0, z + 12f), 200f, "More/mo_nbk_clown");
        Folk(g, "Student Council Sue", "#9575cd", 11, V(cx - 14f, 0, z + 3f), 150f, "More/mo_nbk_council");
        Gk(g, "The food fight", V(cx, 0, z + 10f), 180f, "More/mo_nbk_fight", "fly", PrimitiveType.Sphere, V(0.3f, 0.3f, 0.3f), "GiftGreen", 3f).target.localPosition = V(0, 0.9f, 0);
        Gk(g, "The mystery meat", V(cx + 4f, 0, z + 20f), 180f, "More/mo_nbk_meat", "shake", PrimitiveType.Sphere, V(0.5f, 0.3f, 0.4f), "DoxieBrown").target.localPosition = V(0, 1.15f, 0);
        Gk(g, "The milk carton", V(cx - 10f, 0, z + 14f), 180f, "More/mo_nbk_milk", "spin", PrimitiveType.Cube, V(0.2f, 0.35f, 0.2f), "Milk");
        report.Add("The Cafeteria (the Notebook School's new wing): 6 lunch tables and the lunch line, all drawn in pen; 3 voiced people, 3 gags");
    }

    // ------------------------------------------------------------------------------------------------ the Seabed Town

    static void WingSeabed(Transform root)
    {
        float cx = 2430f, z = 56f;
        var g = Wing(root, "The Seabed Town", 2410f, 2450f, z, 26f, 14f, "PixelSand", "InkBlue", "InkBlue",
                     "THE KELP FOREST", new Color(0.4f, 1f, 0.7f), "tall kelp, jellyfish fields, and a clam with opinions");
        for (int k = 0; k < 22; k++)
        {
            float fx = Mathf.Repeat(k * 0.618f, 1f), fz = Mathf.Repeat(k * 0.382f + 0.2f, 1f);
            DPrim(PrimitiveType.Cylinder, "Kelp", g, V(cx - 18f + fx * 36f, 4f + (k % 3), z + 3f + fz * 21f), V(0.3f, 4f + (k % 3), 0.3f), M(k % 2 == 0 ? "GiftGreen" : "PixelGreenDark"));
        }
        for (int k = 0; k < 12; k++) DPrim(PrimitiveType.Sphere, "Bubble", g, V(cx - 16f + k * 3f, 2f + (k % 4) * 2.5f, z + 12f), Vector3.one * 0.3f, M("Glass"));
        Folk(g, "Shelly the Snail Racer", "#ffb74d", 16, V(cx - 8f, 0, z + 6f), 160f, "More/mo_sea_snail");
        Folk(g, "Deep-Sea Diver Dex", "#90a4ae", 7, V(cx + 10f, 0, z + 18f), 200f, "More/mo_sea_diver");
        Folk(g, "Coral the Reef Guide", "#f06292", 2, V(cx + 2f, 0, z + 4f), 180f, "More/mo_sea_guide");
        Gk(g, "The jellyfish", V(cx - 4f, 0, z + 14f), 0f, "More/mo_sea_jelly", "fly", PrimitiveType.Sphere, V(0.8f, 0.5f, 0.8f), "GemPink", 4f).target.localPosition = V(0, 1.5f, 0);
        Gk(g, "The giant clam", V(cx + 12f, 0, z + 8f), 180f, "More/mo_sea_clam", "flip", PrimitiveType.Sphere, V(1.6f, 0.4f, 1.2f), "GemWhite");
        Gk(g, "The treasure bubbles", V(cx - 14f, 0, z + 20f), 180f, "More/mo_sea_treasure", "pop", PrimitiveType.Cube, V(1f, 0.6f, 0.7f), "GlowGold", 0.6f);
        report.Add("The Kelp Forest (the Seabed Town's new wing): 22 kelp stalks, bubbles; 3 voiced people, 3 gags");
    }

    // ------------------------------------------------------------------------------------------------ Battle Island

    static void WingBattle(Transform root)
    {
        float cx = 2485f, z = 70f;
        var g = Wing(root, "Battle Island", 2460f, 2510f, z, 30f, 22f, "PixelGreen", "PixelSky", "Sky",
                     "THE STORM'S EDGE", new Color(0.7f, 0.4f, 1f), "the far side of the island, right where the storm stops. mostly.");
        Box(g, "Storm wall", V(cx, 11f, z + 29f), V(50f, 22f, 0.3f), M("PortalPurple"), false);
        for (int k = 0; k < 8; k++) Box(g, "Build ramp", V(cx - 20f + k * 5.5f, 0.8f + (k % 3) * 0.8f, z + 14f), V(3f, 0.2f, 3f), M("DeskWood"));
        Scatter(g, "Loot crate", 8, cx - 20f, cx + 20f, z + 4f, z + 24f, PrimitiveType.Cube, V(1f, 0.8f, 0.8f), new[] { "GlowGold", "GiftBlue" });
        var loot = new List<Vector3>(); for (int k = 0; k < 8; k++) loot.Add(V(cx - 18f + k * 5f, 0.8f, z + 20f));
        Collectables(g, "Shiny loot", loot.ToArray(), (t, i) => { DPrim(PrimitiveType.Cube, "Loot", t, Vector3.zero, V(0.4f, 0.4f, 0.4f), M(i % 2 == 0 ? "GlowGold" : "GemPurple"), Quaternion.Euler(0, 45, 0)); return true; }, V(cx - 7f, 1.6f, z + 0.35f), Vector3.forward);
        Folk(g, "Builder Bea", "#ffca28", 7, V(cx - 10f, 0, z + 8f), 170f, "More/mo_bat_builder");
        Folk(g, "Emote Eddie", "#4fc3f7", 16, V(cx + 10f, 0, z + 8f), 190f, "More/mo_bat_emote");
        Folk(g, "The Loot Goblin", "#ab47bc", 15, V(cx + 18f, 0, z + 24f), 220f, "More/mo_bat_goblin");
        var drop = Gk(g, "The supply drop", V(cx, 0, z + 18f), 0f, "More/mo_bat_drop", "drop", PrimitiveType.Cube, V(1.2f, 1.2f, 1.2f), "GiftBlue", 12f);
        Prim(PrimitiveType.Sphere, "Balloon", drop.target, V(0, 2.2f, 0), V(1.4f, 1.6f, 1.4f), M("GlowRed"));
        Gk(g, "The instant wall", V(cx - 16f, 0, z + 22f), 180f, "More/mo_bat_wall", "pop", PrimitiveType.Cube, V(3f, 3f, 0.3f), "DeskWood", 3f);
        Gk(g, "The storm creeps in", V(cx + 14f, 0, z + 27f), 180f, "More/mo_bat_storm", "slide", PrimitiveType.Cube, V(4f, 5f, 0.3f), "PortalPurple", 3f).target.localPosition = V(0, 0, 0.5f);
        report.Add("The Storm's Edge (Battle Island's new wing): the storm wall, build ramps, loot crates and 8 pieces of loot; 3 voiced people, 3 gags");
    }

    // ------------------------------------------------------------------------------------------------ Site Bone (SCP-style, all-new anomalies)

    static void WingScp(Transform root)
    {
        float cx = 2540f, z = 56f;
        var g = Wing(root, "The Containment Facility", 2520f, 2560f, z, 24f, 6f, "LabFloor", "LabGrey", "LabWhite",
                     "CONTAINMENT WING B", new Color(1f, 0.85f, 0.2f), "three more anomalies. all safe. all extremely safe. stop asking.");
        for (int k = 0; k < 3; k++)
        {
            float x = cx - 12f + k * 12f;
            Box(g, "Cell glass", V(x, 1.5f, z + 16f), V(8f, 3f, 0.1f), M("Glass"), false);
            Box(g, "Cell back", V(x, 1.5f, z + 23.8f), V(8f, 3f, 0.2f), M("LabWhite"));
            Box(g, "Cell divider", V(x + 4f, 1.5f, z + 20f), V(0.2f, 3f, 8f), M("LabGrey"));
            Text("Cell label", g, V(x, 3.3f, z + 15.94f), Face(Vector3.back), V2(6f, 0.5f), 0.3f, "<b>SCB-" + (950 + k * 7) + "</b>").color = new Color(0.1f, 0.1f, 0.1f);
        }
        Folk(g, "Researcher Rhomboid", "#e0f2f1", 9, V(cx - 14f, 0, z + 6f), 180f, "More/mo_scb_researcher");
        Folk(g, "Security Chief Stirrup", "#455a64", 11, V(cx + 14f, 0, z + 6f), 180f, "More/mo_scb_security");
        Folk(g, "Test Volunteer Vic", "#ff8a65", 20, V(cx, 0, z + 8f), 180f, "More/mo_scb_volunteer");
        var a = Gk(g, "SCB-950, the polite door", V(cx - 12f, 0, z + 20f), 180f, "More/mo_scb_door", "flip", PrimitiveType.Cube, V(1.2f, 2.2f, 0.1f), "DoorWood");
        a.rotateBy = V(0, 90f, 0);
        Gk(g, "SCB-957, the counting cube", V(cx, 0, z + 20f), 180f, "More/mo_scb_cube", "spin", PrimitiveType.Cube, V(0.8f, 0.8f, 0.8f), "AlienGlow");
        Gk(g, "SCB-964, the extra stair", V(cx + 12f, 0, z + 20f), 180f, "More/mo_scb_stair", "pop", PrimitiveType.Cube, V(1.4f, 0.3f, 0.8f), "LabGrey", 0.5f);
        report.Add("Containment Wing B (Site Bone's new wing): 3 cells with 3 brand-new anomalies (SCB-950, 957, 964); 3 voiced staff, 3 gags");
    }

    // ------------------------------------------------------------------------------------------------ the Interdimensional Garage

    static void WingGarage(Transform root)
    {
        float cx = 2588f, z = 50f;
        var g = Wing(root, "The Interdimensional Garage", 2570f, 2606f, z, 22f, 6f, "Concrete", "Wall2", "Ceiling",
                     "THE INVENTION SHED", new Color(0.4f, 1f, 0.4f), "half-finished gadgets. do not press anything. everything is a button.");
        for (int k = 0; k < 3; k++) Box(g, "Workbench", V(cx - 12f + k * 12f, 0.5f, z + 20f), V(5f, 1f, 1.6f), M("DeskWood"));
        Scatter(g, "Gadget bit", 18, cx - 16f, cx + 16f, z + 19.5f, z + 20.5f, PrimitiveType.Cube, V(0.3f, 0.3f, 0.3f), new[] { "PortalGreen", "Metal", "GlowYellow" }, 1.15f);
        GreenPortal(g, V(cx + 14f, 0, z + 10f), 270f);
        Folk(g, "Nosy Neighbour Norbert", "#ffcc80", 2, V(cx - 12f, 0, z + 6f), 150f, "More/mo_inv2_neighbour");
        Folk(g, "Zorp the Alien Tourist", "#69f0ae", 16, V(cx + 8f, 0, z + 12f), 220f, "More/mo_inv2_alien");
        Folk(g, "Helpful Robot H-3", "#b0bec5", 7, V(cx, 0, z + 17f), 180f, "More/mo_inv2_robot");
        Gk(g, "The shrink ray", V(cx - 12f, 0, z + 18f), 180f, "More/mo_inv2_ray", "grow", PrimitiveType.Capsule, V(0.25f, 0.4f, 0.25f), "PortalGreen").target.localPosition = V(0, 1.1f, 0);
        Gk(g, "The gravity flipper", V(cx, 0, z + 12f), 0f, "More/mo_inv2_gravity", "flip", PrimitiveType.Cube, V(1f, 0.3f, 1f), "Metal");
        Gk(g, "The snack teleporter", V(cx + 12f, 0, z + 18f), 180f, "More/mo_inv2_snack", "pop", PrimitiveType.Sphere, V(0.3f, 0.3f, 0.3f), "GlowOrange", 0.4f).target.localPosition = V(0, 0.75f, 0);
        report.Add("The Invention Shed (the Interdimensional Garage's new wing): 3 workbenches of gadgets and another portal; 3 voiced people, 3 gags");
    }

    // ------------------------------------------------------------------------------------------------ the Water Park

    static void WingWater(Transform root)
    {
        float cx = 2750f, z = 90f;
        var g = Wing(root, "Splashbone Water Park", 2710f, 2790f, z, 30f, 30f, "Concrete", "PixelSky", "Sky",
                     "THE WAVE POOL AND LAZY RIVER", new Color(0.2f, 0.8f, 1f), "float along. nobody's in a hurry. nobody's been in a hurry since 1970.");
        Box(g, "Wave pool", V(cx - 15f, 0.03f, z + 16f), V(30f, 0.04f, 20f), M("PoolBlue"), false);
        Box(g, "Lazy river", V(cx + 22f, 0.03f, z + 16f), V(6f, 0.04f, 24f), M("PoolGreen"), false);
        for (int k = 0; k < 6; k++) Box(g, "Beach chair", V(cx + 4f + (k % 3) * 3f, 0.3f, z + 4f + (k / 3) * 3f), V(0.9f, 0.3f, 2f), M(k % 2 == 0 ? "GlowYellow" : "CaneWhite"));
        DPrim(PrimitiveType.Cylinder, "Umbrella pole", g, V(cx + 8f, 1.4f, z + 9f), V(0.1f, 1.4f, 0.1f), M("CaneWhite"));
        DPrim(PrimitiveType.Cylinder, "Umbrella", g, V(cx + 8f, 2.8f, z + 9f), V(3f, 0.1f, 3f), M("CaneRed"));
        Folk(g, "Lifeguard Lola", "#ff7043", 11, V(cx - 2f, 0, z + 6f), 200f, "More/mo_wat_guard");
        Folk(g, "Snack Bar Sid", "#ffd54f", 9, V(cx + 14f, 0, z + 3f), 180f, "More/mo_wat_snack");
        Folk(g, "Float Rental Frank", "#4dd0e1", 7, V(cx + 18f, 0, z + 26f), 180f, "More/mo_wat_float");
        var wave = Gk(g, "The wave machine", V(cx - 15f, 0, z + 26f), 180f, "More/mo_wat_wave", "slide", PrimitiveType.Cube, V(28f, 0.8f, 1f), "PoolBlue", 18f);
        var tube = Gk(g, "The drifting tube", V(cx + 22f, 0, z + 6f), 0f, "More/mo_wat_tube", "slide", PrimitiveType.Cylinder, V(1.4f, 0.2f, 1.4f), "GlowOrange", 18f);
        Prim(PrimitiveType.Sphere, "Rider", tube.target, V(0, 0.5f, 0), Vector3.one * 0.4f, M("BoneWhite"));
        var bucket = Gk(g, "The tipping bucket", V(cx - 30f, 0, z + 6f), 180f, "More/mo_wat_bucket", "flip", PrimitiveType.Cylinder, V(1.6f, 1f, 1.6f), "GlowYellow");
        bucket.target.localPosition = V(0, 6f, 0); bucket.rotateBy = V(0, 0, 120f);
        DPrim(PrimitiveType.Cylinder, "Bucket tower", g, V(cx - 30f, 3f, z + 6.8f), V(0.4f, 3f, 0.4f), M("Metal"));
        report.Add("The Wave Pool and Lazy River (the water park's new wing): a wave pool, a lazy river, beach chairs and a tipping bucket; 3 voiced people, 3 gags");
    }

    // ------------------------------------------------------------------------------------------------ the Chris and Tripp Show

    static void WingCT(Transform root)
    {
        float cx = 2840f, z = 60f;
        var g = Wing(root, "The Chris and Tripp Show", 2810f, 2870f, z, 24f, 6f, "Floor2", "WallDark", "Ceiling",
                     "BEHIND THE SCENES", new Color(1f, 0.9f, 0.3f), "the crew's side of the show. lights, camera, skeletons.");
        for (int k = 0; k < 3; k++)
        {
            var cam = V(cx - 16f + k * 16f, 0, z + 10f);
            DPrim(PrimitiveType.Cylinder, "Tripod", g, cam + V(0, 0.7f, 0), V(0.15f, 0.7f, 0.15f), M("Black"));
            DPrim(PrimitiveType.Cube, "Camera", g, cam + V(0, 1.55f, 0), V(0.5f, 0.35f, 0.7f), M("Black"));
            DPrim(PrimitiveType.Cylinder, "Light stand", g, cam + V(3f, 1.2f, 4f), V(0.08f, 1.2f, 0.08f), M("Metal"));
            DPrim(PrimitiveType.Cube, "Softbox", g, cam + V(3f, 2.5f, 4f), V(0.8f, 0.6f, 0.4f), M("GlowWarm"));
        }
        Box(g, "Green room couch", V(cx + 20f, 0.4f, z + 20f), V(4f, 0.8f, 1.2f), M("GiftGreen"));
        Box(g, "Snack table", V(cx - 20f, 0.45f, z + 20f), V(3f, 0.9f, 1.2f), M("CaneWhite"));
        Scatter(g, "Snack", 8, cx - 21.3f, cx - 18.7f, z + 19.7f, z + 20.3f, PrimitiveType.Sphere, V(0.2f, 0.2f, 0.2f), new[] { "GlowOrange", "CornYellow" }, 1.0f);
        Folk(g, "Camera Op Carla", "#90caf9", 7, V(cx - 16f, 0, z + 8.5f), 0f, "More/mo_ctb_camera");
        Folk(g, "Director Dirk", "#ffcc80", 11, V(cx, 0, z + 6f), 180f, "More/mo_ctb_director");
        Folk(g, "Superfan Sal", "#f48fb1", 16, V(cx + 18f, 0, z + 17f), 200f, "More/mo_ctb_fan");
        var clap = Gk(g, "The clapperboard", V(cx - 6f, 0, z + 14f), 180f, "More/mo_ctb_clap", "flip", PrimitiveType.Cube, V(0.6f, 0.08f, 0.04f), "Black");
        clap.target.localPosition = V(0, 1.5f, 0); clap.rotateBy = V(0, 0, 35f); clap.inTime = 0.1f;
        Prim(PrimitiveType.Cube, "Slate", g, V(cx - 6f, 1.25f, z + 14f), V(0.6f, 0.45f, 0.04f), M("Black"));
        Gk(g, "The boom mic", V(cx + 6f, 0, z + 14f), 180f, "More/mo_ctb_boom", "drop", PrimitiveType.Capsule, V(0.15f, 0.3f, 0.15f), "TurfGrey", 2.5f);
        Gk(g, "The blooper reel", V(cx + 14f, 0, z + 22f), 180f, "More/mo_ctb_blooper", "shake", PrimitiveType.Cube, V(1.6f, 1f, 0.1f), "DarkGlowCyan").target.localPosition = V(0, 1.2f, 0);
        report.Add("Behind the Scenes (the Chris and Tripp Show's new wing): 3 cameras with lights, the green room couch and a snack table; 3 voiced crew, 3 gags");
    }
}
#endif
