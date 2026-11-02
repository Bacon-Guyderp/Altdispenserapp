using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace N64AssetStudio.Core.Export;

/// <summary>.unitypackage writer: a gzipped tar where each asset lives in a folder named after its GUID
/// containing "asset", "asset.meta" and "pathname".</summary>
public sealed class UnityPackage
{
    readonly string _root;
    public Dictionary<string, byte[]> Files { get; } = new();

    public UnityPackage(string root = "Assets/N64") { _root = root.TrimEnd('/'); }

    public static string Guid(string path) => Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes("n64studio:" + path))).ToLowerInvariant();

    public void AddBytes(string rel, byte[] data)
    {
        var p = (rel.Length == 0 ? _root : $"{_root}/{rel}").Replace('\\', '/');
        while (p.Contains("//")) p = p.Replace("//", "/");
        Files[p] = data;
    }

    public void AddFile(string rel, string src) => AddBytes(rel, File.ReadAllBytes(src));

    public void AddFolderTree(string relDir, string srcDir)
    {
        foreach (var f in Directory.EnumerateFiles(srcDir, "*", SearchOption.AllDirectories))
            AddFile((relDir.Length > 0 ? relDir + "/" : "") + Path.GetRelativePath(srcDir, f).Replace('\\', '/'), f);
    }

    static string Meta(string guid, bool folder, string ext)
    {
        var l = new List<string> { "fileFormatVersion: 2", $"guid: {guid}" };
        if (folder) l.AddRange(new[] { "folderAsset: yes", "DefaultImporter:", "  externalObjects: {}" });
        else if (ext is ".png" or ".tga" or ".bmp" or ".tif")
            l.AddRange(new[] { "TextureImporter:", "  mipmaps:", "    enableMipMap: 0", "  textureSettings:", "    filterMode: 0", "  alphaIsTransparency: 1" });
        else if (ext is ".fbx" or ".obj" or ".dae")
            l.AddRange(new[] { "ModelImporter:", "  materials:", "    materialImportMode: 1", "    materialLocation: 1" });
        else if (ext is ".wav" or ".ogg" or ".aiff" or ".mp3") l.Add("AudioImporter:");
        else l.AddRange(new[] { "DefaultImporter:", "  externalObjects: {}" });
        l.AddRange(new[] { "  userData: ", "  assetBundleName: ", "  assetBundleVariant: " });
        return string.Join("\n", l) + "\n";
    }

    public string Save(string path)
    {
        var folders = new SortedSet<string>();
        foreach (var p in Files.Keys)
        {
            var parts = p.Split('/');
            for (int i = 1; i < parts.Length; i++) folders.Add(string.Join("/", parts.Take(i)));
        }
        folders.Remove("Assets");
        using var fs = File.Create(path);
        using var gz = new GZipStream(fs, CompressionLevel.Optimal);
        using var tar = new TarWriter(gz, TarEntryFormat.Ustar, leaveOpen: false);
        void Add(string name, byte[] data)
        {
            var e = new UstarTarEntry(TarEntryType.RegularFile, name) { DataStream = new MemoryStream(data), ModificationTime = DateTimeOffset.UtcNow };
            tar.WriteEntry(e);
        }
        foreach (var f in folders)
        {
            var g = Guid(f);
            Add($"{g}/asset.meta", Encoding.UTF8.GetBytes(Meta(g, true, "")));
            Add($"{g}/pathname", Encoding.UTF8.GetBytes(f));
        }
        foreach (var (p, data) in Files.OrderBy(k => k.Key))
        {
            var g = Guid(p);
            Add($"{g}/asset", data);
            Add($"{g}/asset.meta", Encoding.UTF8.GetBytes(Meta(g, false, Path.GetExtension(p).ToLowerInvariant())));
            Add($"{g}/pathname", Encoding.UTF8.GetBytes(p));
        }
        return path;
    }
}
