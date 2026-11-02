namespace N64AssetStudio.Core;

/// <summary>Simple 32-bit RGBA image (row-major, top-down).</summary>
public sealed class RgbaImage
{
    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }

    public RgbaImage(int w, int h, byte[]? px = null)
    {
        Width = w;
        Height = h;
        Pixels = px ?? new byte[w * h * 4];
    }

    public (byte r, byte g, byte b, byte a) Get(int x, int y)
    {
        int i = (y * Width + x) * 4;
        return (Pixels[i], Pixels[i + 1], Pixels[i + 2], Pixels[i + 3]);
    }

    public void Set(int x, int y, byte r, byte g, byte b, byte a)
    {
        int i = (y * Width + x) * 4;
        Pixels[i] = r; Pixels[i + 1] = g; Pixels[i + 2] = b; Pixels[i + 3] = a;
    }

    public bool HasAlpha
    {
        get
        {
            for (int i = 3; i < Pixels.Length; i += 4) if (Pixels[i] < 255) return true;
            return false;
        }
    }

    public RgbaImage Scale(int factor)
    {
        if (factor <= 1) return this;
        var o = new RgbaImage(Width * factor, Height * factor);
        for (int y = 0; y < o.Height; y++)
            for (int x = 0; x < o.Width; x++)
                Array.Copy(Pixels, ((y / factor) * Width + x / factor) * 4, o.Pixels, (y * o.Width + x) * 4, 4);
        return o;
    }

    public int CountColors()
    {
        var set = new HashSet<int>();
        for (int i = 0; i < Pixels.Length; i += 4) set.Add(BitConverter.ToInt32(Pixels, i));
        return set.Count;
    }
}

/// <summary>N64 RDP texture formats.</summary>
public static class Texture
{
    public static readonly string[] Formats = { "RGBA16", "RGBA32", "CI4", "CI8", "IA4", "IA8", "IA16", "I4", "I8", "YUV16" };
    static readonly string[] FmtNames = { "RGBA", "YUV", "CI", "IA", "I" };
    static readonly int[] SizBits = { 4, 8, 16, 32 };

    public static string FormatName(int fmt, int siz) =>
        (fmt < FmtNames.Length ? FmtNames[fmt] : "?") + (siz < 4 ? SizBits[siz] : 0);

    public static int BitsOf(string fmt) => int.Parse(new string(fmt.Where(char.IsDigit).ToArray()));

    public static int ByteSize(int w, int h, string fmt) => (w * h * BitsOf(fmt) + 7) / 8;

    static byte At(byte[] d, int i) => i >= 0 && i < d.Length ? d[i] : (byte)0;

    static void Rgba16(int v, byte[] o, int i)
    {
        o[i] = (byte)(((v >> 11) & 0x1F) * 255 / 31);
        o[i + 1] = (byte)(((v >> 6) & 0x1F) * 255 / 31);
        o[i + 2] = (byte)(((v >> 1) & 0x1F) * 255 / 31);
        o[i + 3] = (byte)((v & 1) * 255);
    }

    /// <summary>Decode a TLUT (palette) as RGBA bytes (count*4).</summary>
    public static byte[] DecodePalette(byte[] d, int off, int count, bool ia = false)
    {
        var o = new byte[count * 4];
        for (int k = 0; k < count; k++)
        {
            int v = (At(d, off + k * 2) << 8) | At(d, off + k * 2 + 1);
            if (ia) { o[k * 4] = o[k * 4 + 1] = o[k * 4 + 2] = (byte)(v >> 8); o[k * 4 + 3] = (byte)v; }
            else Rgba16(v, o, k * 4);
        }
        return o;
    }

    public static byte[] GrayPalette(int count)
    {
        var o = new byte[count * 4];
        for (int k = 0; k < count; k++)
        {
            byte g = (byte)(k * 255 / (count - 1));
            o[k * 4] = o[k * 4 + 1] = o[k * 4 + 2] = g;
            o[k * 4 + 3] = 255;
        }
        return o;
    }

    public static RgbaImage Decode(byte[] d, int off, int w, int h, string fmt, byte[]? palette = null)
    {
        fmt = fmt.ToUpperInvariant();
        w = Math.Max(1, w);
        h = Math.Max(1, h);
        var img = new RgbaImage(w, h);
        var o = img.Pixels;
        int n = w * h;
        switch (fmt)
        {
            case "RGBA16":
                for (int i = 0; i < n; i++) Rgba16((At(d, off + i * 2) << 8) | At(d, off + i * 2 + 1), o, i * 4);
                break;
            case "RGBA32":
                for (int i = 0; i < n * 4; i++) o[i] = At(d, off + i);
                break;
            case "IA16":
                for (int i = 0; i < n; i++)
                {
                    byte v = At(d, off + i * 2), a = At(d, off + i * 2 + 1);
                    o[i * 4] = o[i * 4 + 1] = o[i * 4 + 2] = v; o[i * 4 + 3] = a;
                }
                break;
            case "IA8":
                for (int i = 0; i < n; i++)
                {
                    byte b = At(d, off + i);
                    o[i * 4] = o[i * 4 + 1] = o[i * 4 + 2] = (byte)((b >> 4) * 17);
                    o[i * 4 + 3] = (byte)((b & 0xF) * 17);
                }
                break;
            case "IA4":
                for (int i = 0; i < n; i++)
                {
                    int nb = Nibble(d, off, i);
                    o[i * 4] = o[i * 4 + 1] = o[i * 4 + 2] = (byte)((nb >> 1) * 255 / 7);
                    o[i * 4 + 3] = (byte)((nb & 1) * 255);
                }
                break;
            case "I8":
                for (int i = 0; i < n; i++) { o[i * 4] = o[i * 4 + 1] = o[i * 4 + 2] = At(d, off + i); o[i * 4 + 3] = 255; }
                break;
            case "I4":
                for (int i = 0; i < n; i++) { o[i * 4] = o[i * 4 + 1] = o[i * 4 + 2] = (byte)(Nibble(d, off, i) * 17); o[i * 4 + 3] = 255; }
                break;
            case "CI4":
            case "CI8":
                {
                    int count = fmt == "CI4" ? 16 : 256;
                    var pal = palette is { Length: > 0 } ? palette : GrayPalette(count);
                    for (int i = 0; i < n; i++)
                    {
                        int idx = fmt == "CI4" ? Nibble(d, off, i) : At(d, off + i);
                        if (idx * 4 + 3 < pal.Length) Array.Copy(pal, idx * 4, o, i * 4, 4);
                    }
                    break;
                }
            case "YUV16":
                for (int i = 0; i + 1 < n + 1; i += 2)
                {
                    int q = off + i * 2;
                    double u = At(d, q) - 128, y0 = At(d, q + 1), v = At(d, q + 2) - 128, y1 = At(d, q + 3);
                    for (int k = 0; k < 2 && i + k < n; k++)
                    {
                        double y = k == 0 ? y0 : y1;
                        int p = (i + k) * 4;
                        o[p] = Clamp(y + 1.402 * v);
                        o[p + 1] = Clamp(y - 0.344 * u - 0.714 * v);
                        o[p + 2] = Clamp(y + 1.772 * u);
                        o[p + 3] = 255;
                    }
                }
                break;
            default:
                throw new ArgumentException($"unsupported texture format {fmt}");
        }
        return img;
    }

    static byte Clamp(double v) => (byte)Math.Clamp((int)v, 0, 255);

    static int Nibble(byte[] d, int off, int i)
    {
        byte b = At(d, off + i / 2);
        return (i & 1) == 0 ? b >> 4 : b & 0xF;
    }
}
