using System.Diagnostics;
using System.Drawing.Imaging;
using System.Media;
using System.Runtime.InteropServices;
using System.Text.Json;
using N64AssetStudio.Core;
using N64AssetStudio.Core.Export;

namespace N64AssetStudio;

public sealed class AppSettings
{
    public string BlenderPath { get; set; } = "";
    public string FfmpegPath { get; set; } = "";
    public double ModelScale { get; set; } = 1.0;
    public bool DeepCompression { get; set; } = true;
    public bool ScanRomDisplayLists { get; set; } = true;
    public double TextureBudget { get; set; } = 90;
    public bool ShowChildDls { get; set; }
    public string LastDir { get; set; } = "";
    public string ExportDir { get; set; } = "";
    public List<string> Recent { get; set; } = new();

    static string FilePath
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "N64AssetStudio");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "settings.json");
        }
    }

    public static AppSettings Load()
    {
        try { return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new(); }
        catch (Exception) { return new(); }
    }

    public void Save()
    {
        try { File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); }
        catch (Exception) { }
    }

    public void Apply()
    {
        BlenderExport.BlenderPath = BlenderPath;
        SoundWriter.FfmpegPath = FfmpegPath;
    }
}

public static class Gfx
{
    public static Bitmap ToBitmap(RgbaImage img)
    {
        var bmp = new Bitmap(img.Width, img.Height, PixelFormat.Format32bppArgb);
        var data = bmp.LockBits(new Rectangle(0, 0, img.Width, img.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        var bgra = new byte[img.Pixels.Length];
        for (int i = 0; i < bgra.Length; i += 4)
        {
            bgra[i] = img.Pixels[i + 2]; bgra[i + 1] = img.Pixels[i + 1]; bgra[i + 2] = img.Pixels[i]; bgra[i + 3] = img.Pixels[i + 3];
        }
        for (int y = 0; y < img.Height; y++)
            Marshal.Copy(bgra, y * img.Width * 4, data.Scan0 + y * data.Stride, img.Width * 4);
        bmp.UnlockBits(data);
        return bmp;
    }

    public static Icon? AppIcon()
    {
        var s = typeof(Gfx).Assembly.GetManifestResourceStream("icon.ico");
        return s == null ? null : new Icon(s);
    }
}

/// <summary>Double-buffered surface that shows a bitmap, optionally zoomed with nearest-neighbour.</summary>
public sealed class ImageCanvas : Control
{
    Bitmap? _image;
    public bool CenterImage { get; set; }
    public int ZoomFactor { get; set; }
    public string Caption { get; set; } = "";

    public ImageCanvas()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Color.FromArgb(40, 42, 48);
    }

    public Bitmap? Image
    {
        get => _image;
        set { var old = _image; _image = value; if (!ReferenceEquals(old, value)) old?.Dispose(); Invalidate(); }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        if (_image != null)
        {
            int z = ZoomFactor > 0 ? ZoomFactor : 1;
            int w = _image.Width * z, h = _image.Height * z;
            int x = CenterImage ? (Width - w) / 2 : 4, y = CenterImage ? (Height - h) / 2 : 4;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            g.DrawImage(_image, new Rectangle(x, y, w, h));
            if (Caption.Length > 0) TextRenderer.DrawText(g, Caption, Font, new Point(CenterImage ? 6 : x + w + 8, 6), Color.White);
        }
        else if (Caption.Length > 0) TextRenderer.DrawText(g, Caption, Font, new Point(6, 6), Color.White);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _image?.Dispose();
        base.Dispose(disposing);
    }
}

public sealed class Player
{
    SoundPlayer? _sp;

    public void Play(short[] pcm, int rate)
    {
        Stop();
        _sp = new SoundPlayer(new MemoryStream(SoundWriter.Wav(pcm, rate)));
        _sp.Play();
    }

    public void Stop()
    {
        _sp?.Stop();
        _sp?.Dispose();
        _sp = null;
    }
}

public static class Util
{
    public static int ParseInt(string s)
    {
        s = s.Trim().ToLowerInvariant();
        if (s.StartsWith("0x")) return Convert.ToInt32(s[2..], 16);
        if (s.Any(c => c is >= 'a' and <= 'f')) return Convert.ToInt32(s, 16);
        return int.Parse(s);
    }

    public static void OpenWithShell(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception e) { MessageBox.Show(e.Message, "N64 Asset Studio"); }
    }

    public static string Filter(IEnumerable<KeyValuePair<string, string>> fmts) =>
        string.Join("|", fmts.Select(kv => $"{kv.Value} (*.{kv.Key})|*.{kv.Key}"));
}
