using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Blend2Unity
{
    /// <summary>
    /// Writes Unity's .unitypackage format: a gzip-compressed tar archive where
    /// every asset (file or folder) lives in a directory named after its GUID:
    ///   &lt;guid&gt;/pathname    project path, e.g. "Assets/Foo/bar.fbx"
    ///   &lt;guid&gt;/asset       file contents (missing for folders)
    ///   &lt;guid&gt;/asset.meta  Unity .meta file holding the same GUID
    /// GUIDs are derived from the project path, so importing a newer package for
    /// the same model updates the existing assets instead of duplicating them.
    /// </summary>
    public static class UnityPackage
    {
        const string FolderMeta =
            "fileFormatVersion: 2\nguid: {0}\nfolderAsset: yes\nDefaultImporter:\n" +
            "  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n";

        // Unity fills in default importer settings for anything not listed.
        const string FileMeta = "fileFormatVersion: 2\nguid: {0}\n";

        public static string GuidFor(string unityPath)
        {
            using (var md5 = MD5.Create())
            {
                var hash = md5.ComputeHash(Encoding.UTF8.GetBytes("blend2unity:" + unityPath));
                var sb = new StringBuilder(32);
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        public static string NormalizePath(string unityPath)
        {
            var parts = unityPath.Replace('\\', '/').Split('/')
                .Where(p => p.Length > 0 && p != ".").ToList();
            if (parts.Contains(".."))
                throw new ArgumentException("Unity path may not contain '..': " + unityPath);
            if (parts.Count == 0 || parts[0] != "Assets")
                parts.Insert(0, "Assets");
            return string.Join("/", parts);
        }

        /// <param name="assets">(source file on disk, Unity project path) pairs.</param>
        /// <param name="progress">Called with 0..1 as file data is written.</param>
        public static void Build(string output, IEnumerable<KeyValuePair<string, string>> assets,
                                 Action<double> progress = null)
        {
            var files = assets.Select(a => new KeyValuePair<string, string>(a.Key, NormalizePath(a.Value))).ToList();
            if (files.Count == 0) throw new ArgumentException("No assets to package");

            var folders = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var f in files)
            {
                var parts = f.Value.Split('/');
                for (int i = parts.Length - 1; i > 1; i--)
                    folders.Add(string.Join("/", parts, 0, i));
            }

            long total = Math.Max(1, files.Sum(f => new FileInfo(f.Key).Length));
            long written = 0;
            var mtime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var dir = Path.GetDirectoryName(Path.GetFullPath(output));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var tmp = output + ".part";

            try
            {
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write))
                using (var gz = new GZipStream(fs, CompressionLevel.Optimal))
                {
                    var tar = new TarWriter(gz, mtime);
                    foreach (var folder in folders)
                    {
                        var guid = GuidFor(folder);
                        tar.AddDirectory(guid);
                        tar.AddText(guid + "/pathname", folder);
                        tar.AddText(guid + "/asset.meta", string.Format(FolderMeta, guid));
                    }
                    foreach (var f in files)
                    {
                        var guid = GuidFor(f.Value);
                        tar.AddDirectory(guid);
                        tar.AddText(guid + "/pathname", f.Value);
                        tar.AddText(guid + "/asset.meta", string.Format(FileMeta, guid));
                        using (var src = File.OpenRead(f.Key))
                        {
                            long before = written;
                            tar.AddFile(guid + "/asset", src, n =>
                            {
                                written = before + n;
                                progress?.Invoke((double)written / total);
                            });
                        }
                    }
                    tar.Finish();
                }
                if (File.Exists(output)) File.Delete(output);
                File.Move(tmp, output);
            }
            finally
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }
        }

        /// <summary>Returns {unity path: bytes, or null for folders}. Used by tests.</summary>
        public static Dictionary<string, byte[]> Read(string path)
        {
            var entries = new Dictionary<string, byte[]>();
            using (var fs = File.OpenRead(path))
            using (var gz = new GZipStream(fs, CompressionMode.Decompress))
            {
                var header = new byte[512];
                while (ReadFull(gz, header) == 512 && header[0] != 0)
                {
                    var name = Encoding.UTF8.GetString(header, 0, 100).TrimEnd('\0');
                    var size = Convert.ToInt64(Encoding.ASCII.GetString(header, 124, 11).Trim('\0', ' '), 8);
                    var data = new byte[size];
                    ReadFull(gz, data);
                    var pad = (512 - size % 512) % 512;
                    ReadFull(gz, new byte[pad]);
                    if (header[156] != (byte)'5') entries[name] = data;
                }
            }
            var result = new Dictionary<string, byte[]>();
            foreach (var kv in entries.Where(e => e.Key.EndsWith("/pathname")))
            {
                var guid = kv.Key.Substring(0, kv.Key.Length - "/pathname".Length);
                var pathname = Encoding.UTF8.GetString(kv.Value).Split('\n')[0];
                entries.TryGetValue(guid + "/asset", out var asset);
                result[pathname] = asset;
            }
            return result;
        }

        static int ReadFull(Stream s, byte[] buf)
        {
            int n = 0, r;
            while (n < buf.Length && (r = s.Read(buf, n, buf.Length - n)) > 0) n += r;
            return n;
        }

        /// <summary>Minimal ustar writer (all names here are short).</summary>
        sealed class TarWriter
        {
            readonly Stream _out;
            readonly long _mtime;

            public TarWriter(Stream output, long mtime) { _out = output; _mtime = mtime; }

            public void AddDirectory(string name) => WriteHeader(name + "/", 0, '5', "755");

            public void AddText(string name, string text)
            {
                var data = Encoding.UTF8.GetBytes(text);
                WriteHeader(name, data.Length, '0', "644");
                _out.Write(data, 0, data.Length);
                Pad(data.Length);
            }

            public void AddFile(string name, Stream src, Action<long> progress)
            {
                long size = src.Length;
                WriteHeader(name, size, '0', "644");
                var buf = new byte[1 << 16];
                long done = 0;
                int r;
                while (done < size && (r = src.Read(buf, 0, (int)Math.Min(buf.Length, size - done))) > 0)
                {
                    _out.Write(buf, 0, r);
                    done += r;
                    progress(done);
                }
                if (done != size) throw new IOException("File changed while packaging: " + name);
                Pad(size);
            }

            public void Finish() => _out.Write(new byte[1024], 0, 1024);

            void Pad(long size)
            {
                var pad = (int)((512 - size % 512) % 512);
                if (pad > 0) _out.Write(new byte[pad], 0, pad);
            }

            void WriteHeader(string name, long size, char type, string mode)
            {
                var nameBytes = Encoding.UTF8.GetBytes(name);
                if (nameBytes.Length > 100) throw new ArgumentException("tar name too long: " + name);
                var h = new byte[512];
                Array.Copy(nameBytes, h, nameBytes.Length);
                Octal(h, 100, 8, Convert.ToInt64(mode, 8));
                Octal(h, 108, 8, 0);
                Octal(h, 116, 8, 0);
                Octal(h, 124, 12, size);
                Octal(h, 136, 12, _mtime);
                for (int i = 148; i < 156; i++) h[i] = (byte)' ';
                h[156] = (byte)type;
                Encoding.ASCII.GetBytes("ustar\0").CopyTo(h, 257);
                Encoding.ASCII.GetBytes("00").CopyTo(h, 263);
                long sum = 0;
                foreach (var b in h) sum += b;
                var chk = Convert.ToString(sum, 8).PadLeft(6, '0');
                Encoding.ASCII.GetBytes(chk).CopyTo(h, 148);
                h[154] = 0;
                h[155] = (byte)' ';
                _out.Write(h, 0, 512);
            }

            static void Octal(byte[] h, int offset, int len, long value)
            {
                var s = Convert.ToString(value, 8).PadLeft(len - 1, '0');
                Encoding.ASCII.GetBytes(s).CopyTo(h, offset);
                h[offset + len - 1] = 0;
            }
        }
    }
}
