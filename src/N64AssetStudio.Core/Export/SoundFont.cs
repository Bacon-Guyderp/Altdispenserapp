using System.Text;

namespace N64AssetStudio.Core.Export;

/// <summary>SoundFont 2 writer for libultra instrument banks.</summary>
public static class SoundFont
{
    public sealed class Zone
    {
        public short[] Pcm = Array.Empty<short>();
        public int Rate, Root = 60, Fine, Lo, Hi = 127, Pan = 64, AttackUs, ReleaseUs = 200000;
        public (int start, int end)? Loop;
        public string Name = "";
    }

    public sealed class Inst
    {
        public string Name = "";
        public int Preset, Bank;
        public List<Zone> Zones = new();
    }

    const int GenKeyRange = 43, GenInstrument = 41, GenFineTune = 52, GenSampleId = 53, GenSampleModes = 54, GenRootKey = 58,
        GenPan = 17, GenAttack = 34, GenRelease = 38;

    static byte[] Name20(string s)
    {
        var b = new byte[20];
        var a = Encoding.ASCII.GetBytes(s);
        Array.Copy(a, b, Math.Min(19, a.Length));
        return b;
    }

    static int Timecents(int us)
    {
        double sec = Math.Max(us, 1) / 1_000_000.0;
        return Math.Clamp((int)Math.Round(1200 * Math.Log2(sec)), -12000, 8000);
    }

    static byte[] Chunk(string tag, byte[] data)
    {
        int len = data.Length + (data.Length & 1);
        var o = new byte[8 + len];
        Encoding.ASCII.GetBytes(tag).CopyTo(o, 0);
        BitConverter.TryWriteBytes(o.AsSpan(4), data.Length);
        data.CopyTo(o, 8);
        return o;
    }

    static byte[] List(string tag, params byte[][] chunks)
    {
        var body = Encoding.ASCII.GetBytes(tag).Concat(chunks.SelectMany(c => c)).ToArray();
        return Chunk("LIST", body);
    }

    public static byte[] Build(string bankName, List<Inst> insts)
    {
        var smpl = new MemoryStream(); var shdr = new MemoryStream(); var inst = new MemoryStream(); var ibag = new MemoryStream();
        var igen = new MemoryStream(); var phdr = new MemoryStream(); var pbag = new MemoryStream(); var pgen = new MemoryStream();
        var wS = new BinaryWriter(shdr); var wI = new BinaryWriter(inst); var wIb = new BinaryWriter(ibag); var wIg = new BinaryWriter(igen);
        var wP = new BinaryWriter(phdr); var wPb = new BinaryWriter(pbag); var wPg = new BinaryWriter(pgen); var wSm = new BinaryWriter(smpl);
        int sampleIdx = 0, bagI = 0, genI = 0, pbagI = 0, pgenI = 0;
        for (int ii = 0; ii < insts.Count; ii++)
        {
            var ins = insts[ii];
            wI.Write(Name20(ins.Name)); wI.Write((ushort)bagI);
            foreach (var z in ins.Zones)
            {
                int start = (int)(smpl.Length / 2);
                foreach (var s in z.Pcm) wSm.Write(s);
                wSm.Write(new byte[92]); // 46 zero samples padding required by the spec
                int end = start + z.Pcm.Length;
                int ls = z.Loop.HasValue ? start + z.Loop.Value.start : start;
                int le = z.Loop.HasValue ? start + z.Loop.Value.end : end;
                le = Math.Min(Math.Max(le, ls + 1), end);
                wS.Write(Name20(z.Name)); wS.Write(start); wS.Write(end); wS.Write(ls); wS.Write(le); wS.Write(z.Rate);
                wS.Write((byte)(z.Root & 0x7F)); wS.Write((sbyte)0); wS.Write((ushort)0); wS.Write((ushort)1);
                var gens = new (int op, int amt)[]
                {
                    (GenKeyRange, z.Lo | (z.Hi << 8)), (GenPan, (int)((z.Pan - 64) / 64.0 * 500)),
                    (GenAttack, Timecents(z.AttackUs)), (GenRelease, Timecents(z.ReleaseUs)), (GenFineTune, z.Fine),
                    (GenRootKey, z.Root & 0x7F), (GenSampleModes, z.Loop.HasValue ? 1 : 0), (GenSampleId, sampleIdx),
                };
                wIb.Write((ushort)genI); wIb.Write((ushort)0);
                bagI++;
                foreach (var (op, amt) in gens) { wIg.Write((ushort)op); wIg.Write((ushort)(amt & 0xFFFF)); genI++; }
                sampleIdx++;
            }
            wP.Write(Name20(ins.Name)); wP.Write((ushort)ins.Preset); wP.Write((ushort)ins.Bank); wP.Write((ushort)pbagI); wP.Write(0); wP.Write(0); wP.Write(0);
            wPb.Write((ushort)pgenI); wPb.Write((ushort)0); pbagI++;
            wPg.Write((ushort)GenInstrument); wPg.Write((ushort)ii); pgenI++;
        }
        wP.Write(Name20("EOP")); wP.Write((ushort)0); wP.Write((ushort)0); wP.Write((ushort)pbagI); wP.Write(0); wP.Write(0); wP.Write(0);
        wPb.Write((ushort)pgenI); wPb.Write((ushort)0);
        wPg.Write(0);
        wI.Write(Name20("EOI")); wI.Write((ushort)bagI);
        wIb.Write((ushort)genI); wIb.Write((ushort)0);
        wIg.Write(0);
        wS.Write(Name20("EOS")); wS.Write(new byte[26]);
        var modTerm = new byte[10];
        var info = List("INFO", Chunk("ifil", new byte[] { 2, 0, 1, 0 }), Chunk("isng", Encoding.ASCII.GetBytes("EMU8000\0")),
            Chunk("INAM", Encoding.ASCII.GetBytes(bankName + "\0")), Chunk("ISFT", Encoding.ASCII.GetBytes("N64 Asset Studio\0")));
        var sdta = List("sdta", Chunk("smpl", smpl.ToArray()));
        var pdta = List("pdta", Chunk("phdr", phdr.ToArray()), Chunk("pbag", pbag.ToArray()), Chunk("pmod", modTerm),
            Chunk("pgen", pgen.ToArray()), Chunk("inst", inst.ToArray()), Chunk("ibag", ibag.ToArray()), Chunk("imod", modTerm),
            Chunk("igen", igen.ToArray()), Chunk("shdr", shdr.ToArray()));
        var body = "sfbk"u8.ToArray().Concat(info).Concat(sdta).Concat(pdta).ToArray();
        var riff = Chunk("RIFF", body);
        return riff;
    }

    public static string FromBankFile(Project p, int fi, string path)
    {
        var bf = p.BankFiles[fi];
        var insts = new List<Inst>();
        var byKey = new Dictionary<(int, int, bool), Inst>();
        foreach (var r in p.AllSounds().Where(r => r.BankFile == fi))
        {
            var key = (r.Bank.Index, r.Instrument.Index, r.Instrument.IsPercussion);
            if (!byKey.TryGetValue(key, out var e))
            {
                bool perc = r.Instrument.IsPercussion;
                e = new Inst
                {
                    Name = $"B{r.Bank.Index} {(perc ? "Perc" : "Inst")} {r.Instrument.Index}",
                    Preset = perc ? 0 : Math.Min(r.Instrument.Index, 127), Bank = perc ? 128 : r.Bank.Index,
                };
                byKey[key] = e;
                insts.Add(e);
            }
            short[] pcm; int rate;
            try { (pcm, rate) = p.DecodeSound(r); } catch (InvalidDataException) { continue; }
            var w = r.Sound.Wave;
            e.Zones.Add(new Zone
            {
                Pcm = pcm, Rate = rate, Root = r.Sound.KeyBase, Fine = r.Sound.Detune, Lo = r.Sound.KeyMin, Hi = r.Sound.KeyMax,
                Loop = w.LoopCount != 0 && w.LoopEnd > w.LoopStart ? ((int)w.LoopStart, (int)Math.Min(w.LoopEnd, (uint)pcm.Length)) : null,
                Pan = r.Sound.Pan, AttackUs = r.Sound.Attack, ReleaseUs = r.Sound.Release, Name = $"s{r.Instrument.Index}_{r.Sound.Index}",
            });
        }
        insts = insts.Where(i => i.Zones.Count > 0).ToList();
        if (insts.Count == 0) throw new InvalidDataException("No decodable samples in this bank");
        File.WriteAllBytes(path, Build($"N64 bank {fi} @ 0x{bf.Offset:X}", insts));
        return path;
    }
}
