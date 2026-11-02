namespace N64AssetStudio.Core;

public sealed record TableFile(int Index, uint Vrom, int RomStart, int RomEnd, bool Compressed, byte[] Data);

/// <summary>Game-specific file tables (exact file boundaries where a known table exists).</summary>
public static class Games
{
    static readonly byte[] DmadataHead = { 0, 0, 0, 0, 0, 0, 0x10, 0x60, 0, 0, 0, 0, 0, 0, 0, 0 };

    /// <summary>Ocarina of Time / Majora's Mask "dmadata" table.</summary>
    public static List<TableFile> FindZeldaDmadata(byte[] rom, Action<double, string>? progress = null)
    {
        int pos = BE.Find(rom, DmadataHead, 0);
        while (pos != -1)
        {
            if (pos % 16 == 0)
            {
                var files = ReadDmadata(rom, pos, progress);
                if (files.Count > 20) return files;
            }
            pos = BE.Find(rom, DmadataHead, pos + 16);
        }
        return new();
    }

    static List<TableFile> ReadDmadata(byte[] rom, int pos, Action<double, string>? progress)
    {
        var entries = new List<(uint vs, uint ve, uint ps, uint pe)>();
        for (int p = pos; p + 16 <= rom.Length; p += 16)
        {
            uint vs = BE.U32(rom, p), ve = BE.U32(rom, p + 4), ps = BE.U32(rom, p + 8), pe = BE.U32(rom, p + 12);
            if (vs == 0 && ve == 0 && entries.Count > 0) break;
            if (ve < vs || ve - vs > 0x1000000 || entries.Count > 4096) return new();
            entries.Add((vs, ve, ps, pe));
        }
        var files = new List<TableFile>();
        for (int i = 0; i < entries.Count; i++)
        {
            var (vs, ve, ps, pe) = entries[i];
            if (i % 32 == 0) progress?.Invoke((double)i / entries.Count, $"Reading Zelda file table ({i}/{entries.Count})");
            if (ps == 0xFFFFFFFF || ve == vs || ps >= rom.Length) continue;
            if (pe == 0)
            {
                int len = (int)Math.Min(ve - vs, rom.Length - ps);
                files.Add(new TableFile(i, vs, (int)ps, (int)ps + len, false, rom.AsSpan((int)ps, len).ToArray()));
            }
            else
            {
                try
                {
                    var (d, _) = Compression.Yaz0(rom, (int)ps);
                    files.Add(new TableFile(i, vs, (int)ps, (int)pe, true, d));
                }
                catch (DecompressionException) { }
            }
        }
        return files;
    }
}
