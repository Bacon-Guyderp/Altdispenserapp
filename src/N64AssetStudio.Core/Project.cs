using System.Diagnostics;

namespace N64AssetStudio.Core;

/// <summary>A contiguous chunk of game data: the ROM itself or a decompressed file.</summary>
public sealed record Blob(int Index, string Name, byte[] Data, int RomOffset, string Kind, int CompressedSize);

public sealed class ModelAsset
{
    public int Index, BlobIndex;
    public DisplayList Dl = null!;
    public string Name = "";
    public int? BaseOverride;
}

public sealed class TextureAsset
{
    public int Index, BlobIndex, Offset, Width, Height;
    public string Fmt = "RGBA16";
    public int? TlutOffset;
    public bool TlutIa;
    public int PaletteIndex;
    public string Name = "";
    public (int, int, int, int, string, int?) Key => (BlobIndex, Offset, Width, Height, Fmt, TlutOffset);
}

public sealed record SoundRef(int BankFile, Bank Bank, Instrument Instrument, Sound Sound, string Name);

public sealed class ScanOptions
{
    public bool DeepCompression = true;
    public bool ZeldaTables = true;
    public bool ScanRomDisplayLists = true;
    public bool EnumerateTextures = true;
    public double TextureTimeBudget = 90;
}

public sealed class Project
{
    public Rom Rom;
    public List<Blob> Blobs = new();
    public List<ModelAsset> Models = new();
    public List<TextureAsset> Textures = new();
    public List<BankFile> BankFiles = new();
    public List<Sequence> Sequences = new();
    public List<uint> Functions = new();
    public List<string> Log = new();
    readonly Dictionary<(int, int?), Mesh> _meshCache = new();
    readonly Dictionary<(int, uint, int), short[]> _pcmCache = new();
    List<SoundRef>? _sounds;

    public Project(Rom rom) { Rom = rom; }
    public static Project Open(string path) => new(Rom.Load(path));

    public void Scan(Action<double, string>? progress = null, ScanOptions? options = null)
    {
        var opt = options ?? new ScanOptions();
        var prog = progress ?? ((_, _) => { });
        Action<double, string> Sub(double lo, double hi) => (f, m) => prog(lo + (hi - lo) * f, m);
        var data = Rom.Data;
        Blobs = new() { new Blob(0, "ROM (raw)", data, 0, "rom", data.Length) };
        var covered = new List<(int lo, int hi)>();

        if (opt.ZeldaTables && !Rom.IsDisk)
        {
            prog(0.01, "Looking for Zelda 64 file table");
            var files = Games.FindZeldaDmadata(data, Sub(0.01, 0.15));
            foreach (var tf in files)
            {
                string kind = tf.Compressed ? "Yaz0" : "file";
                Blobs.Add(new Blob(Blobs.Count, $"file {tf.Index:D4} ({kind} @ 0x{tf.RomStart:X8})", tf.Data, tf.RomStart, kind, tf.RomEnd - tf.RomStart));
                covered.Add((tf.RomStart, tf.RomEnd));
            }
            if (files.Count > 0) Log.Add($"Zelda 64 dmadata table: {files.Count} files");
        }

        prog(0.15, "Scanning for compressed data");
        foreach (var b in Compression.Scan(data, Sub(0.15, 0.45), opt.DeepCompression))
        {
            if (covered.Any(c => c.lo <= b.Offset && b.Offset < c.hi)) continue;
            Blobs.Add(new Blob(Blobs.Count, $"{b.Kind} @ 0x{b.Offset:X8}", b.Data, b.Offset, b.Kind, b.CompressedSize));
        }
        foreach (var blob in Blobs.Skip(1).ToList())
            foreach (var b in Compression.Scan(blob.Data, null, false))
            {
                if (b.Offset == 0) continue;
                Blobs.Add(new Blob(Blobs.Count, $"{b.Kind} @ 0x{b.Offset:X} in [{blob.Name}]", b.Data, blob.RomOffset, b.Kind, b.CompressedSize));
            }
        Log.Add($"{Blobs.Count - 1} compressed/table files");

        prog(0.45, "Searching for display lists (3D models)");
        Models = new();
        for (int bi = 0; bi < Blobs.Count; bi++)
        {
            if (bi == 0 && !opt.ScanRomDisplayLists) continue;
            if (bi % 16 == 0) prog(0.45 + 0.25 * bi / Blobs.Count, $"Searching for display lists ({bi}/{Blobs.Count})");
            foreach (var dl in F3D.FindDisplayLists(Blobs[bi].Data, bi))
                Models.Add(new ModelAsset { Index = Models.Count, BlobIndex = bi, Dl = dl, Name = $"{dl.Ucode} DL @ 0x{dl.Offset:X}" });
        }

        prog(0.70, "Searching for audio banks and sequences");
        BankFiles = new();
        var seqs = new List<Sequence>();
        foreach (var blob in Blobs)
        {
            BankFiles.AddRange(Audio.FindBankFiles(blob.Data, blob.Index));
            seqs.AddRange(SequenceFormats.FindSeqFiles(blob.Data, blob.Index));
            seqs.AddRange(SequenceFormats.FindMidi(blob.Data, blob.Index));
        }
        Sequences = seqs.GroupBy(s => (s.BlobIndex, s.Offset)).Select(g => g.First()).ToList();
        foreach (var s in Sequences) if (s.Name == "") s.Name = $"{s.Kind} @ 0x{s.Offset:X}";
        _sounds = null;

        if (!Rom.IsDisk && Rom.Header.EntryPoint != 0 && data.Length > 0x1000)
        {
            prog(0.75, "Finding code functions");
            Functions = Mips.FindFunctions(data, 0x1000, Math.Min(0x100000, data.Length - 0x1000), Rom.Header.EntryPoint);
        }

        Textures = new();
        if (opt.EnumerateTextures) EnumerateTextures(Sub(0.78, 1.0), opt.TextureTimeBudget);
        prog(1.0, "Done");
        Log.Add($"{Models.Count} display lists, {Textures.Count} textures, {AllSounds().Count} sounds in {BankFiles.Count} banks, " +
                $"{Sequences.Count} sequences, {Functions.Count} functions");
    }

    void EnumerateTextures(Action<double, string> prog, double budget)
    {
        var sw = Stopwatch.StartNew();
        var seen = new HashSet<(int, int, int, int, string, int?)>();
        for (int i = 0; i < Models.Count; i++)
        {
            if (sw.Elapsed.TotalSeconds > budget) { Log.Add("texture enumeration stopped (time budget); open models to find more"); break; }
            if (i % 32 == 0) prog((double)i / Models.Count, $"Collecting textures ({i}/{Models.Count})");
            foreach (var t in TexturesInDl(Models[i]))
                if (seen.Add(t.Key)) { t.Index = Textures.Count; Textures.Add(t); }
        }
    }

    IEnumerable<TextureAsset> TexturesInDl(ModelAsset m)
    {
        var d = Blobs[m.BlobIndex].Data;
        var dl = m.Dl;
        dl.Base ??= F3D.GuessBase(d, dl, m.BlobIndex != 0);
        int b = m.BaseOverride ?? dl.Base.Value;
        var interp = new F3D.DisplayListInterpreter(d, dl.Ucode, b, F3D.HomeSegment(d, dl), Rom);
        uint? timg = null;
        int? tlut = null, loaded = null;
        var tile = new Dictionary<int, (int fmt, int siz, int pal)>();
        var outList = new List<TextureAsset>();
        for (int q = dl.Offset; q < dl.End; q += 8)
        {
            uint w0 = BE.U32(d, q), w1 = BE.U32(d, q + 4);
            int op = (int)(w0 >> 24);
            if (op == F3D.G_SETTIMG) timg = w1;
            else if (op is F3D.G_LOADBLOCK or F3D.G_LOADTILE) loaded = timg.HasValue ? interp.Resolve(timg.Value) : null;
            else if (op == F3D.G_LOADTLUT) tlut = timg.HasValue ? interp.Resolve(timg.Value) : null;
            else if (op == F3D.G_SETTILE) tile[(int)((w1 >> 24) & 7)] = ((int)((w0 >> 21) & 7), (int)((w0 >> 19) & 3), (int)((w1 >> 20) & 0xF));
            else if (op == F3D.G_SETTILESIZE)
            {
                int t = (int)((w1 >> 24) & 7);
                if (t == 7 || loaded == null || !tile.TryGetValue(t, out var ti)) continue;
                int w = (int)(((w1 >> 12) & 0xFFF) - ((w0 >> 12) & 0xFFF)) / 4 + 1;
                int h = (int)((w1 & 0xFFF) - (w0 & 0xFFF)) / 4 + 1;
                if (w < 1 || w > 1024 || h < 1 || h > 1024 || ti.fmt == 1) continue;
                string name = Texture.FormatName(ti.fmt, ti.siz);
                if (!new[] { "RGBA16", "RGBA32", "CI4", "CI8", "IA4", "IA8", "IA16", "I4", "I8" }.Contains(name)) continue;
                outList.Add(new TextureAsset
                {
                    BlobIndex = m.BlobIndex, Offset = loaded.Value, Width = w, Height = h, Fmt = name,
                    TlutOffset = ti.fmt == 2 ? tlut : null, PaletteIndex = ti.pal, Name = $"{name} {w}x{h} @ 0x{loaded.Value:X}",
                });
            }
        }
        return outList;
    }

    public string SafeName(ModelAsset m) => $"model_b{m.BlobIndex}_{m.Dl.Offset:X6}";

    public Mesh GetMesh(ModelAsset m)
    {
        lock (_meshCache)
        {
            if (_meshCache.TryGetValue((m.Index, m.BaseOverride), out var c)) return c;
            var mesh = F3D.BuildMesh(Blobs[m.BlobIndex].Data, m.Dl, m.BaseOverride, Rom, SafeName(m), m.BlobIndex != 0);
            if (!mesh.HasNormals) mesh.ComputeNormals();
            return _meshCache[(m.Index, m.BaseOverride)] = mesh;
        }
    }

    public Mesh? GetBlobMesh(int blobIndex, bool rootsOnly = true)
    {
        Mesh? mesh = null;
        foreach (var m in Models.Where(m => m.BlobIndex == blobIndex && !(rootsOnly && m.Dl.IsChild)))
        {
            var part = GetMesh(m);
            mesh = mesh == null ? part.MergedWith(new Mesh(""), $"file_{blobIndex}") : mesh.MergedWith(part, $"file_{blobIndex}");
        }
        return mesh;
    }

    public RgbaImage TextureImage(TextureAsset t)
    {
        var d = Blobs[t.BlobIndex].Data;
        byte[]? pal = null;
        if (t.Fmt is "CI4" or "CI8" && t.TlutOffset.HasValue)
            pal = Texture.DecodePalette(d, t.TlutOffset.Value + (t.Fmt == "CI4" ? t.PaletteIndex * 32 : 0), t.Fmt == "CI4" ? 16 : 256, t.TlutIa);
        return Texture.Decode(d, t.Offset, t.Width, t.Height, t.Fmt, pal);
    }

    public List<SoundRef> AllSounds()
    {
        if (_sounds != null) return _sounds;
        var o = new List<SoundRef>();
        for (int fi = 0; fi < BankFiles.Count; fi++)
            foreach (var (b, i, s) in BankFiles[fi].AllSounds())
                o.Add(new SoundRef(fi, b, i, s, $"bankfile{fi}_bank{b.Index}_{(i.IsPercussion ? "perc" : "inst")}{i.Index}_snd{s.Index}"));
        return _sounds = o;
    }

    public (int blob, int off)? LocateTbl(int fi)
    {
        var bf = BankFiles[fi];
        if (bf.TblOffset == null && Audio.LocateTbl(bf, Blobs.Select(b => b.Data).ToList()) is { } loc)
        {
            bf.TblBlob = loc.blob;
            bf.TblOffset = loc.off;
        }
        return bf.TblOffset.HasValue ? (bf.TblBlob ?? 0, bf.TblOffset.Value) : null;
    }

    public void ClearSoundCache() { lock (_pcmCache) _pcmCache.Clear(); }

    public (short[] pcm, int rate) DecodeSound(SoundRef r)
    {
        int rate = r.Bank.SampleRate > 0 ? r.Bank.SampleRate : 22050;
        var key = (r.BankFile, r.Sound.Wave.Base, r.Sound.Wave.Length);
        lock (_pcmCache)
        {
            if (_pcmCache.TryGetValue(key, out var c)) return (c, rate);
            var loc = LocateTbl(r.BankFile) ?? throw new InvalidDataException(
                "Could not locate the wave table (.tbl) for this bank. Set it manually in the sound panel.");
            var pcm = Audio.DecodeWave(Blobs[loc.blob].Data, loc.off, r.Sound.Wave);
            _pcmCache[key] = pcm;
            return (pcm, rate);
        }
    }

    public byte[]? SequenceMidi(Sequence s) => SequenceFormats.ToMidi(Blobs[s.BlobIndex].Data, s);
    public byte[] SequenceRaw(Sequence s) => Blobs[s.BlobIndex].Data.AsSpan(s.Offset, s.Length).ToArray();
}
