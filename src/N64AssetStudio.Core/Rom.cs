using System.Buffers.Binary;
using System.Text;

namespace N64AssetStudio.Core;

/// <summary>Big-endian reading helpers.</summary>
public static class BE
{
    public static ushort U16(byte[] d, int o) => BinaryPrimitives.ReadUInt16BigEndian(d.AsSpan(o, 2));
    public static short S16(byte[] d, int o) => BinaryPrimitives.ReadInt16BigEndian(d.AsSpan(o, 2));
    public static uint U32(byte[] d, int o) => BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(o, 4));
    public static int S32(byte[] d, int o) => BinaryPrimitives.ReadInt32BigEndian(d.AsSpan(o, 4));
    public static void W16(byte[] d, int o, int v) => BinaryPrimitives.WriteUInt16BigEndian(d.AsSpan(o, 2), (ushort)v);
    public static void W32(byte[] d, int o, uint v) => BinaryPrimitives.WriteUInt32BigEndian(d.AsSpan(o, 4), v);

    public static int Find(byte[] data, ReadOnlySpan<byte> needle, int start)
    {
        if (start >= data.Length) return -1;
        int i = data.AsSpan(start).IndexOf(needle);
        return i < 0 ? -1 : i + start;
    }
}

public enum ByteOrder { Z64, V64, N64, Raw }

public sealed class RomHeader
{
    public uint PiConfig, ClockRate, EntryPoint, Release, Crc1, Crc2;
    public string Title = "", Media = "", GameId = "", Region = "";
    public int Version;
    public string GameCode => Media + GameId + Region;
}

public sealed class Rom
{
    public static readonly string[] Extensions = { ".z64", ".v64", ".n64", ".bin", ".rom", ".u64", ".d64", ".ndd" };

    static readonly Dictionary<uint, string> CicByCrc = new()
    {
        [0x6170A4A1] = "6101", [0x90BB6CB5] = "6102", [0x0B050EE0] = "6103", [0x98BC2C86] = "6105",
        [0xACC8580A] = "6106", [0x009E9EA3] = "7102", [0x0E018159] = "8303 (64DD)",
    };
    static readonly Dictionary<string, uint> CicSeeds = new()
    {
        ["6101"] = 0xF8CA4DDC, ["7102"] = 0xF8CA4DDC, ["6102"] = 0xF8CA4DDC,
        ["6103"] = 0xA3886759, ["6105"] = 0xDF26F436, ["6106"] = 0x1FEA617A,
    };
    static readonly Dictionary<string, string> Regions = new()
    {
        ["7"] = "Beta", ["A"] = "Asian (NTSC)", ["B"] = "Brazilian", ["C"] = "Chinese", ["D"] = "German",
        ["E"] = "North America", ["F"] = "French", ["G"] = "Gateway 64 (NTSC)", ["H"] = "Dutch",
        ["I"] = "Italian", ["J"] = "Japanese", ["K"] = "Korean", ["L"] = "Gateway 64 (PAL)",
        ["N"] = "Canadian", ["P"] = "European", ["S"] = "Spanish", ["U"] = "Australian",
        ["W"] = "Scandinavian", ["X"] = "European", ["Y"] = "European", ["Z"] = "European",
    };
    static readonly Dictionary<string, string> MediaNames = new()
    {
        ["N"] = "Cartridge", ["D"] = "64DD disk", ["C"] = "Cartridge for 64DD", ["E"] = "64DD expansion", ["Z"] = "Aleck64",
    };

    public string Path = "";
    public byte[] Data = Array.Empty<byte>();
    public ByteOrder OriginalOrder;
    public RomHeader Header = new();
    public string Cic = "unknown";
    public bool IsDisk;

    public int Size => Data.Length;
    public string Name => System.IO.Path.GetFileName(Path);
    public string DisplayTitle => string.IsNullOrWhiteSpace(Header.Title)
        ? System.IO.Path.GetFileNameWithoutExtension(Path) : Header.Title.Trim();

    public static ByteOrder DetectByteOrder(byte[] d)
    {
        if (d.Length < 4) return ByteOrder.Raw;
        if (d[0] == 0x80 && d[1] == 0x37 && d[2] == 0x12 && d[3] == 0x40) return ByteOrder.Z64;
        if (d[0] == 0x37 && d[1] == 0x80 && d[2] == 0x40 && d[3] == 0x12) return ByteOrder.V64;
        if (d[0] == 0x40 && d[1] == 0x12 && d[2] == 0x37 && d[3] == 0x80) return ByteOrder.N64;
        if (d[0] == 0x80) return ByteOrder.Z64;
        if (d[1] == 0x80) return ByteOrder.V64;
        if (d[3] == 0x80) return ByteOrder.N64;
        return ByteOrder.Raw;
    }

    /// <summary>Convert between big endian and the given order (the operation is its own inverse).</summary>
    public static byte[] Swap(byte[] src, ByteOrder order)
    {
        var b = (byte[])src.Clone();
        if (order == ByteOrder.V64)
            for (int i = 0; i + 1 < b.Length; i += 2) (b[i], b[i + 1]) = (b[i + 1], b[i]);
        else if (order == ByteOrder.N64)
            for (int i = 0; i + 3 < b.Length; i += 4)
            {
                (b[i], b[i + 3]) = (b[i + 3], b[i]);
                (b[i + 1], b[i + 2]) = (b[i + 2], b[i + 1]);
            }
        return b;
    }

    public static Rom Load(string path)
    {
        var raw = File.ReadAllBytes(path);
        if (raw.Length < 0x40) throw new InvalidDataException("File is too small to be a Nintendo 64 ROM");
        var order = DetectByteOrder(raw);
        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        var rom = new Rom
        {
            Path = path,
            OriginalOrder = order,
            Data = order is ByteOrder.V64 or ByteOrder.N64 ? Swap(raw, order) : raw,
        };
        if (order == ByteOrder.Raw || ext is ".d64" or ".ndd")
        {
            rom.IsDisk = true;
            return rom;
        }
        rom.Header = ParseHeader(rom.Data);
        rom.Cic = DetectCic(rom.Data);
        return rom;
    }

    public void Save(string path, ByteOrder order) => File.WriteAllBytes(path, Swap(Data, order));

    public static RomHeader ParseHeader(byte[] d)
    {
        var h = new RomHeader
        {
            PiConfig = BE.U32(d, 0), ClockRate = BE.U32(d, 4), EntryPoint = BE.U32(d, 8),
            Release = BE.U32(d, 12), Crc1 = BE.U32(d, 16), Crc2 = BE.U32(d, 20),
        };
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            h.Title = Encoding.GetEncoding(932).GetString(d, 0x20, 20).TrimEnd('\0', ' ');
        }
        catch
        {
            h.Title = Encoding.ASCII.GetString(d, 0x20, 20).TrimEnd('\0', ' ');
        }
        static string C(byte b) => b >= 32 && b < 127 ? ((char)b).ToString() : "";
        h.Media = C(d[0x3B]);
        h.GameId = C(d[0x3C]) + C(d[0x3D]);
        h.Region = C(d[0x3E]);
        h.Version = d[0x3F];
        return h;
    }

    public static string DetectCic(byte[] d)
    {
        if (d.Length < 0x1000) return "unknown";
        uint crc = Crc32.Compute(d, 0x40, 0x1000 - 0x40);
        return CicByCrc.TryGetValue(crc, out var c) ? c : $"unknown (IPL3 CRC32 {crc:X8})";
    }

    public int? EntryToRom(uint ram)
    {
        uint ep = Header.EntryPoint;
        if (ep == 0) return null;
        long off = (long)ram - ep + 0x1000;
        return off >= 0x1000 && off < Data.Length ? (int)off : null;
    }

    static uint Rol(uint v, int n) { n &= 31; return n == 0 ? v : (v << n) | (v >> (32 - n)); }

    public static (uint, uint)? CalculateCrc(byte[] rom, string cic)
    {
        if (!CicSeeds.TryGetValue(cic, out uint seed) || rom.Length < 0x101000) return null;
        uint t1 = seed, t2 = seed, t3 = seed, t4 = seed, t5 = seed, t6 = seed;
        for (int i = 0x1000, idx = 0; i < 0x101000; i += 4, idx++)
        {
            uint d = BE.U32(rom, i);
            if (t6 + d < t6) t4++;
            t6 += d;
            t3 ^= d;
            uint r = Rol(d, (int)(d & 0x1F));
            t5 += r;
            if (t2 > d) t2 ^= r; else t2 ^= t6 ^ d;
            if (cic == "6105") t1 += BE.U32(rom, 0x750 + (idx & 0x3F) * 4) ^ d;
            else t1 += t5 ^ d;
        }
        return cic switch
        {
            "6103" => ((t6 ^ t4) + t3, (t5 ^ t2) + t1),
            "6106" => (t6 * t4 + t3, t5 * t2 + t1),
            _ => (t6 ^ t4 ^ t3, t5 ^ t2 ^ t1),
        };
    }

    public (bool? ok, (uint, uint)? calc) VerifyCrc()
    {
        var calc = CalculateCrc(Data, Cic.Split(' ')[0]);
        if (calc == null) return (null, null);
        return (calc.Value == (Header.Crc1, Header.Crc2), calc);
    }

    public List<(string, string)> InfoLines()
    {
        var rows = new List<(string, string)>
        {
            ("File", Path),
            ("Size", $"{Size:N0} bytes ({Size / 1048576.0:F2} MiB)"),
            ("Original byte order", OriginalOrder switch
            {
                ByteOrder.Z64 => "Big endian (.z64)", ByteOrder.V64 => "Byte-swapped (.v64)",
                ByteOrder.N64 => "Little endian (.n64)", _ => "Unknown / raw data",
            }),
        };
        if (IsDisk)
        {
            rows.Add(("Type", "64DD disk image / headerless data (generic scan only)"));
            return rows;
        }
        var h = Header;
        rows.Add(("Internal title", h.Title));
        rows.Add(("Game code", h.GameCode));
        rows.Add(("Media", MediaNames.GetValueOrDefault(h.Media, h.Media == "" ? "?" : h.Media)));
        rows.Add(("Region", Regions.GetValueOrDefault(h.Region, h.Region == "" ? "?" : h.Region)));
        rows.Add(("Version", $"1.{h.Version}"));
        rows.Add(("Entry point", $"0x{h.EntryPoint:X8}"));
        rows.Add(("Clock rate", $"0x{h.ClockRate:X8}"));
        rows.Add(("libultra release", $"0x{h.Release:X8}"));
        rows.Add(("CRC1 / CRC2", $"0x{h.Crc1:X8} / 0x{h.Crc2:X8}"));
        rows.Add(("CIC (boot chip)", Cic));
        return rows;
    }
}

public static class Crc32
{
    static readonly uint[] Table = Build();
    static uint[] Build()
    {
        var t = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            t[i] = c;
        }
        return t;
    }
    public static uint Compute(byte[] d, int off, int len) => Update(0, d.AsSpan(off, len));
    public static uint Compute(ReadOnlySpan<byte> s) => Update(0, s);
    public static uint Update(uint crc, ReadOnlySpan<byte> s)
    {
        crc ^= 0xFFFFFFFF;
        foreach (var b in s) crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFF;
    }
}
