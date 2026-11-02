namespace N64AssetStudio.Core;

public sealed class AdpcmBook
{
    public int Order, NPredictors;
    public short[] Coefs = Array.Empty<short>();
}

public sealed class Wave
{
    public uint Base;
    public int Length;
    public int Type; // 0 = VADPCM, 1 = raw PCM16
    public uint LoopStart, LoopEnd;
    public int LoopCount;
    public AdpcmBook? Book;
}

public sealed class Sound
{
    public int Index;
    public Wave Wave = null!;
    public int KeyBase = 60, Detune, KeyMin, KeyMax = 127, Pan = 64, Volume = 127;
    public int Attack, Decay, Release;
}

public sealed class Instrument
{
    public int Index, Volume, Pan;
    public List<Sound> Sounds = new();
    public bool IsPercussion;
}

public sealed class Bank
{
    public int Index, SampleRate;
    public List<Instrument> Instruments = new();
}

public sealed class BankFile
{
    public int BlobIndex, Offset, Size;
    public List<Bank> Banks = new();
    public int? TblBlob, TblOffset;

    public IEnumerable<(Bank, Instrument, Sound)> AllSounds()
    {
        foreach (var b in Banks)
            foreach (var i in b.Instruments)
                foreach (var s in i.Sounds) yield return (b, i, s);
    }

    public long MaxWaveExtent => AllSounds().Select(x => (long)x.Item3.Wave.Base + x.Item3.Wave.Length).DefaultIfEmpty(0).Max();
}

/// <summary>libultra sound banks ("B1" .ctl), wave tables (.tbl) and VADPCM decoding.</summary>
public static class Audio
{
    sealed class Reader
    {
        readonly byte[] _d; readonly int _base, _limit;
        public int MaxSeen;
        public Reader(byte[] d, int b, int limit) { _d = d; _base = b; _limit = limit; }
        public int At(long off, int size)
        {
            if (off < 0 || off + size > _limit || _base + off + size > _d.Length)
                throw new InvalidDataException($"offset 0x{off:X} outside bank");
            MaxSeen = (int)Math.Max(MaxSeen, off + size);
            return (int)(_base + off);
        }
        public byte U8(long o) => _d[At(o, 1)];
        public sbyte S8(long o) => (sbyte)_d[At(o, 1)];
        public short S16(long o) => BE.S16(_d, At(o, 2));
        public uint U32(long o) => BE.U32(_d, At(o, 4));
        public int S32(long o) => BE.S32(_d, At(o, 4));
    }

    static Wave ParseWave(Reader r, uint off, Dictionary<uint, Wave> cache)
    {
        if (cache.TryGetValue(off, out var c)) return c;
        uint b = r.U32(off);
        int len = r.S32(off + 4);
        int type = r.U8(off + 8);
        uint loopOff = r.U32(off + 12);
        uint bookOff = type == 0 ? r.U32(off + 16) : 0;
        if ((type != 0 && type != 1) || len <= 0 || len > 0x800000) throw new InvalidDataException("bad wavetable");
        var w = new Wave { Base = b, Length = len, Type = type };
        if (loopOff != 0) { w.LoopStart = r.U32(loopOff); w.LoopEnd = r.U32(loopOff + 4); w.LoopCount = r.S32(loopOff + 8); }
        if (type == 0)
        {
            if (bookOff == 0) throw new InvalidDataException("ADPCM wave without codebook");
            int order = r.S32(bookOff), np = r.S32(bookOff + 4);
            if (order < 1 || order > 8 || np < 1 || np > 16) throw new InvalidDataException("bad ADPCM book");
            var coefs = new short[order * np * 8];
            for (int i = 0; i < coefs.Length; i++) coefs[i] = r.S16(bookOff + 8 + i * 2);
            w.Book = new AdpcmBook { Order = order, NPredictors = np, Coefs = coefs };
        }
        return cache[off] = w;
    }

    static Instrument ParseInstrument(Reader r, uint off, int idx, Dictionary<uint, Wave> cache)
    {
        var inst = new Instrument { Index = idx, Volume = r.U8(off), Pan = r.S8(off + 1) };
        int count = r.S16(off + 14);
        if (count < 0 || count > 128) throw new InvalidDataException("bad sound count");
        for (int si = 0; si < count; si++)
        {
            uint so = r.U32(off + 16 + si * 4);
            uint env = r.U32(so), key = r.U32(so + 4), wav = r.U32(so + 8);
            var snd = new Sound { Index = si, Wave = ParseWave(r, wav, cache), Pan = r.U8(so + 12), Volume = r.U8(so + 13) };
            if (key != 0)
            {
                int kmin = r.U8(key + 2), kmax = r.U8(key + 3);
                snd.KeyBase = r.U8(key + 4);
                snd.Detune = r.S8(key + 5);
                if (kmin <= kmax && kmax <= 127) { snd.KeyMin = kmin; snd.KeyMax = kmax; }
            }
            if (env != 0) { snd.Attack = r.S32(env); snd.Decay = r.S32(env + 4); snd.Release = r.S32(env + 8); }
            inst.Sounds.Add(snd);
        }
        return inst;
    }

    public static BankFile? ParseBankFile(byte[] d, int off, int blobIndex = 0)
    {
        if (off + 4 > d.Length || BE.S16(d, off) != 0x4231) return null;
        int count = BE.S16(d, off + 2);
        if (count < 1 || count > 64) return null;
        var r = new Reader(d, off, Math.Min(d.Length - off, 0x200000));
        var bf = new BankFile { BlobIndex = blobIndex, Offset = off };
        var cache = new Dictionary<uint, Wave>();
        try
        {
            for (int bi = 0; bi < count; bi++)
            {
                uint bo = r.U32(4 + bi * 4);
                if (bo == 0 || bo % 4 != 0) return null;
                int instCount = r.S16(bo), rate = r.S32(bo + 4);
                uint perc = r.U32(bo + 8);
                if (instCount < 0 || instCount > 256 || rate < 1000 || rate > 96000) return null;
                var bank = new Bank { Index = bi, SampleRate = rate };
                for (int ii = 0; ii < instCount; ii++)
                {
                    uint io = r.U32(bo + 12 + ii * 4);
                    if (io != 0) bank.Instruments.Add(ParseInstrument(r, io, ii, cache));
                }
                if (perc != 0)
                {
                    var p = ParseInstrument(r, perc, instCount, cache);
                    p.IsPercussion = true;
                    bank.Instruments.Add(p);
                }
                bf.Banks.Add(bank);
            }
        }
        catch (InvalidDataException) { return null; }
        catch (IndexOutOfRangeException) { return null; }
        if (!bf.AllSounds().Any()) return null;
        bf.Size = (r.MaxSeen + 15) & ~15;
        return bf;
    }

    public static List<BankFile> FindBankFiles(byte[] d, int blobIndex = 0)
    {
        var o = new List<BankFile>();
        var magic = new byte[] { 0x42, 0x31 };
        int i = BE.Find(d, magic, 0);
        while (i != -1)
        {
            if (i % 4 == 0 && ParseBankFile(d, i, blobIndex) is BankFile bf)
            {
                o.Add(bf);
                i = BE.Find(d, magic, i + Math.Max(bf.Size, 2));
                continue;
            }
            i = BE.Find(d, magic, i + 1);
        }
        return o;
    }

    static bool HeadersOk(byte[] d, long start, List<Wave> waves)
    {
        foreach (var w in waves)
        {
            int frames = Math.Min(12, Math.Max(1, w.Length / 9));
            for (int f = 0; f < frames; f++)
            {
                long i = start + w.Base + f * 9;
                if (i >= d.Length) return false;
                int h = d[i];
                if ((h & 0xF) >= w.Book!.NPredictors || (h >> 4) > 12) return false;
            }
        }
        return true;
    }

    /// <summary>Locate the .tbl that a bank's wave offsets point into.</summary>
    public static (int blob, int off)? LocateTbl(BankFile bf, IReadOnlyList<byte[]> blobs, int romBlob = 0)
    {
        var waves = bf.AllSounds().Select(x => x.Item3.Wave).Where(w => w.Type == 0)
            .GroupBy(w => w.Base).Select(g => g.First()).Take(24).ToList();
        long extent = bf.MaxWaveExtent;
        int after = (bf.Offset + bf.Size + 15) & ~15;
        foreach (var start in new[] { after, after + 16 })
        {
            var d = blobs[bf.BlobIndex];
            if (start + extent <= d.Length && (waves.Count == 0 || HeadersOk(d, start, waves))) return (bf.BlobIndex, start);
        }
        if (waves.Count == 0) return null;
        foreach (var bi in new[] { bf.BlobIndex, romBlob }.Distinct())
        {
            var d = blobs[bi];
            if (d.Length < extent) continue;
            var hits = new List<int>();
            for (long s = 0; s + extent <= d.Length; s += 16)
                if (HeadersOk(d, s, waves))
                {
                    hits.Add((int)s);
                    if (bi != bf.BlobIndex) break;
                }
            if (hits.Count > 0)
                return (bi, bi == bf.BlobIndex ? hits.OrderBy(h => Math.Abs(h - bf.Offset)).First() : hits[0]);
        }
        return null;
    }

    // ------------------------------------------------------------------ VADPCM
    static int[][][] ExpandBook(AdpcmBook book)
    {
        int order = book.Order;
        var tables = new int[book.NPredictors][][];
        for (int p = 0; p < book.NPredictors; p++)
        {
            var t = new int[8][];
            for (int k = 0; k < 8; k++) t[k] = new int[order + 8];
            int b = p * order * 8;
            for (int j = 0; j < order; j++)
                for (int k = 0; k < 8; k++) t[k][j] = book.Coefs[b + j * 8 + k];
            for (int k = 1; k < 8; k++) t[k][order] = t[k - 1][order - 1];
            t[0][order] = 2048;
            for (int k = 1; k < 8; k++)
            {
                for (int j = 0; j < k; j++) t[j][k + order] = 0;
                for (int j = k; j < 8; j++) t[j][k + order] = t[j - k][order];
            }
            tables[p] = t;
        }
        return tables;
    }

    /// <summary>Decode VADPCM (9-byte frames of 16 samples), bit-exact with the SDK decoder.</summary>
    public static short[] DecodeVadpcm(byte[] data, int offset, int length, AdpcmBook book)
    {
        var tables = ExpandBook(book);
        int order = book.Order;
        int frames = Math.Max(0, Math.Min(length, data.Length - offset)) / 9;
        var o = new short[frames * 16];
        var state = new int[16];
        var cur = new int[16];
        var ix = new int[16];
        var inVec = new int[order + 8];
        int pos = offset;
        for (int f = 0; f < frames; f++)
        {
            int hdr = data[pos];
            int scale = 1 << (hdr >> 4);
            int pred = hdr & 0xF;
            if (pred >= tables.Length) pred = 0;
            var tab = tables[pred];
            for (int k = 0; k < 8; k++)
            {
                int b = data[pos + 1 + k];
                int hi = b >> 4, lo = b & 0xF;
                ix[k * 2] = (hi >= 8 ? hi - 16 : hi) * scale;
                ix[k * 2 + 1] = (lo >= 8 ? lo - 16 : lo) * scale;
            }
            pos += 9;
            for (int j = 0; j < 2; j++)
            {
                for (int i = 0; i < order; i++) inVec[i] = j == 0 ? state[16 - order + i] : cur[8 - order + i];
                for (int i = 0; i < 8; i++) inVec[order + i] = ix[j * 8 + i];
                for (int i = 0; i < 8; i++)
                {
                    var row = tab[i];
                    long acc = 0;
                    for (int k = 0; k < order + 8; k++) acc += (long)row[k] * inVec[k];
                    long v = acc >> 11; // arithmetic shift == floor, matches SDK rounding
                    cur[j * 8 + i] = (int)Math.Clamp(v, -32768, 32767);
                }
            }
            Array.Copy(cur, state, 16);
            for (int i = 0; i < 16; i++) o[f * 16 + i] = (short)cur[i];
        }
        return o;
    }

    public static short[] DecodeWave(byte[] tbl, int tblOff, Wave w)
    {
        long start = (long)tblOff + w.Base;
        if (start >= tbl.Length) return Array.Empty<short>();
        if (w.Type == 1)
        {
            int n = (int)Math.Min(w.Length, tbl.Length - start) / 2;
            var o = new short[n];
            for (int i = 0; i < n; i++) o[i] = BE.S16(tbl, (int)start + i * 2);
            return o;
        }
        return DecodeVadpcm(tbl, (int)start, w.Length, w.Book!);
    }

    public static short[] DecodeRawPcm(byte[] d, int off, int len, string fmt)
    {
        len = Math.Max(0, Math.Min(len, d.Length - off));
        switch (fmt)
        {
            case "s16be": { var o = new short[len / 2]; for (int i = 0; i < o.Length; i++) o[i] = BE.S16(d, off + i * 2); return o; }
            case "s16le": { var o = new short[len / 2]; for (int i = 0; i < o.Length; i++) o[i] = BitConverter.ToInt16(d, off + i * 2); return o; }
            case "s8": { var o = new short[len]; for (int i = 0; i < len; i++) o[i] = (short)((sbyte)d[off + i] << 8); return o; }
            case "u8": { var o = new short[len]; for (int i = 0; i < len; i++) o[i] = (short)((d[off + i] - 128) << 8); return o; }
        }
        throw new ArgumentException(fmt);
    }
}
