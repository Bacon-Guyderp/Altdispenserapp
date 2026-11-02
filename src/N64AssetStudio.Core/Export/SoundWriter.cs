using System.Diagnostics;

namespace N64AssetStudio.Core.Export;

/// <summary>WAV, AIFF, FLAC, AU and raw natively; OGG/MP3/OPUS/M4A/WMA through ffmpeg if installed.</summary>
public static class SoundWriter
{
    public static readonly Dictionary<string, string> Native = new()
    {
        ["wav"] = "WAV (PCM 16-bit)", ["aiff"] = "AIFF (PCM 16-bit)", ["flac"] = "FLAC (lossless)", ["au"] = "Sun AU", ["raw"] = "Raw PCM s16le",
    };
    public static readonly Dictionary<string, string> ViaFfmpeg = new()
    {
        ["ogg"] = "Ogg Vorbis", ["mp3"] = "MP3", ["opus"] = "Opus", ["m4a"] = "AAC (.m4a)", ["wma"] = "WMA",
    };

    public static string? FfmpegPath { get; set; }

    public static string? FindFfmpeg()
    {
        if (!string.IsNullOrEmpty(FfmpegPath) && File.Exists(FfmpegPath)) return FfmpegPath;
        var exe = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            try
            {
                var p = Path.Combine(dir.Trim('"'), exe);
                if (File.Exists(p)) return p;
            }
            catch (ArgumentException) { }
        }
        return null;
    }

    public static Dictionary<string, string> Formats()
    {
        var o = new Dictionary<string, string>(Native);
        if (FindFfmpeg() != null) foreach (var kv in ViaFfmpeg) o[kv.Key] = kv.Value;
        return o;
    }

    public static byte[] Wav(short[] pcm, int rate)
    {
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        w.Write("RIFF"u8); w.Write(36 + pcm.Length * 2); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8); w.Write(pcm.Length * 2);
        foreach (var s in pcm) w.Write(s);
        return ms.ToArray();
    }

    static byte[] Extended80(double v)
    {
        var o = new byte[10];
        if (v == 0) return o;
        int e = (int)Math.Floor(Math.Log2(v));
        ulong mant = (ulong)(v / Math.Pow(2, e) * Math.Pow(2, 63));
        BE.W16(o, 0, e + 16383);
        for (int i = 0; i < 8; i++) o[2 + i] = (byte)(mant >> (56 - 8 * i));
        return o;
    }

    static byte[] Aiff(short[] pcm, int rate)
    {
        var comm = new byte[18];
        BE.W16(comm, 0, 1); BE.W32(comm, 2, (uint)pcm.Length); BE.W16(comm, 6, 16);
        Extended80(rate).CopyTo(comm, 8);
        var ssnd = new byte[8 + pcm.Length * 2];
        for (int i = 0; i < pcm.Length; i++) BE.W16(ssnd, 8 + i * 2, pcm[i]);
        var body = new MemoryStream();
        body.Write("AIFF"u8);
        void Chunk(string id, byte[] d) { body.Write(System.Text.Encoding.ASCII.GetBytes(id)); var l = new byte[4]; BE.W32(l, 0, (uint)d.Length); body.Write(l); body.Write(d); }
        Chunk("COMM", comm);
        Chunk("SSND", ssnd);
        var o = new byte[8 + body.Length];
        "FORM"u8.CopyTo(o);
        BE.W32(o, 4, (uint)body.Length);
        body.ToArray().CopyTo(o, 8);
        return o;
    }

    static byte[] Au(short[] pcm, int rate)
    {
        var o = new byte[24 + pcm.Length * 2];
        ".snd"u8.CopyTo(o);
        BE.W32(o, 4, 24); BE.W32(o, 8, (uint)(pcm.Length * 2)); BE.W32(o, 12, 3); BE.W32(o, 16, (uint)rate); BE.W32(o, 20, 1);
        for (int i = 0; i < pcm.Length; i++) BE.W16(o, 24 + i * 2, pcm[i]);
        return o;
    }

    // -------------------------------------------------------------------- FLAC
    static byte Crc8(ReadOnlySpan<byte> d)
    {
        int c = 0;
        foreach (var b in d)
        {
            c ^= b;
            for (int k = 0; k < 8; k++) c = (c & 0x80) != 0 ? ((c << 1) ^ 0x07) & 0xFF : (c << 1) & 0xFF;
        }
        return (byte)c;
    }

    static ushort Crc16(ReadOnlySpan<byte> d)
    {
        int c = 0;
        foreach (var b in d)
        {
            c ^= b << 8;
            for (int k = 0; k < 8; k++) c = (c & 0x8000) != 0 ? ((c << 1) ^ 0x8005) & 0xFFFF : (c << 1) & 0xFFFF;
        }
        return (ushort)c;
    }

    static byte[] Utf8Num(int n) => n switch
    {
        < 0x80 => new[] { (byte)n },
        < 0x800 => new[] { (byte)(0xC0 | (n >> 6)), (byte)(0x80 | (n & 0x3F)) },
        < 0x10000 => new[] { (byte)(0xE0 | (n >> 12)), (byte)(0x80 | ((n >> 6) & 0x3F)), (byte)(0x80 | (n & 0x3F)) },
        _ => new[] { (byte)(0xF0 | (n >> 18)), (byte)(0x80 | ((n >> 12) & 0x3F)), (byte)(0x80 | ((n >> 6) & 0x3F)), (byte)(0x80 | (n & 0x3F)) },
    };

    /// <summary>FLAC using VERBATIM subframes: valid and lossless without an encoder.</summary>
    static byte[] Flac(short[] pcm, int rate)
    {
        const int block = 4096;
        var o = new MemoryStream();
        o.Write("fLaC"u8);
        var si = new byte[34];
        BE.W16(si, 0, block); BE.W16(si, 2, block);
        ulong v = ((ulong)rate << 44) | (15UL << 36) | (ulong)pcm.Length;
        for (int i = 0; i < 8; i++) si[10 + i] = (byte)(v >> (56 - 8 * i));
        o.WriteByte(0x80); o.WriteByte(0); o.WriteByte(0); o.WriteByte(34);
        o.Write(si);
        for (int fi = 0, start = 0; start < pcm.Length; fi++, start += block)
        {
            int n = Math.Min(block, pcm.Length - start);
            var hdr = new List<byte> { 0xFF, 0xF8, (byte)(n - 1 < 256 ? 0x60 : 0x70), 0x08 };
            hdr.AddRange(Utf8Num(fi));
            if (n - 1 < 256) hdr.Add((byte)(n - 1)); else { hdr.Add((byte)((n - 1) >> 8)); hdr.Add((byte)(n - 1)); }
            hdr.Add(Crc8(hdr.ToArray()));
            var frame = new byte[hdr.Count + 1 + n * 2];
            hdr.CopyTo(frame);
            frame[hdr.Count] = 0x02;
            for (int i = 0; i < n; i++) BE.W16(frame, hdr.Count + 1 + i * 2, pcm[start + i]);
            o.Write(frame);
            ushort crc = Crc16(frame);
            o.WriteByte((byte)(crc >> 8)); o.WriteByte((byte)crc);
        }
        return o.ToArray();
    }

    public static string Save(short[] pcm, int rate, string path, string? fmt = null)
    {
        string ext = (fmt ?? Path.GetExtension(path).TrimStart('.')).ToLowerInvariant();
        if (ext == "aif") ext = "aiff";
        if (!path.EndsWith("." + ext, StringComparison.OrdinalIgnoreCase)) path = path + "." + ext;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        byte[] data;
        switch (ext)
        {
            case "wav": data = Wav(pcm, rate); break;
            case "aiff": data = Aiff(pcm, rate); break;
            case "flac": data = Flac(pcm, rate); break;
            case "au": data = Au(pcm, rate); break;
            case "raw": data = new byte[pcm.Length * 2]; Buffer.BlockCopy(pcm, 0, data, 0, data.Length); break;
            default:
                if (!ViaFfmpeg.ContainsKey(ext)) throw new ArgumentException($"unsupported audio format {ext}");
                var ff = FindFfmpeg() ?? throw new InvalidOperationException($"Exporting .{ext} needs ffmpeg (install it or set its path in Settings)");
                var tmp = Path.Combine(Path.GetTempPath(), $"n64studio_{Guid.NewGuid():N}.wav");
                File.WriteAllBytes(tmp, Wav(pcm, rate));
                try
                {
                    var psi = new ProcessStartInfo(ff) { CreateNoWindow = true, UseShellExecute = false, RedirectStandardError = true };
                    foreach (var a in new[] { "-y", "-loglevel", "error", "-i", tmp, path }) psi.ArgumentList.Add(a);
                    using var p = Process.Start(psi)!;
                    string err = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    if (p.ExitCode != 0) throw new InvalidOperationException("ffmpeg failed: " + err);
                }
                finally { File.Delete(tmp); }
                return path;
        }
        File.WriteAllBytes(path, data);
        return path;
    }
}
