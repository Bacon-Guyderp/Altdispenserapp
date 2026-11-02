using N64AssetStudio.Core.Export;

namespace N64AssetStudio;

static class Dialogs
{
    public static string? Choose(IWin32Window owner, string title, List<string> values, string def)
    {
        using var f = new Form
        {
            Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
            StartPosition = FormStartPosition.CenterParent, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(10),
        };
        var flow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown };
        var cb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
        cb.Items.AddRange(values.Cast<object>().ToArray());
        cb.SelectedItem = values.Contains(def) ? def : values.FirstOrDefault();
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK };
        flow.Controls.AddRange(new Control[] { new Label { Text = title, AutoSize = true }, cb, ok });
        f.Controls.Add(flow);
        f.AcceptButton = ok;
        return f.ShowDialog(owner) == DialogResult.OK ? cb.SelectedItem as string : null;
    }

    public const string HelpText = """
N64 games have no common file system or model format - every developer stored data differently.
N64 Asset Studio therefore works the way the N64 hardware does.

FILES
  Compressed blocks (Yaz0, Yay0, MIO0, gzip, zlib, Rare 0x1172 deflate) are located by signature and
  unpacked. Zelda 64 engine games (Ocarina of Time, Majora's Mask) are split along their real
  'dmadata' file table.

MODELS
  Everything the N64 draws goes through RSP display lists. The scanner finds lists for the F3D (Fast3D),
  F3DEX and F3DEX2 microcodes - which cover nearly all commercial games - and replays them to rebuild
  triangles, UVs, vertex colours/normals and bound textures.
  Display lists use segment addresses; the program works out which file offset each segment points at.
  If a model looks scrambled, adjust the "segment base" in the model view. Games with custom
  microcodes need the Texture Ripper and Raw views instead.

TEXTURES
  Textures referenced by display lists are listed automatically. The Texture Ripper shows any data
  as any N64 format (RGBA16/32, CI4/8, IA4/8/16, I4/8, YUV16).

AUDIO
  libultra sound banks ('B1' .ctl files) are parsed; samples (VADPCM or PCM16) are decoded from the
  matching wave table (.tbl). If the .tbl isn't found automatically, enter its location. Banks can be
  exported as SoundFonts. Nintendo's later in-house audio format (SM64 / OoT) and custom engines can be
  explored with Raw Audio.

MUSIC
  Standard MIDI files and libultra compressed MIDI ('S1' sequence banks) are converted to standard .mid.
  Other sequence formats are exported raw (.seq).

CODE
  The main code segment is disassembled (MIPS R4300i) and each function is shown as assembly plus a
  literal pseudo-C translation. This is a reading aid, not a compilable decompilation.

EXPORT FORMATS
  Models: glTF/GLB, FBX, OBJ, DAE, PLY, STL, Blender .blend (needs Blender installed; a ready-to-run
  import script is written otherwise), Unity .unitypackage.
  Images: PNG, BMP, TGA, TIFF, DDS, ICO, QOI, PPM.
  Audio: WAV, AIFF, FLAC, AU, raw (+ OGG, MP3, OPUS, M4A, WMA when ffmpeg is installed),
  SoundFont 2 for instrument banks, MIDI for music.

COMMAND LINE
  N64AssetStudio.exe --cli info game.z64
  N64AssetStudio.exe --cli export game.z64 C:\out --models fbx --audio flac --unity --blender --code
""";
}

sealed class ExportAllDialog : Form
{
    public string OutputFolder => _out.Text;
    readonly TextBox _out;

    public ExportAllDialog(BatchOptions opt, string folder)
    {
        Text = "Export All Assets";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);
        var t = new TableLayoutPanel { AutoSize = true, ColumnCount = 2 };
        Controls.Add(t);

        CheckBox Check(string text, bool value) => new() { Text = text, Checked = value, AutoSize = true };
        ComboBox Combo(IEnumerable<string> items, string sel)
        {
            var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
            c.Items.AddRange(items.Cast<object>().ToArray());
            c.SelectedItem = sel;
            return c;
        }
        void Row(Control a, Control? b = null) { t.Controls.Add(a); if (b != null) t.Controls.Add(b); else t.SetColumnSpan(a, 2); }

        var models = Check("3D models as", opt.Models); var mf = Combo(ModelExport.Formats.Keys, opt.ModelFormat);
        var roots = Check("    skip sub-meshes (avoid duplicates)", opt.RootsOnly);
        var tex = Check("Textures as", opt.Textures); var imf = Combo(ImageWriter.Formats.Keys, opt.ImageFormat);
        var aud = Check("Sounds as", opt.Audio); var af = Combo(SoundWriter.Formats().Keys, opt.AudioFormat);
        var sf2 = Check("Instrument banks as SoundFont (.sf2)", opt.SoundFonts);
        var mus = Check("Music as MIDI (.mid)", opt.Music);
        var raw = Check("Decompressed raw files (.bin)", opt.RawFiles);
        var code = Check("Code as pseudo-C (.c)", opt.Code);
        var unity = Check("Unity package (.unitypackage), models as", opt.UnityPackage); var uf = Combo(new[] { "fbx", "obj", "glb" }, opt.UnityModelFormat);
        var blend = Check("Blender scene (.blend)", opt.Blender);
        Row(models, mf); Row(roots); Row(tex, imf); Row(aud, af); Row(sf2); Row(mus); Row(raw); Row(code); Row(unity, uf); Row(blend);

        var outRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 10, 0, 0) };
        _out = new TextBox { Text = folder, Width = 360 };
        var browse = new Button { Text = "Browse...", AutoSize = true };
        browse.Click += (_, _) =>
        {
            using var fd = new FolderBrowserDialog { SelectedPath = _out.Text };
            if (fd.ShowDialog(this) == DialogResult.OK) _out.Text = fd.SelectedPath;
        };
        outRow.Controls.AddRange(new Control[] { new Label { Text = "Output folder:", AutoSize = true, Margin = new Padding(3, 7, 3, 0) }, _out, browse });
        Row(outRow);

        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        var ok = new Button { Text = "Export", DialogResult = DialogResult.OK };
        buttons.Controls.AddRange(new Control[] { cancel, ok });
        Row(buttons);
        AcceptButton = ok;
        CancelButton = cancel;
        FormClosing += (_, e) =>
        {
            if (DialogResult != DialogResult.OK) return;
            opt.Models = models.Checked; opt.ModelFormat = (string)mf.SelectedItem!;
            opt.RootsOnly = roots.Checked;
            opt.Textures = tex.Checked; opt.ImageFormat = (string)imf.SelectedItem!;
            opt.Audio = aud.Checked; opt.AudioFormat = (string)af.SelectedItem!;
            opt.SoundFonts = sf2.Checked; opt.Music = mus.Checked; opt.RawFiles = raw.Checked; opt.Code = code.Checked;
            opt.UnityPackage = unity.Checked; opt.UnityModelFormat = (string)uf.SelectedItem!; opt.Blender = blend.Checked;
            try { Directory.CreateDirectory(_out.Text); }
            catch (Exception ex) { MessageBox.Show(this, "Invalid output folder: " + ex.Message); e.Cancel = true; }
        };
    }
}

sealed class SettingsDialog : Form
{
    public SettingsDialog(AppSettings s)
    {
        Text = "Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);
        var t = new TableLayoutPanel { AutoSize = true, ColumnCount = 3 };
        Controls.Add(t);
        TextBox Field(string label, string value, bool browse)
        {
            t.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(3, 7, 3, 0) });
            var tb = new TextBox { Text = value, Width = 380 };
            t.Controls.Add(tb);
            if (browse)
            {
                var b = new Button { Text = "...", Width = 30 };
                b.Click += (_, _) =>
                {
                    using var d = new OpenFileDialog { Filter = "Programs (*.exe)|*.exe|All files|*.*" };
                    if (d.ShowDialog(this) == DialogResult.OK) tb.Text = d.FileName;
                };
                t.Controls.Add(b);
            }
            else t.Controls.Add(new Label());
            return tb;
        }
        var bl = Field("Blender executable", s.BlenderPath.Length > 0 ? s.BlenderPath : BlenderExport.FindBlender() ?? "", true);
        var ff = Field("ffmpeg executable (OGG/MP3/...)", s.FfmpegPath.Length > 0 ? s.FfmpegPath : SoundWriter.FindFfmpeg() ?? "", true);
        var sc = Field("Model export scale", s.ModelScale.ToString(System.Globalization.CultureInfo.InvariantCulture), false);
        var tb2 = Field("Texture discovery time limit (s)", s.TextureBudget.ToString(System.Globalization.CultureInfo.InvariantCulture), false);
        var deep = new CheckBox { Text = "Deep compression scan (zlib / Rare deflate - slower)", Checked = s.DeepCompression, AutoSize = true };
        var romdl = new CheckBox { Text = "Search uncompressed ROM data for models", Checked = s.ScanRomDisplayLists, AutoSize = true };
        t.Controls.Add(deep); t.SetColumnSpan(deep, 3);
        t.Controls.Add(romdl); t.SetColumnSpan(romdl, 3);
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK };
        buttons.Controls.AddRange(new Control[] { cancel, ok });
        t.Controls.Add(buttons); t.SetColumnSpan(buttons, 3);
        AcceptButton = ok;
        CancelButton = cancel;
        FormClosing += (_, _) =>
        {
            if (DialogResult != DialogResult.OK) return;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            s.BlenderPath = bl.Text.Trim();
            s.FfmpegPath = ff.Text.Trim();
            if (double.TryParse(sc.Text, System.Globalization.NumberStyles.Float, inv, out var scale) && scale > 0) s.ModelScale = scale;
            if (double.TryParse(tb2.Text, System.Globalization.NumberStyles.Float, inv, out var budget) && budget > 0) s.TextureBudget = budget;
            s.DeepCompression = deep.Checked;
            s.ScanRomDisplayLists = romdl.Checked;
        };
    }
}
