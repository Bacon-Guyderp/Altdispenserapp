#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// The alien roster (Assets/XenoDial/Data/aliens.txt) and the generator that turns each
/// line into a model. Every alien is built from a body plan plus extras, in metres with the
/// feet at the origin, facing +Z and an eye height of about 1.6 m. Its primitives are
/// merged into a handful of vertex-coloured meshes that share one material:
///   Main        everything that doesn't move
///   A0, A1...   limbs that swing together; B0, B1... swing the other way
///   P           glowing bits that pulse (flames, crystals, antenna tips)
/// XenoBody and the watch find the parts by those names.
///
/// This file is pure geometry (no scene or asset calls) so tools/preview_aliens can compile
/// it outside Unity and draw every alien. XenoDialWorldBuilder turns the result into objects.
/// </summary>
public static class XenoDialAliens
{
    public const string RosterPath = "Assets/XenoDial/Data/aliens.txt";

    public const int FIRE = 0, SLAM = 1, DASH = 2, FLAP = 3, HOP = 4, CRYSTAL = 5, LIGHTNING = 6,
                     BLINK = 7, THRUST = 8, GRAPPLE = 9, LAUNCH = 10, SPIN = 11, BLAST = 12;
    static readonly string[] PowerNames = { "fire", "slam", "dash", "flap", "hop", "crystal", "lightning",
                                            "blink", "thrust", "grapple", "launch", "spin", "blast" };
    static readonly string[] PowerText =
    {
        "Throws energy bolts", "Ground-slam shockwave", "Super speed and a smashing dash", "Flap to fly, glide when falling",
        "Huge hops", "Grows crystal platforms to climb", "Lightning that arcs between targets", "Teleports forward",
        "Jets through the air", "Grapple line that yanks you along", "Rockets straight up",
        "Spin attack that smashes everything nearby", "Explosive blasts",
    };
    //                                     fire slam dash flap hop  cryst light blink thrst grapl launc spin blast
    static readonly float[] BaseWalk =   { 3f,  2.5f, 8f,  3f,  2.5f, 3f,  3.5f, 3f,  3f,   3f,   3f,   4f,   2.8f };
    static readonly float[] BaseRun =    { 5f,  4.5f, 15f, 5f,  4.5f, 5f,  6f,   5f,  5f,   5.5f, 5f,   6.5f, 4.5f };
    static readonly float[] BaseJump =   { 4f,  6f,   4f,  5f,  3.5f, 4f,  4f,   4f,  4f,   4f,   5f,   4f,   4f };
    static readonly float[] BaseGrav =   { 1f,  1.3f, 1f,  0.3f, 0.7f, 1f, 1f,   1f,  0.6f, 1f,   1f,   1f,   1f };
    static readonly float[] BaseCool =   { 0.3f, 1.4f, 1f, 0.25f, 0.6f, 0.45f, 0.6f, 0.9f, 0.1f, 0.8f, 1.5f, 0.8f, 1.2f };

    public class Alien
    {
        public string name, plan, extras, sizeName;
        public int power;
        public float strength = 1f, eyeHeight, walk, run, jump, gravity, cooldown;
        public Color main, glow;
        public string Description => PowerText[power] + SizeNote;
        string SizeNote => sizeName == "tiny" ? " (tiny: fits the tunnel)" : sizeName == "giant" ? " (giant)"
                         : sizeName == "large" ? " (large)" : sizeName == "small" ? " (small)" : "";
        public bool Has(char c) => extras.IndexOf(c) >= 0;
    }

    // ------------------------------------------------------------------ roster

    public static List<Alien> LoadRoster(out string error, string path = RosterPath)
    {
        error = null;
        var list = new List<Alien>();
        if (!File.Exists(path)) { error = "Missing " + path; return list; }
        int lineNo = 0;
        foreach (var raw in File.ReadAllLines(path))
        {
            lineNo++;
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            var f = line.Split('|').Select(s => s.Trim()).ToArray();
            if (f.Length < 6) { error = $"aliens.txt line {lineNo}: expected 'name | body | power | size | colour | glow | extras'"; return list; }
            var a = new Alien { name = f[0], plan = f[1].ToLowerInvariant(), extras = f.Length > 6 ? f[6].ToLowerInvariant() : "" };
            var p = f[2].ToLowerInvariant().Split('*');
            a.power = Array.IndexOf(PowerNames, p[0].Trim());
            if (a.power < 0) { error = $"aliens.txt line {lineNo}: unknown power '{p[0]}'"; return list; }
            if (p.Length > 1) float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out a.strength);
            a.strength = Mathf.Clamp(a.strength, 0.3f, 3f);
            a.sizeName = f[3].ToLowerInvariant();
            a.eyeHeight = a.sizeName == "tiny" ? 0.3f : a.sizeName == "small" ? 0.9f : a.sizeName == "large" ? 2.4f
                        : a.sizeName == "giant" ? 3.4f : 0f;
            a.main = Hex(f[4], Color.gray);
            a.glow = Hex(f[5], Color.white);
            Stats(a);
            list.Add(a);
        }
        var dup = list.GroupBy(x => x.name.ToLowerInvariant()).FirstOrDefault(g => g.Count() > 1);
        if (dup != null) error = "aliens.txt: the name '" + dup.Key + "' is used twice";
        return list;
    }

    static Color Hex(string s, Color fallback)
    {
        Color c;
        return ColorUtility.TryParseHtmlString("#" + s.TrimStart('#'), out c) ? c : fallback;
    }

    static void Stats(Alien a)
    {
        int p = a.power;
        float v = 0.92f + 0.16f * Rand01(a.name, 7);   // a little personality per alien
        a.walk = BaseWalk[p] * v;
        a.run = BaseRun[p] * v;
        a.jump = BaseJump[p];
        a.gravity = BaseGrav[p];
        a.cooldown = BaseCool[p];
        if (a.sizeName == "giant") { a.jump = Mathf.Max(a.jump, 7.5f); a.gravity *= 1.1f; a.walk *= 0.9f; }
        else if (a.sizeName == "large") a.jump *= 1.25f;
        else if (a.sizeName == "tiny") { a.walk *= 0.8f; a.run *= 0.8f; }
    }

    static uint Hash(string s)
    {
        uint h = 2166136261;
        foreach (char c in s) { h ^= c; h *= 16777619; }
        return h;
    }

    static float Rand01(string s, int salt) => (Hash(s + salt) % 10000) / 10000f;

    // ------------------------------------------------------------------ generation

    /// <summary>One movable part of an alien: a mesh whose origin is the part's pivot.</summary>
    public class Piece { public string name; public Vector3 origin; public Mesh mesh; }

    /// <summary>Builds one alien's pieces (Main, A0.., B0.., P) in roster-independent form.</summary>
    public static List<Piece> Generate(Alien a)
    {
        var rig = new Rig(a);
        switch (a.plan)
        {
            case "brute": Brute(rig); break;
            case "slim": Slim(rig); break;
            case "avian": Avian(rig); break;
            case "quad": Quad(rig); break;
            case "bug": Bug(rig); break;
            case "floater": Floater(rig); break;
            case "serpent": Serpent(rig); break;
            default: Biped(rig); break;
        }
        Extras(rig);
        var pieces = new List<Piece>();
        foreach (var g in rig.groups)
        {
            if (g.Value.parts.Count == 0) continue;
            var mesh = g.Value.Build(a.name + " " + g.Key);
            pieces.Add(new Piece { name = g.Key, origin = g.Value.origin, mesh = mesh });
        }
        return pieces;
    }

    // ------------------------------------------------------------------ mesh building

    class Part { public Mesh mesh; public Vector3 pos, size; public Quaternion rot; public Color c; }

    class Group
    {
        public readonly List<Part> parts = new List<Part>();
        public Vector3 origin;
        public bool autoOrigin;

        public Mesh Build(string name)
        {
            if (autoOrigin && parts.Count > 0)
                origin = parts.Aggregate(Vector3.zero, (s, p) => s + p.pos) / parts.Count;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
            foreach (var p in parts)
            {
                // position = pos + rot * (size * v); normal = rot * (n / size), the inverse-transpose of that.
                int b = v.Count;
                var sv = p.mesh.vertices; var sn = p.mesh.normals;
                var inv = new Vector3(1f / p.size.x, 1f / p.size.y, 1f / p.size.z);
                for (int i = 0; i < sv.Length; i++)
                {
                    v.Add(p.pos + p.rot * Vector3.Scale(p.size, sv[i]) - origin);
                    n.Add((p.rot * Vector3.Scale(inv, sn[i])).normalized);
                    c.Add(p.c);
                }
                foreach (var idx in p.mesh.triangles) t.Add(b + idx);
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetColors(c);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    class Rig
    {
        public readonly Alien a;
        public readonly System.Random rnd;
        public readonly Dictionary<string, Group> groups = new Dictionary<string, Group>();
        Group cur;
        // anatomy anchors the extras attach to
        public Vector3 head, back, hip;
        public float headR = 0.2f, width = 0.5f;
        public Vector3 shoulder;        // right shoulder; mirrored for the left
        public bool hasArms;
        public Color main, dark, light, glow, white = new Color(0.95f, 0.93f, 0.88f, 0f);

        public Rig(Alien a)
        {
            this.a = a;
            rnd = new System.Random((int)Hash(a.name));
            main = new Color(a.main.r, a.main.g, a.main.b, 0f);
            dark = new Color(a.main.r * 0.55f, a.main.g * 0.55f, a.main.b * 0.55f, 0f);
            light = Color.Lerp(main, new Color(1, 1, 1, 0), 0.35f);
            glow = new Color(a.glow.r, a.glow.g, a.glow.b, 1f);
            Use("Main", Vector3.zero);
        }

        public float R(float lo, float hi) => lo + (float)rnd.NextDouble() * (hi - lo);
        public bool Chance(float p) => rnd.NextDouble() < p;

        public void Use(string name, Vector3 pivot, bool auto = false)
        {
            if (!groups.TryGetValue(name, out cur))
            {
                cur = new Group { origin = pivot, autoOrigin = auto };
                groups[name] = cur;
            }
        }

        void Add(Mesh m, Vector3 pos, Vector3 size, Vector3 euler, Color c)
            => cur.parts.Add(new Part { mesh = m, pos = pos, size = size, rot = Quaternion.Euler(euler), c = c });
        public void Ball(Vector3 pos, Vector3 size, Color c, Vector3 euler = default) => Add(Shapes.Ball, pos, size, euler, c);
        public void Ball(Vector3 pos, float d, Color c) => Add(Shapes.Ball, pos, Vector3.one * d, Vector3.zero, c);
        public void Box(Vector3 pos, Vector3 size, Color c, Vector3 euler = default) => Add(Shapes.Box, pos, size, euler, c);
        public void Cyl(Vector3 pos, Vector3 size, Color c, Vector3 euler = default) => Add(Shapes.Cyl, pos, size, euler, c);
        public void Torso(Vector3 pos, Vector3 size, Color c, bool boxy, Vector3 euler = default)
        { if (boxy) Box(pos, size, c, euler); else Ball(pos, size, c, euler); }
    }

    /// <summary>Low-poly unit shapes (size 1, centred) so a hundred aliens stay light.</summary>
    static class Shapes
    {
        static Mesh _ball, _box, _cyl;
        public static Mesh Ball => _ball ? _ball : (_ball = MakeBall(10, 7));
        public static Mesh Box => _box ? _box : (_box = MakeBox());
        public static Mesh Cyl => _cyl ? _cyl : (_cyl = MakeCyl(10));

        static Mesh MakeBall(int seg, int rings)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            for (int r = 0; r <= rings; r++)
            {
                float th = Mathf.PI * r / rings;
                for (int s = 0; s <= seg; s++)
                {
                    float ph = 2 * Mathf.PI * s / seg;
                    v.Add(new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph)) * 0.5f);
                }
            }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < seg; s++)
                {
                    int a = r * (seg + 1) + s, b = a + seg + 1;
                    t.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                }
            var m = new Mesh();
            m.SetVertices(v);
            m.SetNormals(v.Select(x => x.normalized).ToList());
            m.SetTriangles(t, 0);
            FixWinding(m);
            return m;
        }

        static Mesh MakeBox()
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
            Vector3[] dirs = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var d in dirs)
            {
                Vector3 u = Mathf.Abs(d.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 w = Vector3.Cross(d, u);
                int b = v.Count;
                v.Add((d - u - w) * 0.5f); v.Add((d + u - w) * 0.5f); v.Add((d + u + w) * 0.5f); v.Add((d - u + w) * 0.5f);
                for (int i = 0; i < 4; i++) n.Add(d);
                t.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            }
            var m = new Mesh();
            m.SetVertices(v); m.SetNormals(n); m.SetTriangles(t, 0);
            FixWinding(m);
            return m;
        }

        static Mesh MakeCyl(int seg)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
            for (int s = 0; s <= seg; s++)
            {
                float ph = 2 * Mathf.PI * s / seg;
                var d = new Vector3(Mathf.Cos(ph), 0, Mathf.Sin(ph));
                v.Add(d * 0.5f + Vector3.up * 0.5f); n.Add(d);
                v.Add(d * 0.5f - Vector3.up * 0.5f); n.Add(d);
            }
            for (int s = 0; s < seg; s++)
            {
                int a = s * 2;
                t.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
            }
            for (int cap = 0; cap < 2; cap++)
            {
                float y = cap == 0 ? 0.5f : -0.5f;
                var up = cap == 0 ? Vector3.up : Vector3.down;
                int c = v.Count;
                v.Add(new Vector3(0, y, 0)); n.Add(up);
                for (int s = 0; s <= seg; s++)
                {
                    float ph = 2 * Mathf.PI * s / seg;
                    v.Add(new Vector3(Mathf.Cos(ph) * 0.5f, y, Mathf.Sin(ph) * 0.5f)); n.Add(up);
                }
                for (int s = 0; s < seg; s++) t.AddRange(new[] { c, c + 1 + s, c + 2 + s });
            }
            var m = new Mesh();
            m.SetVertices(v); m.SetNormals(n); m.SetTriangles(t, 0);
            FixWinding(m);
            return m;
        }

        /// <summary>Makes every triangle face along its vertex normals (Unity culls clockwise-from-outside).</summary>
        static void FixWinding(Mesh m)
        {
            var v = m.vertices; var n = m.normals; var t = m.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                var face = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                if (Vector3.Dot(face, n[t[i]] + n[t[i + 1]] + n[t[i + 2]]) < 0) { int x = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = x; }
            }
            m.triangles = t;
        }
    }

    // ------------------------------------------------------------------ body plans

    static void Biped(Rig r)
    {
        bool boxy = r.Chance(0.35f);
        float w = r.R(0.48f, 0.62f);
        r.width = w;
        r.Torso(new Vector3(0, 1.15f, 0), new Vector3(w, 0.8f, 0.38f), r.main, boxy);
        r.Box(new Vector3(0, 1.28f, 0.19f), new Vector3(0.13f, 0.13f, 0.04f), r.glow, new Vector3(0, 0, 45));   // emblem
        r.Box(new Vector3(0, 0.82f, 0), new Vector3(w * 0.9f, 0.12f, 0.34f), r.dark);
        r.head = new Vector3(0, 1.72f, 0.02f);
        r.headR = r.R(0.17f, 0.21f);
        Head(r, r.Chance(0.3f));
        r.back = new Vector3(0, 1.4f, -0.2f);
        r.hip = new Vector3(0, 0.85f, -0.15f);
        Arms(r, new Vector3(w / 2 + 0.08f, 1.5f, 0), 0.16f, 0.62f, r.main, r.a.Has('f') ? r.glow : r.light, 0.17f);
        Legs(r, 0.15f, 0.8f, 0.2f, r.dark);
    }

    static void Brute(Rig r)
    {
        float w = r.R(0.9f, 1.05f);
        r.width = w;
        r.Box(new Vector3(0, 1.12f, 0), new Vector3(w, 0.75f, 0.62f), r.main, new Vector3(4, 0, 0));
        r.Ball(new Vector3(0, 0.78f, -0.02f), new Vector3(w * 0.8f, 0.4f, 0.55f), r.dark);
        for (int s = -1; s <= 1; s += 2) r.Ball(new Vector3(0.55f * s * w, 1.46f, 0), 0.48f, r.dark);
        r.Box(new Vector3(0, 1.25f, 0.32f), new Vector3(0.18f, 0.18f, 0.04f), r.glow, new Vector3(0, 0, 45));
        r.head = new Vector3(0, 1.62f, 0.12f);
        r.headR = 0.18f;
        Head(r, true);
        r.back = new Vector3(0, 1.45f, -0.3f);
        r.hip = new Vector3(0, 0.8f, -0.3f);
        r.hasArms = true;
        r.shoulder = new Vector3(0.62f * w + 0.03f, 1.42f, 0);
        for (int s = -1; s <= 1; s += 2)
        {
            r.Use(s < 0 ? "A0" : "B0", new Vector3(r.shoulder.x * s, r.shoulder.y, 0));
            var o = new Vector3(r.shoulder.x * s, r.shoulder.y, 0);
            r.Box(o + new Vector3(0, -0.45f, 0.05f), new Vector3(0.32f, 0.72f, 0.32f), r.main);
            r.Box(o + new Vector3(0, -0.9f, 0.06f), new Vector3(0.46f, 0.4f, 0.46f), r.dark, new Vector3(0, 12, 0));
        }
        r.Use("Main", Vector3.zero);
        Legs(r, 0.26f, 0.75f, 0.38f, r.main, true);
    }

    static void Slim(Rig r)
    {
        r.width = 0.34f;
        r.Ball(new Vector3(0, 1.22f, 0.03f), new Vector3(0.34f, 0.72f, 0.26f), r.main, new Vector3(r.R(0, 12), 0, 0));
        r.Box(new Vector3(0, 1.32f, 0.15f), new Vector3(0.08f, 0.2f, 0.03f), r.glow);
        r.head = new Vector3(0, 1.72f, 0.06f);
        r.headR = 0.15f;
        r.Ball(r.head, new Vector3(0.26f, 0.32f, 0.32f), r.main);
        Eyes(r);
        r.back = new Vector3(0, 1.4f, -0.12f);
        r.hip = new Vector3(0, 0.92f, -0.1f);
        Arms(r, new Vector3(0.22f, 1.5f, 0.03f), 0.09f, 0.72f, r.dark, r.glow, 0.1f);
        Legs(r, 0.11f, 0.9f, 0.12f, r.main);
    }

    static void Avian(Rig r)
    {
        r.width = 0.48f;
        r.Ball(new Vector3(0, 1.18f, 0), new Vector3(0.48f, 0.74f, 0.42f), r.main);
        r.Ball(new Vector3(0, 1.15f, 0.12f), new Vector3(0.34f, 0.5f, 0.25f), r.light);
        r.head = new Vector3(0, 1.7f, 0.04f);
        r.headR = 0.17f;
        r.Ball(r.head, 0.34f, r.main);
        r.Box(r.head + new Vector3(0, -0.03f, 0.2f), new Vector3(0.08f, 0.06f, 0.22f), new Color(r.glow.r, r.glow.g, r.glow.b, 0.3f));
        Eyes(r);
        r.back = new Vector3(0, 1.4f, -0.2f);
        r.hip = new Vector3(0, 0.95f, -0.2f);
        r.Box(new Vector3(0, 0.95f, -0.32f), new Vector3(0.45f, 0.04f, 0.35f), r.dark, new Vector3(-15, 0, 0));
        r.hasArms = true;
        r.shoulder = new Vector3(0.25f, 1.5f, 0);
        for (int s = -1; s <= 1; s += 2)
        {
            var o = new Vector3(0.25f * s, 1.5f, 0);
            r.Use(s < 0 ? "A0" : "B0", o);
            r.Ball(o + new Vector3(0, -0.3f, 0), new Vector3(0.12f, 0.6f, 0.12f), r.main);
            r.Box(o + new Vector3(0.4f * s, -0.2f, -0.05f), new Vector3(0.8f, 0.55f, 0.03f), r.dark, new Vector3(0, 0, -12 * s));
            r.Box(o + new Vector3(0.75f * s, -0.32f, -0.05f), new Vector3(0.12f, 0.35f, 0.035f), r.glow, new Vector3(0, 0, -12 * s));
        }
        r.Use("Main", Vector3.zero);
        Legs(r, 0.13f, 0.8f, 0.1f, r.dark);
    }

    static void Quad(Rig r)
    {
        r.width = 0.7f;
        r.Ball(new Vector3(0, 0.95f, -0.1f), new Vector3(0.72f, 0.66f, 1.5f), r.main);
        r.Ball(new Vector3(0, 0.82f, -0.05f), new Vector3(0.55f, 0.4f, 1.2f), r.light);
        r.Ball(new Vector3(0, 1.22f, 0.5f), new Vector3(0.32f, 0.5f, 0.32f), r.main, new Vector3(30, 0, 0));
        r.head = new Vector3(0, 1.45f, 0.75f);
        r.headR = 0.2f;
        r.Ball(r.head, new Vector3(0.4f, 0.38f, 0.46f), r.main);
        r.Box(r.head + new Vector3(0, -0.07f, 0.24f), new Vector3(0.22f, 0.16f, 0.2f), r.dark);
        Eyes(r);
        r.back = new Vector3(0, 1.28f, -0.1f);
        r.hip = new Vector3(0, 1.0f, -0.8f);
        for (int s = -1; s <= 1; s += 2)
            for (int f = -1; f <= 1; f += 2)
            {
                var o = new Vector3(0.26f * s, 0.82f, 0.5f * f - 0.1f);
                r.Use((s * f < 0 ? "A" : "B") + (f > 0 ? "0" : "1"), o);
                r.Ball(o + new Vector3(0, -0.4f, 0), new Vector3(0.18f, 0.82f, 0.18f), r.dark);
                r.Ball(o + new Vector3(0, -0.78f, 0.04f), new Vector3(0.2f, 0.09f, 0.26f), r.dark);
            }
        r.Use("Main", Vector3.zero);
    }

    static void Bug(Rig r)
    {
        r.width = 1f;
        r.Ball(new Vector3(0, 0.9f, -0.1f), new Vector3(1f, 0.9f, 1.2f), r.main);
        r.Box(new Vector3(0, 1.34f, -0.15f), new Vector3(0.12f, 0.05f, 0.95f), r.dark);
        r.head = new Vector3(0, 1.45f, 0.3f);
        r.headR = 0.31f;
        r.Ball(r.head, 0.62f, r.main);
        Eyes(r);
        r.back = new Vector3(0, 1.35f, -0.2f);
        r.hip = new Vector3(0, 0.8f, -0.65f);
        int pair = 0;
        for (int s = -1; s <= 1; s += 2)
        {
            int k = 0;
            foreach (float z in new[] { 0.25f, -0.1f, -0.45f })
            {
                var o = new Vector3(0.42f * s, 0.62f, z);
                r.Use(((s < 0) == (k % 2 == 0) ? "A" : "B") + (pair++), o);
                r.Ball(o + new Vector3(0.05f * s, -0.3f, 0), new Vector3(0.13f, 0.64f, 0.13f), r.dark, new Vector3(0, 0, 12 * s));
                k++;
            }
        }
        r.Use("Main", Vector3.zero);
    }

    static void Floater(Rig r)
    {
        float d = r.R(0.9f, 1.2f);
        r.width = d;
        r.head = new Vector3(0, 1.4f, 0);
        r.headR = d / 2;
        r.Ball(r.head, d, r.main);
        r.Ball(new Vector3(0, 1.4f - d * 0.33f, 0), new Vector3(d * 0.95f, d * 0.25f, d * 0.95f), r.dark);
        Eyes(r);
        r.back = r.head + new Vector3(0, d * 0.2f, -d * 0.4f);
        r.hip = r.head + new Vector3(0, -d * 0.2f, -d * 0.45f);
        r.Use("P", Vector3.zero, true);
        r.Ball(new Vector3(0, 1.4f - d * 0.42f, 0), d * 0.3f, r.glow);
        int n = r.rnd.Next(4, 7);
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2 / n;
            var o = new Vector3(Mathf.Cos(a) * d * 0.3f, 1.4f - d * 0.38f, Mathf.Sin(a) * d * 0.3f);
            r.Use((i % 2 == 0 ? "A" : "B") + i, o);
            float len = r.R(0.55f, 0.85f);
            r.Ball(o + new Vector3(0, -len / 2, 0), new Vector3(0.1f, len, 0.1f), r.light);
            r.Ball(o + new Vector3(0, -len, 0), 0.08f, r.glow);
        }
        r.Use("Main", Vector3.zero);
    }

    static void Serpent(Rig r)
    {
        r.width = 0.5f;
        for (int i = 0; i < 7; i++)
        {
            float y = 0.2f + i * 0.21f, z = Mathf.Sin(i * 0.9f) * 0.22f - 0.15f, s = Mathf.Lerp(0.62f, 0.38f, i / 6f);
            r.Ball(new Vector3(0, y, z), new Vector3(s, s * 0.85f, s), i % 2 == 0 ? r.main : r.dark);
        }
        for (int i = 0; i < 3; i++)
            r.Ball(new Vector3(0, 0.12f, -0.55f - i * 0.3f), new Vector3(0.45f - i * 0.1f, 0.22f, 0.4f), i % 2 == 0 ? r.dark : r.main);
        r.head = new Vector3(0, 1.66f, 0.12f);
        r.headR = 0.2f;
        r.Ball(r.head, new Vector3(0.38f, 0.32f, 0.48f), r.main);
        r.Box(r.head + new Vector3(0, -0.1f, 0.12f), new Vector3(0.26f, 0.06f, 0.3f), r.dark);
        Eyes(r);
        r.back = new Vector3(0, 1.3f, -0.2f);
        r.hip = new Vector3(0, 0.15f, -1.4f);
        r.hasArms = true;
        r.shoulder = new Vector3(0.2f, 1.25f, 0.05f);
        for (int s = -1; s <= 1; s += 2)
        {
            var o = new Vector3(0.2f * s, 1.25f, 0.05f);
            r.Use(s < 0 ? "A0" : "B0", o);
            r.Ball(o + new Vector3(0.03f * s, -0.15f, 0.05f), new Vector3(0.08f, 0.3f, 0.08f), r.dark);
        }
        r.Use("Main", Vector3.zero);
    }

    // ------------------------------------------------------------------ shared pieces

    static void Head(Rig r, bool boxy)
    {
        if (boxy) r.Box(r.head, new Vector3(r.headR * 2.1f, r.headR * 1.75f, r.headR * 2f), r.main);
        else r.Ball(r.head, r.headR * 2f, r.main);
        Eyes(r);
    }

    static void Eyes(Rig r)
    {
        var face = r.head + new Vector3(0, r.headR * 0.15f, r.headR * 0.85f);
        if (r.a.Has('y')) { r.Ball(face, r.headR * 0.8f, r.glow); return; }
        float e = r.headR * 0.38f;
        for (int s = -1; s <= 1; s += 2) r.Ball(face + new Vector3(r.headR * 0.42f * s, 0, -r.headR * 0.08f), e, r.glow);
        if (r.a.Has('e')) r.Ball(face + new Vector3(0, r.headR * 0.45f, -r.headR * 0.12f), e * 0.85f, r.glow);
    }

    static void Arms(Rig r, Vector3 shoulder, float thick, float len, Color c, Color hand, float handD)
    {
        r.hasArms = true;
        r.shoulder = shoulder;
        for (int s = -1; s <= 1; s += 2)
        {
            var o = new Vector3(shoulder.x * s, shoulder.y, shoulder.z);
            r.Use(s < 0 ? "A0" : "B0", o);
            r.Ball(o + new Vector3(0, -len / 2, 0), new Vector3(thick, len, thick), c);
            r.Ball(o + new Vector3(0, -len - 0.05f, 0), handD, hand);
        }
        r.Use("Main", Vector3.zero);
    }

    static void Legs(Rig r, float x, float hipY, float thick, Color c, bool boxy = false)
    {
        for (int s = -1; s <= 1; s += 2)
        {
            var o = new Vector3(x * s, hipY, 0);
            r.Use(s < 0 ? "B1" : "A1", o);
            if (boxy) r.Box(o + new Vector3(0, -hipY / 2, 0), new Vector3(thick, hipY, thick * 1.1f), c);
            else r.Ball(o + new Vector3(0, -hipY / 2, 0), new Vector3(thick, hipY, thick), c);
            r.Box(o + new Vector3(0, -hipY + 0.04f, 0.06f), new Vector3(thick, 0.08f, thick * 1.5f), r.dark);
        }
        r.Use("Main", Vector3.zero);
    }

    static void Extras(Rig r)
    {
        var a = r.a;
        var h = r.head; float hr = r.headR;
        if (a.Has('h'))
            for (int s = -1; s <= 1; s += 2)
                r.Ball(h + new Vector3(hr * 0.6f * s, hr * 0.95f, -hr * 0.1f), new Vector3(0.08f, hr * 1.5f, 0.08f), r.light, new Vector3(-10, 0, -25 * s));
        if (a.Has('g'))
            for (int s = -1; s <= 1; s += 2)
                r.Ball(h + new Vector3(hr * 0.4f * s, -hr * 0.55f, hr * 0.8f), new Vector3(0.06f, 0.26f, 0.06f), r.white, new Vector3(35, 0, 10 * s));
        if (a.Has('b') && a.plan != "avian")
            r.Box(h + new Vector3(0, -hr * 0.15f, hr * 1.05f), new Vector3(0.08f, 0.06f, 0.2f), r.light);
        if (a.Has('s'))
            for (int i = 0; i < 4; i++)
                r.Box(r.back + new Vector3(0, -i * 0.17f + 0.15f, -0.02f * i), new Vector3(0.07f, 0.28f, 0.07f), r.light, new Vector3(-35, 0, 0));
        if (a.Has('t'))
        {
            for (int i = 0; i < 4; i++)
                r.Ball(r.hip + new Vector3(0, -i * 0.06f, -0.15f - i * 0.2f), Vector3.one * (0.24f - i * 0.04f), r.main);
            r.Ball(r.hip + new Vector3(0, -0.27f, -0.98f), 0.12f, r.glow);
        }
        if (a.Has('m'))
            for (int i = 0; i < 9; i++)
            {
                float ang = Mathf.PI * (0.1f + 0.8f * i / 8f);
                r.Ball(h + new Vector3(Mathf.Cos(ang) * hr * 1.15f, Mathf.Sin(ang) * hr * 1.0f, -hr * 0.5f), hr * 0.75f, i % 2 == 0 ? r.dark : r.light);
            }
        if (a.Has('n'))
        {
            r.Box(h + new Vector3(0, hr + 0.06f, -0.05f), new Vector3(0.04f, 0.22f, 0.36f), r.dark, new Vector3(-20, 0, 0));
            r.Box(r.back + new Vector3(0, 0, -0.05f), new Vector3(0.04f, 0.4f, 0.32f), r.dark, new Vector3(-30, 0, 0));
        }
        if (a.Has('k'))
            r.Ball(r.back + new Vector3(0, -0.05f, -0.05f), new Vector3(r.width * 1.05f, r.width * 0.75f, r.width * 0.9f), r.dark);
        if (a.Has('w'))
            for (int s = -1; s <= 1; s += 2)
                r.Box(r.back + new Vector3(0.38f * s, 0.12f, -0.12f), new Vector3(0.7f, 0.42f, 0.02f),
                      new Color(r.glow.r, r.glow.g, r.glow.b, 0.35f), new Vector3(0, 25 * s, 20 * s));
        if (a.Has('x') && r.hasArms)
            for (int s = -1; s <= 1; s += 2)
            {
                var o = new Vector3(r.shoulder.x * s * 0.95f, r.shoulder.y - 0.3f, r.shoulder.z + 0.05f);
                r.Use(s < 0 ? "B2" : "A2", o);
                r.Ball(o + new Vector3(0, -0.28f, 0.05f), new Vector3(0.13f, 0.56f, 0.13f), r.main);
                r.Ball(o + new Vector3(0, -0.58f, 0.06f), 0.14f, r.light);
                r.Use("Main", Vector3.zero);
            }
        // glowing parts that pulse
        if (a.Has('f'))
        {
            r.Use("P", Vector3.zero, true);
            float fr = Mathf.Min(hr, 0.2f);   // big-headed aliens get a normal-sized flame
            r.Ball(h + new Vector3(0, hr + fr * 0.05f, -0.02f), fr * 1.7f, new Color(r.glow.r, r.glow.g * 0.6f, r.glow.b * 0.3f, 1f));
            r.Ball(h + new Vector3(0.02f, hr + fr * 1.1f, -0.05f), fr * 1.1f, r.glow);
            r.Ball(h + new Vector3(-0.03f, hr + fr * 1.9f, -0.08f), fr * 0.6f, r.glow);
            r.Use("Main", Vector3.zero);
        }
        if (a.Has('c'))
        {
            r.Use("P", Vector3.zero, true);
            var g = new Color(r.glow.r, r.glow.g, r.glow.b, 0.6f);
            for (int s = -1; s <= 1; s += 2)
            {
                r.Box(r.back + new Vector3(r.width * 0.55f * s, 0.25f, 0.1f), new Vector3(0.12f, 0.46f, 0.12f), g, new Vector3(0, 0, -25 * s));
                r.Box(r.back + new Vector3(0.13f * s, 0.05f, -0.06f), new Vector3(0.1f, 0.5f, 0.1f), g, new Vector3(-30, 0, -15 * s));
            }
            r.Use("Main", Vector3.zero);
        }
        if (a.Has('a'))
        {
            for (int s = -1; s <= 1; s += 2)
                r.Cyl(h + new Vector3(hr * 0.42f * s, hr + 0.14f, 0), new Vector3(0.03f, 0.34f, 0.03f), r.dark, new Vector3(0, 0, -22 * s));
            r.Use("P", Vector3.zero, true);
            for (int s = -1; s <= 1; s += 2)
                r.Ball(h + new Vector3(hr * 0.42f * s + 0.065f * s, hr + 0.3f, 0), 0.08f, r.glow);
            r.Use("Main", Vector3.zero);
        }
    }

    // ------------------------------------------------------------------ gallery

    /// <summary>Scale for statues so sizes read at a glance without giants towering over everything.</summary>
    public static float StatueScale(Alien a) => a.sizeName == "tiny" ? 0.35f : a.sizeName == "small" ? 0.6f
        : a.sizeName == "large" ? 1.3f : a.sizeName == "giant" ? 1.7f : 1f;
}
#endif
