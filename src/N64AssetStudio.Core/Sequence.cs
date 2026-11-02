namespace N64AssetStudio.Core;

public sealed class Sequence
{
    public int BlobIndex, Offset, Length;
    public string Kind = "raw"; // MIDI | CSeq | raw
    public string Name = "";
}

/// <summary>Standard MIDI, libultra "S1" sequence banks and compressed MIDI (ALCSeq) to .mid.</summary>
public static class SequenceFormats
{
    static int? MidiLength(byte[] d, int off)
    {
        if (off + 14 > d.Length || BE.U32(d, off + 4) != 6) return null;
        int ntrks = BE.U16(d, off + 10);
        if (ntrks == 0 || ntrks > 64) return null;
        long p = off + 14;
        for (int t = 0; t < ntrks; t++)
        {
            if (p + 8 > d.Length || d[p] != 'M' || d[p + 1] != 'T' || d[p + 2] != 'r' || d[p + 3] != 'k') return null;
            p += 8 + BE.U32(d, (int)p + 4);
            if (p > d.Length) return null;
        }
        return (int)(p - off);
    }

    public static List<Sequence> FindMidi(byte[] d, int blob)
    {
        var o = new List<Sequence>();
        var magic = "MThd"u8.ToArray();
        int i = BE.Find(d, magic, 0);
        while (i != -1)
        {
            if (MidiLength(d, i) is int len) o.Add(new Sequence { BlobIndex = blob, Offset = i, Length = len, Kind = "MIDI" });
            i = BE.Find(d, magic, i + 4);
        }
        return o;
    }

    public static bool LooksLikeCSeq(byte[] d, int off, int len)
    {
        if (len < 0x48 || off + 0x44 > d.Length) return false;
        bool any = false;
        for (int t = 0; t < 16; t++)
        {
            uint o = BE.U32(d, off + t * 4);
            if (o == 0) continue;
            if (o < 0x44 || o >= len) return false;
            any = true;
        }
        uint div = BE.U32(d, off + 0x40);
        return any && div > 0 && div < 0x10000;
    }

    public static List<Sequence> FindSeqFiles(byte[] d, int blob)
    {
        var o = new List<Sequence>();
        var magic = "S1"u8.ToArray();
        int i = BE.Find(d, magic, 0);
        while (i != -1)
        {
            if (i % 4 == 0 && i + 4 <= d.Length)
            {
                int count = BE.S16(d, i + 2);
                if (count >= 1 && count <= 512 && i + 4 + count * 8 <= d.Length)
                {
                    int limit = Math.Min(d.Length - i, 0x1000000);
                    var entries = Enumerable.Range(0, count).Select(k => (off: BE.U32(d, i + 4 + k * 8), len: BE.S32(d, i + 8 + k * 8))).ToList();
                    if (entries.All(e => e.len == 0 || (e.len > 0 && e.len < 0x100000 && 4 + count * 8 <= e.off && e.off + e.len <= limit)))
                    {
                        var found = new List<Sequence>();
                        for (int k = 0; k < count; k++)
                        {
                            var (eo, el) = entries[k];
                            if (el == 0) continue;
                            int start = i + (int)eo;
                            string kind = d[start] == 'M' && d[start + 1] == 'T' && d[start + 2] == 'h' && d[start + 3] == 'd' ? "MIDI"
                                : LooksLikeCSeq(d, start, el) ? "CSeq" : "raw";
                            found.Add(new Sequence { BlobIndex = blob, Offset = start, Length = el, Kind = kind, Name = $"seq {k}" });
                        }
                        if (found.Count > 0 && found.Count(s => s.Kind != "raw") >= Math.Max(1, found.Count / 2)) o.AddRange(found);
                    }
                }
            }
            i = BE.Find(d, magic, i + 1);
        }
        return o;
    }

    sealed class Track
    {
        readonly byte[] _d; int _p; readonly int _end; int _bu = -1; int _buLen;
        public Track(byte[] d, int start, int end) { _d = d; _p = start; _end = end; }

        public int Byte()
        {
            if (_bu >= 0)
            {
                int b = _d[_bu++];
                if (--_buLen <= 0) _bu = -1;
                return b;
            }
            if (_p >= _end) throw new EndOfStreamException();
            int x = _d[_p++];
            if (x == 0xFE)
            {
                int hi = _d[_p++];
                if (hi == 0xFE) return 0xFE;
                int lo = _d[_p], ln = _d[_p + 1];
                _p += 2;
                _bu = _p - ((hi << 8) | lo) - 4;
                _buLen = ln;
                if (_bu < 0 || ln == 0) throw new EndOfStreamException();
                return Byte();
            }
            return x;
        }

        public int VarLen()
        {
            int v = 0;
            for (int k = 0; k < 4; k++)
            {
                int b = Byte();
                v = (v << 7) | (b & 0x7F);
                if ((b & 0x80) == 0) break;
            }
            return v;
        }
    }

    static void WriteVarLen(List<byte> o, int v)
    {
        var buf = new List<byte> { (byte)(v & 0x7F) };
        v >>= 7;
        while (v > 0) { buf.Add((byte)((v & 0x7F) | 0x80)); v >>= 7; }
        buf.Reverse();
        o.AddRange(buf);
    }

    public static byte[] CSeqToMidi(byte[] data, int off, int length)
    {
        var seq = data.AsSpan(off, Math.Min(length, data.Length - off)).ToArray();
        var offs = Enumerable.Range(0, 16).Select(t => (int)BE.U32(seq, t * 4)).ToArray();
        int division = (int)BE.U32(seq, 0x40);
        if (division == 0) division = 96;
        var ends = offs.Where(o => o != 0).OrderBy(o => o).Append(seq.Length).ToList();
        var chunks = new List<byte[]>();
        foreach (int o in offs)
        {
            if (o == 0) continue;
            int end = ends.First(e => e > o);
            var t = new Track(seq, o, end);
            var events = new List<(long time, int order, byte[] ev)>();
            long now = 0;
            int status = 0, order = 0;
            try
            {
                for (int guard = 0; guard < 200000; guard++)
                {
                    now += t.VarLen();
                    int b = t.Byte();
                    if (b == 0xFF)
                    {
                        int mt = t.Byte();
                        if (mt == 0x51)
                        {
                            int ln = t.Byte();
                            var tempo = new byte[Math.Max(ln, 3)];
                            for (int k = 0; k < (ln == 0 ? 3 : ln); k++) tempo[k] = (byte)t.Byte();
                            events.Add((now, order, new byte[] { 0xFF, 0x51, 0x03, tempo[0], tempo[1], tempo[2] }));
                        }
                        else if (mt == 0x2F) break;
                        else if (mt == 0x2E) { t.Byte(); t.Byte(); events.Add((now, order, Meta6("loopStart"))); }
                        else if (mt == 0x2D) { for (int k = 0; k < 6; k++) t.Byte(); events.Add((now, order, Meta6("loopEnd"))); }
                        else
                        {
                            int ln = t.Byte();
                            var ev = new List<byte> { 0xFF, (byte)mt };
                            WriteVarLen(ev, ln);
                            for (int k = 0; k < ln; k++) ev.Add((byte)t.Byte());
                            events.Add((now, order, ev.ToArray()));
                        }
                        order++;
                        continue;
                    }
                    int d1;
                    if ((b & 0x80) != 0) { status = b; d1 = t.Byte(); } else d1 = b;
                    if (status == 0) break;
                    int kind = status & 0xF0;
                    int d2 = kind is 0xC0 or 0xD0 ? 0 : t.Byte();
                    if (kind == 0x90)
                    {
                        int dur = t.VarLen();
                        events.Add((now, order, new[] { (byte)status, (byte)d1, (byte)d2 }));
                        events.Add((now + dur, -1, new[] { (byte)(0x80 | (status & 0xF)), (byte)d1, (byte)0 }));
                    }
                    else if (kind is 0xC0 or 0xD0) events.Add((now, order, new[] { (byte)status, (byte)d1 }));
                    else events.Add((now, order, new[] { (byte)status, (byte)d1, (byte)d2 }));
                    order++;
                }
            }
            catch (EndOfStreamException) { }
            catch (IndexOutOfRangeException) { }
            var body = new List<byte>();
            long last = 0;
            foreach (var (tm, _, ev) in events.OrderBy(e => e.time).ThenBy(e => e.order))
            {
                WriteVarLen(body, (int)(tm - last));
                body.AddRange(ev);
                last = tm;
            }
            body.AddRange(new byte[] { 0x00, 0xFF, 0x2F, 0x00 });
            var chunk = new byte[8 + body.Count];
            "MTrk"u8.CopyTo(chunk);
            BE.W32(chunk, 4, (uint)body.Count);
            body.CopyTo(chunk, 8);
            chunks.Add(chunk);
        }
        var hdr = new byte[14];
        "MThd"u8.CopyTo(hdr);
        BE.W32(hdr, 4, 6);
        BE.W16(hdr, 8, 1);
        BE.W16(hdr, 10, chunks.Count);
        BE.W16(hdr, 12, division & 0x7FFF);
        return hdr.Concat(chunks.SelectMany(c => c)).ToArray();
    }

    static byte[] Meta6(string text)
    {
        var t = System.Text.Encoding.ASCII.GetBytes(text);
        return new byte[] { 0xFF, 0x06, (byte)t.Length }.Concat(t).ToArray();
    }

    public static byte[]? ToMidi(byte[] data, Sequence s) => s.Kind switch
    {
        "MIDI" => data.AsSpan(s.Offset, s.Length).ToArray(),
        "CSeq" => CSeqToMidi(data, s.Offset, s.Length),
        _ => null,
    };
}
