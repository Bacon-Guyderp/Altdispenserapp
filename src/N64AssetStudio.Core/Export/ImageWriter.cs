using System.IO.Compression;

namespace N64AssetStudio.Core.Export;

/// <summary>Self-contained image encoders (no external libraries).</summary>
public static class ImageWriter
{
    public static readonly Dictionary<string, string> Formats = new()
    {
        ["png"] = "PNG image", ["bmp"] = "Windows bitmap", ["tga"] = "Targa", ["tif"] = "TIFF",
        ["dds"] = "DirectDraw Surface", ["ico"] = "Windows icon", ["qoi"] = "QOI image", ["ppm"] = "Portable pixmap",
    };

    public static string Save(RgbaImage img, string path, string? fmt = null, int scale = 1)
    {
        string ext = (fmt ?? Path.GetExtension(path).TrimStart('.')).ToLowerInvariant();
        if (ext == "tiff") ext = "tif";
        if (!Formats.ContainsKey(ext)) throw new ArgumentException($"unsupported image format {ext}");
        if (!path.EndsWith("." + ext, StringComparison.OrdinalIgnoreCase)) path = Path.ChangeExtension(path, ext);
        img = img.Scale(scale);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllBytes(path, Encode(img, ext));
        return path;
    }

    public static byte[] Encode(RgbaImage img, string ext) => ext switch
    {
        "png" => Png(img), "bmp" => Bmp(img), "tga" => Tga(img), "tif" => Tiff(img), "dds" => Dds(img),
        "ico" => Ico(img), "qoi" => Qoi(img), "ppm" => Ppm(img),
        _ => throw new ArgumentException(ext),
    };

    static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4];
        BE.W32(len, 0, (uint)data.Length);
        s.Write(len);
        var td = new byte[4 + data.Length];
        System.Text.Encoding.ASCII.GetBytes(type).CopyTo(td, 0);
        data.CopyTo(td, 4);
        s.Write(td);
        var crc = new byte[4];
        BE.W32(crc, 0, Crc32.Compute(td));
        s.Write(crc);
    }

    public static byte[] Png(RgbaImage img)
    {
        var ms = new MemoryStream();
        ms.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        var ihdr = new byte[13];
        BE.W32(ihdr, 0, (uint)img.Width);
        BE.W32(ihdr, 4, (uint)img.Height);
        ihdr[8] = 8; ihdr[9] = 6; // 8-bit RGBA
        Chunk(ms, "IHDR", ihdr);
        var raw = new MemoryStream();
        using (var z = new ZLibStream(raw, CompressionLevel.Optimal, true))
        {
            int stride = img.Width * 4;
            for (int y = 0; y < img.Height; y++)
            {
                z.WriteByte(0);
                z.Write(img.Pixels, y * stride, stride);
            }
        }
        Chunk(ms, "IDAT", raw.ToArray());
        Chunk(ms, "IEND", Array.Empty<byte>());
        return ms.ToArray();
    }

    static byte[] BgraBottomUp(RgbaImage img)
    {
        var o = new byte[img.Width * img.Height * 4];
        for (int y = 0; y < img.Height; y++)
            for (int x = 0; x < img.Width; x++)
            {
                int s = (y * img.Width + x) * 4, d = ((img.Height - 1 - y) * img.Width + x) * 4;
                o[d] = img.Pixels[s + 2]; o[d + 1] = img.Pixels[s + 1]; o[d + 2] = img.Pixels[s]; o[d + 3] = img.Pixels[s + 3];
            }
        return o;
    }

    public static byte[] Bmp(RgbaImage img)
    {
        var px = BgraBottomUp(img);
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        int hdr = 14 + 108;
        w.Write((byte)'B'); w.Write((byte)'M'); w.Write(hdr + px.Length); w.Write(0); w.Write(hdr);
        w.Write(108); w.Write(img.Width); w.Write(img.Height); w.Write((short)1); w.Write((short)32);
        w.Write(3); w.Write(px.Length); w.Write(2835); w.Write(2835); w.Write(0); w.Write(0);
        w.Write(0x00FF0000); w.Write(0x0000FF00); w.Write(0x000000FF); w.Write(unchecked((int)0xFF000000));
        w.Write(0x57696E20); // 'Win ' colour space
        w.Write(new byte[36 + 12]);
        w.Write(px);
        return ms.ToArray();
    }

    public static byte[] Tga(RgbaImage img)
    {
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        w.Write((byte)0); w.Write((byte)0); w.Write((byte)2); w.Write(new byte[5]);
        w.Write((short)0); w.Write((short)0); w.Write((short)img.Width); w.Write((short)img.Height);
        w.Write((byte)32); w.Write((byte)8);
        w.Write(BgraBottomUp(img));
        return ms.ToArray();
    }

    public static byte[] Tiff(RgbaImage img)
    {
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        int dataOff = 8, dataLen = img.Pixels.Length;
        int bpsOff = dataOff + dataLen, ifdOff = bpsOff + 8;
        w.Write((byte)'I'); w.Write((byte)'I'); w.Write((short)42); w.Write(ifdOff);
        w.Write(img.Pixels);
        w.Write((short)8); w.Write((short)8); w.Write((short)8); w.Write((short)8);
        var tags = new (short tag, short type, int count, int value)[]
        {
            (256, 4, 1, img.Width), (257, 4, 1, img.Height), (258, 3, 4, bpsOff), (259, 3, 1, 1), (262, 3, 1, 2),
            (273, 4, 1, dataOff), (277, 3, 1, 4), (278, 4, 1, img.Height), (279, 4, 1, dataLen), (284, 3, 1, 1), (338, 3, 1, 2),
        };
        w.Write((short)tags.Length);
        foreach (var t in tags)
        {
            w.Write(t.tag); w.Write(t.type); w.Write(t.count);
            if (t.type == 3 && t.count == 1) { w.Write((short)t.value); w.Write((short)0); } else w.Write(t.value);
        }
        w.Write(0);
        return ms.ToArray();
    }

    public static byte[] Dds(RgbaImage img)
    {
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        w.Write(0x20534444); // "DDS "
        w.Write(124); w.Write(0x1 | 0x2 | 0x4 | 0x8 | 0x1000); w.Write(img.Height); w.Write(img.Width);
        w.Write(img.Width * 4); w.Write(0); w.Write(0); w.Write(new byte[44]);
        w.Write(32); w.Write(0x41); w.Write(0); w.Write(32);
        w.Write(0x00FF0000); w.Write(0x0000FF00); w.Write(0x000000FF); w.Write(unchecked((int)0xFF000000));
        w.Write(0x1000); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
        for (int i = 0; i < img.Pixels.Length; i += 4)
        {
            w.Write(img.Pixels[i + 2]); w.Write(img.Pixels[i + 1]); w.Write(img.Pixels[i]); w.Write(img.Pixels[i + 3]);
        }
        return ms.ToArray();
    }

    public static byte[] Ico(RgbaImage img)
    {
        if (img.Width > 256 || img.Height > 256)
        {
            int s = Math.Max((img.Width + 255) / 256, (img.Height + 255) / 256);
            var small = new RgbaImage(img.Width / s, img.Height / s);
            for (int y = 0; y < small.Height; y++)
                for (int x = 0; x < small.Width; x++)
                    Array.Copy(img.Pixels, ((y * s) * img.Width + x * s) * 4, small.Pixels, (y * small.Width + x) * 4, 4);
            img = small;
        }
        var png = Png(img);
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        w.Write((short)0); w.Write((short)1); w.Write((short)1);
        w.Write((byte)(img.Width & 0xFF)); w.Write((byte)(img.Height & 0xFF)); w.Write((byte)0); w.Write((byte)0);
        w.Write((short)1); w.Write((short)32); w.Write(png.Length); w.Write(22);
        w.Write(png);
        return ms.ToArray();
    }

    public static byte[] Ppm(RgbaImage img)
    {
        var head = System.Text.Encoding.ASCII.GetBytes($"P6\n{img.Width} {img.Height}\n255\n");
        var o = new byte[head.Length + img.Width * img.Height * 3];
        head.CopyTo(o, 0);
        for (int i = 0, j = head.Length; i < img.Pixels.Length; i += 4, j += 3)
        {
            int a = img.Pixels[i + 3];
            o[j] = (byte)(img.Pixels[i] * a / 255); o[j + 1] = (byte)(img.Pixels[i + 1] * a / 255); o[j + 2] = (byte)(img.Pixels[i + 2] * a / 255);
        }
        return o;
    }

    public static byte[] Qoi(RgbaImage img)
    {
        var o = new List<byte>(img.Pixels.Length + 22);
        o.AddRange("qoif"u8.ToArray());
        var hdr = new byte[10];
        BE.W32(hdr, 0, (uint)img.Width); BE.W32(hdr, 4, (uint)img.Height); hdr[8] = 4; hdr[9] = 0;
        o.AddRange(hdr);
        var index = new byte[64 * 4];
        byte pr = 0, pg = 0, pb = 0, pa = 255;
        int run = 0;
        var p = img.Pixels;
        for (int i = 0; i < p.Length; i += 4)
        {
            byte r = p[i], g = p[i + 1], b = p[i + 2], a = p[i + 3];
            if (r == pr && g == pg && b == pb && a == pa)
            {
                if (++run == 62 || i + 4 == p.Length) { o.Add((byte)(0xC0 | (run - 1))); run = 0; }
                continue;
            }
            if (run > 0) { o.Add((byte)(0xC0 | (run - 1))); run = 0; }
            int h = (r * 3 + g * 5 + b * 7 + a * 11) % 64;
            if (index[h * 4] == r && index[h * 4 + 1] == g && index[h * 4 + 2] == b && index[h * 4 + 3] == a) o.Add((byte)h);
            else
            {
                index[h * 4] = r; index[h * 4 + 1] = g; index[h * 4 + 2] = b; index[h * 4 + 3] = a;
                if (a == pa)
                {
                    int dr = (sbyte)(r - pr), dg = (sbyte)(g - pg), db = (sbyte)(b - pb);
                    int drg = dr - dg, dbg = db - dg;
                    if (dr is >= -2 and <= 1 && dg is >= -2 and <= 1 && db is >= -2 and <= 1)
                        o.Add((byte)(0x40 | ((dr + 2) << 4) | ((dg + 2) << 2) | (db + 2)));
                    else if (dg is >= -32 and <= 31 && drg is >= -8 and <= 7 && dbg is >= -8 and <= 7)
                    {
                        o.Add((byte)(0x80 | (dg + 32)));
                        o.Add((byte)(((drg + 8) << 4) | (dbg + 8)));
                    }
                    else { o.Add(0xFE); o.Add(r); o.Add(g); o.Add(b); }
                }
                else { o.Add(0xFF); o.Add(r); o.Add(g); o.Add(b); o.Add(a); }
            }
            pr = r; pg = g; pb = b; pa = a;
        }
        o.AddRange(new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 });
        return o.ToArray();
    }
}
