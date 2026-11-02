using N64AssetStudio.Core.Export;

namespace N64AssetStudio.Core;

/// <summary>
/// Command line:  info &lt;rom&gt;  |  export &lt;rom&gt; &lt;outdir&gt; [--models fbx] [--images png] [--audio wav]
///                [--scale 1] [--unity] [--blender] [--code] [--raw] [--fast]
/// </summary>
public static class Cli
{
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length == 2 && args[0] == "make-test-rom")
        {
            foreach (var order in new[] { ByteOrder.Z64, ByteOrder.V64, ByteOrder.N64 }) output.WriteLine(TestRom.Write(args[1], order));
            return 0;
        }
        if (args.Length < 2 || (args[0] != "info" && args[0] != "export") || (args[0] == "export" && args.Length < 3))
        {
            error.WriteLine("usage:\n  info <rom>\n  make-test-rom <outdir>\n  export <rom> <outdir> [--models glb|gltf|fbx|obj|dae|ply|stl|none] [--images png|bmp|tga|tif|dds|ico|qoi|ppm]\n" +
                            "         [--audio wav|aiff|flac|au|raw|ogg|mp3|none] [--scale N] [--unity] [--blender] [--code] [--raw] [--fast]");
            return 2;
        }
        string Opt(string name, string def)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : def;
        }
        bool Flag(string name) => args.Contains(name);
        void Progress(double f, string m) => error.Write($"\r[{f * 100,5:F1}%] {(m.Length > 70 ? m[..70] : m),-70}");

        var proj = Project.Open(args[1]);
        proj.Scan(Progress, new ScanOptions { DeepCompression = !Flag("--fast") });
        error.WriteLine();
        foreach (var (k, v) in proj.Rom.InfoLines()) output.WriteLine($"{k,20}: {v}");
        foreach (var l in proj.Log) output.WriteLine(" - " + l);
        if (args[0] == "export")
        {
            string models = Opt("--models", "glb"), audio = Opt("--audio", "wav");
            var opt = new BatchOptions
            {
                Models = models != "none", ModelFormat = models == "none" ? "glb" : models,
                ModelScale = double.Parse(Opt("--scale", "1"), System.Globalization.CultureInfo.InvariantCulture),
                ImageFormat = Opt("--images", "png"), Audio = audio != "none", AudioFormat = audio == "none" ? "wav" : audio,
                UnityPackage = Flag("--unity"), Blender = Flag("--blender"), Code = Flag("--code"), RawFiles = Flag("--raw"),
            };
            var report = BatchExport.ExportAll(proj, args[2], opt, Progress);
            error.WriteLine();
            foreach (var l in report) output.WriteLine(l);
        }
        return 0;
    }
}
