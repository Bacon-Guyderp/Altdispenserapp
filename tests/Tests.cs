using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Blend2Unity;

static class Tests
{
    static int failures;

    static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what);
        if (!ok) failures++;
    }

    static void Run(string name, Action test)
    {
        Console.WriteLine(name);
        try { test(); }
        catch (Exception ex) { Check(false, "threw " + ex); }
    }

    static string TempDir()
    {
        var d = Path.Combine(Path.GetTempPath(), "b2u_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        return d;
    }

    const string MakeScene = @"
import bpy, sys
out = sys.argv[sys.argv.index('--') + 1]
bpy.ops.mesh.primitive_monkey_add()
ob = bpy.context.active_object
mat = bpy.data.materials.new('Mat'); mat.use_nodes = True
img = bpy.data.images.new('Painted', 16, 16)
node = mat.node_tree.nodes.new('ShaderNodeTexImage'); node.image = img
ob.data.materials.append(mat)
ob.keyframe_insert('location', frame=1); ob.location.x = 2; ob.keyframe_insert('location', frame=10)
bpy.ops.wm.save_as_mainfile(filepath=out)
";

    static int Main()
    {
        Run("package round trip", () =>
        {
            var d = TempDir();
            File.WriteAllText(Path.Combine(d, "a.fbx"), "fbx");
            File.WriteAllText(Path.Combine(d, "b.png"), "png");
            var pkg = Path.Combine(d, "x.unitypackage");
            UnityPackage.Build(pkg, new[]
            {
                new KeyValuePair<string, string>(Path.Combine(d, "a.fbx"), "Assets/M/a.fbx"),
                new KeyValuePair<string, string>(Path.Combine(d, "b.png"), "M/Textures/b.png"),
            });
            var c = UnityPackage.Read(pkg);
            Check(c.Count == 4, "4 entries (2 folders + 2 files): " + string.Join(", ", c.Keys));
            Check(c.ContainsKey("Assets/M") && c["Assets/M"] == null, "folder Assets/M");
            Check(c.ContainsKey("Assets/M/Textures") && c["Assets/M/Textures"] == null, "folder Assets/M/Textures");
            Check(Encoding.UTF8.GetString(c["Assets/M/a.fbx"]) == "fbx", "a.fbx contents");
            Check(Encoding.UTF8.GetString(c["Assets/M/Textures/b.png"]) == "png", "b.png contents");
            Check(!File.Exists(pkg + ".part"), "no temp file left");
            // System tar must accept the archive too.
            var tar = Process.Start(new ProcessStartInfo("tar", $"tzf \"{pkg}\"") { RedirectStandardOutput = true });
            var listing = tar.StandardOutput.ReadToEnd(); tar.WaitForExit();
            Check(tar.ExitCode == 0 && listing.Contains(UnityPackage.GuidFor("Assets/M/a.fbx") + "/asset.meta"), "readable by tar");
            Directory.Delete(d, true);
        });

        Run("guids and paths", () =>
        {
            Check(UnityPackage.GuidFor("Assets/x.fbx") == UnityPackage.GuidFor("Assets/x.fbx"), "stable");
            Check(UnityPackage.GuidFor("Assets/x.fbx").Length == 32, "32 hex chars");
            bool threw = false;
            try { UnityPackage.NormalizePath("Assets/../x"); } catch (ArgumentException) { threw = true; }
            Check(threw, "rejects ..");
        });

        Run("argument quoting", () =>
        {
            Check(Converter.QuoteArg("plain") == "plain", "plain");
            Check(Converter.QuoteArg("a b") == "\"a b\"", "space");
            Check(Converter.QuoteArg(@"C:\My Files\") == "\"C:\\My Files\\\\\"", "trailing backslash");
            Check(Converter.QuoteArg("say \"hi\"") == "\"say \\\"hi\\\"\"", "quotes");
            Check(Converter.QuoteArg("") == "\"\"", "empty");
        });

        var blender = Environment.GetEnvironmentVariable("BLENDER_PATH");
        if (string.IsNullOrEmpty(blender) || !File.Exists(blender))
        {
            Console.WriteLine("(set BLENDER_PATH to run the Blender tests)");
        }
        else
        {
            var d = TempDir();
            var blend = Path.Combine(d, "Test \"Model\" v2.blend".Replace("\"", "'"));
            var script = Path.Combine(d, "make.py");
            File.WriteAllText(script, MakeScene);
            var mk = Process.Start(new ProcessStartInfo(blender,
                $"-b --factory-startup --python {Converter.QuoteArg(script)} -- {Converter.QuoteArg(blend)}")
                { RedirectStandardOutput = true, RedirectStandardError = true });
            mk.StandardError.ReadToEndAsync(); mk.StandardOutput.ReadToEnd(); mk.WaitForExit();

            Run("convert .blend", () =>
            {
                var seen = new List<double>();
                var log = new List<string>();
                var outPkg = Converter.Convert(blend, Path.Combine(d, "out dir"), blender, new ConvertOptions(),
                    (f, m) => seen.Add(f), log.Add, CancellationToken.None);
                var c = UnityPackage.Read(outPkg);
                var name = Path.GetFileNameWithoutExtension(blend);
                Check(c.ContainsKey($"Assets/{name}/{name}.fbx"), "contains FBX: " + string.Join(", ", c.Keys));
                Check(c.ContainsKey($"Assets/{name}/Textures/Painted.png"), "contains texture");
                Check(seen.SequenceEqual(seen.OrderBy(x => x)), "progress never goes backwards");
                Check(seen.Last() == 1.0, "progress ends at 100%");
                Check(log.Any(l => l.StartsWith("Wrote ")), "log reports output");
            });

            Run("include .blend + custom folder", () =>
            {
                var outPkg = Converter.Convert(blend, Path.Combine(d, "out2"), blender,
                    new ConvertOptions { IncludeBlend = true, UnityFolder = "Assets/Models/{name}", IncludeTextures = false },
                    null, null, CancellationToken.None);
                var c = UnityPackage.Read(outPkg);
                var name = Path.GetFileNameWithoutExtension(blend);
                Check(c.ContainsKey($"Assets/Models/{name}/{name}.blend"), "contains .blend");
                Check(c.ContainsKey("Assets/Models") && !c.Keys.Any(k => k.Contains("Textures")), "folder layout, no textures");
            });

            Run("bad file", () =>
            {
                var bad = Path.Combine(d, "bad.blend");
                File.WriteAllText(bad, "not a blend file");
                try
                {
                    Converter.Convert(bad, d, blender, null, null, null, CancellationToken.None);
                    Check(false, "should have thrown");
                }
                catch (ConversionException ex) { Check(!(ex is ConversionCancelledException), "ConversionException: " + ex.Message); }
            });

            Run("cancel", () =>
            {
                var cts = new CancellationTokenSource(200);
                var sw = Stopwatch.StartNew();
                try
                {
                    Converter.Convert(blend, Path.Combine(d, "cancelled"), blender, null, null, null, cts.Token);
                    Check(false, "should have been cancelled");
                }
                catch (ConversionCancelledException) { Check(sw.ElapsedMilliseconds < 5000, "cancelled quickly"); }
                Check(!Directory.Exists(Path.Combine(d, "cancelled")) ||
                      !Directory.GetFiles(Path.Combine(d, "cancelled")).Any(), "no output written");
            });

            Directory.Delete(d, true);
        }

        Console.WriteLine(failures == 0 ? "ALL PASSED" : $"{failures} FAILURE(S)");
        return failures == 0 ? 0 : 1;
    }
}
