using System.IO.Compression;

namespace N64AssetStudio.Core;

public sealed class DecompressionException : Exception
{
    public DecompressionException(string m) : base(m) { }
}

public sealed record CompressedBlock(string Kind, int Offset, int CompressedSize, byte[] Data);

/// <summary>Yaz0, Yay0, MIO0, gzip, zlib and Rare (0x1172/0x1173) deflate.</summary>
public static class Compression
{
    public const int MaxOut = 16 * 1024 * 1024;

    sealed class Out
    {
        public byte[] Buf;
        public int Len;
        public Out(int size) { Buf = new byte[size]; }
        public void Put(byte b)
        {
            if (Len >= Buf.Length) throw new DecompressionException("output overflow");
            Buf[Len++] = b;
        }
        public void Copy(int dist, int n)
        {
            int start = Len - dist;
            if (start < 0) throw new DecompressionException("back-reference before start of output");
            n = Math.Min(n, Buf.Length - Len);
            for (int i = 0; i < n; i++) Buf[Len++] = Buf[start + i]; // overlap-safe
        }
    }

    static void Check(byte[] s, int off, string magic)
    {
        if (off + 16 > s.Length || s[off] != magic[0] || s[off + 1] != magic[1] || s[off + 2] != magic[2] || s[off + 3] != magic[3])
            throw new DecompressionException($"missing {magic} magic");
    }

    public static (byte[] data, int used) Yaz0(byte[] src, int off = 0)
    {
        Check(src, off, "Yaz0");
        int size = (int)Math.Min(BE.U32(src, off + 4), int.MaxValue);
        if (size <= 0 || size > MaxOut) throw new DecompressionException("implausible Yaz0 size");
        var o = new Out(size);
        int p = off + 16;
        try
        {
            while (o.Len < size)
            {
                int code = src[p++];
                for (int bit = 7; bit >= 0 && o.Len < size; bit--)
                {
                    if ((code & (1 << bit)) != 0) o.Put(src[p++]);
                    else
                    {
                        int b1 = src[p], b2 = src[p + 1];
                        p += 2;
                        int dist = (((b1 & 0x0F) << 8) | b2) + 1;
                        int n = b1 >> 4;
                        n = n == 0 ? src[p++] + 0x12 : n + 2;
                        o.Copy(dist, n);
                    }
                }
            }
        }
        catch (IndexOutOfRangeException) { throw new DecompressionException("truncated Yaz0 stream"); }
        return (o.Buf, p - off);
    }

    public static (byte[] data, int used) Yay0(byte[] src, int off = 0)
    {
        Check(src, off, "Yay0");
        uint size = BE.U32(src, off + 4), linkOff = BE.U32(src, off + 8), chunkOff = BE.U32(src, off + 12);
        if (size == 0 || size > MaxOut || linkOff >= src.Length - off || chunkOff >= src.Length - off)
            throw new DecompressionException("implausible Yay0 header");
        var o = new Out((int)size);
        int maskP = off + 16, linkP = off + (int)linkOff, chunkP = off + (int)chunkOff;
        uint mask = 0;
        int bits = 0;
        try
        {
            while (o.Len < size)
            {
                if (bits == 0) { mask = BE.U32(src, maskP); maskP += 4; bits = 32; }
                if ((mask & 0x80000000) != 0) o.Put(src[chunkP++]);
                else
                {
                    int v = (src[linkP] << 8) | src[linkP + 1];
                    linkP += 2;
                    int dist = (v & 0xFFF) + 1, n = v >> 12;
                    n = n == 0 ? src[chunkP++] + 0x12 : n + 2;
                    o.Copy(dist, n);
                }
                mask <<= 1;
                bits--;
            }
        }
        catch (Exception e) when (e is IndexOutOfRangeException or ArgumentOutOfRangeException)
        { throw new DecompressionException("truncated Yay0 stream"); }
        return (o.Buf, Math.Max(maskP, Math.Max(linkP, chunkP)) - off);
    }

    public static (byte[] data, int used) Mio0(byte[] src, int off = 0)
    {
        Check(src, off, "MIO0");
        uint size = BE.U32(src, off + 4), compOff = BE.U32(src, off + 8), uncOff = BE.U32(src, off + 12);
        if (size == 0 || size > MaxOut || compOff >= src.Length - off || uncOff >= src.Length - off)
            throw new DecompressionException("implausible MIO0 header");
        var o = new Out((int)size);
        int flagP = off + 16, compP = off + (int)compOff, uncP = off + (int)uncOff;
        int flag = 0, bits = 0;
        try
        {
            while (o.Len < size)
            {
                if (bits == 0) { flag = src[flagP++]; bits = 8; }
                if ((flag & 0x80) != 0) o.Put(src[uncP++]);
                else
                {
                    int v = (src[compP] << 8) | src[compP + 1];
                    compP += 2;
                    o.Copy((v & 0xFFF) + 1, (v >> 12) + 3);
                }
                flag = (flag << 1) & 0xFF;
                bits--;
            }
        }
        catch (IndexOutOfRangeException) { throw new DecompressionException("truncated MIO0 stream"); }
        return (o.Buf, Math.Max(flagP, Math.Max(compP, uncP)) - off);
    }

    /// <summary>Counts the bytes a stream reads so we know where the compressed data ended.</summary>
    sealed class CountingStream : Stream
    {
        readonly byte[] _d; readonly int _start; readonly int _end; int _pos;
        public CountingStream(byte[] d, int start, int end) { _d = d; _start = start; _end = end; _pos = start; }
        public int Consumed => _pos - _start;
        public override int Read(byte[] buffer, int offset, int count)
        {
            // feed one small chunk at a time so over-read past the end of the stream stays small
            int n = Math.Min(Math.Min(count, 64), _end - _pos);
            if (n <= 0) return 0;
            Array.Copy(_d, _pos, buffer, offset, n);
            _pos += n;
            return n;
        }
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _pos; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException();
        public override void SetLength(long v) => throw new NotSupportedException();
        public override void Write(byte[] b, int o, int c) => throw new NotSupportedException();
    }

    static (byte[], int) Inflate(byte[] src, int start, string kind)
    {
        if (start >= src.Length) throw new DecompressionException("no data");
        var cs = new CountingStream(src, start, Math.Min(src.Length, start + 4 * 1024 * 1024));
        using Stream ds = kind switch
        {
            "gzip" => new GZipStream(cs, CompressionMode.Decompress),
            "zlib" => new ZLibStream(cs, CompressionMode.Decompress),
            _ => new DeflateStream(cs, CompressionMode.Decompress),
        };
        var ms = new MemoryStream();
        var buf = new byte[65536];
        try
        {
            int n;
            while ((n = ds.Read(buf, 0, buf.Length)) > 0)
            {
                ms.Write(buf, 0, n);
                if (ms.Length > MaxOut) throw new DecompressionException("too large");
            }
        }
        catch (InvalidDataException e) { throw new DecompressionException(e.Message); }
        catch (IOException e) { throw new DecompressionException(e.Message); }
        // Decoders buffer their input, so this over-estimates the true length by < 64 bytes.
        return (ms.ToArray(), cs.Consumed);
    }

    static uint Adler32(byte[] d)
    {
        uint a = 1, b = 0;
        foreach (var x in d) { a = (a + x) % 65521; b = (b + a) % 65521; }
        return (b << 16) | a;
    }

    /// <summary>Find a 4/8-byte trailer near the end of the consumed input; returns exact end or -1.</summary>
    static int FindTrailer(byte[] src, int start, int used, ReadOnlySpan<byte> trailer)
    {
        int lo = Math.Max(start + 2, start + used - 80), hi = Math.Min(src.Length - trailer.Length, start + used + 4);
        for (int p = hi; p >= lo; p--)
            if (src.AsSpan(p, trailer.Length).SequenceEqual(trailer)) return p + trailer.Length;
        return -1;
    }

    public static (byte[] data, int used) Gzip(byte[] src, int off = 0)
    {
        var (d, used) = Inflate(src, off, "gzip");
        if (d.Length == 0) throw new DecompressionException("empty gzip");
        var t = new byte[8];
        BitConverter.TryWriteBytes(t.AsSpan(0, 4), Crc32.Compute(d));
        BitConverter.TryWriteBytes(t.AsSpan(4, 4), (uint)d.Length);
        int end = FindTrailer(src, off, used, t);
        if (end < 0) throw new DecompressionException("gzip trailer mismatch");
        return (d, end - off);
    }

    public static (byte[] data, int used) Zlib(byte[] src, int off = 0)
    {
        if (off + 2 > src.Length || ((src[off] << 8) | src[off + 1]) % 31 != 0 || (src[off] & 0x0F) != 8)
            throw new DecompressionException("bad zlib header");
        var (d, used) = Inflate(src, off, "zlib");
        if (d.Length == 0) throw new DecompressionException("empty zlib");
        var t = new byte[4];
        BE.W32(t, 0, Adler32(d));
        int end = FindTrailer(src, off, used, t);
        if (end < 0) throw new DecompressionException("zlib checksum mismatch");
        return (d, end - off);
    }

    /// <summary>Rare's header (0x1172/0x1173 + 3 or 4 byte size) followed by raw deflate.</summary>
    public static (byte[] data, int used) Rare(byte[] src, int off = 0)
    {
        if (off + 8 > src.Length || src[off] != 0x11 || (src[off + 1] != 0x72 && src[off + 1] != 0x73))
            throw new DecompressionException("missing Rare header");
        foreach (int sizeLen in new[] { 3, 4 })
        {
            int size = 0;
            for (int i = 0; i < sizeLen; i++) size = (size << 8) | src[off + 2 + i];
            if (size < 16 || size > MaxOut) continue;
            try
            {
                var (d, used) = Inflate(src, off + 2 + sizeLen, "deflate");
                if (d.Length == size) return (d, used + 2 + sizeLen);
            }
            catch (DecompressionException) { }
        }
        throw new DecompressionException("not a Rare deflate block");
    }

    public static (byte[] data, int used) Decompress(string kind, byte[] src, int off) => kind switch
    {
        "Yaz0" => Yaz0(src, off), "Yay0" => Yay0(src, off), "MIO0" => Mio0(src, off),
        "gzip" => Gzip(src, off), "zlib" => Zlib(src, off), "Rare" => Rare(src, off),
        _ => throw new ArgumentException(kind),
    };

    static IEnumerable<int> FindAll(byte[] data, byte[] needle, int align)
    {
        int i = BE.Find(data, needle, 0);
        while (i != -1)
        {
            if (i % align == 0) yield return i;
            i = BE.Find(data, needle, i + 1);
        }
    }

    public static List<CompressedBlock> Scan(byte[] data, Action<double, string>? progress = null, bool deep = true)
    {
        var cand = new List<(int off, string kind)>();
        foreach (var m in new[] { "Yaz0", "Yay0", "MIO0" })
            foreach (var i in FindAll(data, System.Text.Encoding.ASCII.GetBytes(m), 1)) cand.Add((i, m));
        foreach (var i in FindAll(data, new byte[] { 0x1F, 0x8B, 0x08 }, 1)) cand.Add((i, "gzip"));
        if (deep)
        {
            foreach (var lead in new[] { new byte[] { 0x78, 0x9C }, new byte[] { 0x78, 0xDA }, new byte[] { 0x78, 0x01 }, new byte[] { 0x78, 0x5E } })
                foreach (var i in FindAll(data, lead, 1)) cand.Add((i, "zlib")); // verified by Adler-32, so any alignment
            foreach (var lead in new[] { new byte[] { 0x11, 0x72 }, new byte[] { 0x11, 0x73 } })
                foreach (var i in FindAll(data, lead, 2)) cand.Add((i, "Rare"));
        }
        cand.Sort();
        var blocks = new List<CompressedBlock>();
        int coveredUntil = 0;
        for (int idx = 0; idx < cand.Count; idx++)
        {
            var (off, kind) = cand[idx];
            if (progress != null && idx % 64 == 0) progress((double)idx / cand.Count, $"Decompressing ({kind} @ 0x{off:X})");
            if (off < coveredUntil) continue;
            try
            {
                var (o, used) = Decompress(kind, data, off);
                if (kind is "zlib" or "Rare" or "gzip" && o.Length < 64) continue;
                blocks.Add(new CompressedBlock(kind, off, used, o));
                coveredUntil = off + used;
            }
            catch (DecompressionException) { }
            catch (IndexOutOfRangeException) { }
            catch (ArgumentOutOfRangeException) { }
        }
        return blocks;
    }
}
