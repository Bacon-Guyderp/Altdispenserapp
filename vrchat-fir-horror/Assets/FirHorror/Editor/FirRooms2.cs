#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// The new rooms, part 2: four more rooms (original takes: a singing-monster island, a shifting government bureau,
/// a dark lakeside town, a colour-coded hero squad), and a wing for every one of the nineteen new rooms.
/// </summary>
public static partial class FirCheckBuilder
{
    /// <summary>A round cartoon monster made of shapes: a body, eyes, a mouth, optional horns or feet.</summary>
    static Transform Blob(Transform g, string name, Vector3 at, float yaw, float s, string mat, int style)
    {
        var b = Group(g, name, at);
        b.localRotation = Quaternion.Euler(0, yaw, 0);
        Prim(style == 2 ? PrimitiveType.Cube : PrimitiveType.Sphere, "Body", b, V(0, 0.8f * s, 0), V(1.4f * s, 1.5f * s, 1.3f * s), M(mat));
        int eyes = style == 1 ? 1 : 2;
        for (int k = 0; k < eyes; k++)
        {
            float ex = eyes == 1 ? 0f : (k == 0 ? -0.28f : 0.28f) * s;
            Prim(PrimitiveType.Sphere, "Eye", b, V(ex, 1.15f * s, 0.6f * s), Vector3.one * (eyes == 1 ? 0.45f : 0.3f) * s, M("CaneWhite"));
            Prim(PrimitiveType.Sphere, "Pupil", b, V(ex, 1.15f * s, 0.72f * s), Vector3.one * (eyes == 1 ? 0.2f : 0.13f) * s, M("Black"));
        }
        Prim(PrimitiveType.Sphere, "Mouth", b, V(0, 0.6f * s, 0.62f * s), V(0.6f * s, 0.25f * s, 0.12f * s), M("Black"));
        if (style == 3) foreach (float hx in new[] { -0.4f, 0.4f }) Prim(PrimitiveType.Capsule, "Horn", b, V(hx * s, 1.6f * s, 0), V(0.15f * s, 0.3f * s, 0.15f * s), M("CaneWhite"));
        foreach (float fx in new[] { -0.35f, 0.35f }) Prim(PrimitiveType.Sphere, "Foot", b, V(fx * s, 0.08f * s, 0.2f * s), V(0.4f * s, 0.18f * s, 0.5f * s), M(mat));
        return b;
    }

    // ================================================================================================ 16 Monster Choir Island

    static void RoomMonsters(Transform root)
    {
        float cx = 4085f;
        var g = Room(root, "Monster Choir Island", 4060f, 4110f, 70f, 20f, "PixelGreen", "PixelSky", "Sky",
                     "MONSTER CHOIR ISLAND", new Color(0.4f, 0.95f, 0.5f), "every monster sings one part. press them all and the island sings the whole song.", "MONSTER ISLAND");
        Box(g, "Sea", V(cx, 0.02f, 66f), V(50f, 0.04f, 8f), M("PoolBlue"), false);
        Box(g, "Beach", V(cx, 0.01f, 60.5f), V(50f, 0.02f, 3f), M("PixelSand"), false);
        // the singing stage: four pads in a ring; each monster is a gag, so pressing it makes it sing its part
        var parts = new[] {
            new { name = "Thumpkin the drum blob", at = V(4073f, 0, 42f), mat = RM("MonsterOrange", 1f, 0.55f, 0.15f), style = 0, clip = "msm_thump", kind = "grow" },
            new { name = "Hootle the whistler", at = V(4081f, 0, 48f), mat = RM("MonsterTeal", 0.2f, 0.75f, 0.7f), style = 1, clip = "msm_hoot", kind = "shake" },
            new { name = "Bassalt the singing rock", at = V(4089f, 0, 48f), mat = M("Stone"), style = 2, clip = "msm_bass", kind = "shake" },
            new { name = "Fizzle the bubble singer", at = V(4097f, 0, 42f), mat = RM("MonsterPink", 1f, 0.45f, 0.75f), style = 3, clip = "msm_fizz", kind = "spin" } };
        foreach (var p in parts)
        {
            DPrim(PrimitiveType.Cylinder, "Singing pad", g, p.at + V(0, 0.08f, 0), V(3f, 0.08f, 3f), M("GlowGold"));
            var gag = RG(g, p.name, p.at, 180f, p.clip, p.kind, PrimitiveType.Sphere, V(0.05f, 0.05f, 0.05f), "Black");
            if (p.kind == "spin") gag.target.localPosition = Vector3.zero;
            Blob(gag.target, p.name + " body", Vector3.zero, 0f, 1.1f, p.mat.name.Replace("Check_", ""), p.style);
        }
        Box(g, "Castle", V(4100f, 3f, 58f), V(8f, 6f, 4f), M("GemPurple"));
        foreach (float tx in new[] { 4096.5f, 4103.5f }) DPrim(PrimitiveType.Cylinder, "Castle tower", g, V(tx, 4.5f, 58f), V(2f, 4.5f, 2f), M("GemPurple"));
        foreach (float px in new[] { 4067f, 4073f }) DPrim(PrimitiveType.Cylinder, "Breeding pillar", g, V(px, 2.5f, 56f), V(1f, 2.5f, 1f), M("GemPink"));
        Box(g, "Breeding arch", V(4070f, 5.2f, 56f), V(7f, 0.6f, 1.2f), M("GemPink"), false);
        for (int k = 0; k < 4; k++) DPrim(PrimitiveType.Sphere, "Monster egg", g, V(4064f + k * 2f, 0.5f, 30f + (k % 2) * 1.5f), V(1f, 1.3f, 1f), M(new[] { "GlowYellow", "MonsterTeal", "MonsterPink", "GemWhite" }[k]));
        Scatter(g, "Gold coin", 14, 4066f, 4104f, 28f, 36f, PrimitiveType.Cylinder, V(0.4f, 0.04f, 0.4f), new[] { "GlowGold" }, 0.05f);
        RF(g, "Island Keeper Kiri", "#ffcc80", 18, V(4085f, 0, 37f), 180f, "msm_keeper");
        RF(g, "Egg Breeder Bo", "#a5d6a7", 9, V(4070f, 0, 52f), 180f, "msm_breeder");
        RF(g, "Coin Collector Cass", "#fff59d", 16, V(4100f, 0, 32f), 200f, "msm_coins");
        report.Add("Monster Choir Island: four singing monsters on their pads (a drum blob, a whistler, a singing rock, a bubble singer: press each for its part), a castle, a breeding arch, eggs, coins and the sea; 3 voiced people, 4 gags");
    }

    // ================================================================================================ 17 the Shifting Bureau

    static void RoomBureau(Transform root)
    {
        float cx = 4155f;
        RM("Brutal", 0.6f, 0.58f, 0.55f, 0f, "concrete");
        var g = Room(root, "The Shifting Bureau", 4130f, 4180f, 70f, 14f, "Concrete", "Brutal", "Brutal",
                     "THE SHIFTING BUREAU", new Color(0.9f, 0.15f, 0.15f), "a government building that is bigger on the inside, and rearranges itself when nobody's looking", "SHIFTING BUREAU");
        for (int ix = 0; ix < 4; ix++) for (int iz = 0; iz < 3; iz++)
        {
            float px = 4136f + ix * 12.7f, pz = 32f + iz * 13f;
            if (Mathf.Abs(px - cx) < 5f && pz < 40f) continue;
            Box(g, "Pillar", V(px, 7f, pz), V(1.8f, 14f, 1.8f), M("Brutal"));
        }
        for (int k = 0; k < 6; k++) Box(g, "Floating desk", V(4140f + k * 6f, 6f + Mathf.Sin(k) * 2f, 60f + (k % 2) * 3f), V(1.6f, 0.8f, 0.9f), M("DeskWood"), false).transform.localRotation = Quaternion.Euler(k * 11f, k * 37f, k * 7f);
        for (int k = 0; k < 8; k++) Box(g, "Filing cabinet", V(4131f, 0.75f, 28f + k * 1.1f), V(0.8f, 1.5f, 1f), M("Metal"));
        DPrim(PrimitiveType.Cube, "The black pyramid", g, V(cx, 9f, 56f), V(3f, 3f, 3f), M("Black"), Quaternion.Euler(45f, 0, 45f));
        for (int k = 0; k < 20; k++) Prim(PrimitiveType.Sphere, "Red hum", g, V(4134f + Mathf.Repeat(k * 0.618f, 1f) * 42f, 1f + Mathf.Repeat(k * 0.382f, 1f) * 10f, 24f + Mathf.Repeat(k * 0.73f, 1f) * 44f), Vector3.one * 0.12f, M("GlowRed"));
        Box(g, "Director's desk", V(cx, 0.45f, 64f), V(3f, 0.9f, 1.4f), M("DeskWood"));
        Box(g, "Phone table", V(4172f, 0.45f, 36f), V(1f, 0.9f, 1f), M("DeskWood"));
        Text("Motto", g, V(cx, 10f, 69.85f), Face(Vector3.back), V2(30f, 1.2f), 0.9f, "<b>WHERE THE STRANGE IS FILED</b>").color = new Color(0.9f, 0.9f, 0.85f);
        RF(g, "Director Hale", "#90a4ae", 3, V(cx, 0, 62f), 180f, "bur_director");
        RF(g, "Janitor Toivo", "#a1887f", 20, V(4140f, 0, 44f), 120f, "bur_janitor");
        Hat("Janitor Toivo", PrimitiveType.Cylinder, V(0, 0.12f, 0), V(0.3f, 0.05f, 0.3f), "TrimDark");
        RF(g, "Researcher Dr Pell", "#eceff1", 7, V(4170f, 0, 50f), 240f, "bur_researcher");
        var phone = RG(g, "The red phone", V(4172f, 0, 36f), 270f, "bur_phone", "shake", PrimitiveType.Cube, V(0.4f, 0.2f, 0.3f), "GlowRed");
        phone.target.localPosition = V(0, 0.95f, 0);
        RG(g, "The floating chair", V(4146f, 0, 30f), 180f, "bur_chair", "fly", PrimitiveType.Cube, V(0.6f, 1f, 0.6f), "Seat", 3f);
        var duck = RG(g, "The Object of Power", V(4163f, 0, 30f), 180f, "bur_duck", "spin", PrimitiveType.Sphere, V(0.4f, 0.35f, 0.5f), "GlowYellow");
        Prim(PrimitiveType.Sphere, "Duck head", duck.target, V(0, 0.55f, 0.15f), Vector3.one * 0.25f, M("GlowYellow"));
        RG(g, "The shifting wall", V(4150f, 0, 50f), 90f, "bur_wall", "slide", PrimitiveType.Cube, V(4f, 4f, 0.4f), "Brutal", 3f);
        report.Add("The Shifting Bureau: a brutalist government building (concrete pillars, floating desks, filing cabinets, a black pyramid hanging in the air, a red hum) with a red phone that rings; 3 voiced people, 4 gags");
    }

    // ================================================================================================ 18 the Dark Lake

    static void RoomDarkLake(Transform root)
    {
        float cx = 4230f;
        var g = Room(root, "The Dark Lake", 4200f, 4260f, 80f, 25f, "DarkGround", "NightSky", "NightSky",
                     "THE DARK LAKE", new Color(0.85f, 0.85f, 1f), "a lakeside town at night. a writer who lost a week. keep to the light.", "DARK LAKE");
        Box(g, "The lake", V(cx, 0.02f, 68f), V(60f, 0.04f, 24f), RM("DarkWater", 0.05f, 0.08f, 0.14f, 0.1f), false);
        Box(g, "Island", V(4242f, 0.15f, 70f), V(8f, 0.3f, 6f), M("DarkGround"));
        Box(g, "Island cabin", V(4242f, 1.6f, 70f), V(4f, 3f, 3f), M("DoorWood"), false);
        Prim(PrimitiveType.Sphere, "Cabin window", g, V(4242f, 1.8f, 68.45f), V(0.6f, 0.5f, 0.05f), M("GlowWarm"));
        House(g, "The lakeside diner", V(4210f, 0, 40f), V(9f, 4f, 7f), "CaneWhite", "CaneRed", "LAKESIDE DINER", Vector3.right);
        Box(g, "Diner glow", V(4214.6f, 1.6f, 40f), V(0.05f, 1f, 6f), M("GlowWarm"), false);
        DPrim(PrimitiveType.Cylinder, "Lighthouse", g, V(4254f, 6f, 56f), V(2.4f, 6f, 2.4f), M("CaneWhite"));
        DPrim(PrimitiveType.Cylinder, "Lighthouse band", g, V(4254f, 8f, 56f), V(2.45f, 0.6f, 2.45f), M("CaneRed"));
        for (int k = 0; k < 5; k++)
        {
            var lp = V(4220f + k * 8f, 0, 52f);
            if (Mathf.Abs(lp.x - cx) < 3f) lp.x += 4f;
            DPrim(PrimitiveType.Cylinder, "Street lamp", g, lp + V(0, 2f, 0), V(0.12f, 2f, 0.12f), M("Black"));
            Prim(PrimitiveType.Sphere, "Lamp", g, lp + V(0, 4.1f, 0), Vector3.one * 0.5f, M("GlowWarm"));
            DPrim(PrimitiveType.Cylinder, "Pool of light", g, lp + V(0, 0.01f, 0), V(3.5f, 0.005f, 3.5f), RM("LampPool", 0.9f, 0.85f, 0.6f, 0.3f));
        }
        for (int k = 0; k < 6; k++) DPrim(PrimitiveType.Capsule, "Shadow figure", g, V(4203f + k * 2.5f, 1f, 60f + (k % 2) * 2f), V(0.6f, 1f, 0.5f), RM("Shadow", 0.02f, 0.02f, 0.04f));
        for (int k = 0; k < 8; k++) { float px = 4202f + k * 7.5f; if (Mathf.Abs(px - cx) < 4f) continue; DPrim(PrimitiveType.Cylinder, "Dark pine", g, V(px, 4f, 78f), V(1f, 4f, 1f), M("TrimDark")); }
        var pages = new List<Vector3>(); for (int k = 0; k < 10; k++) pages.Add(V(4204f + (k % 5) * 12f, 0.7f, 28f + (k / 5) * 22f + (k % 2) * 3f));
        Collectables(g, "Manuscript pages", pages.ToArray(), (t, i) => { Prim(PrimitiveType.Cube, "Page", t, Vector3.zero, V(0.3f, 0.02f, 0.4f), M("Paper")); return true; }, V(4222f, 1.6f, 20.15f), Vector3.forward);
        RF(g, "Novelist Ezra Vance", "#cfd8dc", 10, V(4236f, 0, 44f), 200f, "lak_writer");
        RF(g, "Waitress Rosa", "#f48fb1", 9, V(4216f, 0, 44f), 90f, "lak_waitress");
        RF(g, "Night Radio Host Ned", "#ffe082", 3, V(4250f, 0, 36f), 220f, "lak_radio");
        RF(g, "Park Ranger Lou", "#a5d6a7", 16, V(4250f, 0, 50f), 220f, "lak_ranger");
        Hat("Park Ranger Lou", PrimitiveType.Cylinder, V(0, 0.12f, 0), V(0.42f, 0.05f, 0.42f), "Trunk");
        var flash = RG(g, "The flashlight", V(4226f, 0, 34f), 0f, "lak_flash", "grow", PrimitiveType.Cylinder, V(0.15f, 0.3f, 0.15f), "Black");
        flash.target.localPosition = V(0, 1f, 0);
        Prim(PrimitiveType.Cylinder, "Beam", flash.target, V(0, 0.2f, 1.5f), V(0.6f, 1.5f, 0.6f), RM("Beam", 1f, 0.95f, 0.7f, 0.6f)).transform.localRotation = Quaternion.Euler(90, 0, 0);
        RG(g, "The shadow figure", V(4206f, 0, 56f), 180f, "lak_shadow", "pop", PrimitiveType.Capsule, V(0.8f, 2.2f, 0.6f), "Shadow", 2.4f);
        var tw = RG(g, "The typewriter", V(4240f, 0, 30f), 180f, "lak_type", "shake", PrimitiveType.Cube, V(0.6f, 0.25f, 0.5f), "Black");
        tw.target.localPosition = V(0, 0.9f, 0);
        Box(g, "Typewriter desk", V(4240f, 0.4f, 30f), V(1.4f, 0.8f, 0.8f), M("DeskWood"));
        var lamp = RG(g, "The lighthouse lamp", V(4251f, 0, 52f), 0f, "lak_lamp", "spin", PrimitiveType.Cube, V(0.3f, 0.3f, 3f), "Beam");
        lamp.target.localPosition = V(0, 12f, 4f);
        report.Add("The Dark Lake: a lakeside town at night (a black lake with a cabin on an island, the diner, a lighthouse, street lamps, shadow figures, dark pines), 10 manuscript pages to find; 4 voiced people, 4 gags");
    }

    // ================================================================================================ 19 the Spectrum Squad

    static void RoomSpectrum(Transform root)
    {
        float cx = 4305f;
        var g = Room(root, "The Spectrum Squad", 4280f, 4330f, 70f, 20f, "LabFloor", "PixelSky", "Sky",
                     "THE SPECTRUM SQUAD", new Color(1f, 0.2f, 0.2f), "five teenagers, five colours, one giant robot, and a lot of clay goons", "SPECTRUM SQUAD");
        Box(g, "Quarry", V(4318f, 0.01f, 55f), V(24f, 0.02f, 30f), M("PixelSand"), false);
        for (int k = 0; k < 5; k++) DPrim(PrimitiveType.Sphere, "Quarry rock", g, V(4310f + k * 4.5f, 0.6f, 67f - (k % 2) * 2f), V(3f, 2f, 2.5f), M("Stone"));
        // the command centre: consoles, a tube with the mentor in it, alarm lights
        Box(g, "Console", V(4288f, 0.6f, 50f), V(1.2f, 1.2f, 10f), M("LabGrey"));
        for (int k = 0; k < 8; k++) Box(g, "Console light", V(4288.65f, 1.0f, 46f + k * 1.1f), V(0.05f, 0.15f, 0.3f), M(new[] { "GlowRed", "GlowGreen", "GiftBlue", "GlowYellow" }[k % 4]), false);
        DPrim(PrimitiveType.Cylinder, "Mentor tube", g, V(4290f, 3f, 62f), V(2.4f, 3f, 2.4f), M("Glass"));
        Prim(PrimitiveType.Sphere, "Mentor", g, V(4290f, 3.4f, 62f), V(1.2f, 1.5f, 1.1f), M("DarkGlowCyan"));
        string[] col = { "GlowRed", "GiftBlue", "GlowYellow", "GiftGreen", "GemPink" };
        // the giant robot: five vehicles stacked into one (a statue in the quarry)
        var bot = Group(g, "The Spectrum Megabot", V(4320f, 0, 58f));
        Box(bot, "Legs (green and blue)", V(-1.2f, 3f, 0), V(1.8f, 6f, 2f), M(col[3]), false);
        Box(bot, "Leg (blue)", V(1.2f, 3f, 0), V(1.8f, 6f, 2f), M(col[1]), false);
        Box(bot, "Body (red)", V(0, 8f, 0), V(4.6f, 4f, 2.6f), M(col[0]), false);
        Box(bot, "Arm (yellow)", V(-3f, 8f, 0), V(1.4f, 4.5f, 1.6f), M(col[2]), false);
        Box(bot, "Arm (pink)", V(3f, 8f, 0), V(1.4f, 4.5f, 1.6f), M(col[4]), false);
        Box(bot, "Head", V(0, 11f, 0), V(1.8f, 1.8f, 1.8f), M("Metal"), false);
        Box(bot, "Visor", V(0, 11.1f, -0.92f), V(1.4f, 0.4f, 0.05f), M("GlowYellow"), false);
        for (int k = 0; k < 6; k++) Mini(g, "Clay goon " + k, V(4310f + (k % 3) * 3f, 0, 44f + (k / 3) * 3f), 200f, 0.9f, "#9e9e9e");
        string[] who = { "Squad Red Riley", "Squad Blue Bea", "Squad Yellow Yusuf", "Squad Green Gil", "Squad Pink Pia" };
        string[] hex = { "#ef5350", "#42a5f5", "#ffee58", "#66bb6a", "#f06292" };
        string[] clip = { "spe_red", "spe_blue", "spe_yellow", "spe_green", "spe_pink" };
        for (int k = 0; k < 5; k++)
        {
            RF(g, who[k], hex[k], new[] { 16, 18, 21, 10, 9 }[k], V(4293f + k * 3.5f, 0, 34f + (k % 2) * 1.5f), 180f, clip[k]);
            if (rigs.TryGetValue(who[k], out var r)) { Prim(PrimitiveType.Sphere, "Helmet", r[2], V(0, 0.02f, 0), V(0.33f, 0.33f, 0.33f), M(col[k])); Prim(PrimitiveType.Cube, "Visor", r[2], V(0, 0.03f, 0.14f), V(0.22f, 0.08f, 0.06f), M("Black")); }
        }
        RG(g, "The clay goons", V(4313f, 0, 46f), 200f, "spe_goons", "shake", PrimitiveType.Sphere, V(0.1f, 0.1f, 0.1f), "TurfGrey");
        RG(g, "The megabot combine", V(4320f, 0, 52f), 180f, "spe_robot", "grow", PrimitiveType.Cube, V(1.2f, 1.2f, 1.2f), "GlowRed");
        var alarm = RG(g, "The alarm", V(4288f, 0, 56f), 90f, "spe_alarm", "spin", PrimitiveType.Cylinder, V(0.5f, 0.3f, 0.5f), "GlowRed");
        alarm.target.localPosition = V(0, 2.4f, 0);
        var mentor = RG(g, "The mentor in the tube", V(4292.5f, 0, 60f), 270f, "spe_mentor", "shake", PrimitiveType.Sphere, V(0.3f, 0.3f, 0.3f), "DarkGlowCyan");
        mentor.target.localPosition = V(0, 3.4f, 0);
        report.Add("The Spectrum Squad: a command centre (consoles, alarm lights, the mentor in a tube), a quarry with clay goons and a statue of the five-vehicle giant robot, and five squad members in coloured helmets; 5 voiced people, 4 gags");
    }

    // ================================================================================================ a wing for every new room

    /// <summary>
    /// A room's wing: 3 voiced people and 3 voiced gags in set places (all clips More/mo_*), with `decor` for the rest.
    /// Slots (from the wing's centre x and its south wall z): people at (-9, 9), (0, 14), (9, 9); gags at (-12, 18), (5, 20), (13, 15).
    /// </summary>
    static void RoomWing(Transform root, string area, float x0, float x1, float zWall, float depth, float h, string floor, string wall, string ceil,
                         string title, Color col, string sub, float doorX, string[] folk, string[] gags, Action<Transform, float, float> decor, string line)
    {
        float cx = (x0 + x1) / 2f;
        var g = Wing(root, area, x0, x1, zWall, depth, h, floor, wall, ceil, title, col, sub, doorX);
        var fp = new[] { V(-9f, 0, 9f), V(0, 0, 14f), V(9f, 0, 9f) };
        var fy = new[] { 160f, 180f, 200f };
        for (int i = 0; i < 3; i++)
        {
            var f = folk[i].Split('|');   // name|hex|act|clip
            Folk(g, f[0], f[1], int.Parse(f[2]), V(cx, 0, zWall) + fp[i], fy[i], "More/mo_" + f[3]);
        }
        var gp = new[] { V(-12f, 0, 18f), V(5f, 0, 20f), V(13f, 0, 15f) };
        for (int i = 0; i < 3; i++)
        {
            var q = gags[i].Split('|');   // name|clip|kind|shape|sx,sy,sz|mat|amount|liftY
            var sz = q[4].Split(',').Select(x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            var shape = q[3] == "cube" ? PrimitiveType.Cube : q[3] == "cyl" ? PrimitiveType.Cylinder : q[3] == "cap" ? PrimitiveType.Capsule : PrimitiveType.Sphere;
            var gag = Gk(g, q[0], V(cx, 0, zWall) + gp[i], 180f, "More/mo_" + q[1], q[2], shape, V(sz[0], sz[1], sz[2]), q[5], float.Parse(q[6], System.Globalization.CultureInfo.InvariantCulture));
            if (q.Length > 7) gag.target.localPosition = V(0, float.Parse(q[7], System.Globalization.CultureInfo.InvariantCulture), 0);
        }
        if (decor != null) decor(g, cx, zWall);
        report.Add(title + " (" + area + "'s new wing): " + line + "; 3 voiced people, 3 gags");
    }

    static void RoomWings(Transform root)
    {
        RoomWing(root, "The Cutaway House", 2900f, 2940f, 56f, 24f, 6f, "Floor2", "WallDark", "Ceiling", "THE SALTY BARNACLE", new Color(0.4f, 0.8f, 1f),
                 "the neighbourhood bar. the same four stools, the same three regulars, every night for twenty years", 2932f,
                 new[] { "Bartender Hector|#bcaaa4|20|rcw_barman", "Neighbour Duncan|#ffe0b2|11|rcw_duncan", "Officer Pat|#90caf9|3|rcw_officer" },
                 new[] { "The jukebox|rcw_jukebox|spin|cube|0.9,1.4,0.6|GemPurple|2", "The dart board|rcw_darts|shake|cyl|0.6,0.05,0.6|GlowRed|2|1.6", "The bar fight cutaway|rcw_fight|slide|cube|1.6,1.2,0.1|GlowWarm|2|0.6" },
                 (g, cx, z) => { Box(g, "Bar", V(cx, 0.55f, z + 22f), V(14f, 1.1f, 1f), M("DoorWood")); for (int k = 0; k < 6; k++) DPrim(PrimitiveType.Cylinder, "Stool", g, V(cx - 6f + k * 2.4f, 0.4f, z + 20.6f), V(0.5f, 0.4f, 0.5f), M("CaneRed")); Box(g, "Bottle shelf", V(cx, 2f, z + 23.8f), V(12f, 0.1f, 0.3f), M("DeskWood"), false); },
                 "a bar with stools and a shelf of bottles");
        RoomWing(root, "Yellowville", 2960f, 3020f, 80f, 26f, 12f, "Grass", "PixelSky", "Sky", "YELLOWVILLE ELEMENTARY", new Color(1f, 0.85f, 0.1f),
                 "the school. detention is in room 4. the science fair is on fire.", float.NaN,
                 new[] { "Principal Pomeroy|#ffd90f|3|ryw_principal", "Teacher Mrs Hollis|#ffd90f|9|ryw_teacher", "Bus Driver Dewey|#ffd90f|21|ryw_driver" },
                 new[] { "The detention chalkboard|ryw_chalk|shake|cube|3,1.6,0.1|GiftGreen|2|1", "The school bell|ryw_bell|shake|sph|0.6,0.6,0.6|Brass|2|2.6", "The science fair volcano|ryw_volcano|pop|cyl|1.2,1,1.2|Brick|1.2" },
                 (g, cx, z) => { Box(g, "School", V(cx + 16f, 3f, z + 22f), V(20f, 6f, 6f), M("Brick")); Box(g, "Yellow bus", V(cx - 18f, 1.2f, z + 10f), V(2.4f, 2.4f, 8f), M("GlowYellow")); },
                 "the school building and a yellow bus");
        RoomWing(root, "The Spooky Mansion Case", 3040f, 3090f, 70f, 24f, 8f, "Floor2", "WallDark", "Black", "THE SECRET BASEMENT", new Color(0.6f, 1f, 0.4f),
                 "behind the bookcase, down the stairs: where the ghost keeps its costumes", float.NaN,
                 new[] { "Costume Maker Mabel|#ce93d8|9|rmw_mabel", "Nephew Ned|#ffcc80|21|rmw_ned", "Lost Tourist Lou|#80cbc4|11|rmw_lou" },
                 new[] { "The trap door|rmw_trapdoor|drop|cube|1.4,0.1,1.4|DoorWood|2", "The net trap|rmw_net|drop|sph|1.6,1,1.6|Cobweb|3", "The costume rack|rmw_rack|shake|cube|2,1.8,0.3|GlowGhost|2|0.4" },
                 (g, cx, z) => { for (int k = 0; k < 4; k++) DPrim(PrimitiveType.Sphere, "Ghost costume", g, V(cx - 18f + k * 3f, 2f, z + 22f), V(0.8f, 1.6f, 0.4f), M("GlowGhost")); Box(g, "Projector", V(cx + 18f, 0.5f, z + 22f), V(1f, 1f, 1f), M("Metal")); },
                 "ghost costumes on hooks and the projector that makes the ghost");
        RoomWing(root, "Paper Mountain Town", 3110f, 3160f, 70f, 24f, 8f, "Floor2", "WallGreen", "Ceiling", "THE COMMUNITY CENTER", new Color(1f, 1f, 1f),
                 "town meetings every tuesday. they never fix anything. there are snacks.", float.NaN,
                 new[] { "Counselor Morty|#b39ddb|3|rpw_counselor", "PE Teacher Tilda|#ef9a9a|10|rpw_pe", "Lumberjack Lou|#8d6e63|16|rpw_lumberjack" },
                 new[] { "The talent show|rpw_talent|pop|cube|1,1.8,0.1|GlowYellow|1.8", "The pinata|rpw_pinata|shake|sph|0.8,0.8,0.8|GemPink|2|2.4", "The paper airplane|rpw_plane|fly|cube|0.6,0.05,0.5|Paper|4|1.4" },
                 (g, cx, z) => { for (int r = 0; r < 3; r++) for (int k = 0; k < 6; k++) Box(g, "Folding chair", V(cx - 6f + k * 2.4f, 0.4f, z + 4f + r * 1.6f + 18f), V(0.5f, 0.8f, 0.5f), M("Metal")); },
                 "rows of folding chairs for the town meeting");
        RoomWing(root, "Four Oaks", 3180f, 3240f, 80f, 26f, 14f, "Grass", "PixelSky", "Sky", "THE ACORN FESTIVAL", new Color(0.55f, 0.35f, 0.15f),
                 "the street fair: rides for the kids, a stage, food you'll regret, and a parade", float.NaN,
                 new[] { "Festival MC Dwayne|#ffcc80|18|row_mc", "Funnel Cake Fern|#f8bbd0|9|row_fern", "Fiddler Fitz|#a1887f|19|row_fiddler" },
                 new[] { "The parade float|row_float|slide|cube|3,1.6,4|GiftGreen|3", "The acorn toss|row_toss|fly|sph|0.4,0.5,0.4|Trunk|4|0.8", "The pie-eating contest|row_pie|shake|cyl|0.8,0.1,0.8|Bun|2|0.9" },
                 (g, cx, z) => { Box(g, "Stage", V(cx, 0.5f, z + 23f), V(10f, 1f, 4f), M("Stage")); for (int k = 0; k < 3; k++) Stall(g, new[] { "FUNNEL CAKE", "CORN DOGS", "LEMONADE" }[k], V(cx + 16f + k * 4f - 4f, 0, z + 5f), 0f, "CaneRed", "CaneWhite"); },
                 "a stage and food stalls");
        RoomWing(root, "The Dino Sing-Along Studio", 3260f, 3300f, 56f, 22f, 6f, "Floor2", "Wall2", "Ceiling", "THE DRESSING ROOM", new Color(0.3f, 0.85f, 0.75f),
                 "where the dinosaur takes his head off. please knock.", float.NaN,
                 new[] { "Costume Fixer Faye|#f48fb1|9|rdw_faye", "Producer Pete|#90a4ae|3|rdw_producer", "Former Dino Gary|#4db6ac|11|rdw_gary" },
                 new[] { "The spare dinosaur head|rdw_head|spin|sph|1,0.9,1.1|GiftGreen|2", "The makeup mirror|rdw_mirror|shake|cube|1.2,1,0.05|Glass|2|1.3", "The fan mail|rdw_mail|drop|cube|1,0.5,0.6|Paper|2.5" },
                 (g, cx, z) => { Box(g, "Dressing table", V(cx - 10f, 0.45f, z + 20.5f), V(6f, 0.9f, 1f), M("DeskWood")); for (int k = 0; k < 5; k++) Prim(PrimitiveType.Sphere, "Mirror bulb", g, V(cx - 12f + k, 2.4f, z + 21.8f), Vector3.one * 0.15f, M("GlowWarm")); },
                 "a dressing table with mirror bulbs");
        RoomWing(root, "The Back Rooms", 3320f, 3380f, 80f, 24f, 3.2f, "Concrete", "LabGrey", "Concrete", "LEVEL TWO: THE PIPES", new Color(0.5f, 0.45f, 0.3f),
                 "it's warmer down here. the pipes knock. something knocks back.", 3355f,
                 new[] { "Pipe Fitter Pam|#bcaaa4|20|rbw_pam", "Delivery Driver Dex|#ffab91|10|rbw_dex", "The Echo|#9e9e9e|19|rbw_echo" },
                 new[] { "The steam valve|rbw_valve|shake|cyl|0.6,0.1,0.6|GlowRed|2|1.2", "The door to nowhere|rbw_door|flip|cube|1.1,2.1,0.1|DoorWood|2", "The thing in the dark|rbw_dark|pop|cap|0.8,2.4,0.8|Black|2.6" },
                 (g, cx, z) => { for (int k = 0; k < 8; k++) DPrim(PrimitiveType.Cylinder, "Pipe", g, V(cx - 28f + k * 8f, 2.8f, z + 12f), V(0.4f, 12f, 0.4f), M("Metal"), Quaternion.Euler(90f, 0, 0)); },
                 "pipes along the ceiling, concrete, a hiss of steam");
        RoomWing(root, "The Quiet Town", 3400f, 3450f, 70f, 24f, 14f, "GGrass", "GSky", "GSky", "THE TOWN SQUARE", new Color(0.95f, 0.95f, 0.95f),
                 "the bandstand, the courthouse and the bench where the old men sit and disagree", float.NaN,
                 new[] { "Mayor Pruitt|#bdbdbd|3|rqw_mayor", "Gas Attendant Clyde|#9e9e9e|21|rqw_clyde", "Choir Lady Clara|#e0e0e0|9|rqw_clara" },
                 new[] { "The town band|rqw_band|shake|cyl|0.5,0.6,0.5|G1|2|0.6", "The runaway cow|rqw_cow|slide|cube|1,1.2,2|G2|6", "The church bell|rqw_bell|shake|sph|0.8,0.8,0.8|G3|2|3" },
                 (g, cx, z) => { DPrim(PrimitiveType.Cylinder, "Bandstand", g, V(cx + 16f, 0.3f, z + 20f), V(6f, 0.3f, 6f), M("G1")); Box(g, "Courthouse", V(cx - 14f, 3f, z + 21f), V(12f, 6f, 4f), M("GBrick")); Box(g, "Bench", V(cx, 0.3f, z + 22f), V(3f, 0.6f, 0.7f), M("GWood")); },
                 "a bandstand, the courthouse and a bench");
        RoomWing(root, "Castaway Island", 3470f, 3530f, 80f, 26f, 20f, "PixelSand", "PixelSky", "Sky", "THE OTHER SIDE OF THE ISLAND", new Color(1f, 0.85f, 0.4f),
                 "jungle, a cave, a volcano and a very suspicious tiki statue", float.NaN,
                 new[] { "Hermit Horace|#d7ccc8|19|rcaw_hermit", "Scientist Dr Wu|#e0f7fa|7|rcaw_scientist", "Parrot Keeper Polly|#a5d6a7|21|rcaw_parrot" },
                 new[] { "The tiki idol|rcaw_tiki|shake|cube|1,2.2,1|Trunk|2", "The volcano|rcaw_volcano|pop|sph|1.2,1.2,1.2|GlowFlame|2|1", "The message in a bottle|rcaw_bottle|fly|cap|0.2,0.4,0.2|Glass|3|0.3" },
                 (g, cx, z) => { DPrim(PrimitiveType.Sphere, "Volcano", g, V(cx + 18f, 0f, z + 22f), V(14f, 16f, 10f), M("CaveRock")); for (int k = 0; k < 5; k++) Palm(g, V(cx - 24f + k * 5f, 0, z + 23f), 6f, k + 3); },
                 "the volcano and a jungle of palms");
        RoomWing(root, "The Wishing Hills", 3550f, 3600f, 70f, 24f, 20f, "CaneWhite", "PastelSky", "Sky", "THE CLOUD CASTLE", new Color(0.95f, 0.5f, 0.8f),
                 "up the rainbow and into the clouds. the floor is soft. mostly.", float.NaN,
                 new[] { "Queen Cumula|#f8bbd0|3|rww_queen", "Castle Guard Nimbus|#b0bec5|20|rww_guard", "Little Dragon Sprout|#aed581|21|rww_sprout" },
                 new[] { "The rainbow slide|rww_slide|slide|cube|1,0.2,3|GlowYellow|4", "The thunder drum|rww_drum|shake|cyl|1.2,0.8,1.2|GiftBlue|2", "The cloud bed|rww_bed|grow|sph|2,0.8,1.6|CaneWhite|2" },
                 (g, cx, z) => { for (int k = 0; k < 4; k++) DPrim(PrimitiveType.Cylinder, "Castle tower", g, V(cx - 15f + k * 10f, 5f, z + 23f), V(2.4f, 5f, 2.4f), M(k % 2 == 0 ? "GemPink" : "GemPurple")); },
                 "pink and purple towers on a floor of cloud");
        RoomWing(root, "The Kitchen Counter Theatre", 3620f, 3656f, 56f, 22f, 6f, "LabFloor", "PoolBlue", "LabWhite", "BACKSTAGE IN THE FRIDGE", new Color(0.5f, 0.9f, 0.3f),
                 "the cast waits in here between scenes. it's four degrees. bring a coat.", 3653f,
                 new[] { "Fridge Manager Frost|#b3e5fc|3|rkw_frost", "Props Master Penny|#ffccbc|9|rkw_penny", "Lighting Tech Lars|#c5e1a5|20|rkw_lars" },
                 new[] { "The nervous asparagus|rkw_asparagus|shake|cap|0.3,1.4,0.3|GiftGreen|2", "The singing cheese|rkw_cheese|spin|cube|0.8,0.6,0.6|CornYellow|2", "The pickle chorus|rkw_pickles|grow|cap|0.4,0.7,0.4|PixelGreenDark|2" },
                 (g, cx, z) => { for (int k = 0; k < 4; k++) Box(g, "Fridge shelf", V(cx - 3f, 0.8f + k * 1.2f, z + 21.2f), V(24f, 0.08f, 1.2f), M("Glass")); },
                 "glass fridge shelves");
        RoomWing(root, "The Clubhouse on the Hill", 3680f, 3730f, 70f, 24f, 10f, "Grass", "PixelSky", "Sky", "THE GADGET GARAGE", new Color(1f, 0.25f, 0.2f),
                 "where the clubhouse keeps its gadgets, and its spare gadgets, and its gadget for finding gadgets", 3727f,
                 new[] { "Inventor Izzy|#ffab91|7|rclw_izzy", "Mechanic Mo|#90a4ae|16|rclw_mo", "Courier Kip|#fff59d|21|rclw_kip" },
                 new[] { "The bubble car|rclw_car|slide|sph|1.6,1.2,2|CaneRed|4", "The toolbox|rclw_toolbox|flip|cube|1,0.15,0.6|GiftBlue|2|0.6", "The rocket pogo stick|rclw_pogo|fly|cap|0.3,1.4,0.3|GlowOrange|3" },
                 (g, cx, z) => { Box(g, "Workbench", V(cx - 16f, 0.45f, z + 22f), V(8f, 0.9f, 1.2f), M("DeskWood")); Box(g, "Pegboard", V(cx - 16f, 2f, z + 23.8f), V(8f, 2f, 0.1f), M("Pegboard"), false); },
                 "a workbench and a pegboard of tools");
        RoomWing(root, "The Suspicious Starship", 3750f, 3800f, 70f, 22f, 5f, "LabFloor", "LabGrey", "LabWhite", "THE ENGINE ROOM", new Color(1f, 0.3f, 0.3f),
                 "two big engines, a fuel task nobody wants, and an airlock with a red button", 3792f,
                 new[] { "Crewmate Indigo|#7986cb|10|rsw_indigo", "Crewmate Maroon|#a1887f|20|rsw_maroon", "Crewmate Rose|#f48fb1|9|rsw_rose" },
                 new[] { "The fuel task|rsw_fuel|shake|cyl|0.6,0.8,0.6|GlowYellow|2", "The sabotage alarm|rsw_alarm|spin|cyl|0.6,0.3,0.6|GlowRed|2|2", "The airlock|rsw_airlock|slide|cube|2.4,3,0.2|Metal|2" },
                 (g, cx, z) => { foreach (float ex in new[] { -16f, 16f }) DPrim(PrimitiveType.Cylinder, "Engine", g, V(cx + ex, 2f, z + 18f), V(5f, 2f, 5f), M("Metal"), Quaternion.Euler(90f, 0, 0)); },
                 "two big engines");
        RoomWing(root, "The Meme Museum", 3820f, 3860f, 60f, 22f, 6f, "Floor2", "Wall", "Ceiling", "THE GIFT SHOP", new Color(1f, 0.85f, 0.3f),
                 "exit through the gift shop. the gift shop is the only exit.", 3850f,
                 new[] { "Cashier Kyle|#ffe0b2|20|rmmw_cashier", "Historian Dr Viral|#d1c4e9|3|rmmw_historian", "Kid Collector Zoe|#ffccbc|21|rmmw_zoe" },
                 new[] { "The skeleton plushie|rmmw_plush|grow|sph|0.5,0.6,0.5|BoneWhite|2|0.9", "The postcard rack|rmmw_postcards|spin|cube|0.6,1.6,0.6|Paper|2", "The I-survived t-shirt|rmmw_shirt|drop|cube|0.8,0.9,0.05|GlowRed|2" },
                 (g, cx, z) => { Box(g, "Counter", V(cx - 12f, 0.5f, z + 20f), V(5f, 1f, 1f), M("DeskWood")); for (int k = 0; k < 3; k++) Box(g, "Shelf", V(cx + 10f, 0.6f + k * 0.6f, z + 21.5f), V(10f, 0.06f, 0.8f), M("DeskWood"), false); },
                 "a till and shelves of souvenirs");
        RoomWing(root, "Orlando", 3880f, 4040f, 140f, 28f, 25f, "PixelSand", "PixelSky", "Sky", "THE ROCKET GARDEN", new Color(1f, 0.55f, 0.1f),
                 "an hour east, on the space coast: old rockets standing in the sun, and a launch every so often", float.NaN,
                 new[] { "Retired Engineer Rollo|#b0bec5|3|rolw_engineer", "Surfer Sky|#ffcc80|21|rolw_surfer", "Launch Watcher Lena|#ce93d8|9|rolw_watcher" },
                 new[] { "The launch|rolw_launch|fly|cap|0.8,3,0.8|CaneWhite|8", "The sea turtle|rolw_turtle|slide|sph|1,0.4,1.2|PixelGreenDark|3", "The sand castle|rolw_castle|grow|cube|1,0.8,1|PixelSand|2" },
                 (g, cx, z) => { for (int k = 0; k < 6; k++) { float rx = cx - 60f + k * 24f; DPrim(PrimitiveType.Cylinder, "Old rocket", g, V(rx, 8f + k % 2 * 2f, z + 21f), V(2f, 8f + k % 2 * 2f, 2f), M(k % 2 == 0 ? "CaneWhite" : "LabGrey")); DPrim(PrimitiveType.Capsule, "Rocket nose", g, V(rx, 16.5f + k % 2 * 4f, z + 21f), V(2f, 1.5f, 2f), M("CaneRed")); } Box(g, "Sea", V(cx, 0.02f, z + 26f), V(160f, 0.04f, 4f), M("PoolBlue"), false); },
                 "six old rockets standing in the sand by the sea");
        RoomWing(root, "Monster Choir Island", 4060f, 4110f, 70f, 24f, 16f, "Snow", "PixelSky", "Sky", "THE COLD ISLAND", new Color(0.6f, 0.85f, 1f),
                 "the monsters up here sing slower. it's the cold. their teeth chatter in time.", float.NaN,
                 new[] { "Snow Keeper Yuki|#e1f5fe|18|rmsw_yuki", "Ice Fisher Ivo|#b0bec5|16|rmsw_ivo", "Egg Warmer Edda|#ffccbc|9|rmsw_edda" },
                 new[] { "The yeti singer|rmsw_yeti|shake|sph|1.6,2,1.4|CaneWhite|2", "The ice chimes|rmsw_chimes|spin|cyl|1,0.1,1|Glass|2|2", "The snowball choir|rmsw_snowballs|grow|sph|0.8,0.8,0.8|Snow|2" },
                 (g, cx, z) => { for (int k = 0; k < 5; k++) DPrim(PrimitiveType.Cube, "Ice block", g, V(cx - 20f + k * 10f, 1.5f, z + 22f), V(3f, 3f, 3f), M("Glass"), Quaternion.Euler(0, k * 30f, 0)); },
                 "ice blocks and snow");
        RoomWing(root, "The Shifting Bureau", 4130f, 4180f, 70f, 24f, 12f, "Concrete", "Brutal", "Brutal", "THE RESEARCH SECTOR", new Color(0.9f, 0.15f, 0.15f),
                 "where the strange objects are studied. the fridge is not a fridge. do not open the fridge.", float.NaN,
                 new[] { "Dr Okafor|#d7ccc8|7|rbuw_okafor", "Intern Bram|#c5e1a5|21|rbuw_bram", "Security Officer Vega|#90a4ae|20|rbuw_vega" },
                 new[] { "The containment box|rbuw_box|shake|cube|1.2,1.2,1.2|Black|2", "The hovering fridge|rbuw_fridge|fly|cube|1,2,0.8|LabWhite|3", "The slide projector|rbuw_projector|spin|cube|0.6,0.4,0.6|Metal|2|1" },
                 (g, cx, z) => { for (int k = 0; k < 4; k++) Box(g, "Lab bench", V(cx - 15f + k * 10f, 0.45f, z + 22f), V(5f, 0.9f, 1.2f), M("LabWhite")); },
                 "lab benches under bare concrete");
        RoomWing(root, "The Dark Lake", 4200f, 4260f, 80f, 26f, 20f, "DarkGround", "NightSky", "NightSky", "THE OLD BOATHOUSE", new Color(0.85f, 0.85f, 1f),
                 "across the water. the writer's typewriter is here. it's typing on its own.", float.NaN,
                 new[] { "Old Fisherman Harlan|#a1887f|16|rlkw_harlan", "Detective Mara Quinn|#b39ddb|3|rlkw_quinn", "Lighthouse Keeper Ida|#e0e0e0|9|rlkw_ida" },
                 new[] { "The burning page|rlkw_page|pop|cube|0.4,0.02,0.5|GlowFlame|1.2", "The old lamp|rlkw_lamp|grow|sph|0.5,0.5,0.5|GlowWarm|2|1.8", "The rocking boat|rlkw_boat|shake|cube|1.4,0.6,3.4|DoorWood|2" },
                 (g, cx, z) => { Box(g, "Boathouse", V(cx + 18f, 3f, z + 21f), V(14f, 6f, 6f), M("DoorWood")); Box(g, "Dock", V(cx - 19f, 0.2f, z + 20f), V(3f, 0.4f, 10f), M("DoorWood")); Box(g, "Water", V(cx - 20f, 0.02f, z + 20f), V(18f, 0.04f, 10f), M("DarkWater"), false); },
                 "a boathouse, a dock and black water");
        RoomWing(root, "The Spectrum Squad", 4280f, 4330f, 70f, 24f, 16f, "Stone", "NightSky", "NightSky", "THE VILLAINS' MOON BASE", new Color(0.7f, 0.3f, 1f),
                 "the empress watches the squad through her telescope. she has opinions about their outfits.", float.NaN,
                 new[] { "Empress Grimvexa|#9575cd|3|rspw_empress", "Henchbot Three|#90a4ae|20|rspw_henchbot", "Toastasaur|#ffb74d|11|rspw_toastasaur" },
                 new[] { "The telescope|rspw_telescope|spin|cyl|0.5,1.6,0.5|Brass|2", "The monster grow ray|rspw_growray|grow|cap|0.6,1,0.6|GlowGreen|2", "The goon dispenser|rspw_goons|pop|cap|0.6,1.6,0.6|TurfGrey|1.8" },
                 (g, cx, z) => { Box(g, "Throne", V(cx, 1.5f, z + 23f), V(3f, 3f, 1.4f), M("GemPurple")); DPrim(PrimitiveType.Sphere, "Earth in the window", g, V(cx - 16f, 9f, z + 23.8f), V(6f, 6f, 0.2f), M("GiftBlue")); },
                 "a throne and the Earth hanging in the sky");
    }
}
#endif
