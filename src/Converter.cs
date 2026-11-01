using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Blend2Unity
{
    public sealed class ConvertOptions
    {
        public string UnityFolder = "Assets/{name}"; // {name} = .blend file name without extension
        public bool ApplyTransform = true;           // bake axis conversion (avoids -90° X rotation)
        public bool ApplyModifiers = true;
        public bool IncludeAnimation = true;
        public bool IncludeTextures = true;
        public bool IncludeBlend = false;            // also ship the original .blend
    }

    public class ConversionException : Exception
    {
        public ConversionException(string message) : base(message) { }
    }

    public sealed class ConversionCancelledException : ConversionException
    {
        public ConversionCancelledException() : base("Cancelled") { }
    }

    /// <summary>
    /// .blend -> .unitypackage:
    ///  1. Blender runs in the background with the embedded BlenderExport.py,
    ///     which exports a Unity-ready FBX and copies the textures it uses.
    ///  2. The results are packed into a .unitypackage.
    /// </summary>
    public static class Converter
    {
        const string Tag = "@@B2U";
        /// <summary>Share of a file's progress spent in Blender; the rest is packaging.</summary>
        public const double BlenderShare = 0.85;

        static readonly bool IsWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;

        // ------------------------------------------------------------ Blender
        public static string FindBlender()
        {
            var candidates = new List<string>();
            var env = Environment.GetEnvironmentVariable("BLENDER_PATH");
            if (!string.IsNullOrEmpty(env)) candidates.Add(env);

            var exe = IsWindows ? "blender.exe" : "blender";
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                try { if (dir.Length > 0) candidates.Add(Path.Combine(dir.Trim('"'), exe)); }
                catch (ArgumentException) { }
            }

            if (IsWindows)
            {
                var bases = new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetEnvironmentVariable("ProgramW6432"),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                };
                foreach (var b in bases.Where(b => !string.IsNullOrEmpty(b)).Distinct())
                {
                    var foundation = Path.Combine(b, "Blender Foundation");
                    if (Directory.Exists(foundation))
                        candidates.AddRange(Directory.GetDirectories(foundation, "Blender*")
                            .OrderByDescending(VersionKey, new VersionComparer())
                            .Select(d => Path.Combine(d, "blender.exe")));
                    candidates.Add(Path.Combine(b, "Steam", "steamapps", "common", "Blender", "blender.exe"));
                }
            }
            else
            {
                candidates.Add("/Applications/Blender.app/Contents/MacOS/Blender");
                candidates.AddRange(new[] { "/snap/bin/blender", "/usr/bin/blender", "/usr/local/bin/blender" });
            }
            return candidates.FirstOrDefault(File.Exists);
        }

        static int[] VersionKey(string s) =>
            Regex.Matches(s, @"\d+").Cast<Match>().Select(m => int.TryParse(m.Value, out var n) ? n : 0).ToArray();

        sealed class VersionComparer : IComparer<int[]>
        {
            public int Compare(int[] a, int[] b)
            {
                for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
                    if (a[i] != b[i]) return a[i].CompareTo(b[i]);
                return a.Length.CompareTo(b.Length);
            }
        }

        public static string BlenderVersion(string blender)
        {
            try
            {
                using (var p = Process.Start(StartInfo(blender, new[] { "--version" })))
                {
                    p.StandardError.ReadToEndAsync();
                    var output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(60000);
                    return output.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("Blender "));
                }
            }
            catch (Exception) { return null; }
        }

        static ProcessStartInfo StartInfo(string exe, IEnumerable<string> args) => new ProcessStartInfo
        {
            FileName = exe,
            Arguments = string.Join(" ", args.Select(QuoteArg)),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        /// <summary>Quotes one argument following the Windows command-line rules.</summary>
        public static string QuoteArg(string arg)
        {
            if (arg.Length > 0 && arg.IndexOfAny(new[] { ' ', '\t', '\n', '"' }) < 0) return arg;
            var sb = new StringBuilder("\"");
            int backslashes = 0;
            foreach (var c in arg)
            {
                if (c == '\\') { backslashes++; continue; }
                if (c == '"') sb.Append('\\', backslashes * 2 + 1);
                else sb.Append('\\', backslashes);
                backslashes = 0;
                sb.Append(c);
            }
            sb.Append('\\', backslashes * 2);
            return sb.Append('"').ToString();
        }

        static string ExportScript()
        {
            var asm = Assembly.GetExecutingAssembly();
            var name = asm.GetManifestResourceNames().First(n => n.EndsWith("BlenderExport.py"));
            using (var s = asm.GetManifestResourceStream(name))
            using (var r = new StreamReader(s, Encoding.UTF8))
                return r.ReadToEnd();
        }

        // --------------------------------------------------------- Conversion
        /// <summary>Converts one .blend file and returns the path of the .unitypackage.</summary>
        /// <param name="outputDir">null writes next to the .blend file.</param>
        public static string Convert(string blend, string outputDir, string blender, ConvertOptions options,
                                     Action<double, string> progress, Action<string> log,
                                     CancellationToken cancel)
        {
            options = options ?? new ConvertOptions();
            progress = progress ?? ((f, m) => { });
            log = log ?? (m => { });
            blend = Path.GetFullPath(blend);
            if (!File.Exists(blend)) throw new ConversionException("File not found: " + blend);
            if (string.IsNullOrEmpty(blender) || !File.Exists(blender))
                throw new ConversionException("Blender executable not found. Set its location first.");

            var name = Path.GetFileNameWithoutExtension(blend);
            outputDir = string.IsNullOrEmpty(outputDir) ? Path.GetDirectoryName(blend) : outputDir;
            var outPath = Path.Combine(outputDir, name + ".unitypackage");
            var unityFolder = options.UnityFolder.Replace("{name}", SafeUnityName(name)).Trim('/', '\\', ' ');
            if (unityFolder.Length == 0) unityFolder = "Assets";

            var work = Path.Combine(Path.GetTempPath(), "blend2unity_" + Guid.NewGuid().ToString("N"));
            var exportDir = Path.Combine(work, "export");
            Directory.CreateDirectory(exportDir);
            try
            {
                var script = Path.Combine(work, "blender_export.py");
                File.WriteAllText(script, ExportScript(), new UTF8Encoding(false));

                RunBlender(blender, blend, script, exportDir, name, options, progress, log, cancel);
                cancel.ThrowIfCancellationRequestedAsConversion();
                progress(BlenderShare, "Building .unitypackage");

                var assets = Directory.GetFiles(exportDir, "*", SearchOption.AllDirectories)
                    .OrderBy(f => f, StringComparer.Ordinal)
                    .Select(f => new KeyValuePair<string, string>(
                        f, unityFolder + "/" + f.Substring(exportDir.Length + 1).Replace('\\', '/')))
                    .ToList();
                if (!assets.Any(a => a.Value.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)))
                    throw new ConversionException("Blender did not produce an FBX file");
                if (options.IncludeBlend)
                    assets.Add(new KeyValuePair<string, string>(blend, unityFolder + "/" + Path.GetFileName(blend)));

                Directory.CreateDirectory(outputDir);
                UnityPackage.Build(outPath, assets,
                    f => progress(BlenderShare + (1 - BlenderShare) * f, "Building .unitypackage"));
                progress(1.0, "Done");
                log($"Wrote {outPath} ({assets.Count} asset(s))");
                return outPath;
            }
            catch (IOException ex) { throw new ConversionException(ex.Message); }
            catch (UnauthorizedAccessException ex) { throw new ConversionException(ex.Message); }
            finally
            {
                try { Directory.Delete(work, true); } catch (Exception) { }
            }
        }

        static void ThrowIfCancellationRequestedAsConversion(this CancellationToken ct)
        {
            if (ct.IsCancellationRequested) throw new ConversionCancelledException();
        }

        static string SafeUnityName(string name)
        {
            var s = Regex.Replace(name, "[<>:\"\\\\|?*\\x00-\\x1f]", "_").Trim();
            return s.Length > 0 ? s : "Model";
        }

        static void RunBlender(string blender, string blend, string script, string exportDir, string name,
                               ConvertOptions o, Action<double, string> progress, Action<string> log,
                               CancellationToken cancel)
        {
            var args = new List<string>
            {
                "-b", "--factory-startup", blend, "--python-exit-code", "1", "--python", script,
                "--", "--out", exportDir, "--name", name,
            };
            if (o.ApplyTransform) args.Add("--apply-transform");
            if (o.ApplyModifiers) args.Add("--modifiers");
            if (o.IncludeAnimation) args.Add("--animation");
            if (o.IncludeTextures) args.Add("--textures");

            var errors = new List<string>();
            string lastLine = null;
            var gate = new object();

            void OnLine(string line)
            {
                if (string.IsNullOrWhiteSpace(line)) return;
                lock (gate)
                {
                    if (!line.StartsWith(Tag + " "))
                    {
                        // e.g. 'Error: File format is not supported in file "x.blend"'
                        if (line.StartsWith("Error", StringComparison.OrdinalIgnoreCase)) errors.Add(line.Trim());
                        lastLine = line;
                        log("  " + line);
                        return;
                    }
                    var parts = line.Split(new[] { ' ' }, 3);
                    var kind = parts.Length > 1 ? parts[1] : "";
                    var text = parts.Length > 2 ? parts[2] : "";
                    switch (kind)
                    {
                        case "PROGRESS":
                            var sp = text.IndexOf(' ');
                            var num = sp < 0 ? text : text.Substring(0, sp);
                            if (double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                                progress(f * BlenderShare, sp < 0 ? "" : text.Substring(sp + 1));
                            break;
                        case "ERROR": errors.Add(text); log("ERROR: " + text); break;
                        case "WARN": log("Warning: " + text); break;
                        case "INFO": log(text); break;
                        case "DONE": log("Exported " + Path.GetFileName(text)); break;
                    }
                }
            }

            progress(0.02, "Starting Blender");
            Process proc;
            try
            {
                proc = Process.Start(StartInfo(blender, args));
            }
            catch (Exception ex)
            {
                throw new ConversionException($"Could not start Blender ({blender}): {ex.Message}");
            }

            using (proc)
            {
                proc.OutputDataReceived += (s, e) => { if (e.Data != null) OnLine(e.Data); };
                proc.ErrorDataReceived += (s, e) => { if (e.Data != null) OnLine(e.Data); };
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                proc.StandardInput.Close();

                while (!proc.WaitForExit(100))
                {
                    if (cancel.IsCancellationRequested)
                    {
                        try { proc.Kill(); } catch (InvalidOperationException) { }
                        proc.WaitForExit();
                        throw new ConversionCancelledException();
                    }
                }
                proc.WaitForExit(); // flush remaining output events

                if (proc.ExitCode != 0)
                {
                    string detail;
                    lock (gate) detail = errors.LastOrDefault() ?? lastLine ?? "no output";
                    throw new ConversionException($"Blender failed (exit code {proc.ExitCode}): {detail}");
                }
            }
        }
    }
}
