using System.Text;
using N64AssetStudio.Core;
using N64AssetStudio.Core.Export;
using N64AssetStudio.Core.Render;

namespace N64AssetStudio;

public sealed record NodeData(string Kind, object? Obj, Action<TreeNode>? Lazy = null);

/// <summary>Builds a preview page: a stack of rows at the top and an optional fill control below.</summary>
sealed class Page
{
    public readonly Panel Root = new() { Dock = DockStyle.Fill, Padding = new Padding(8) };
    readonly FlowLayoutPanel _top = new()
    {
        Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
        FlowDirection = FlowDirection.TopDown, WrapContents = false,
    };

    public Page() { Root.Controls.Add(_top); }

    public T Add<T>(T c) where T : Control { _top.Controls.Add(c); return c; }

    public FlowLayoutPanel Row(params Control[] cs)
    {
        var r = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = new Padding(0, 2, 0, 2) };
        foreach (var c in cs) r.Controls.Add(c);
        return Add(r);
    }

    public void Fill(Control c)
    {
        c.Dock = DockStyle.Fill;
        Root.Controls.Add(c);
        c.BringToFront();
    }

    public void Header(string text) =>
        Add(new Label { Text = text, AutoSize = true, Font = new Font("Segoe UI", 12f, FontStyle.Bold), Margin = new Padding(0, 0, 0, 4) });

    public void Note(string text, Color? color = null) =>
        Add(new Label { Text = text, AutoSize = true, MaximumSize = new Size(1000, 0), ForeColor = color ?? Color.DimGray });

    public void Info(IEnumerable<(string, string)> rows)
    {
        var t = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 4, 0, 4) };
        foreach (var (k, v) in rows)
        {
            t.Controls.Add(new Label { Text = k + ":", AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) });
            t.Controls.Add(new Label { Text = v, AutoSize = true });
        }
        Add(t);
    }

    public static Button Btn(string text, Action click)
    {
        var b = new Button { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(4, 0, 4, 0) };
        b.Click += (_, _) => click();
        return b;
    }

    public static Label Lbl(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(3, 7, 3, 0) };
    public static TextBox Box(string text, int width) => new() { Text = text, Width = width };
}

public sealed class MainForm : Form
{
    readonly AppSettings _settings = AppSettings.Load();
    readonly Player _player = new();
    Project? _project;
    bool _busy;

    readonly TreeView _tree = new() { Dock = DockStyle.Fill, HideSelection = false, Font = new Font("Segoe UI", 9f) };
    readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    readonly TabPage _tabPreview = new("Preview"), _tabHex = new("Hex"), _tabRipper = new("Texture Ripper"), _tabRaw = new("Raw Audio");
    readonly ToolStripStatusLabel _status = new("Open a Nintendo 64 ROM to begin.") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    readonly ToolStripProgressBar _progress = new() { Width = 260, Maximum = 1000 };
    readonly ToolStripTextBox _filter = new() { Width = 200 };
    readonly ToolStripMenuItem _recentMenu = new("Recent ROMs");
    CheckBox _showChildren = null!;

    // model view state
    MeshView? _meshView;
    ImageCanvas? _modelCanvas;
    MeshView.Mode _renderMode = MeshView.Mode.Textured;
    bool _edges;
    Point? _drag;
    bool _dragPan;

    public MainForm(string? path)
    {
        _settings.Apply();
        Text = "N64 Asset Studio";
        Icon = Gfx.AppIcon();
        Size = new Size(1320, 840);
        MinimumSize = new Size(900, 560);
        StartPosition = FormStartPosition.CenterScreen;
        AllowDrop = true;
        DragEnter += (_, e) => e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
        DragDrop += (_, e) => { if (e.Data?.GetData(DataFormats.FileDrop) is string[] f && f.Length > 0) OpenRom(f[0]); };
        KeyPreview = true;

        BuildMenu();
        BuildBody();
        ShowWelcome();
        if (path != null) Shown += (_, _) => OpenRom(path);
        FormClosing += (_, _) => { _player.Stop(); _settings.Save(); };
    }

    // ================================================================ layout
    void BuildMenu()
    {
        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("&File");
        file.DropDownItems.Add(new ToolStripMenuItem("&Open ROM...", null, (_, _) => OpenDialog(), Keys.Control | Keys.O));
        file.DropDownItems.Add(_recentMenu);
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("Save ROM as .z64 (big endian)...", null, (_, _) => SaveRomAs(ByteOrder.Z64));
        file.DropDownItems.Add("Save ROM as .v64 (byte-swapped)...", null, (_, _) => SaveRomAs(ByteOrder.V64));
        file.DropDownItems.Add("Save ROM as .n64 (little endian)...", null, (_, _) => SaveRomAs(ByteOrder.N64));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("E&xit", null, (_, _) => Close());
        var export = new ToolStripMenuItem("&Export");
        export.DropDownItems.Add(new ToolStripMenuItem("Export &Selected...", null, (_, _) => ExportSelected(), Keys.Control | Keys.E));
        export.DropDownItems.Add(new ToolStripMenuItem("Export &All Assets...", null, (_, _) => ExportAllDialog(), Keys.Control | Keys.Shift | Keys.E));
        export.DropDownItems.Add(new ToolStripSeparator());
        export.DropDownItems.Add("Export Everything as Unity Package...", null, (_, _) => ExportAllDialog("unity"));
        export.DropDownItems.Add("Export All Models to Blender (.blend)...", null, (_, _) => ExportAllDialog("blender"));
        var tools = new ToolStripMenuItem("&Tools");
        tools.DropDownItems.Add("Texture Ripper", null, (_, _) => _tabs.SelectedTab = _tabRipper);
        tools.DropDownItems.Add("Raw Audio Explorer", null, (_, _) => _tabs.SelectedTab = _tabRaw);
        tools.DropDownItems.Add("Verify ROM checksum", null, (_, _) => VerifyCrc());
        tools.DropDownItems.Add(new ToolStripSeparator());
        tools.DropDownItems.Add("&Settings...", null, (_, _) => SettingsDialog());
        var help = new ToolStripMenuItem("&Help");
        help.DropDownItems.Add("How it works", null, (_, _) => ShowHelp());
        help.DropDownItems.Add("About", null, (_, _) => MessageBox.Show(this,
            "N64 Asset Studio 2.0\n\nNintendo 64 ROM asset browser and exporter.\nUse only with ROMs you have dumped from cartridges you own.",
            "About", MessageBoxButtons.OK, MessageBoxIcon.Information));
        menu.Items.AddRange(new ToolStripItem[] { file, export, tools, help });
        MainMenuStrip = menu;

        var bar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(4, 2, 4, 2) };
        bar.Items.Add(new ToolStripButton("Open ROM", null, (_, _) => OpenDialog()));
        bar.Items.Add(new ToolStripButton("Export Selected", null, (_, _) => ExportSelected()));
        bar.Items.Add(new ToolStripButton("Export All...", null, (_, _) => ExportAllDialog()));
        bar.Items.Add(new ToolStripSeparator());
        bar.Items.Add(new ToolStripLabel("Filter:"));
        _filter.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { PopulateTree(); e.SuppressKeyPress = true; } };
        bar.Items.Add(_filter);
        bar.Items.Add(new ToolStripButton("Apply", null, (_, _) => PopulateTree()));
        _showChildren = new CheckBox { Text = "Show sub-meshes", Checked = _settings.ShowChildDls, AutoSize = true, BackColor = Color.Transparent };
        _showChildren.CheckedChanged += (_, _) => { _settings.ShowChildDls = _showChildren.Checked; PopulateTree(); };
        bar.Items.Add(new ToolStripControlHost(_showChildren));

        var status = new StatusStrip();
        status.Items.Add(_status);
        status.Items.Add(_progress);

        Controls.Add(status);
        Controls.Add(bar);
        Controls.Add(menu);
        RefreshRecent();
    }

    void BuildBody()
    {
        var split = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1 };
        Controls.Add(split);
        split.BringToFront();
        split.Panel1.Controls.Add(_tree);
        split.Panel2.Controls.Add(_tabs);
        Load += (_, _) => split.SplitterDistance = 360;
        _tabs.TabPages.AddRange(new[] { _tabPreview, _tabHex, _tabRipper, _tabRaw });
        _tree.BeforeExpand += (_, e) =>
        {
            if (e.Node?.Tag is NodeData { Lazy: not null } nd)
            {
                _tree.BeginUpdate();
                e.Node.Nodes.Clear();
                e.Node.Tag = nd with { Lazy = null };
                nd.Lazy(e.Node);
                _tree.EndUpdate();
            }
        };
        _tree.AfterSelect += (_, _) => OnSelect();
        _tree.NodeMouseDoubleClick += (_, _) => ExportSelected();
        BuildHexTab();
        BuildRipperTab();
        BuildRawAudioTab();
    }

    Page NewPreview()
    {
        _player.Stop();
        _meshView = null;
        _modelCanvas = null;
        foreach (Control c in _tabPreview.Controls) c.Dispose();
        _tabPreview.Controls.Clear();
        var p = new Page();
        _tabPreview.Controls.Add(p.Root);
        return p;
    }

    void ShowWelcome()
    {
        var p = NewPreview();
        p.Header("N64 Asset Studio");
        p.Note("Open any Nintendo 64 ROM (.z64 .v64 .n64 .bin .rom .u64 .d64) - use File > Open, or drag a ROM onto this window.\n\n" +
               "The ROM is normalised to big endian, its compressed files (Yaz0, Yay0, MIO0, zlib/gzip, Rare deflate) are unpacked, " +
               "and every file is scanned for:\n\n" +
               "  •  3D models: F3D / F3DEX / F3DEX2 display lists, with textures, UVs and vertex colours\n" +
               "  •  Textures: RGBA16/32, CI4/8 (with palettes), IA4/8/16, I4/8 - plus a manual Texture Ripper\n" +
               "  •  Sound: libultra sound banks (VADPCM / PCM samples), SoundFont export\n" +
               "  •  Music: MIDI and compressed-MIDI sequences converted to standard .mid\n" +
               "  •  Code: MIPS R4300i disassembly and pseudo-C of every function\n\n" +
               "Export to glTF/GLB, FBX, OBJ, DAE, PLY, STL, Blender (.blend), Unity (.unitypackage), PNG/BMP/TGA/TIFF/DDS/ICO/QOI, " +
               "WAV/AIFF/FLAC (+ OGG/MP3 with ffmpeg), SF2 and MIDI.", Color.Black);
    }

    void SetStatus(string text, double? frac = null)
    {
        _status.Text = text;
        if (frac.HasValue) _progress.Value = (int)Math.Clamp(frac.Value * 1000, 0, 1000);
    }

    async Task<T?> RunBg<T>(string title, Func<Action<double, string>, T> work) where T : class
    {
        if (_busy) { MessageBox.Show(this, "Please wait for the current task to finish.", Text); return null; }
        _busy = true;
        UseWaitCursor = true;
        var prog = new Progress<(double f, string m)>(x => SetStatus(x.m, x.f));
        IProgress<(double, string)> ip = prog;
        try { return await Task.Run(() => work((f, m) => ip.Report((f, m)))); }
        catch (Exception e)
        {
            SetStatus($"{title} failed: {e.Message}", 0);
            MessageBox.Show(this, $"{title} failed:\n{e.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }
        finally { _busy = false; UseWaitCursor = false; }
    }

    // ================================================================ files
    void OpenDialog()
    {
        using var d = new OpenFileDialog
        {
            Title = "Open Nintendo 64 ROM",
            Filter = "N64 ROMs|" + string.Join(";", Rom.Extensions.Select(e => "*" + e)) + "|All files|*.*",
            InitialDirectory = Directory.Exists(_settings.LastDir) ? _settings.LastDir : "",
        };
        if (d.ShowDialog(this) == DialogResult.OK) OpenRom(d.FileName);
    }

    void RefreshRecent()
    {
        _recentMenu.DropDownItems.Clear();
        foreach (var p in _settings.Recent.Take(10)) _recentMenu.DropDownItems.Add(p, null, (_, _) => OpenRom(p));
        if (_settings.Recent.Count == 0) _recentMenu.DropDownItems.Add(new ToolStripMenuItem("(none)") { Enabled = false });
    }

    async void OpenRom(string path)
    {
        if (!File.Exists(path)) { MessageBox.Show(this, $"File not found:\n{path}", Text); return; }
        _settings.LastDir = Path.GetDirectoryName(path) ?? "";
        _settings.Recent = new[] { path }.Concat(_settings.Recent.Where(r => r != path)).Take(10).ToList();
        _settings.Save();
        RefreshRecent();
        _tree.Nodes.Clear();
        ShowWelcome();
        var opts = new ScanOptions
        {
            DeepCompression = _settings.DeepCompression, ScanRomDisplayLists = _settings.ScanRomDisplayLists,
            TextureTimeBudget = _settings.TextureBudget,
        };
        var proj = await RunBg("Loading ROM", prog => { var p = Project.Open(path); p.Scan(prog, opts); return p; });
        if (proj == null) return;
        _project = proj;
        Text = $"N64 Asset Studio - {proj.Rom.DisplayTitle} [{proj.Rom.Name}]";
        PopulateTree();
        RipperRefreshBlobs();
        SetStatus($"Loaded {proj.Rom.Name}: {proj.Log.LastOrDefault()}", 1);
        if (_tree.Nodes.Count > 0) _tree.SelectedNode = _tree.Nodes[0];
    }

    void SaveRomAs(ByteOrder order)
    {
        if (_project == null) return;
        string ext = order.ToString().ToLowerInvariant();
        using var d = new SaveFileDialog { Filter = $".{ext} ROM|*.{ext}", FileName = Path.GetFileNameWithoutExtension(_project.Rom.Name) + "." + ext };
        if (d.ShowDialog(this) != DialogResult.OK) return;
        _project.Rom.Save(d.FileName, order);
        SetStatus($"Saved {d.FileName}");
    }

    async void VerifyCrc()
    {
        if (_project == null || _project.Rom.IsDisk) return;
        var rom = _project.Rom;
        var res = await RunBg("Checksum", prog => { prog(0.5, "Calculating checksum..."); return (object)rom.VerifyCrc(); });
        if (res is not ValueTuple<bool?, (uint, uint)?> r) return;
        if (r.Item1 == null) MessageBox.Show(this, "The checksum can't be verified for this CIC / ROM size.", Text);
        else
            MessageBox.Show(this, (r.Item1.Value ? "Checksum OK" : "Checksum MISMATCH") +
                $"\nHeader:      {rom.Header.Crc1:X8} {rom.Header.Crc2:X8}\nCalculated: {r.Item2!.Value.Item1:X8} {r.Item2.Value.Item2:X8}", Text);
        SetStatus("Checksum verified", 1);
    }

    // ================================================================= tree
    TreeNode AddNode(TreeNodeCollection parent, string text, string kind, object? obj = null, Action<TreeNode>? lazy = null)
    {
        var n = new TreeNode(text) { Tag = new NodeData(kind, obj, lazy) };
        if (lazy != null) n.Nodes.Add("...");
        parent.Add(n);
        return n;
    }

    void PopulateTree()
    {
        _tree.BeginUpdate();
        _tree.Nodes.Clear();
        var p = _project;
        if (p == null) { _tree.EndUpdate(); return; }
        string flt = _filter.Text.Trim().ToLowerInvariant();
        bool Match(string s) => flt.Length == 0 || s.ToLowerInvariant().Contains(flt);

        AddNode(_tree.Nodes, $"ROM: {p.Rom.DisplayTitle}", "rom");
        AddNode(_tree.Nodes, $"Files ({p.Blobs.Count})", "group", null, n =>
        {
            foreach (var b in p.Blobs)
            {
                var t = $"{b.Name}  [{b.Data.Length:N0} bytes]";
                if (Match(t)) AddNode(n.Nodes, t, "blob", b);
            }
        });

        bool kids = _showChildren.Checked;
        var byBlob = p.Models.Where(m => kids || !m.Dl.IsChild).GroupBy(m => m.BlobIndex).ToList();
        AddNode(_tree.Nodes, $"Models ({byBlob.Sum(g => g.Count())})", "group", null, n =>
        {
            foreach (var g in byBlob)
                AddNode(n.Nodes, $"{p.Blobs[g.Key].Name}  ({g.Count()})", "group", null, gn =>
                {
                    if (g.Count() > 1) AddNode(gn.Nodes, "[whole file as one model]", "blobmodel", g.Key);
                    foreach (var m in g)
                    {
                        var t = $"{m.Name}  ({m.Dl.TriCount} tris)" + (m.Dl.IsChild ? "  [sub]" : "");
                        if (Match(t)) AddNode(gn.Nodes, t, "model", m);
                    }
                });
        });

        var texByBlob = p.Textures.GroupBy(t => t.BlobIndex).ToList();
        AddNode(_tree.Nodes, $"Textures ({p.Textures.Count})", "group", null, n =>
        {
            foreach (var g in texByBlob)
                AddNode(n.Nodes, $"{p.Blobs[g.Key].Name}  ({g.Count()})", "group", null, gn =>
                {
                    foreach (var t in g) if (Match(t.Name)) AddNode(gn.Nodes, t.Name, "texture", t);
                });
        });

        var sounds = p.AllSounds();
        AddNode(_tree.Nodes, $"Sound banks ({p.BankFiles.Count} files, {sounds.Count} sounds)", "group", null, n =>
        {
            for (int fi = 0; fi < p.BankFiles.Count; fi++)
            {
                int f = fi;
                var bf = p.BankFiles[fi];
                AddNode(n.Nodes, $"Bank file {fi} @ 0x{bf.Offset:X} in {p.Blobs[bf.BlobIndex].Name} ({sounds.Count(s => s.BankFile == fi)} sounds)",
                    "bankfile", fi, bn =>
                    {
                        foreach (var s in sounds.Where(s => s.BankFile == f && Match(s.Name)))
                            AddNode(bn.Nodes, $"{s.Name}  ({(s.Sound.Wave.Type == 0 ? "VADPCM" : "PCM16")}, {s.Sound.Wave.Length:N0} bytes)", "sound", s);
                    });
            }
        });

        AddNode(_tree.Nodes, $"Music ({p.Sequences.Count})", "group", null, n =>
        {
            for (int i = 0; i < p.Sequences.Count; i++)
            {
                var s = p.Sequences[i];
                var t = $"{i:D3} {s.Name} [{s.Kind}, {s.Length:N0} bytes]";
                if (Match(t)) AddNode(n.Nodes, t, "sequence", s);
            }
        });

        AddNode(_tree.Nodes, $"Code ({p.Functions.Count} functions)", "group", null, n =>
        {
            foreach (var fn in p.Functions.Take(50000))
            {
                var t = $"func_{fn:X8}";
                if (Match(t)) AddNode(n.Nodes, t, "function", fn);
            }
        });
        _tree.EndUpdate();
    }

    NodeData? Selected => _tree.SelectedNode?.Tag as NodeData;

    void OnSelect()
    {
        if (Selected is not NodeData nd || _project == null) return;
        try
        {
            switch (nd.Kind)
            {
                case "rom": ShowRom(); break;
                case "blob": ShowBlob((Blob)nd.Obj!); break;
                case "model": ShowModel((ModelAsset)nd.Obj!); break;
                case "blobmodel": ShowBlobModel((int)nd.Obj!); break;
                case "texture": ShowTexture((TextureAsset)nd.Obj!); break;
                case "sound": ShowSound((SoundRef)nd.Obj!); break;
                case "bankfile": ShowBankFile((int)nd.Obj!); break;
                case "sequence": ShowSequence((Sequence)nd.Obj!); break;
                case "function": ShowFunction((uint)nd.Obj!); break;
            }
        }
        catch (Exception e)
        {
            var p = NewPreview();
            p.Note($"Could not decode this asset:\n{e.Message}", Color.Firebrick);
        }
    }

    // ============================================================= previews
    void ShowRom()
    {
        var pr = _project!;
        var p = NewPreview();
        p.Header(pr.Rom.DisplayTitle);
        p.Info(pr.Rom.InfoLines());
        p.Header("Scan results");
        p.Info(new[]
        {
            ("Files / compressed blocks", (pr.Blobs.Count - 1).ToString()), ("Display lists (models)", pr.Models.Count.ToString()),
            ("Textures", pr.Textures.Count.ToString()), ("Sound bank files", pr.BankFiles.Count.ToString()),
            ("Sounds", pr.AllSounds().Count.ToString()), ("Music sequences", pr.Sequences.Count.ToString()),
            ("Code functions", pr.Functions.Count.ToString()),
        });
        foreach (var l in pr.Log) p.Note("•  " + l, Color.Black);
        p.Row(Page.Btn("Export All Assets...", () => ExportAllDialog()), Page.Btn("Verify checksum", VerifyCrc));
        HexShow(0, 0);
    }

    void ShowBlob(Blob b)
    {
        var pr = _project!;
        var p = NewPreview();
        p.Header(b.Name);
        int nModels = pr.Models.Count(m => m.BlobIndex == b.Index);
        p.Info(new[]
        {
            ("Type", b.Kind), ("ROM offset", $"0x{b.RomOffset:X8}"), ("Stored size", $"{b.CompressedSize:N0} bytes"),
            ("Decompressed size", $"{b.Data.Length:N0} bytes"), ("Display lists", nModels.ToString()),
            ("Textures", pr.Textures.Count(t => t.BlobIndex == b.Index).ToString()),
        });
        var row = p.Row(Page.Btn("Export raw file (.bin)...", () => ExportBlobRaw(b)),
            Page.Btn("Open in Texture Ripper", () => RipperOpen(b.Index, 0)),
            Page.Btn("Open in Raw Audio", () => RawAudioOpen(b.Index, 0)));
        if (nModels > 0) row.Controls.Add(Page.Btn("Preview all models in file", () => ShowBlobModel(b.Index)));
        HexShow(b.Index, 0);
    }

    // --------------------------------------------------------------- models
    void ShowModel(ModelAsset m)
    {
        ModelView(_project!.GetMesh(m), m);
        HexShow(m.BlobIndex, m.Dl.Offset);
    }

    void ShowBlobModel(int bi)
    {
        var mesh = _project!.GetBlobMesh(bi, !_showChildren.Checked);
        if (mesh != null) ModelView(mesh, null);
    }

    void ModelView(Mesh mesh, ModelAsset? asset)
    {
        var p = NewPreview();
        p.Header(mesh.Name);
        var modes = new[] { ("Textured", MeshView.Mode.Textured), ("Solid", MeshView.Mode.Solid), ("Wireframe", MeshView.Mode.Wire) };
        var row = p.Row();
        foreach (var (label, mode) in modes)
        {
            var rb = new RadioButton { Text = label, AutoSize = true, Checked = _renderMode == mode, Margin = new Padding(3, 6, 6, 0) };
            rb.CheckedChanged += (_, _) => { if (rb.Checked) { _renderMode = mode; RedrawModel(); } };
            row.Controls.Add(rb);
        }
        var edges = new CheckBox { Text = "Edges", AutoSize = true, Checked = _edges, Margin = new Padding(3, 6, 6, 0) };
        edges.CheckedChanged += (_, _) => { _edges = edges.Checked; RedrawModel(); };
        row.Controls.Add(edges);
        row.Controls.Add(Page.Btn("Reset view", () => { _meshView?.Reset(); RedrawModel(); }));
        row.Controls.Add(Page.Btn("Export...", ExportSelected));
        int texCount = mesh.Materials.Count(x => x.Texture != null);
        p.Note($"{mesh.Vertices.Count} vertices, {mesh.Triangles.Count} triangles, {mesh.Materials.Count} materials ({texCount} textured).   " +
               "Drag = rotate, right-drag = pan, wheel = zoom.", Color.Black);
        if (asset != null)
        {
            var baseBox = Page.Box($"0x{asset.BaseOverride ?? asset.Dl.Base ?? 0:X}", 90);
            void Apply(int delta)
            {
                int v;
                try { v = Util.ParseInt(baseBox.Text); } catch (Exception) { return; }
                asset.BaseOverride = Math.Max(0, v + delta);
                ShowModel(asset);
            }
            p.Row(Page.Lbl("Segment base offset (adjust if the model looks scrambled):"), baseBox,
                Page.Btn("Apply", () => Apply(0)), Page.Btn("-16", () => Apply(-16)), Page.Btn("+16", () => Apply(16)),
                Page.Btn("Auto", () => { asset.BaseOverride = null; ShowModel(asset); }));
        }
        if (mesh.Notes.Count > 0) p.Note(string.Join("; ", mesh.Notes));
        var thumbs = mesh.Materials.Where(x => x.Texture != null).Take(24).ToList();
        if (thumbs.Count > 0)
        {
            var strip = p.Row();
            foreach (var t in thumbs)
            {
                var img = t.Texture!;
                int z = Math.Max(1, 48 / Math.Max(img.Width, img.Height));
                strip.Controls.Add(new PictureBox { Image = Gfx.ToBitmap(img.Scale(z)), SizeMode = PictureBoxSizeMode.AutoSize, Margin = new Padding(2) });
            }
        }
        var canvas = new ImageCanvas();
        p.Fill(canvas);
        _modelCanvas = canvas;
        _meshView = new MeshView(mesh);
        canvas.Resize += (_, _) => RedrawModel();
        canvas.MouseDown += (_, e) => { _drag = e.Location; _dragPan = e.Button == MouseButtons.Right; canvas.Focus(); };
        canvas.MouseMove += (_, e) =>
        {
            if (_drag is not Point d || _meshView == null) return;
            if (_dragPan) { _meshView.PanX += e.X - d.X; _meshView.PanY += e.Y - d.Y; }
            else
            {
                _meshView.Yaw += (e.X - d.X) * 0.01;
                _meshView.Pitch = Math.Clamp(_meshView.Pitch + (e.Y - d.Y) * 0.01, -1.57, 1.57);
            }
            _drag = e.Location;
            RedrawModel(fast: true);
        };
        canvas.MouseUp += (_, _) => { _drag = null; RedrawModel(); };
        canvas.MouseWheel += (_, e) => { if (_meshView != null) { _meshView.Zoom *= e.Delta > 0 ? 1.1 : 1 / 1.1; RedrawModel(); } };
        RedrawModel();
    }

    void RedrawModel(bool fast = false)
    {
        if (_meshView == null || _modelCanvas == null || _modelCanvas.Width < 20 || _modelCanvas.Height < 20) return;
        var mode = _renderMode;
        // while dragging, use the cheaper flat-shaded mode so rotation stays smooth
        if (fast && mode == MeshView.Mode.Textured) mode = MeshView.Mode.Solid;
        _modelCanvas.Image = Gfx.ToBitmap(_meshView.Render(_modelCanvas.Width, _modelCanvas.Height, mode, _edges));
    }

    // ------------------------------------------------------------- textures
    static ImageCanvas TextureCanvas(RgbaImage img)
    {
        var c = new ImageCanvas { CenterImage = true, BackColor = Color.FromArgb(85, 85, 85) };
        void Update()
        {
            int z = Math.Max(1, Math.Min(16, Math.Min(c.Width / Math.Max(1, img.Width), c.Height / Math.Max(1, img.Height))));
            c.ZoomFactor = z;
            c.Caption = $"{img.Width}x{img.Height}  zoom {z}x";
            c.Invalidate();
        }
        c.Image = Gfx.ToBitmap(MeshView.Checker(img.Scale(1), 4));
        c.Resize += (_, _) => Update();
        Update();
        return c;
    }

    void ShowTexture(TextureAsset t)
    {
        var p = NewPreview();
        p.Header(t.Name);
        p.Row(Page.Btn("Export...", ExportSelected),
            Page.Btn("Open in Texture Ripper", () => RipperOpen(t.BlobIndex, t.Offset, t.Width, t.Height, t.Fmt, t.TlutOffset)));
        p.Info(new[]
        {
            ("Format", t.Fmt), ("Size", $"{t.Width} x {t.Height}"), ("File", _project!.Blobs[t.BlobIndex].Name),
            ("Offset", $"0x{t.Offset:X}"), ("Palette", t.TlutOffset.HasValue ? $"0x{t.TlutOffset:X}" : "-"),
        });
        p.Fill(TextureCanvas(_project.TextureImage(t)));
        HexShow(t.BlobIndex, t.Offset);
    }

    // ---------------------------------------------------------------- sound
    void TblControls(Page p, int fi)
    {
        var bf = _project!.BankFiles[fi];
        var bBox = Page.Box((bf.TblBlob ?? 0).ToString(), 40);
        var oBox = Page.Box(bf.TblOffset.HasValue ? $"0x{bf.TblOffset:X}" : "", 90);
        p.Row(Page.Lbl("Wave table (.tbl) location - file #, offset:"), bBox, oBox, Page.Btn("Apply", () =>
        {
            try { bf.TblBlob = int.Parse(bBox.Text); bf.TblOffset = Util.ParseInt(oBox.Text); } catch (Exception) { return; }
            _project.ClearSoundCache();
            OnSelect();
        }));
    }

    static ImageCanvas WaveCanvas(short[] pcm)
    {
        var c = new ImageCanvas { BackColor = Color.FromArgb(24, 26, 30) };
        c.Resize += (_, _) => { if (c.Width > 16 && c.Height > 16) c.Image = Gfx.ToBitmap(MeshView.Waveform(pcm, c.Width - 8, c.Height - 8)); };
        return c;
    }

    void ShowSound(SoundRef r)
    {
        var p = NewPreview();
        p.Header(r.Name);
        var w = r.Sound.Wave;
        short[] pcm; int rate;
        try { (pcm, rate) = _project!.DecodeSound(r); }
        catch (InvalidDataException e) { p.Note(e.Message, Color.Firebrick); TblControls(p, r.BankFile); return; }
        p.Info(new[]
        {
            ("Codec", w.Type == 0 ? "VADPCM" : "PCM 16-bit"), ("Sample rate", $"{rate} Hz"), ("Samples", $"{pcm.Length:N0}"),
            ("Duration", $"{pcm.Length / (double)rate:F2} s"), ("Root key", r.Sound.KeyBase.ToString()),
            ("Loop", w.LoopCount != 0 ? $"{w.LoopStart}-{w.LoopEnd} x{w.LoopCount}" : "none"), ("Wave offset in .tbl", $"0x{w.Base:X}"),
        });
        var rateBox = new ComboBox { Width = 80, Text = rate.ToString() };
        rateBox.Items.AddRange(new object[] { "8000", "11025", "16000", "22050", "32000", "44100" });
        p.Row(Page.Btn("Play", () => _player.Play(pcm, int.TryParse(rateBox.Text, out var rr) ? rr : rate)), Page.Btn("Stop", _player.Stop),
            Page.Lbl("Playback rate:"), rateBox, Page.Btn("Export...", ExportSelected));
        TblControls(p, r.BankFile);
        p.Fill(WaveCanvas(pcm));
        if (_project!.LocateTbl(r.BankFile) is { } loc) HexShow(loc.blob, loc.off + (int)w.Base);
    }

    void ShowBankFile(int fi)
    {
        var pr = _project!;
        var bf = pr.BankFiles[fi];
        var p = NewPreview();
        p.Header($"Sound bank file {fi}");
        var loc = pr.LocateTbl(fi);
        p.Info(new[]
        {
            ("Location", $"{pr.Blobs[bf.BlobIndex].Name} @ 0x{bf.Offset:X}"), ("Banks", bf.Banks.Count.ToString()),
            ("Instruments", bf.Banks.Sum(b => b.Instruments.Count).ToString()),
            ("Sounds", pr.AllSounds().Count(s => s.BankFile == fi).ToString()),
            ("Sample rate", string.Join(", ", bf.Banks.Select(b => b.SampleRate))),
            ("Wave table", loc.HasValue ? $"file {loc.Value.blob} @ 0x{loc.Value.off:X}" : "not found"),
        });
        TblControls(p, fi);
        p.Row(Page.Btn("Export SoundFont (.sf2)...", () => ExportSf2(fi)), Page.Btn("Export all sounds...", () => ExportBankSounds(fi)));
        HexShow(bf.BlobIndex, bf.Offset);
    }

    // ---------------------------------------------------------------- music
    void ShowSequence(Sequence s)
    {
        var p = NewPreview();
        p.Header(s.Name);
        string fmt = s.Kind switch
        {
            "MIDI" => "Standard MIDI file", "CSeq" => "libultra compressed MIDI (converted to .mid)", _ => "Unknown sequence format (raw export only)",
        };
        p.Info(new[] { ("Format", fmt), ("File", _project!.Blobs[s.BlobIndex].Name), ("Offset", $"0x{s.Offset:X}"), ("Size", $"{s.Length:N0} bytes") });
        var row = p.Row(Page.Btn("Export...", ExportSelected));
        if (s.Kind != "raw")
            row.Controls.Add(Page.Btn("Play in default MIDI player", () =>
            {
                var midi = _project.SequenceMidi(s);
                if (midi == null) return;
                var path = Path.Combine(Path.GetTempPath(), "n64studio_preview.mid");
                File.WriteAllBytes(path, midi);
                Util.OpenWithShell(path);
            }));
        HexShow(s.BlobIndex, s.Offset);
    }

    // ----------------------------------------------------------------- code
    static TextBox CodeBox(string text) => new()
    {
        Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 10f),
        BackColor = Color.FromArgb(30, 31, 36), ForeColor = Color.FromArgb(215, 218, 224), Text = text.Replace("\n", "\r\n"),
    };

    void ShowFunction(uint fn)
    {
        var pr = _project!;
        var p = NewPreview();
        p.Header($"func_{fn:X8}");
        if (pr.Rom.EntryToRom(fn) is not int off) { p.Note("Function is outside the ROM"); return; }
        p.Row(Page.Btn("Export...", ExportSelected));
        p.Note($"ROM offset 0x{off:X}. The pseudo-C is a literal per-instruction translation (MIPS delay slots execute before the branch).");
        p.Fill(CodeBox(Mips.FunctionToPseudoC(pr.Rom.Data, off, fn)));
        HexShow(0, off);
    }

    // ================================================================== hex
    TextBox _hexBox = null!, _hexBlob = null!, _hexOff = null!;
    const int HexPage = 0x4000;

    void BuildHexTab()
    {
        var p = new Page();
        _tabHex.Controls.Add(p.Root);
        _hexBlob = Page.Box("0", 50);
        _hexOff = Page.Box("0x0", 100);
        _hexOff.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { HexGo(); e.SuppressKeyPress = true; } };
        p.Row(Page.Lbl("File #"), _hexBlob, Page.Lbl("Offset"), _hexOff, Page.Btn("Go", HexGo),
            Page.Btn("<< Page", () => HexPageMove(-1)), Page.Btn("Page >>", () => HexPageMove(1)));
        _hexBox = CodeBox("");
        _hexBox.BackColor = SystemColors.Window;
        _hexBox.ForeColor = SystemColors.WindowText;
        p.Fill(_hexBox);
    }

    void HexShow(int blob, int off) { _hexBlob.Text = blob.ToString(); _hexOff.Text = $"0x{off:X}"; HexGo(); }

    void HexPageMove(int d)
    {
        int off = 0;
        try { off = Util.ParseInt(_hexOff.Text); } catch (Exception) { }
        _hexOff.Text = $"0x{Math.Max(0, off + d * HexPage):X}";
        HexGo();
    }

    void HexGo()
    {
        if (_project == null) return;
        byte[] data; int off;
        try { data = _project.Blobs[int.Parse(_hexBlob.Text)].Data; off = Util.ParseInt(_hexOff.Text); } catch (Exception) { return; }
        int start = Math.Min(off & ~0xF, Math.Max(0, (data.Length - 1) & ~0xF));
        var sb = new StringBuilder();
        for (int i = start; i < Math.Min(data.Length, start + HexPage); i += 16)
        {
            sb.Append($"{i:X8}  ");
            var asc = new StringBuilder();
            for (int k = 0; k < 16; k++)
            {
                if (i + k < data.Length) { sb.Append($"{data[i + k]:X2} "); asc.Append(data[i + k] is >= 32 and < 127 ? (char)data[i + k] : '.'); }
                else sb.Append("   ");
            }
            sb.Append(' ').Append(asc).Append("\r\n");
        }
        _hexBox.Text = sb.ToString();
    }

    // ========================================================= texture ripper
    ComboBox _rpBlob = null!, _rpFmt = null!, _rpZoom = null!;
    TextBox _rpOff = null!, _rpW = null!, _rpH = null!, _rpPal = null!;
    ImageCanvas _rpCanvas = null!;
    RgbaImage? _rpImage;

    void BuildRipperTab()
    {
        var p = new Page();
        _tabRipper.Controls.Add(p.Root);
        p.Note("Browse any file as raw texture data - for games whose textures aren't referenced by a recognised display list. " +
               "Step through the data until the image looks right.", Color.Black);
        _rpBlob = new ComboBox { Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
        _rpOff = Page.Box("0x0", 90); _rpW = Page.Box("32", 45); _rpH = Page.Box("32", 45); _rpPal = Page.Box("", 80);
        _rpFmt = new ComboBox { Width = 80, DropDownStyle = ComboBoxStyle.DropDownList };
        _rpFmt.Items.AddRange(Texture.Formats);
        _rpFmt.SelectedItem = "RGBA16";
        _rpZoom = new ComboBox { Width = 50, DropDownStyle = ComboBoxStyle.DropDownList };
        _rpZoom.Items.AddRange(new object[] { "1", "2", "3", "4", "6", "8", "12", "16" });
        _rpZoom.SelectedItem = "4";
        foreach (var tb in new[] { _rpOff, _rpW, _rpH, _rpPal })
            tb.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { RipperDraw(); e.SuppressKeyPress = true; } };
        foreach (var cb in new[] { _rpBlob, _rpFmt, _rpZoom }) cb.SelectedIndexChanged += (_, _) => RipperDraw();
        p.Row(Page.Lbl("File"), _rpBlob, Page.Lbl("Offset"), _rpOff, Page.Lbl("W"), _rpW, Page.Lbl("H"), _rpH,
            Page.Lbl("Format"), _rpFmt, Page.Lbl("Palette @"), _rpPal, Page.Lbl("Zoom"), _rpZoom);
        var nav = p.Row();
        foreach (var (t, d) in new (string, object)[] { ("<< image", "-img"), ("< row", "-row"), ("< 8", -8), ("< 2", -2), ("2 >", 2), ("8 >", 8), ("row >", "+row"), ("image >>", "+img") })
            nav.Controls.Add(Page.Btn(t, () => RipperStep(d)));
        nav.Controls.Add(Page.Btn("Width /2", () => RipperScaleW(0.5)));
        nav.Controls.Add(Page.Btn("Width x2", () => RipperScaleW(2)));
        nav.Controls.Add(Page.Btn("Export image...", RipperExport));
        _rpCanvas = new ImageCanvas { BackColor = Color.FromArgb(85, 85, 85) };
        _rpCanvas.MouseWheel += (_, e) => RipperStep(e.Delta > 0 ? "-row" : "+row");
        _rpCanvas.MouseEnter += (_, _) => _rpCanvas.Focus();
        p.Fill(_rpCanvas);
    }

    void RipperRefreshBlobs()
    {
        foreach (var cb in new[] { _rpBlob, _raBlob })
        {
            cb.Items.Clear();
            foreach (var b in _project!.Blobs) cb.Items.Add($"{b.Index}: {b.Name}");
            if (cb.Items.Count > 0) cb.SelectedIndex = 0;
        }
    }

    void RipperOpen(int bi, int off, int? w = null, int? h = null, string? fmt = null, int? pal = null)
    {
        _rpOff.Text = $"0x{off:X}";
        if (w.HasValue) { _rpW.Text = w.ToString(); _rpH.Text = h.ToString(); }
        if (fmt != null) _rpFmt.SelectedItem = fmt;
        _rpPal.Text = pal.HasValue ? $"0x{pal:X}" : "";
        _rpBlob.SelectedIndex = bi;
        _tabs.SelectedTab = _tabRipper;
        RipperDraw();
    }

    bool RipperParams(out int bi, out int off, out int w, out int h, out string fmt)
    {
        bi = _rpBlob.SelectedIndex; off = 0; w = h = 0; fmt = (string?)_rpFmt.SelectedItem ?? "RGBA16";
        try
        {
            off = Math.Max(0, Util.ParseInt(_rpOff.Text));
            w = Math.Clamp(int.Parse(_rpW.Text), 1, 4096);
            h = Math.Clamp(int.Parse(_rpH.Text), 1, 4096);
        }
        catch (Exception) { return false; }
        return bi >= 0 && _project != null;
    }

    void RipperStep(object d)
    {
        if (!RipperParams(out _, out int off, out int w, out int h, out var fmt)) return;
        int size = Texture.ByteSize(w, h, fmt), row = Math.Max(1, Texture.ByteSize(w, 1, fmt));
        int step = d switch { "-img" => -size, "+img" => size, "-row" => -row, "+row" => row, int i => i, _ => 0 };
        _rpOff.Text = $"0x{Math.Max(0, off + step):X}";
        RipperDraw();
    }

    void RipperScaleW(double k)
    {
        if (!RipperParams(out _, out _, out int w, out int h, out _)) return;
        _rpW.Text = Math.Max(1, (int)(w * k)).ToString();
        _rpH.Text = Math.Max(1, (int)(h / k)).ToString();
        RipperDraw();
    }

    void RipperDraw()
    {
        if (!RipperParams(out int bi, out int off, out int w, out int h, out var fmt)) return;
        var data = _project!.Blobs[bi].Data;
        byte[]? pal = null;
        if (fmt is "CI4" or "CI8" && _rpPal.Text.Trim().Length > 0)
            try { pal = Texture.DecodePalette(data, Util.ParseInt(_rpPal.Text), fmt == "CI4" ? 16 : 256); } catch (Exception) { }
        _rpImage = Texture.Decode(data, off, w, h, fmt, pal);
        _rpCanvas.ZoomFactor = int.Parse((string)_rpZoom.SelectedItem!);
        _rpCanvas.Caption = $"{fmt} {w}x{h} @ 0x{off:X}  ({Texture.ByteSize(w, h, fmt)} bytes)";
        _rpCanvas.Image = Gfx.ToBitmap(MeshView.Checker(_rpImage, 4));
    }

    void RipperExport()
    {
        if (_rpImage == null) return;
        var path = AskSave($"texture_{_rpOff.Text}", Util.Filter(ImageWriter.Formats), "png");
        if (path != null) SetStatus("Saved " + ImageWriter.Save(_rpImage, path));
    }

    // ============================================================ raw audio
    ComboBox _raBlob = null!, _raFmt = null!, _raRate = null!;
    TextBox _raOff = null!, _raLen = null!;
    ImageCanvas _raCanvas = null!;
    (short[] pcm, int rate)? _raPcm;

    void BuildRawAudioTab()
    {
        var p = new Page();
        _tabRaw.Controls.Add(p.Root);
        p.Note("Interpret any region as uncompressed PCM audio - for games with custom sound engines. " +
               "Try different formats / rates until it sounds right.", Color.Black);
        _raBlob = new ComboBox { Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
        _raOff = Page.Box("0x0", 90); _raLen = Page.Box("0x10000", 90);
        _raFmt = new ComboBox { Width = 70, DropDownStyle = ComboBoxStyle.DropDownList };
        _raFmt.Items.AddRange(new object[] { "s16be", "s16le", "s8", "u8" });
        _raFmt.SelectedIndex = 0;
        _raRate = new ComboBox { Width = 80, Text = "22050" };
        _raRate.Items.AddRange(new object[] { "8000", "11025", "16000", "22050", "32000", "44100" });
        p.Row(Page.Lbl("File"), _raBlob, Page.Lbl("Offset"), _raOff, Page.Lbl("Length"), _raLen, Page.Lbl("Format"), _raFmt, Page.Lbl("Rate"), _raRate);
        p.Row(Page.Btn("Decode", () => RawDecode(false)), Page.Btn("Play", () => RawDecode(true)), Page.Btn("Stop", _player.Stop),
            Page.Btn("Export...", RawExport));
        _raCanvas = new ImageCanvas { BackColor = Color.FromArgb(24, 26, 30) };
        p.Fill(_raCanvas);
    }

    void RawAudioOpen(int bi, int off)
    {
        _raBlob.SelectedIndex = bi;
        _raOff.Text = $"0x{off:X}";
        _tabs.SelectedTab = _tabRaw;
    }

    void RawDecode(bool play)
    {
        if (_project == null || _raBlob.SelectedIndex < 0) return;
        try
        {
            var pcm = Audio.DecodeRawPcm(_project.Blobs[_raBlob.SelectedIndex].Data, Util.ParseInt(_raOff.Text), Util.ParseInt(_raLen.Text), (string)_raFmt.SelectedItem!);
            int rate = int.Parse(_raRate.Text);
            _raPcm = (pcm, rate);
            _raCanvas.Image = Gfx.ToBitmap(MeshView.Waveform(pcm, _raCanvas.Width - 8, _raCanvas.Height - 8));
            if (play) _player.Play(pcm, rate);
        }
        catch (Exception e) { SetStatus(e.Message); }
    }

    void RawExport()
    {
        RawDecode(false);
        if (_raPcm is not { } r) return;
        var path = AskSave("raw_audio", Util.Filter(SoundWriter.Formats()), "wav");
        if (path != null) SetStatus("Saved " + SoundWriter.Save(r.pcm, r.rate, path));
    }

    // =============================================================== export
    string? AskSave(string name, string filter, string defaultExt)
    {
        using var d = new SaveFileDialog
        {
            Title = "Export", FileName = name, Filter = filter, DefaultExt = defaultExt, AddExtension = true,
            InitialDirectory = Directory.Exists(_settings.ExportDir) ? _settings.ExportDir : "",
        };
        if (d.ShowDialog(this) != DialogResult.OK) return null;
        _settings.ExportDir = Path.GetDirectoryName(d.FileName) ?? "";
        var path = d.FileName;
        if (Path.GetExtension(path).Length == 0)
        {
            // take the extension from the selected filter entry
            var parts = filter.Split('|');
            int idx = (d.FilterIndex - 1) * 2 + 1;
            path += idx < parts.Length ? parts[idx].TrimStart('*') : "." + defaultExt;
        }
        return path;
    }

    static string ModelFilter()
    {
        var f = ModelExport.Formats.ToList();
        f.Insert(1, new("blend", "Blender scene (.blend - uses your Blender install)"));
        f.Insert(2, new("unitypackage", "Unity package"));
        return Util.Filter(f);
    }

    void ExportSelected()
    {
        if (_project == null || Selected is not NodeData nd) return;
        var p = _project;
        try
        {
            switch (nd.Kind)
            {
                case "model":
                case "blobmodel":
                    {
                        var mesh = nd.Kind == "model" ? p.GetMesh((ModelAsset)nd.Obj!) : p.GetBlobMesh((int)nd.Obj!, !_showChildren.Checked);
                        if (mesh == null) return;
                        var path = AskSave(ModelExport.Safe(mesh.Name), ModelFilter(), "glb");
                        if (path != null) ExportMesh(mesh, path);
                        break;
                    }
                case "texture":
                    {
                        var t = (TextureAsset)nd.Obj!;
                        var path = AskSave(ModelExport.Safe(t.Name), Util.Filter(ImageWriter.Formats), "png");
                        if (path != null) SetStatus("Saved " + ImageWriter.Save(p.TextureImage(t), path));
                        break;
                    }
                case "sound":
                    {
                        var r = (SoundRef)nd.Obj!;
                        var path = AskSave(r.Name, Util.Filter(SoundWriter.Formats()), "wav");
                        if (path != null) { var (pcm, rate) = p.DecodeSound(r); SetStatus("Saved " + SoundWriter.Save(pcm, rate, path)); }
                        break;
                    }
                case "bankfile": ExportSf2((int)nd.Obj!); break;
                case "sequence":
                    {
                        var s = (Sequence)nd.Obj!;
                        string filter = s.Kind != "raw" ? "MIDI file (*.mid)|*.mid|Raw sequence data (*.seq)|*.seq" : "Raw sequence data (*.seq)|*.seq";
                        var path = AskSave($"seq_{s.Offset:X6}", filter, s.Kind != "raw" ? "mid" : "seq");
                        if (path == null) break;
                        var data = path.EndsWith(".seq", StringComparison.OrdinalIgnoreCase) ? p.SequenceRaw(s) : p.SequenceMidi(s) ?? p.SequenceRaw(s);
                        File.WriteAllBytes(path, data);
                        SetStatus("Saved " + path);
                        break;
                    }
                case "function":
                    {
                        uint fn = (uint)nd.Obj!;
                        var path = AskSave($"func_{fn:X8}", "Pseudo-C (*.c)|*.c|Assembly (*.s)|*.s", "c");
                        if (path == null || p.Rom.EntryToRom(fn) is not int off) break;
                        File.WriteAllText(path, path.EndsWith(".s", StringComparison.OrdinalIgnoreCase)
                            ? Mips.DisassemblyText(p.Rom.Data, off, fn, 4000) : Mips.FunctionToPseudoC(p.Rom.Data, off, fn));
                        SetStatus("Saved " + path);
                        break;
                    }
                case "blob": ExportBlobRaw((Blob)nd.Obj!); break;
                default: ExportAllDialog(); break;
            }
        }
        catch (Exception e) { MessageBox.Show(this, "Export failed:\n" + e.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    async void ExportMesh(Mesh mesh, string path)
    {
        string ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        double scale = _settings.ModelScale;
        if (ext == "blend")
        {
            string folder = Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + "_blender");
            var r = await RunBg("Blender export", prog =>
            {
                prog(0.3, "Building .blend with Blender...");
                return BlenderExport.Export(new[] { mesh }, folder, Path.GetFileNameWithoutExtension(path), scale);
            });
            if (r != null) MessageBox.Show(this, r.Message + $"\n\nFolder: {folder}", Text);
        }
        else if (ext == "unitypackage")
        {
            string tmp = Path.Combine(Path.GetTempPath(), "n64studio_" + Guid.NewGuid().ToString("N"));
            try
            {
                ModelExport.Export(mesh, Path.Combine(tmp, ModelExport.Safe(mesh.Name) + ".fbx"), "fbx", scale);
                var pkg = new UnityPackage($"Assets/N64/{ModelExport.Safe(_project!.Rom.DisplayTitle)}/{ModelExport.Safe(mesh.Name)}");
                pkg.AddFolderTree("", tmp);
                pkg.Save(path);
            }
            finally { try { Directory.Delete(tmp, true); } catch (IOException) { } }
            SetStatus("Saved " + path);
        }
        else
        {
            var files = ModelExport.Export(mesh, path, ext, scale);
            SetStatus($"Saved {files.Count} file(s): {path}");
        }
    }

    void ExportBlobRaw(Blob b)
    {
        var path = AskSave(ModelExport.Safe(b.Name) + ".bin", "Binary (*.bin)|*.bin", "bin");
        if (path == null) return;
        File.WriteAllBytes(path, b.Data);
        SetStatus("Saved " + path);
    }

    async void ExportSf2(int fi)
    {
        var path = AskSave($"bankfile{fi}.sf2", "SoundFont 2 (*.sf2)|*.sf2", "sf2");
        if (path == null) return;
        var r = await RunBg("SoundFont export", _ => SoundFont.FromBankFile(_project!, fi, path));
        if (r != null) SetStatus("Saved " + r, 1);
    }

    async void ExportBankSounds(int fi)
    {
        using var fd = new FolderBrowserDialog { Description = "Export all sounds to folder", UseDescriptionForTitle = true };
        if (fd.ShowDialog(this) != DialogResult.OK) return;
        var fmt = Dialogs.Choose(this, "Audio format", SoundWriter.Formats().Keys.ToList(), "wav");
        if (fmt == null) return;
        string folder = fd.SelectedPath;
        var res = await RunBg("Sound export", prog =>
        {
            var refs = _project!.AllSounds().Where(r => r.BankFile == fi).ToList();
            for (int i = 0; i < refs.Count; i++)
            {
                prog((double)i / Math.Max(1, refs.Count), "Exporting " + refs[i].Name);
                var (pcm, rate) = _project.DecodeSound(refs[i]);
                SoundWriter.Save(pcm, rate, Path.Combine(folder, refs[i].Name), fmt);
            }
            return (object)refs.Count;
        });
        if (res != null) SetStatus($"Exported {res} sounds to {folder}", 1);
    }

    async void ExportAllDialog(string? preset = null)
    {
        if (_project == null) { MessageBox.Show(this, "Open a ROM first.", Text); return; }
        var opt = new BatchOptions { ModelScale = _settings.ModelScale, UnityPackage = preset == "unity", Blender = preset == "blender" };
        string folder = string.IsNullOrEmpty(_settings.ExportDir) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : _settings.ExportDir;
        using var dlg = new ExportAllDialog(opt, folder);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        folder = dlg.OutputFolder;
        _settings.ExportDir = folder;
        var cts = new CancellationTokenSource();
        var report = await RunBg("Export", prog => BatchExport.ExportAll(_project, folder, opt, prog, cts.Token));
        if (report == null) return;
        SetStatus("Export finished", 1);
        if (MessageBox.Show(this, "Export finished:\n\n" + string.Join("\n", report) + "\n\nOpen the output folder?", Text,
                MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            Util.OpenWithShell(folder);
    }

    void SettingsDialog()
    {
        using var d = new SettingsDialog(_settings);
        if (d.ShowDialog(this) == DialogResult.OK) { _settings.Apply(); _settings.Save(); }
    }

    // ============================================================ self test
    /// <summary>Opens a ROM, selects one node of every kind, saves a screenshot of each view and
    /// returns the number of errors. Used by CI (--selftest rom outdir).</summary>
    public async Task<int> SelfTest(string rom, string outDir)
    {
        Directory.CreateDirectory(outDir);
        int errors = 0;
        Application.ThreadException += (_, _) => errors++;
        OpenRom(rom);
        while (_project == null || _busy) await Task.Delay(100);
        void ExpandAll(TreeNodeCollection nodes)
        {
            foreach (TreeNode n in nodes) { n.Expand(); ExpandAll(n.Nodes); }
        }
        ExpandAll(_tree.Nodes);
        var all = new List<TreeNode>();
        void Collect(TreeNodeCollection nodes) { foreach (TreeNode n in nodes) { all.Add(n); Collect(n.Nodes); } }
        Collect(_tree.Nodes);
        var shots = new HashSet<string>();
        foreach (var n in all)
        {
            if (n.Tag is not NodeData nd || nd.Kind == "group") continue;
            _tree.SelectedNode = n;
            Application.DoEvents();
            await Task.Delay(150);
            if (_tabPreview.Controls.Count > 0 && _tabPreview.Controls[0] is Panel pnl &&
                pnl.Controls.OfType<Label>().Any(l => l.Text.StartsWith("Could not decode")))
            { errors++; Console.Error.WriteLine("decode error on " + n.Text); }
            if (shots.Add(nd.Kind)) Snap(Path.Combine(outDir, $"{shots.Count:D2}_{nd.Kind}.png"));
        }
        _player.Stop();
        if (_project.Blobs.Count > 1) RipperOpen(1, 0x10, 32, 32, "RGBA16");
        await Task.Delay(150);
        Snap(Path.Combine(outDir, "90_ripper.png"));
        RawAudioOpen(0, 0);
        RawDecode(false);
        await Task.Delay(150);
        Snap(Path.Combine(outDir, "91_rawaudio.png"));
        _tabs.SelectedTab = _tabHex;
        await Task.Delay(150);
        Snap(Path.Combine(outDir, "92_hex.png"));
        return errors;
    }

    void Snap(string path)
    {
        Application.DoEvents();
        Activate();
        Refresh();
        using var bmp = new Bitmap(Width, Height);
        using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(Bounds.Location, Point.Empty, Bounds.Size);
        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    void ShowHelp()
    {
        var f = new Form { Text = "How it works", Size = new Size(760, 640), StartPosition = FormStartPosition.CenterParent, Icon = Icon };
        f.Controls.Add(new TextBox
        {
            Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10f),
            Text = Dialogs.HelpText.Replace("\n", "\r\n"),
        });
        f.Show(this);
    }
}
