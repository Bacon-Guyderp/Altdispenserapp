namespace N64AssetStudio.Core;

public enum Ucode { F3D, F3DEX, F3DEX2 }

public sealed class DisplayList
{
    public int BlobIndex;
    public int Offset;
    public int End;
    public Ucode Ucode;
    public int VtxCount, TriCount;
    public List<uint> Calls = new();
    public bool IsChild;
    public int? Base;
}

/// <summary>
/// Fast3D / F3DEX / F3DEX2 display-list detection and interpretation. Nearly every N64 game draws
/// through RSP display lists; they always end in G_ENDDL, which is how they're found.
/// </summary>
public static class F3D
{
    public const int G_SETTIMG = 0xFD, G_SETTILE = 0xF5, G_LOADBLOCK = 0xF3, G_LOADTILE = 0xF4,
        G_SETTILESIZE = 0xF2, G_LOADTLUT = 0xF0, G_SETPRIMCOLOR = 0xFA;

    static readonly bool[][] Ops = BuildOps();

    static bool[][] BuildOps()
    {
        var rdp = Enumerable.Range(0xE4, 0x1C).Where(o => o != 0xF1).ToArray();
        int[] f3d = { 0x01, 0x03, 0x04, 0x06, 0xB2, 0xB3, 0xB4, 0xB6, 0xB7, 0xB8, 0xB9, 0xBA, 0xBB, 0xBC, 0xBD, 0xBE, 0xBF, 0xC0 };
        int[] ex = { 0x01, 0x03, 0x04, 0x06, 0xB0, 0xB1, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6, 0xB7, 0xB8, 0xB9, 0xBA, 0xBB, 0xBC, 0xBD, 0xBE, 0xBF, 0xC0 };
        int[] ex2 = { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0xD3, 0xD4, 0xD5, 0xD6, 0xD7, 0xD8, 0xD9, 0xDA, 0xDB, 0xDC, 0xDD, 0xDE, 0xDF, 0xE0, 0xE1, 0xE2, 0xE3 };
        bool[] Make(int[] a) { var t = new bool[256]; foreach (var o in a.Concat(rdp)) t[o] = true; return t; }
        return new[] { Make(f3d), Make(ex), Make(ex2) };
    }

    public static int VtxOp(Ucode u) => u == Ucode.F3DEX2 ? 0x01 : 0x04;
    public static int Tri1Op(Ucode u) => u == Ucode.F3DEX2 ? 0x05 : 0xBF;
    public static int Tri2Op(Ucode u) => u switch { Ucode.F3DEX => 0xB1, Ucode.F3DEX2 => 0x06, _ => -1 };
    public static int QuadOp(Ucode u) => u switch { Ucode.F3DEX => 0xB5, Ucode.F3DEX2 => 0x07, _ => -1 };
    public static int DlOp(Ucode u) => u == Ucode.F3DEX2 ? 0xDE : 0x06;
    public static int EndOp(Ucode u) => u == Ucode.F3DEX2 ? 0xDF : 0xB8;
    static int TextureOp(Ucode u) => u == Ucode.F3DEX2 ? 0xD7 : 0xBB;
    static uint LightingBit(Ucode u) => u == Ucode.F3DEX2 ? 0x00200000u : 0x00020000u;
    static int OtherModeHOp(Ucode u) => u == Ucode.F3DEX2 ? 0xE3 : 0xBA;

    static bool AddrOk(uint a) { uint seg = a >> 24; return seg <= 0x0F || seg == 0x80; }

    static int VtxCountOf(Ucode u, uint w0) => u switch
    {
        Ucode.F3D => (int)((w0 >> 20) & 0xF) + 1,
        Ucode.F3DEX => (int)((w0 >> 10) & 0x3F),
        _ => (int)((w0 >> 12) & 0xFF),
    };

    static bool Idx(uint v, int mul, int max) => v % mul == 0 && v < max;

    public static bool Validate(Ucode u, uint w0, uint w1)
    {
        int op = (int)(w0 >> 24);
        if (!Ops[(int)u][op]) return op == 0 && w0 == 0 && w1 == 0;
        if (op == VtxOp(u))
        {
            if (!AddrOk(w1)) return false;
            if (u == Ucode.F3D)
            {
                uint n = ((w0 >> 20) & 0xF) + 1, v0 = (w0 >> 16) & 0xF;
                return (w0 & 0xFFFF) == n * 16 && v0 + n <= 16;
            }
            if (u == Ucode.F3DEX)
            {
                uint b1 = (w0 >> 16) & 0xFF, n = (w0 >> 10) & 0x3F;
                return b1 % 2 == 0 && n > 0 && (w0 & 0x3FF) == n * 16 - 1 && b1 / 2 + n <= 64;
            }
            uint nn = (w0 >> 12) & 0xFF, vend = (w0 >> 1) & 0x7F;
            return (w0 & 0x00F00F01) == 0 && nn > 0 && nn <= vend && vend <= 64;
        }
        if (op == Tri1Op(u))
        {
            if (u == Ucode.F3D)
                return Idx((w1 >> 16) & 0xFF, 10, 160) && Idx((w1 >> 8) & 0xFF, 10, 160) && Idx(w1 & 0xFF, 10, 160) && (w0 & 0xFFFFFF) == 0;
            uint s = u == Ucode.F3DEX2 ? w0 : w1;
            return Idx((s >> 16) & 0xFF, 2, 128) && Idx((s >> 8) & 0xFF, 2, 128) && Idx(s & 0xFF, 2, 128);
        }
        if (u != Ucode.F3D && (op == Tri2Op(u) || op == QuadOp(u)))
            return Idx((w0 >> 16) & 0xFF, 2, 128) && Idx((w0 >> 8) & 0xFF, 2, 128) && Idx(w0 & 0xFF, 2, 128)
                && Idx((w1 >> 16) & 0xFF, 2, 128) && Idx((w1 >> 8) & 0xFF, 2, 128) && Idx(w1 & 0xFF, 2, 128) && (w1 >> 24) == 0;
        if (op == DlOp(u)) return ((w0 >> 16) & 0xFF) <= 1 && (w0 & 0xFFFF) == 0 && AddrOk(w1);
        if (op == EndOp(u)) return (w0 & 0xFFFFFF) == 0 && w1 == 0;
        switch (op)
        {
            case G_SETTIMG: return AddrOk(w1) && (w0 & 0x0007F000) == 0;
            case 0x00: return w0 == 0 && w1 == 0;
            case 0xFF: return AddrOk(w1) && (w0 & 0x0007F000) == 0;
            case 0xFE: return AddrOk(w1) && (w0 & 0x00FFFFFF) == 0;
            case G_SETTILE: return (w0 & 0x00040000) == 0 && (w1 & 0xF8000000) == 0;
            case G_SETTILESIZE: case G_LOADBLOCK: case G_LOADTILE: case G_LOADTLUT: return (w1 & 0xF8000000) == 0;
            case 0xE6: case 0xE7: case 0xE8: case 0xE9: return (w0 & 0x00FFFFFF) == 0;
        }
        if (u == Ucode.F3DEX2)
        {
            switch (op)
            {
                case 0x04: { uint a = (w0 >> 12) & 0xFFF, b = w0 & 0xFFF; return b % 2 == 0 && a == b / 2 * 5; }
                case 0x03: return (w0 & 0x00FF0000) == 0 && (w1 >> 16) == 0;
                case 0xDA: return AddrOk(w1) && (w0 & 0x00FFFF00) == 0x00380000;
                case 0xDC: return AddrOk(w1);
            }
        }
        else
        {
            switch (op)
            {
                case 0x01: return AddrOk(w1) && (w0 & 0xFFFF) == 0x40;
                case 0x03: return AddrOk(w1);
                case 0xB6: case 0xB7: return (w0 & 0x00FFFFFF) == 0;
            }
        }
        return true;
    }

    static DisplayList ScanFromEnd(byte[] d, int endOff, Ucode u)
    {
        int start = endOff, p = endOff - 8, limit = Math.Max(0, endOff - 0x20000);
        while (p >= limit)
        {
            uint w0 = BE.U32(d, p), w1 = BE.U32(d, p + 4);
            if ((w0 >> 24) == EndOp(u) || !Validate(u, w0, w1)) break;
            start = p;
            p -= 8;
        }
        while (start < endOff && BE.U32(d, start) == 0 && BE.U32(d, start + 4) == 0) start += 8;
        var dl = new DisplayList { Offset = start, End = endOff + 8, Ucode = u };
        int vtx = VtxOp(u), tri1 = Tri1Op(u), tri2 = Tri2Op(u), quad = QuadOp(u), dlOp = DlOp(u);
        for (int q = start; q < endOff; q += 8)
        {
            uint w0 = BE.U32(d, q);
            int op = (int)(w0 >> 24);
            if (op == vtx) dl.VtxCount++;
            else if (op == tri1) dl.TriCount++;
            else if (op == tri2 || op == quad) dl.TriCount += 2;
            else if (op == dlOp) dl.Calls.Add(BE.U32(d, q + 4));
        }
        return dl;
    }

    static Ucode ClassifyF3dOrEx(byte[] d, int endOff)
    {
        int p = endOff - 8, limit = Math.Max(0, endOff - 0x4000), f3d = 0, ex = 0;
        while (p >= limit)
        {
            uint w0 = BE.U32(d, p), w1 = BE.U32(d, p + 4);
            int op = (int)(w0 >> 24);
            if (op == 0xB8) break;
            if (op == 0xBF)
            {
                uint a = (w1 >> 16) & 0xFF, b = (w1 >> 8) & 0xFF, c = w1 & 0xFF;
                bool all10 = a % 10 == 0 && b % 10 == 0 && c % 10 == 0;
                bool small2 = Idx(a, 2, 64) && Idx(b, 2, 64) && Idx(c, 2, 64);
                if (all10 && !small2) f3d++;
                else if (!all10) ex++;
            }
            else if (op is 0xB1 or 0xB5) ex++;
            else if (op == 0x04)
            {
                if ((w0 & 0xFFFF) % 16 == 0 && ((w0 >> 20) & 0xF) + 1 == (w0 & 0xFFFF) / 16) f3d++;
                else if ((w0 & 0x3FF) == ((w0 >> 10) & 0x3F) * 16 - 1) ex++;
            }
            p -= 8;
        }
        return f3d > ex ? Ucode.F3D : Ucode.F3DEX;
    }

    static readonly byte[] EndF3d = { 0xB8, 0, 0, 0, 0, 0, 0, 0 };
    static readonly byte[] EndEx2 = { 0xDF, 0, 0, 0, 0, 0, 0, 0 };

    public static List<DisplayList> FindDisplayLists(byte[] d, int blobIndex = 0, int minTris = 1)
    {
        var found = new Dictionary<int, DisplayList>();
        foreach (var (pattern, ex2) in new[] { (EndF3d, false), (EndEx2, true) })
        {
            int i = BE.Find(d, pattern, 0);
            while (i != -1)
            {
                if (i % 8 == 0 && i >= 8)
                {
                    var u = ex2 ? Ucode.F3DEX2 : ClassifyF3dOrEx(d, i);
                    var dl = ScanFromEnd(d, i, u);
                    if (dl.Offset < i && ((dl.TriCount >= minTris && dl.VtxCount > 0) || dl.Calls.Count > 0))
                    {
                        dl.BlobIndex = blobIndex;
                        if (!found.TryGetValue(dl.Offset, out var prev) || prev.TriCount < dl.TriCount) found[dl.Offset] = dl;
                    }
                }
                i = BE.Find(d, pattern, i + 1);
            }
        }
        var list = found.Values.OrderBy(x => x.Offset).ToList();
        foreach (var dl in list)
            foreach (var a in dl.Calls)
                if (found.TryGetValue((int)(a & 0xFFFFFF), out var child) && child != dl) child.IsChild = true;
        return list.Where(x => x.TriCount > 0 || x.Calls.Any(a => found.ContainsKey((int)(a & 0xFFFFFF)))).ToList();
    }

    // ---------------------------------------------------------------- segment base discovery
    static List<(uint addr, int n)> VertexRefs(byte[] d, DisplayList dl)
    {
        var refs = new List<(uint, int)>();
        int vtx = VtxOp(dl.Ucode);
        for (int q = dl.Offset; q < dl.End - 8; q += 8)
        {
            uint w0 = BE.U32(d, q), w1 = BE.U32(d, q + 4);
            if ((w0 >> 24) == vtx && (w1 >> 24) < 0x10) refs.Add((w1, VtxCountOf(dl.Ucode, w0)));
        }
        return refs;
    }

    public static int? HomeSegment(byte[] d, DisplayList dl)
    {
        var refs = VertexRefs(d, dl);
        if (refs.Count == 0) return null;
        return refs.GroupBy(r => (int)(r.addr >> 24)).OrderByDescending(g => g.Count()).First().Key;
    }

    /// <summary>Lower is better: outlier-robust measure of how much the vertices look like a model.</summary>
    static double Coherence(byte[] d, int b, List<int> offs)
    {
        var pts = new List<(double, double, double)>();
        foreach (var o in offs.Take(256))
        {
            int p = b + o;
            if (p < 0 || p + 6 > d.Length) return 1e9;
            pts.Add((BE.S16(d, p), BE.S16(d, p + 2), BE.S16(d, p + 4)));
        }
        if (pts.Count < 3) return 0.5;
        double Median(IEnumerable<double> xs) { var a = xs.OrderBy(x => x).ToArray(); return a[a.Length / 2]; }
        double cx = Median(pts.Select(p => p.Item1)), cy = Median(pts.Select(p => p.Item2)), cz = Median(pts.Select(p => p.Item3));
        var radius = pts.Select(p => Math.Sqrt((p.Item1 - cx) * (p.Item1 - cx) + (p.Item2 - cy) * (p.Item2 - cy) + (p.Item3 - cz) * (p.Item3 - cz))).ToArray();
        double spread = Median(radius);
        if (spread < 1e-6) return 1e9;
        double outliers = radius.Count(r => r > spread * 8) / (double)radius.Length;
        double uniq = pts.Distinct().Count() / (double)pts.Count;
        double big = pts.Count(p => Math.Abs(p.Item1) > 20000 || Math.Abs(p.Item2) > 20000 || Math.Abs(p.Item3) > 20000) / (double)pts.Count;
        return outliers * 4 + (1 - uniq) * 2 + big * 2;
    }

    public static int GuessBase(byte[] d, DisplayList dl, bool preferZero)
    {
        var refs = VertexRefs(d, dl);
        if (refs.Count == 0) return 0;
        var offs = refs.SelectMany(r => Enumerable.Range(0, r.n).Select(i => (int)(r.addr & 0xFFFFFF) + i * 16)).Distinct().OrderBy(x => x).ToList();
        int maxOff = offs[^1] + 16, minOff = offs[0];
        bool Overlaps(long b) => b + minOff < dl.End && b + maxOff > dl.Offset;
        bool FlagsOk(long b)
        {
            if (b < 0 || b + maxOff > d.Length || Overlaps(b)) return false;
            foreach (var o in offs.Take(128)) if (d[b + o + 6] != 0 || d[b + o + 7] != 0) return false;
            return true;
        }
        if (preferZero && FlagsOk(0) && Coherence(d, 0, offs) < 1e8) return 0;

        const int window = 0x80000;
        int lo = Math.Max(0, dl.Offset - window), hi = Math.Min(d.Length - maxOff, dl.Offset + window);
        if (hi <= lo) return 0;
        var good = new List<int>();
        var check = offs.Take(96).ToArray();
        for (int b = lo - lo % 8; b < hi; b += 8)
        {
            if (Overlaps(b)) continue;
            bool ok = true;
            foreach (var o in check)
            {
                int i = b + o + 6;
                if (i + 1 >= d.Length || d[i] != 0 || d[i + 1] != 0) { ok = false; break; }
            }
            if (ok) good.Add(b);
        }
        if (good.Count == 0) return preferZero ? 0 : Math.Max(0, dl.Offset - maxOff);
        var ordered = good.OrderBy(b => Math.Abs(b + maxOff / 2 - dl.Offset)).Take(64).ToList();
        int best = ordered[0];
        double bestScore = 1e9;
        foreach (var b in ordered)
        {
            double s = Coherence(d, b, offs);
            if (s < bestScore - 1e-9) { best = b; bestScore = s; }
        }
        return best;
    }

    public static Mesh BuildMesh(byte[] d, DisplayList dl, int? baseOverride, Rom? rom, string name, bool preferZero)
    {
        int b;
        if (baseOverride.HasValue) b = baseOverride.Value;
        else { dl.Base ??= GuessBase(d, dl, preferZero); b = dl.Base.Value; }
        var seg = HomeSegment(d, dl);
        var interp = new DisplayListInterpreter(d, dl.Ucode, b, seg, rom);
        var mesh = interp.Run(dl.Offset, name);
        mesh.Notes.Insert(0, $"{dl.Ucode} display list @ 0x{dl.Offset:X}, segment base 0x{b:X}" + (seg.HasValue ? $", home segment 0x{seg:X2}" : ""));
        return mesh;
    }

    // ------------------------------------------------------------------- interpreter
    sealed class Tile
    {
        public int Fmt, Siz = 2, Line, Tmem, Palette, Cms, Cmt, Uls, Ult, Lrs = 31 * 4, Lrt = 31 * 4;
        public int W => ((Lrs - Uls) >> 2) + 1;
        public int H => ((Lrt - Ult) >> 2) + 1;
    }

    record struct RawVtx(int Addr, short X, short Y, short Z, short S, short T, byte C0, byte C1, byte C2, byte C3, bool Lit);

    public sealed class DisplayListInterpreter
    {
        readonly byte[] _d;
        readonly Ucode _u;
        readonly int _base;
        readonly int? _home;
        readonly Rom? _rom;
        public readonly HashSet<int> Unresolved = new();

        public DisplayListInterpreter(byte[] d, Ucode u, int b, int? home, Rom? rom)
        {
            _d = d; _u = u; _base = b; _home = home; _rom = rom;
        }

        public int? Resolve(uint addr)
        {
            int seg = (int)(addr >> 24), off = (int)(addr & 0xFFFFFF);
            if (seg >= 0x80)
                return _rom != null && _d.Length == _rom.Data.Length ? _rom.EntryToRom(addr) : null;
            if (_home.HasValue && seg != _home.Value && seg != 0) { Unresolved.Add(seg); return null; }
            int p = _base + off;
            return p >= 0 && p < _d.Length ? p : null;
        }

        public Mesh Run(int start, string name, int maxCommands = 200000)
        {
            var u = _u;
            var mesh = new Mesh(name);
            var vbuf = new RawVtx?[64];
            var vcache = new Dictionary<(int, int), int>();
            var matCache = new Dictionary<(int?, int, int, int, int, int?, bool), int>();
            var tiles = Enumerable.Range(0, 8).Select(_ => new Tile()).ToArray();
            uint timgAddr = 0;
            int? texAddr = null, tlutAddr = null;
            bool tlutIa = false, lighting = true;
            double scaleS = 1, scaleT = 1;
            int renderTile = 0;
            uint geo = LightingBit(u);
            float[] prim = { 1, 1, 1, 1 };
            var stack = new Stack<int>();
            int pc = start, calls = 0;
            int div = u == Ucode.F3D ? 10 : 2;

            int CurrentMaterial()
            {
                var t = tiles[renderTile];
                int w = t.W, h = t.H;
                var key = (texAddr, t.Fmt, t.Siz, w, h, tlutAddr, tlutIa);
                if (matCache.TryGetValue(key, out int mi)) return mi;
                var mat = new Material { Name = $"mat_{mesh.Materials.Count}", Color = (float[])prim.Clone() };
                if (texAddr.HasValue && w > 0 && w <= 1024 && h > 0 && h <= 1024)
                {
                    string fname = Texture.FormatName(t.Fmt, t.Siz);
                    byte[]? pal = null;
                    if (t.Fmt == 2 && tlutAddr.HasValue)
                    {
                        int count = t.Siz == 0 ? 16 : 256;
                        pal = Texture.DecodePalette(_d, tlutAddr.Value + (t.Siz == 0 ? t.Palette * 32 : 0), count, tlutIa);
                    }
                    try
                    {
                        mat.Texture = Texture.Decode(_d, texAddr.Value, w, h, fname, pal);
                        mat.TextureInfo = $"{fname} {w}x{h} @ 0x{texAddr.Value:X}";
                        mat.Name = $"tex_{texAddr.Value:X6}_{fname}_{w}x{h}";
                    }
                    catch (ArgumentException) { mat.Texture = null; }
                    mat.WrapS = (t.Cms & 2) != 0 ? "clamp" : (t.Cms & 1) != 0 ? "mirror" : "repeat";
                    mat.WrapT = (t.Cmt & 2) != 0 ? "clamp" : (t.Cmt & 1) != 0 ? "mirror" : "repeat";
                }
                mesh.Materials.Add(mat);
                return matCache[key] = mesh.Materials.Count - 1;
            }

            void EmitTri(int i0, int i1, int i2)
            {
                if ((uint)i0 >= 64 || (uint)i1 >= 64 || (uint)i2 >= 64) return;
                if (vbuf[i0] is not RawVtx a || vbuf[i1] is not RawVtx b || vbuf[i2] is not RawVtx c) return;
                int m = CurrentMaterial();
                var t = tiles[renderTile];
                int w = Math.Max(1, t.W), h = Math.Max(1, t.H);
                Span<int> outIdx = stackalloc int[3];
                int k = 0;
                foreach (var raw in new[] { a, b, c })
                {
                    if (!vcache.TryGetValue((raw.Addr, m), out int idx))
                    {
                        var v = new Vertex(raw.X, raw.Y, raw.Z,
                            (float)((raw.S * scaleS / 32.0 - t.Uls / 4.0) / w),
                            (float)((raw.T * scaleT / 32.0 - t.Ult / 4.0) / h));
                        if (raw.Lit)
                        {
                            double nx = (sbyte)raw.C0, ny = (sbyte)raw.C1, nz = (sbyte)raw.C2;
                            double l = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                            if (l == 0) l = 1;
                            v.NX = (float)(nx / l); v.NY = (float)(ny / l); v.NZ = (float)(nz / l);
                            v.A = raw.C3 / 255f;
                            mesh.HasNormals = true;
                        }
                        else
                        {
                            v.R = raw.C0 / 255f; v.G = raw.C1 / 255f; v.B = raw.C2 / 255f; v.A = raw.C3 / 255f;
                            mesh.HasColors = true;
                        }
                        mesh.Vertices.Add(v);
                        idx = vcache[(raw.Addr, m)] = mesh.Vertices.Count - 1;
                    }
                    outIdx[k++] = idx;
                }
                if (outIdx[0] != outIdx[1] && outIdx[1] != outIdx[2] && outIdx[0] != outIdx[2])
                    mesh.Triangles.Add((outIdx[0], outIdx[1], outIdx[2], m));
            }

            for (int steps = 0; steps < maxCommands; steps++)
            {
                if (pc < 0 || pc + 8 > _d.Length)
                {
                    if (stack.Count > 0) { pc = stack.Pop(); continue; }
                    break;
                }
                uint w0 = BE.U32(_d, pc), w1 = BE.U32(_d, pc + 4);
                pc += 8;
                int op = (int)(w0 >> 24);

                if (op == VtxOp(u))
                {
                    int n, v0;
                    if (u == Ucode.F3D) { n = (int)((w0 >> 20) & 0xF) + 1; v0 = (int)((w0 >> 16) & 0xF); }
                    else if (u == Ucode.F3DEX) { v0 = (int)((w0 >> 16) & 0xFF) / 2; n = (int)((w0 >> 10) & 0x3F); }
                    else { n = (int)((w0 >> 12) & 0xFF); v0 = (int)((w0 >> 1) & 0x7F) - n; }
                    var p = Resolve(w1);
                    if (p == null) continue;
                    for (int i = 0; i < n; i++)
                    {
                        int q = p.Value + i * 16;
                        if (q + 16 > _d.Length || v0 + i < 0 || v0 + i >= 64) break;
                        vbuf[v0 + i] = new RawVtx(q, BE.S16(_d, q), BE.S16(_d, q + 2), BE.S16(_d, q + 4),
                            BE.S16(_d, q + 8), BE.S16(_d, q + 10), _d[q + 12], _d[q + 13], _d[q + 14], _d[q + 15], lighting);
                    }
                }
                else if (op == Tri1Op(u))
                {
                    uint s = u == Ucode.F3DEX2 ? w0 : w1;
                    EmitTri((int)((s >> 16) & 0xFF) / div, (int)((s >> 8) & 0xFF) / div, (int)(s & 0xFF) / div);
                }
                else if (op == Tri2Op(u) || op == QuadOp(u))
                {
                    EmitTri((int)((w0 >> 16) & 0xFF) / 2, (int)((w0 >> 8) & 0xFF) / 2, (int)(w0 & 0xFF) / 2);
                    EmitTri((int)((w1 >> 16) & 0xFF) / 2, (int)((w1 >> 8) & 0xFF) / 2, (int)(w1 & 0xFF) / 2);
                }
                else if (op == DlOp(u))
                {
                    var target = Resolve(w1);
                    if (target == null || ++calls > 4096) continue;
                    if (((w0 >> 16) & 0xFF) == 0)
                    {
                        if (stack.Count < 32) { stack.Push(pc); pc = target.Value; }
                    }
                    else pc = target.Value;
                }
                else if (op == EndOp(u))
                {
                    if (stack.Count > 0) pc = stack.Pop(); else break;
                }
                else if (op == TextureOp(u))
                {
                    uint ss = (w1 >> 16) & 0xFFFF, ts = w1 & 0xFFFF;
                    scaleS = (ss == 0 ? 0xFFFF : ss) / 65536.0;
                    scaleT = (ts == 0 ? 0xFFFF : ts) / 65536.0;
                    renderTile = (int)((w0 >> 8) & 7);
                }
                else if (u == Ucode.F3DEX2 && op == 0xD9)
                {
                    geo = (geo & (w0 & 0xFFFFFF)) | w1;
                    lighting = (geo & LightingBit(u)) != 0;
                }
                else if (u != Ucode.F3DEX2 && op == 0xB7) { geo |= w1; lighting = (geo & LightingBit(u)) != 0; }
                else if (u != Ucode.F3DEX2 && op == 0xB6) { geo &= ~w1; lighting = (geo & LightingBit(u)) != 0; }
                else if (op == OtherModeHOp(u))
                {
                    int shift = u == Ucode.F3DEX2 ? 32 - (int)((w0 >> 8) & 0xFF) - ((int)(w0 & 0xFF) + 1) : (int)((w0 >> 8) & 0xFF);
                    if (shift == 14) tlutIa = ((w1 >> 14) & 3) == 3;
                }
                else if (op == G_SETTIMG) timgAddr = w1;
                else if (op == G_SETTILE)
                {
                    var t = tiles[(w1 >> 24) & 7];
                    t.Fmt = (int)((w0 >> 21) & 7); t.Siz = (int)((w0 >> 19) & 3);
                    t.Line = (int)((w0 >> 9) & 0x1FF); t.Tmem = (int)(w0 & 0x1FF);
                    t.Palette = (int)((w1 >> 20) & 0xF); t.Cmt = (int)((w1 >> 18) & 3); t.Cms = (int)((w1 >> 8) & 3);
                }
                else if (op is G_LOADBLOCK or G_LOADTILE) texAddr = Resolve(timgAddr);
                else if (op == G_SETTILESIZE)
                {
                    var t = tiles[(w1 >> 24) & 7];
                    t.Uls = (int)((w0 >> 12) & 0xFFF); t.Ult = (int)(w0 & 0xFFF);
                    t.Lrs = (int)((w1 >> 12) & 0xFFF); t.Lrt = (int)(w1 & 0xFFF);
                }
                else if (op == G_LOADTLUT) tlutAddr = Resolve(timgAddr);
                else if (op == G_SETPRIMCOLOR)
                    prim = new[] { ((w1 >> 24) & 0xFF) / 255f, ((w1 >> 16) & 0xFF) / 255f, ((w1 >> 8) & 0xFF) / 255f, (w1 & 0xFF) / 255f };
                else if (!Ops[(int)u][op] && !(w0 == 0 && w1 == 0))
                {
                    mesh.Notes.Add($"stopped at unknown opcode 0x{op:X2} @ 0x{pc - 8:X}");
                    break;
                }
            }
            if (Unresolved.Count > 0)
                mesh.Notes.Add("unresolved segments: " + string.Join(", ", Unresolved.OrderBy(x => x).Select(x => $"0x{x:X2}")));
            return mesh;
        }
    }
}
