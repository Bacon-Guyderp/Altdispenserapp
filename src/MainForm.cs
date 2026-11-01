using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Blend2Unity
{
    public sealed class MainForm : Form
    {
        const string AppName = "Blend2Unity";

        readonly TextBox _blenderBox = new TextBox();
        readonly ListBox _fileList = new ListBox();
        readonly TextBox _outputBox = new TextBox();
        readonly CheckBox _nextToSource = new CheckBox { Text = "Save next to each .blend file", AutoSize = true };
        readonly TextBox _unityFolderBox = new TextBox();
        readonly CheckBox _textures = Check("Include textures");
        readonly CheckBox _animation = Check("Include animation");
        readonly CheckBox _modifiers = Check("Apply modifiers");
        readonly CheckBox _transform = Check("Apply transform (fixes -90° rotation)");
        readonly CheckBox _includeBlend = Check("Also include original .blend");
        readonly Label _fileStatus = new Label { Text = "Idle", AutoSize = true };
        readonly Label _overallStatus = new Label { Text = "Add .blend files to begin (or drag them onto this window).", AutoSize = true };
        readonly ProgressBar _fileBar = new ProgressBar { Maximum = 1000, Height = 20, Anchor = AnchorStyles.Left | AnchorStyles.Right };
        readonly ProgressBar _overallBar = new ProgressBar { Maximum = 1000, Height = 20, Anchor = AnchorStyles.Left | AnchorStyles.Right };
        readonly TextBox _log = new TextBox
        {
            Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill,
            Font = new Font(FontFamily.GenericMonospace, 8.5f), BackColor = SystemColors.Window,
        };
        Button _addFiles, _addFolder, _remove, _clear, _browseOutput, _convert, _cancel, _openOutput;

        CancellationTokenSource _cts;
        Task _worker;
        string _lastOutputDir;
        bool _closeWhenDone;

        static CheckBox Check(string text) => new CheckBox { Text = text, AutoSize = true, Margin = new Padding(3, 3, 16, 3) };

        public MainForm(IEnumerable<string> initialFiles)
        {
            Text = AppName + " — .blend → .unitypackage";
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Dpi;
            StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }
            AllowDrop = true;
            DragEnter += (s, e) => e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) && !Busy
                ? DragDropEffects.Copy : DragDropEffects.None;
            DragDrop += (s, e) => AddPaths((string[])e.Data.GetData(DataFormats.FileDrop));

            BuildLayout();
            LoadSettings();
            AddPaths(initialFiles);
            UpdateButtons();
        }

        bool Busy => _worker != null && !_worker.IsCompleted;

        // ---------------------------------------------------------------- layout
        void BuildLayout()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(10) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(root);

            // Blender
            var blender = Group("Blender", 3);
            blender.Columns.ColumnStyles[0] = new ColumnStyle(SizeType.Percent, 100);
            _blenderBox.Dock = DockStyle.Fill;
            blender.Columns.Controls.Add(_blenderBox, 0, 0);
            blender.Columns.Controls.Add(Btn("Browse…", BrowseBlender), 1, 0);
            blender.Columns.Controls.Add(Btn("Detect", DetectBlender), 2, 0);
            root.Controls.Add(blender.Box);

            // Files
            var files = Group(".blend files", 2);
            files.Columns.ColumnStyles[0] = new ColumnStyle(SizeType.Percent, 100);
            _fileList.Dock = DockStyle.Fill;
            _fileList.SelectionMode = SelectionMode.MultiExtended;
            _fileList.IntegralHeight = false;
            _fileList.HorizontalScrollbar = true;
            _fileList.KeyDown += (s, e) => { if (e.KeyCode == Keys.Delete) RemoveSelected(); };
            files.Columns.Controls.Add(_fileList, 0, 0);
            var fileButtons = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Margin = Padding.Empty };
            fileButtons.Controls.Add(_addFiles = Btn("Add files…", BrowseFiles), 0, 0);
            fileButtons.Controls.Add(_addFolder = Btn("Add folder…", BrowseFolder), 1, 0);
            fileButtons.Controls.Add(_remove = Btn("Remove", RemoveSelected), 0, 1);
            fileButtons.Controls.Add(_clear = Btn("Clear", ClearFiles), 1, 1);
            foreach (Control b in fileButtons.Controls) b.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            files.Columns.Controls.Add(fileButtons, 1, 0);
            files.Columns.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(files.Box);

            // Output
            var output = Group("Output", 3);
            output.Columns.ColumnStyles[1] = new ColumnStyle(SizeType.Percent, 100);
            output.Columns.Controls.Add(Lbl("Save to:"), 0, 0);
            _outputBox.Dock = DockStyle.Fill;
            output.Columns.Controls.Add(_outputBox, 1, 0);
            output.Columns.Controls.Add(_browseOutput = Btn("Browse…", BrowseOutput), 2, 0);
            _nextToSource.CheckedChanged += (s, e) => UpdateButtons();
            output.Columns.Controls.Add(_nextToSource, 1, 1);
            output.Columns.Controls.Add(Lbl("Unity folder:"), 0, 2);
            _unityFolderBox.Dock = DockStyle.Fill;
            output.Columns.Controls.Add(_unityFolderBox, 1, 2);
            output.Columns.Controls.Add(new Label { Text = "{name} = file name", AutoSize = true, ForeColor = SystemColors.GrayText, Anchor = AnchorStyles.Left }, 2, 2);
            root.Controls.Add(output.Box);

            // Options
            var options = Group("Options", 1);
            options.Columns.ColumnStyles[0] = new ColumnStyle(SizeType.Percent, 100);
            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill };
            flow.Controls.AddRange(new Control[] { _textures, _animation, _modifiers, _transform, _includeBlend });
            options.Columns.Controls.Add(flow, 0, 0);
            root.Controls.Add(options.Box);

            // Progress
            var prog = Group("Progress", 1);
            prog.Columns.Controls.Add(_fileStatus, 0, 0);
            prog.Columns.Controls.Add(_fileBar, 0, 1);
            prog.Columns.Controls.Add(_overallStatus, 0, 2);
            prog.Columns.Controls.Add(_overallBar, 0, 3);
            prog.Columns.ColumnStyles[0] = new ColumnStyle(SizeType.Percent, 100);
            root.Controls.Add(prog.Box);

            // Log
            root.Controls.Add(_log);

            // Actions
            var actions = new TableLayoutPanel { ColumnCount = 4, Dock = DockStyle.Fill, Margin = Padding.Empty };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actions.Controls.Add(_openOutput = Btn("Open output folder", OpenOutput), 0, 0);
            actions.Controls.Add(_cancel = Btn("Cancel", Cancel), 2, 0);
            actions.Controls.Add(_convert = Btn("Convert", Start), 3, 0);
            _openOutput.AutoSize = true;
            _convert.Font = new Font(Font, FontStyle.Bold);
            AcceptButton = _convert;
            root.Controls.Add(actions);

            // Row heights scale with the font (and so with DPI); the file list and log stretch.
            float u = Font.Height;
            float[] rows = { 3.9f, -1, 7.6f, 5.4f, 7.4f, -1, 2.6f };
            foreach (var r in rows)
                root.RowStyles.Add(r < 0 ? new RowStyle(SizeType.Percent, 50) : new RowStyle(SizeType.Absolute, r * u));
            MinimumSize = new Size((int)(44 * u), (int)((rows.Where(r => r > 0).Sum() + 12) * u));
            Size = new Size((int)(50 * u), (int)((rows.Where(r => r > 0).Sum() + 18) * u));
        }

        sealed class GroupParts { public GroupBox Box; public TableLayoutPanel Columns; }

        static GroupParts Group(string title, int columns)
        {
            var box = new GroupBox { Text = title, Dock = DockStyle.Fill, Padding = new Padding(8, 4, 8, 6) };
            var table = new TableLayoutPanel { ColumnCount = columns, Dock = DockStyle.Fill };
            for (int i = 0; i < columns; i++) table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            box.Controls.Add(table);
            return new GroupParts { Box = box, Columns = table };
        }

        static Button Btn(string text, Action onClick)
        {
            var b = new Button { Text = text, AutoSize = true, MinimumSize = new Size(96, 0) };
            b.Click += (s, e) => onClick();
            return b;
        }

        static Label Lbl(string text) => new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left };

        // --------------------------------------------------------------- helpers
        List<string> Files => _fileList.Items.Cast<string>().ToList();

        void AddPaths(IEnumerable<string> paths)
        {
            if (paths == null) return;
            var existing = new HashSet<string>(Files, StringComparer.OrdinalIgnoreCase);
            foreach (var p in paths)
            {
                IEnumerable<string> found;
                try
                {
                    found = Directory.Exists(p)
                        ? Directory.GetFiles(p, "*.blend", SearchOption.AllDirectories).OrderBy(f => f)
                        : new[] { p };
                }
                catch (Exception ex) { Log($"Cannot read {p}: {ex.Message}"); continue; }
                foreach (var f in found)
                {
                    if (!f.EndsWith(".blend", StringComparison.OrdinalIgnoreCase) || !File.Exists(f)) continue;
                    var full = Path.GetFullPath(f);
                    if (existing.Add(full)) _fileList.Items.Add(full);
                }
            }
            UpdateButtons();
        }

        void Log(string message)
        {
            _log.AppendText(message.Replace("\n", Environment.NewLine) + Environment.NewLine);
        }

        void UpdateButtons()
        {
            var busy = Busy;
            foreach (var b in new Control[] { _addFiles, _addFolder, _remove, _clear, _blenderBox, _unityFolderBox,
                                              _nextToSource, _textures, _animation, _modifiers, _transform, _includeBlend })
                b.Enabled = !busy;
            _convert.Enabled = !busy && _fileList.Items.Count > 0;
            _cancel.Enabled = busy;
            _outputBox.Enabled = _browseOutput.Enabled = !busy && !_nextToSource.Checked;
            _openOutput.Enabled = _lastOutputDir != null;
        }

        static void SetBar(ProgressBar bar, double fraction)
        {
            var v = (int)Math.Round(Math.Max(0, Math.Min(1, fraction)) * bar.Maximum);
            // Setting a value then one lower skips the slow Windows fill animation.
            if (v < bar.Maximum) { bar.Value = v + 1; bar.Value = v; }
            else { bar.Maximum++; bar.Value = bar.Maximum; bar.Maximum--; bar.Value = bar.Maximum; }
        }

        // --------------------------------------------------------------- actions
        void BrowseBlender()
        {
            using (var d = new OpenFileDialog { Title = "Select blender.exe", Filter = "Blender (blender.exe)|blender.exe|Programs (*.exe)|*.exe|All files|*.*" })
                if (d.ShowDialog(this) == DialogResult.OK) _blenderBox.Text = d.FileName;
        }

        void DetectBlender()
        {
            var found = Converter.FindBlender();
            if (found != null) { _blenderBox.Text = found; Log("Found Blender: " + found); }
            else MessageBox.Show(this, "Blender could not be found automatically.\nInstall Blender from blender.org or use Browse… to locate blender.exe.",
                                 AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        void BrowseFiles()
        {
            using (var d = new OpenFileDialog { Title = "Select .blend files", Filter = "Blender files (*.blend)|*.blend|All files|*.*", Multiselect = true })
                if (d.ShowDialog(this) == DialogResult.OK) AddPaths(d.FileNames);
        }

        void BrowseFolder()
        {
            using (var d = new FolderBrowserDialog { Description = "Add every .blend file in this folder (including subfolders)" })
                if (d.ShowDialog(this) == DialogResult.OK) AddPaths(new[] { d.SelectedPath });
        }

        void BrowseOutput()
        {
            using (var d = new FolderBrowserDialog { Description = "Choose where to save .unitypackage files", SelectedPath = _outputBox.Text })
                if (d.ShowDialog(this) == DialogResult.OK) _outputBox.Text = d.SelectedPath;
        }

        void RemoveSelected()
        {
            if (Busy) return;
            foreach (var i in _fileList.SelectedIndices.Cast<int>().OrderByDescending(i => i).ToList())
                _fileList.Items.RemoveAt(i);
            UpdateButtons();
        }

        void ClearFiles()
        {
            _fileList.Items.Clear();
            UpdateButtons();
        }

        void OpenOutput()
        {
            if (_lastOutputDir == null) return;
            try { Process.Start(new ProcessStartInfo { FileName = _lastOutputDir, UseShellExecute = true }); }
            catch (Exception ex) { Log("Cannot open folder: " + ex.Message); }
        }

        ConvertOptions CurrentOptions() => new ConvertOptions
        {
            UnityFolder = string.IsNullOrWhiteSpace(_unityFolderBox.Text) ? "Assets/{name}" : _unityFolderBox.Text.Trim(),
            ApplyTransform = _transform.Checked,
            ApplyModifiers = _modifiers.Checked,
            IncludeAnimation = _animation.Checked,
            IncludeTextures = _textures.Checked,
            IncludeBlend = _includeBlend.Checked,
        };

        void Start()
        {
            if (Busy) return;
            var blender = _blenderBox.Text.Trim().Trim('"');
            if (blender.Length == 0 || !File.Exists(blender))
            {
                MessageBox.Show(this, "Please set the location of blender.exe (use Detect or Browse…).", AppName,
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            string outputDir = null;
            if (!_nextToSource.Checked)
            {
                outputDir = _outputBox.Text.Trim().Trim('"');
                if (outputDir.Length == 0)
                {
                    MessageBox.Show(this, "Please choose an output folder.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                try { Directory.CreateDirectory(outputDir); }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Cannot create output folder:\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            SaveSettings();
            var files = Files;
            var options = CurrentOptions();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            SetBar(_fileBar, 0);
            SetBar(_overallBar, 0);
            _worker = Task.Run(() => Work(files, outputDir, blender, options, token));
            UpdateButtons();
        }

        void Cancel()
        {
            if (!Busy) return;
            _cts.Cancel();
            _overallStatus.Text = "Cancelling…";
        }

        void Post(Action a)
        {
            try { if (!IsDisposed) BeginInvoke(a); } catch (InvalidOperationException) { }
        }

        void Work(List<string> files, string outputDir, string blender, ConvertOptions options, CancellationToken token)
        {
            int ok = 0, failed = 0, total = files.Count;
            var version = Converter.BlenderVersion(blender);
            Post(() => Log($"Using {version ?? "Blender"} ({blender})"));
            var lastUi = DateTime.MinValue;

            for (int i = 0; i < total && !token.IsCancellationRequested; i++)
            {
                var name = Path.GetFileName(files[i]);
                int index = i;
                Post(() => Log($"[{index + 1}/{total}] Converting {name}"));
                try
                {
                    var result = Converter.Convert(files[i], outputDir, blender, options,
                        (f, msg) =>
                        {
                            // Throttle UI updates during packaging of big files.
                            var now = DateTime.UtcNow;
                            if (f < 1 && f >= Converter.BlenderShare && (now - lastUi).TotalMilliseconds < 50) return;
                            lastUi = now;
                            Post(() =>
                            {
                                SetBar(_fileBar, f);
                                SetBar(_overallBar, (index + f) / total);
                                _fileStatus.Text = $"{name}: {msg}";
                                _overallStatus.Text = $"File {index + 1} of {total}";
                            });
                        },
                        msg => Post(() => Log(msg)), token);
                    ok++;
                    Post(() => _lastOutputDir = Path.GetDirectoryName(result));
                }
                catch (ConversionCancelledException) { Post(() => Log("Cancelled.")); break; }
                catch (Exception ex)
                {
                    failed++;
                    Post(() => Log($"FAILED {name}: {ex.Message}"));
                }
            }

            bool cancelled = token.IsCancellationRequested;
            Post(() => Finished(ok, failed, total, cancelled));
        }

        void Finished(int ok, int failed, int total, bool cancelled)
        {
            var summary = $"{ok} of {total} converted" + (failed > 0 ? $", {failed} failed" : "") + (cancelled ? " (cancelled)" : "");
            _overallStatus.Text = summary;
            _fileStatus.Text = cancelled ? "Cancelled" : "Finished";
            if (!cancelled) SetBar(_overallBar, 1);
            Log(summary);
            _worker = null;
            UpdateButtons();
            if (_closeWhenDone) { Close(); return; }
            if (failed > 0 && !cancelled)
                MessageBox.Show(this, summary + ".\nSee the log for details.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else if (!cancelled)
                System.Media.SystemSounds.Asterisk.Play();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (Busy)
            {
                e.Cancel = true;
                if (MessageBox.Show(this, "A conversion is running. Cancel it and quit?", AppName,
                                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    _closeWhenDone = true;
                    Cancel();
                }
                return;
            }
            SaveSettings();
            base.OnFormClosing(e);
        }

        // -------------------------------------------------------------- settings
        void LoadSettings()
        {
            var s = Settings.Load();
            _blenderBox.Text = s.Get("blender", "");
            if (!File.Exists(_blenderBox.Text)) _blenderBox.Text = Converter.FindBlender() ?? _blenderBox.Text;
            _outputBox.Text = s.Get("output", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "UnityPackages"));
            _nextToSource.Checked = s.GetBool("next_to_source", false);
            _unityFolderBox.Text = s.Get("unity_folder", "Assets/{name}");
            _textures.Checked = s.GetBool("include_textures", true);
            _animation.Checked = s.GetBool("include_animation", true);
            _modifiers.Checked = s.GetBool("apply_modifiers", true);
            _transform.Checked = s.GetBool("apply_transform", true);
            _includeBlend.Checked = s.GetBool("include_blend", false);
        }

        void SaveSettings()
        {
            var s = new Settings();
            s.Set("blender", _blenderBox.Text.Trim());
            s.Set("output", _outputBox.Text.Trim());
            s.Set("next_to_source", _nextToSource.Checked);
            s.Set("unity_folder", _unityFolderBox.Text.Trim());
            s.Set("include_textures", _textures.Checked);
            s.Set("include_animation", _animation.Checked);
            s.Set("apply_modifiers", _modifiers.Checked);
            s.Set("apply_transform", _transform.Checked);
            s.Set("include_blend", _includeBlend.Checked);
            s.Save();
        }
    }
}
