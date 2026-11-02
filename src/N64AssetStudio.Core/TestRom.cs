

namespace N64AssetStudio.Core;

/// <summary>
/// Builds a synthetic N64 ROM from scratch that exercises every decoder: real Yaz0/Yay0/MIO0
/// encoders, F3D/F3DEX/F3DEX2 display lists, RGBA16 + CI4 textures, a VADPCM sound bank,
/// compressed-MIDI music and MIPS code. No copyrighted data needed.
/// </summary>
public static class TestRom
{
    // ------------------------------------------------------------------ LZ
    abstract record Tok;
    sealed record Lit(byte B) : Tok;
    sealed record Ref(int Dist, int Len) : Tok;

    static List<Tok> LzTokens(byte[] d, int maxLen, int minLen = 3, int window = 0x1000)
    {
        var toks = new List<Tok>();
        var head = new Dictionary<int, List<int>>();
        int Key(int i) => i + 2 < d.Length ? (d[i] << 16) | (d[i + 1] << 8) | d[i + 2] : -1 - i;
        void Index(int i) { int k = Key(i); if (!head.TryGetValue(k, out var l)) head[k] = l = new(); l.Add(i); }
        int pos = 0;
        while (pos < d.Length)
        {
            int bestLen = 0, bestDist = 0;
            if (head.TryGetValue(Key(pos), out var cands))
                for (int c = cands.Count - 1; c >= Math.Max(0, cands.Count - 64); c--)
                {
                    int j = cands[c], dist = pos - j;
                    if (dist > window) break;
                    int ln = 0;
                    while (ln < maxLen && pos + ln < d.Length && d[j + ln] == d[pos + ln]) ln++;
                    if (ln > bestLen) { bestLen = ln; bestDist = dist; }
                }
            if (bestLen >= minLen)
            {
                toks.Add(new Ref(bestDist, bestLen));
                for (int k = 0; k < bestLen; k++) Index(pos + k);
                pos += bestLen;
            }
            else { toks.Add(new Lit(d[pos])); Index(pos); pos++; }
        }
        return toks;
    }

    static void Be32(List<byte> o, uint v) { o.Add((byte)(v >> 24)); o.Add((byte)(v >> 16)); o.Add((byte)(v >> 8)); o.Add((byte)v); }
    static void Be16(List<byte> o, int v) { o.Add((byte)(v >> 8)); o.Add((byte)v); }

    public static byte[] Yaz0(byte[] d)
    {
        var o = new List<byte>("Yaz0"u8.ToArray());
        Be32(o, (uint)d.Length);
        o.AddRange(new byte[8]);
        var toks = LzTokens(d, 0x111);
        for (int g = 0; g < toks.Count; g += 8)
        {
            int code = 0;
            var body = new List<byte>();
            for (int bi = 0; bi < 8 && g + bi < toks.Count; bi++)
            {
                switch (toks[g + bi])
                {
                    case Lit l: code |= 0x80 >> bi; body.Add(l.B); break;
                    case Ref r:
                        int dist = r.Dist - 1;
                        if (r.Len >= 0x12) { body.Add((byte)(dist >> 8)); body.Add((byte)dist); body.Add((byte)(r.Len - 0x12)); }
                        else { body.Add((byte)(((r.Len - 2) << 4) | (dist >> 8))); body.Add((byte)dist); }
                        break;
                }
            }
            o.Add((byte)code);
            o.AddRange(body);
        }
        return o.ToArray();
    }

    public static byte[] Yay0(byte[] d)
    {
        var toks = LzTokens(d, 0x111);
        var masks = new List<uint>(); var links = new List<byte>(); var chunks = new List<byte>();
        uint mask = 0; int bits = 0;
        foreach (var t in toks)
        {
            mask <<= 1;
            if (t is Lit l) { mask |= 1; chunks.Add(l.B); }
            else if (t is Ref r)
            {
                int dist = r.Dist - 1;
                if (r.Len >= 0x12) { Be16(links, dist); chunks.Add((byte)(r.Len - 0x12)); }
                else Be16(links, ((r.Len - 2) << 12) | dist);
            }
            if (++bits == 32) { masks.Add(mask); mask = 0; bits = 0; }
        }
        if (bits > 0) masks.Add(mask << (32 - bits));
        var mb = new List<byte>();
        foreach (var m in masks) Be32(mb, m);
        var o = new List<byte>("Yay0"u8.ToArray());
        Be32(o, (uint)d.Length); Be32(o, (uint)(16 + mb.Count)); Be32(o, (uint)(16 + mb.Count + links.Count));
        o.AddRange(mb); o.AddRange(links); o.AddRange(chunks);
        return o.ToArray();
    }

    public static byte[] Mio0(byte[] d)
    {
        var toks = LzTokens(d, 18);
        var flags = new List<byte>(); var comp = new List<byte>(); var unc = new List<byte>();
        int cur = 0, bits = 0;
        foreach (var t in toks)
        {
            cur <<= 1;
            if (t is Lit l) { cur |= 1; unc.Add(l.B); }
            else if (t is Ref r) Be16(comp, ((r.Len - 3) << 12) | (r.Dist - 1));
            if (++bits == 8) { flags.Add((byte)cur); cur = 0; bits = 0; }
        }
        if (bits > 0) flags.Add((byte)((cur << (8 - bits)) & 0xFF));
        while (flags.Count % 4 != 0) flags.Add(0);
        var o = new List<byte>("MIO0"u8.ToArray());
        Be32(o, (uint)d.Length); Be32(o, (uint)(16 + flags.Count)); Be32(o, (uint)(16 + flags.Count + comp.Count));
        o.AddRange(flags); o.AddRange(comp); o.AddRange(unc);
        return o.ToArray();
    }

    // -------------------------------------------------------------- models
    public static readonly (short, short, short)[] Cube =
    {
        (-100, -100, -100), (100, -100, -100), (100, 100, -100), (-100, 100, -100),
        (-100, -100, 100), (100, -100, 100), (100, 100, 100), (-100, 100, 100),
    };
    public static readonly (int, int, int)[] CubeTris =
    {
        (0, 1, 2), (0, 2, 3), (4, 6, 5), (4, 7, 6), (0, 4, 5), (0, 5, 1),
        (3, 2, 6), (3, 6, 7), (1, 5, 6), (1, 6, 2), (0, 3, 7), (0, 7, 4),
    };

    static byte[] Vertices(IEnumerable<(short x, short y, short z)> pts, bool color)
    {
        var o = new List<byte>();
        int i = 0;
        foreach (var (x, y, z) in pts)
        {
            Be16(o, x); Be16(o, y); Be16(o, z); Be16(o, 0);
            Be16(o, (i & 1) * 1024); Be16(o, ((i >> 1) & 1) * 1024);
            if (color) o.AddRange(new byte[] { 255, (byte)(i * 30), 64, 255 }); else o.AddRange(new byte[] { 0, 127, 0, 255 });
            i++;
        }
        return o.ToArray();
    }

    static byte[] CheckerRgba16(int w = 32, int h = 32)
    {
        var o = new List<byte>();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool on = ((x / 4) + (y / 4)) % 2 == 1;
                int r = 31, g = on ? 0 : 31, b = on ? 0 : 31;
                Be16(o, (r << 11) | (g << 6) | (b << 1) | 1);
            }
        return o.ToArray();
    }

    static void W(List<byte> o, uint w0, uint w1) { Be32(o, w0); Be32(o, w1); }

    public static (byte[] blob, int dl) F3dex2Object(int seg = 6)
    {
        var tex = CheckerRgba16();
        var vtx = Vertices(Cube, false);
        int texOff = 0x10, vtxOff = texOff + tex.Length, dlOff = vtxOff + vtx.Length;
        uint S = (uint)seg << 24;
        var dl = new List<byte>();
        W(dl, 0xE7000000, 0); W(dl, 0xD9FFFFFF, 0x00200004); W(dl, 0xD7000002, 0xFFFFFFFF);
        W(dl, 0xFD100000, S | (uint)texOff); W(dl, 0xF5100000, 0x07000000); W(dl, 0xE6000000, 0);
        W(dl, 0xF3000000, 0x073FF100); W(dl, 0xE7000000, 0); W(dl, 0xF5101000, 0); W(dl, 0xF2000000, 0x0007C07C);
        W(dl, 0x01000000 | (8 << 12) | (8 << 1), S | (uint)vtxOff);
        for (int k = 0; k < CubeTris.Length; k += 2)
        {
            var (a0, a1, a2) = CubeTris[k]; var (b0, b1, b2) = CubeTris[k + 1];
            W(dl, (uint)(0x06000000 | (a0 * 2 << 16) | (a1 * 2 << 8) | a2 * 2), (uint)((b0 * 2 << 16) | (b1 * 2 << 8) | b2 * 2));
        }
        W(dl, 0xDF000000, 0);
        return (new byte[texOff].Concat(tex).Concat(vtx).Concat(dl).ToArray(), dlOff);
    }

    public static (byte[] blob, int dl) F3dexCi4Object(int seg = 7)
    {
        var pix = new List<byte>();
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x += 2) pix.Add((byte)((((x + y) % 16) << 4) | ((x + 1 + y) % 16)));
        var pal = new List<byte>();
        for (int i = 0; i < 16; i++) Be16(pal, ((i * 2) << 11) | ((31 - i * 2) << 6) | (i << 1) | 1);
        var vtx = Vertices(Cube.Take(4), true);
        int palOff = 0x10, pixOff = 0x30, vtxOff = pixOff + pix.Count, dlOff = vtxOff + vtx.Length;
        uint S = (uint)seg << 24;
        var dl = new List<byte>();
        W(dl, 0xB6000000, 0x00020000); W(dl, 0xBB000001, 0xFFFFFFFF); W(dl, 0xBA000E02, 0x00008000);
        W(dl, 0xFD100000, S | (uint)palOff); W(dl, 0xF5000100, 0x07000000); W(dl, 0xF0000000, 0x0703C000);
        W(dl, 0xFD500000, S | (uint)pixOff); W(dl, 0xF5500000, 0x07000000); W(dl, 0xF3000000, 0x0707F800);
        W(dl, 0xF5400200, 0); W(dl, 0xF2000000, 0x0003C03C);
        W(dl, 0x04000000 | (4 << 10) | (4 * 16 - 1), S | (uint)vtxOff);
        W(dl, 0xB1000000 | (0 << 16) | (2 << 8) | 4, (0 << 16) | (4 << 8) | 6);
        W(dl, 0xB8000000, 0);
        return (new byte[palOff].Concat(pal).Concat(pix).Concat(vtx).Concat(dl).ToArray(), dlOff);
    }

    public static (byte[] blob, int dl) F3dObject(int seg = 4)
    {
        var vtx = Vertices(Cube, true);
        int vtxOff = 0x20, dlOff = vtxOff + vtx.Length;
        uint S = (uint)seg << 24;
        var dl = new List<byte>();
        W(dl, 0xB6000000, 0x00020000);
        W(dl, 0x04000000 | (7 << 20) | 0x80, S | (uint)vtxOff);
        foreach (var (a, b, c) in CubeTris) W(dl, 0xBF000000, (uint)((a * 10 << 16) | (b * 10 << 8) | c * 10));
        W(dl, 0xB8000000, 0);
        var prefix = Enumerable.Range(0, 0x20).Select(i => (byte)(i % 2 == 0 ? 0x12 : 0x34));
        return (prefix.Concat(vtx).Concat(dl).ToArray(), dlOff);
    }

    // --------------------------------------------------------------- audio
    public static readonly short[] Book =
    {
        -2000, -1500, -1000, -600, -300, -100, 0, 50, 3000, 2500, 2000, 1500, 1000, 600, 300, 100,
        0, 0, 0, 0, 0, 0, 0, 0, 2048, 0, 0, 0, 0, 0, 0, 0,
    };

    public static byte[] AdpcmFrames(int n = 40)
    {
        var o = new List<byte>();
        for (int f = 0; f < n; f++)
        {
            o.Add((byte)(((f % 5) << 4) | (f % 2)));
            for (int k = 0; k < 8; k++) o.Add((byte)(((f * 3 + k * 5) & 0xFF) ^ 0x5A));
        }
        return o.ToArray();
    }

    public static (byte[] ctl, byte[] tbl) CtlTbl()
    {
        var wave = AdpcmFrames();
        var raw16 = new List<byte>();
        for (int i = 0; i < 64; i++) Be16(raw16, (int)(10000 * ((i % 16) - 8) / 8.0));
        var tbl = wave.Concat(raw16).ToArray();
        const int BANK = 0x10, INST = 0x20, SOUND0 = 0x40, SOUND1 = 0x50, ENV = 0x60, KEY = 0x70, WAVE0 = 0x80, WAVE1 = 0x98, BOOK = 0xB0, LOOP = 0x130;
        var c = new byte[0x160];
        BE.W16(c, 0, 0x4231); BE.W16(c, 2, 1); BE.W32(c, 4, BANK);
        BE.W16(c, BANK, 1); BE.W32(c, BANK + 4, 22050); BE.W32(c, BANK + 8, 0); BE.W32(c, BANK + 12, INST);
        c[INST] = 127; c[INST + 1] = 64; c[INST + 2] = 5; BE.W16(c, INST + 12, 200); BE.W16(c, INST + 14, 2);
        BE.W32(c, INST + 16, SOUND0); BE.W32(c, INST + 20, SOUND1);
        foreach (var (so, wo) in new[] { (SOUND0, WAVE0), (SOUND1, WAVE1) })
        {
            BE.W32(c, so, ENV); BE.W32(c, so + 4, KEY); BE.W32(c, so + 8, (uint)wo); c[so + 12] = 64; c[so + 13] = 127;
        }
        BE.W32(c, ENV, 1000); BE.W32(c, ENV + 4, 100000); BE.W32(c, ENV + 8, 50000); c[ENV + 12] = 127; c[ENV + 13] = 100;
        c[KEY] = 0; c[KEY + 1] = 127; c[KEY + 2] = 0; c[KEY + 3] = 127; c[KEY + 4] = 60; c[KEY + 5] = 0;
        BE.W32(c, WAVE0, 0); BE.W32(c, WAVE0 + 4, (uint)wave.Length); c[WAVE0 + 8] = 0; BE.W32(c, WAVE0 + 12, LOOP); BE.W32(c, WAVE0 + 16, BOOK);
        BE.W32(c, WAVE1, (uint)wave.Length); BE.W32(c, WAVE1 + 4, (uint)raw16.Count); c[WAVE1 + 8] = 1; BE.W32(c, WAVE1 + 12, 0);
        BE.W32(c, BOOK, 2); BE.W32(c, BOOK + 4, 2);
        for (int i = 0; i < Book.Length; i++) BE.W16(c, BOOK + 8 + i * 2, Book[i]);
        BE.W32(c, LOOP, 16); BE.W32(c, LOOP + 4, 600); BE.W32(c, LOOP + 8, 0xFFFFFFFF);
        return (c, tbl);
    }

    public static byte[] CSeq()
    {
        var track = new List<byte>();
        track.AddRange(new byte[] { 0x00, 0xFF, 0x51, 0x03, 0x07, 0xA1, 0x20 });
        track.AddRange(new byte[] { 0x00, 0xC0, 0x05 });
        track.AddRange(new byte[] { 0x00, 0x90, 0x3C, 0x64, 0x60, 0x60, 0x3E, 0x64, 0x60, 0x60, 0x40, 0x64, 0x60 });
        int back = track.Count - (7 + 3 + 5);
        track.AddRange(new byte[] { 0xFE, (byte)(back >> 8), (byte)back, 8 });
        track.AddRange(new byte[] { 0x60, 0xFF, 0x2F });
        var hdr = new List<byte>();
        Be32(hdr, 0x44);
        for (int i = 0; i < 15; i++) Be32(hdr, 0);
        Be32(hdr, 96);
        return hdr.Concat(track).ToArray();
    }

    static byte[] SeqFile(byte[] cseq)
    {
        var o = new List<byte>();
        Be16(o, 0x5331); Be16(o, 1); Be32(o, 0x10); Be32(o, (uint)cseq.Length); Be32(o, 0);
        return o.Concat(cseq).ToArray();
    }

    static byte[] Midi()
    {
        var trk = new byte[] { 0x00, 0x90, 0x3C, 0x40, 0x60, 0x80, 0x3C, 0x00, 0x00, 0xFF, 0x2F, 0x00 };
        var o = new List<byte>("MThd"u8.ToArray());
        Be32(o, 6); Be16(o, 0); Be16(o, 1); Be16(o, 96);
        o.AddRange("MTrk"u8.ToArray()); Be32(o, (uint)trk.Length); o.AddRange(trk);
        return o.ToArray();
    }

    static byte[] Code()
    {
        var o = new List<byte>();
        foreach (var w in new uint[] { 0x27BDFFE8, 0xAFBF0014, 0x0C000108, 0, 0x8FBF0014, 0x03E00008, 0x27BD0018, 0 }) Be32(o, w);
        foreach (var w in new uint[] { 0x3C028000, 0x24420010, 0x03E00008, 0 }) Be32(o, w);
        return o.ToArray();
    }

    // ----------------------------------------------------------------- ROM
    public sealed class Layout
    {
        public int Yaz0Off, Mio0Off, Yay0Off, RawObjOff, RawDl, Ctl, Tbl, Sbk, Midi;
    }

    static void Align(List<byte> r, int a = 16) { while (r.Count % a != 0) r.Add(0); }

    public static (byte[] rom, Layout info) Build()
    {
        var info = new Layout();
        var rom = new List<byte>(new byte[0x1000]);
        var hdr = new byte[0x40];
        BE.W32(hdr, 0, 0x80371240); BE.W32(hdr, 4, 0xF); BE.W32(hdr, 8, 0x80000400); BE.W32(hdr, 12, 0x144B);
        "N64STUDIO TEST      "u8.CopyTo(hdr.AsSpan(0x20));
        "NTSE"u8.CopyTo(hdr.AsSpan(0x3B));
        for (int i = 0; i < 0x40; i++) rom[i] = hdr[i];
        rom.AddRange(Code());
        while (rom.Count < 0x2000) rom.Add(0);
        var (obj, _) = F3dex2Object();
        info.Yaz0Off = rom.Count; rom.AddRange(Yaz0(obj)); Align(rom);
        var (obj2, _) = F3dexCi4Object();
        info.Mio0Off = rom.Count; rom.AddRange(Mio0(obj2)); Align(rom);
        info.Yay0Off = rom.Count; rom.AddRange(Yay0(obj)); Align(rom);
        var (obj3, dl3) = F3dObject();
        info.RawObjOff = rom.Count; info.RawDl = rom.Count + dl3; rom.AddRange(obj3); Align(rom);
        var (ctl, tbl) = CtlTbl();
        info.Ctl = rom.Count; rom.AddRange(ctl); Align(rom);
        info.Tbl = rom.Count; rom.AddRange(tbl); Align(rom);
        info.Sbk = rom.Count; rom.AddRange(SeqFile(CSeq())); Align(rom);
        info.Midi = rom.Count; rom.AddRange(Midi()); Align(rom, 0x100);
        int pad = 0x40000 - rom.Count % 0x40000;
        rom.AddRange(Enumerable.Repeat((byte)0xFF, pad));
        return (rom.ToArray(), info);
    }

    public static string Write(string dir, ByteOrder order = ByteOrder.Z64)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "test." + order.ToString().ToLowerInvariant());
        File.WriteAllBytes(path, Rom.Swap(Build().rom, order));
        return path;
    }
}
