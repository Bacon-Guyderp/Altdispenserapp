namespace N64AssetStudio.Core.Export;

public sealed class BatchOptions
{
    public bool Models = true;
    public string ModelFormat = "glb";
    public double ModelScale = 1.0;
    public bool RootsOnly = true;
    public bool Textures = true;
    public string ImageFormat = "png";
    public bool Audio = true;
    public string AudioFormat = "wav";
    public bool SoundFonts = true;
    public bool Music = true;
    public bool RawFiles;
    public bool Code;
    public bool UnityPackage;
    public string UnityModelFormat = "fbx";
    public bool Blender;
}

/// <summary>Exports every asset of a project in one pass.</summary>
public static class BatchExport
{
    static string TexName(TextureAsset t) => $"b{t.BlobIndex}_{t.Offset:X6}_{t.Fmt}_{t.Width}x{t.Height}";

    public static List<string> ExportAll(Project p, string outDir, BatchOptions opt, Action<double, string>? progress = null,
        CancellationToken cancel = default)
    {
        var prog = progress ?? ((_, _) => { });
        string game = ModelExport.Safe(p.Rom.DisplayTitle);
        string root = Path.Combine(outDir, game);
        Directory.CreateDirectory(root);
        var report = new List<string>();
        int errors = 0;

        var models = p.Models.Where(m => !(opt.RootsOnly && m.Dl.IsChild)).ToList();
        var meshes = new List<Mesh>();
        if (opt.Models || opt.UnityPackage || opt.Blender)
            for (int i = 0; i < models.Count; i++)
            {
                cancel.ThrowIfCancellationRequested();
                if (i % 8 == 0) prog(0.4 * i / Math.Max(1, models.Count), $"Building models ({i}/{models.Count})");
                try { var m = p.GetMesh(models[i]); if (m.Triangles.Count > 0) meshes.Add(m); }
                catch (Exception) { errors++; }
            }

        if (opt.Models)
        {
            string d = Path.Combine(root, "Models");
            bool sub = opt.ModelFormat is "obj" or "gltf" or "dae" or "fbx";
            for (int i = 0; i < meshes.Count; i++)
            {
                cancel.ThrowIfCancellationRequested();
                if (i % 8 == 0) prog(0.4 + 0.15 * i / Math.Max(1, meshes.Count), $"Writing models ({i}/{meshes.Count})");
                var n = ModelExport.Safe(meshes[i].Name);
                try { ModelExport.Export(meshes[i], Path.Combine(sub ? Path.Combine(d, n) : d, $"{n}.{opt.ModelFormat}"), opt.ModelFormat, opt.ModelScale); }
                catch (Exception) { errors++; }
            }
            report.Add($"{meshes.Count} models -> {d}");
        }

        if (opt.Textures)
        {
            string d = Path.Combine(root, "Textures");
            int n = 0;
            for (int i = 0; i < p.Textures.Count; i++)
            {
                cancel.ThrowIfCancellationRequested();
                if (i % 32 == 0) prog(0.55 + 0.1 * i / Math.Max(1, p.Textures.Count), $"Textures ({i}/{p.Textures.Count})");
                try { ImageWriter.Save(p.TextureImage(p.Textures[i]), Path.Combine(d, TexName(p.Textures[i])), opt.ImageFormat); n++; }
                catch (Exception) { errors++; }
            }
            report.Add($"{n} textures -> {d}");
        }

        var sounds = p.AllSounds();
        if (opt.Audio)
        {
            string d = Path.Combine(root, "Audio");
            int n = 0;
            for (int i = 0; i < sounds.Count; i++)
            {
                cancel.ThrowIfCancellationRequested();
                if (i % 16 == 0) prog(0.65 + 0.15 * i / Math.Max(1, sounds.Count), $"Sounds ({i}/{sounds.Count})");
                try
                {
                    var (pcm, rate) = p.DecodeSound(sounds[i]);
                    SoundWriter.Save(pcm, rate, Path.Combine(d, $"bankfile{sounds[i].BankFile}", sounds[i].Name), opt.AudioFormat);
                    n++;
                }
                catch (Exception) { errors++; }
            }
            report.Add($"{n} sounds -> {d}");
        }
        if (opt.SoundFonts)
        {
            string d = Path.Combine(root, "Audio");
            Directory.CreateDirectory(d);
            for (int fi = 0; fi < p.BankFiles.Count; fi++)
            {
                try { SoundFont.FromBankFile(p, fi, Path.Combine(d, $"bankfile{fi}.sf2")); report.Add($"SoundFont bankfile{fi}.sf2"); }
                catch (Exception) { errors++; }
            }
        }

        if (opt.Music)
        {
            string d = Path.Combine(root, "Music");
            Directory.CreateDirectory(d);
            int n = 0;
            for (int i = 0; i < p.Sequences.Count; i++)
            {
                var s = p.Sequences[i];
                string name = $"seq{i:D3}_b{s.BlobIndex}_{s.Offset:X6}";
                try
                {
                    if (p.SequenceMidi(s) is byte[] midi) { File.WriteAllBytes(Path.Combine(d, name + ".mid"), midi); n++; }
                    if (s.Kind != "MIDI") File.WriteAllBytes(Path.Combine(d, name + ".seq"), p.SequenceRaw(s));
                }
                catch (Exception) { errors++; }
            }
            report.Add($"{n} MIDI sequences -> {d}");
        }

        if (opt.RawFiles)
        {
            string d = Path.Combine(root, "Files");
            Directory.CreateDirectory(d);
            foreach (var b in p.Blobs.Skip(1)) File.WriteAllBytes(Path.Combine(d, ModelExport.Safe(b.Name) + ".bin"), b.Data);
            report.Add($"{p.Blobs.Count - 1} decompressed files -> {d}");
        }

        if (opt.Code && p.Functions.Count > 0)
        {
            prog(0.82, "Writing pseudo-C");
            string path = Path.Combine(root, "code_pseudo.c");
            using var w = new StreamWriter(path);
            w.WriteLine($"// {p.Rom.DisplayTitle} - pseudo-C lifted from MIPS R4300i machine code\n");
            foreach (var fn in p.Functions.Take(5000))
                if (p.Rom.EntryToRom(fn) is int off) w.WriteLine(Mips.FunctionToPseudoC(p.Rom.Data, off, fn, 2000) + "\n");
            report.Add($"pseudo-C for {Math.Min(5000, p.Functions.Count)} functions -> {path}");
        }

        if (opt.UnityPackage)
        {
            prog(0.85, "Building Unity package");
            var pkg = new UnityPackage($"Assets/N64/{game}");
            string tmp = Path.Combine(Path.GetTempPath(), "n64studio_" + System.Guid.NewGuid().ToString("N"));
            try
            {
                foreach (var m in meshes)
                {
                    var n = ModelExport.Safe(m.Name);
                    ModelExport.Export(m, Path.Combine(tmp, "Models", n, $"{n}.{opt.UnityModelFormat}"), opt.UnityModelFormat, opt.ModelScale);
                }
                if (opt.Textures)
                    foreach (var t in p.Textures)
                        try { ImageWriter.Save(p.TextureImage(t), Path.Combine(tmp, "Textures", TexName(t) + ".png"), "png"); } catch (Exception) { }
                foreach (var s in sounds)
                    try { var (pcm, rate) = p.DecodeSound(s); SoundWriter.Save(pcm, rate, Path.Combine(tmp, "Audio", $"bankfile{s.BankFile}", s.Name + ".wav"), "wav"); }
                    catch (Exception) { }
                for (int i = 0; i < p.Sequences.Count; i++)
                {
                    byte[]? midi = null;
                    try { midi = p.SequenceMidi(p.Sequences[i]); } catch (Exception) { }
                    if (midi == null) continue;
                    Directory.CreateDirectory(Path.Combine(tmp, "Music"));
                    File.WriteAllBytes(Path.Combine(tmp, "Music", $"seq{i:D3}.mid.bytes"), midi);
                }
                if (Directory.Exists(tmp)) pkg.AddFolderTree("", tmp);
                report.Add($"Unity package -> {pkg.Save(Path.Combine(outDir, game + ".unitypackage"))}");
            }
            finally { try { Directory.Delete(tmp, true); } catch (IOException) { } }
        }

        if (opt.Blender && meshes.Count > 0)
        {
            prog(0.92, "Building Blender scene");
            report.Add(BlenderExport.Export(meshes, Path.Combine(root, "Blender"), game, opt.ModelScale).Message);
        }

        if (errors > 0) report.Add($"{errors} items could not be decoded and were skipped");
        prog(1.0, "Export finished");
        return report;
    }
}
