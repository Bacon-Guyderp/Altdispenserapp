using System.Formats.Tar;
using System.IO.Compression;
using System.Text.Json.Nodes;
using N64AssetStudio.Core;
using N64AssetStudio.Core.Export;
using N64AssetStudio.Core.Render;
using Xunit;

namespace N64AssetStudio.Tests;

public sealed class ProjectFixture : IDisposable
{
    public readonly string Dir = Path.Combine(Path.GetTempPath(), "n64studio_tests_" + Guid.NewGuid().ToString("N"));
    public readonly Project Project;
    public readonly TestRom.Layout Info;

    public ProjectFixture()
    {
        Info = TestRom.Build().info;
        Project = Project.Open(TestRom.Write(Dir));
        Project.Scan();
    }

    public void Dispose() { try { Directory.Delete(Dir, true); } catch (IOException) { } }
}

public class CoreTests : IClassFixture<ProjectFixture>
{
    readonly ProjectFixture _fx;
    Project P => _fx.Project;
    public CoreTests(ProjectFixture fx) { _fx = fx; }

    string TempDir()
    {
        var d = Path.Combine(_fx.Dir, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        return d;
    }

    [Fact]
    public void ByteOrdersAreNormalised()
    {
        var big = TestRom.Build().rom;
        foreach (var order in new[] { ByteOrder.Z64, ByteOrder.V64, ByteOrder.N64 })
        {
            var r = Rom.Load(TestRom.Write(TempDir(), order));
            Assert.Equal(order, r.OriginalOrder);
            Assert.Equal(big, r.Data);
            Assert.Equal("N64STUDIO TEST", r.Header.Title);
            Assert.Equal("NTSE", r.Header.GameCode);
            Assert.Equal(0x80000400u, r.Header.EntryPoint);
        }
    }

    [Fact]
    public void CompressionRoundTrips()
    {
        var payload = System.Text.Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("hello N64 world ", 50)))
            .Concat(Enumerable.Range(0, 1024).Select(i => (byte)i)).Concat(new byte[700]).ToArray();
        foreach (var (enc, dec) in new (Func<byte[], byte[]>, Func<byte[], int, (byte[], int)>)[]
                 { (TestRom.Yaz0, Compression.Yaz0), (TestRom.Yay0, Compression.Yay0), (TestRom.Mio0, Compression.Mio0) })
        {
            var c = enc(payload);
            var (o, used) = dec(c, 0);
            Assert.Equal(payload, o);
            Assert.True(used <= c.Length);
            Assert.True(c.Length < payload.Length);
        }
    }

    [Fact]
    public void RareGzipAndZlibAreFound()
    {
        var payload = Enumerable.Range(0, 2000).Select(i => (byte)(i % 200)).ToArray();
        var ms = new MemoryStream();
        using (var d = new DeflateStream(ms, CompressionLevel.Optimal, true)) d.Write(payload);
        var body = ms.ToArray();
        var rare = new byte[] { 0x11, 0x72, (byte)(payload.Length >> 16), (byte)(payload.Length >> 8), (byte)payload.Length }.Concat(body).ToArray();
        var (o, used) = Compression.Rare(new byte[2].Concat(rare).ToArray(), 2);
        Assert.Equal(payload, o);
        Assert.InRange(used, rare.Length, rare.Length + 64);

        var gz = new MemoryStream();
        using (var g = new GZipStream(gz, CompressionLevel.Optimal, true)) g.Write(payload);
        var zl = new MemoryStream();
        using (var z = new ZLibStream(zl, CompressionLevel.Optimal, true)) z.Write(payload);
        var data = Enumerable.Repeat((byte)0xAA, 16).Concat(gz.ToArray()).Concat(Enumerable.Repeat((byte)0xBB, 16))
            .Concat(zl.ToArray()).Concat(Enumerable.Repeat((byte)0xCC, 16)).ToArray();
        var found = Compression.Scan(data);
        var gzBlock = Assert.Single(found, b => b.Kind == "gzip");
        Assert.Equal(payload, gzBlock.Data);
        Assert.Equal((int)gz.Length, gzBlock.CompressedSize);
        var zlBlock = Assert.Single(found, b => b.Kind == "zlib");
        Assert.Equal(payload, zlBlock.Data);
        Assert.Equal((int)zl.Length, zlBlock.CompressedSize);
    }

    /// <summary>Straight port of the order-2 VADPCM decoder used by the N64 decomp tools.</summary>
    static List<int> ReferenceVadpcm(byte[] data, short[] coefs)
    {
        var o = new List<int>();
        var last = new int[8];
        for (int f = 0; f < data.Length / 9; f++)
        {
            int hdr = data[f * 9], scale = hdr >> 4, pred = hdr & 0xF;
            var p1 = coefs.Skip(pred * 16).Take(8).ToArray();
            var p2 = coefs.Skip(pred * 16 + 8).Take(8).ToArray();
            for (int half = 0; half < 2; half++)
            {
                var tmp = new List<int>();
                for (int k = 0; k < 4; k++)
                {
                    int b = data[f * 9 + 1 + half * 4 + k];
                    foreach (var nib in new[] { b >> 4, b & 0xF }) tmp.Add((nib >= 8 ? nib - 16 : nib) << scale);
                }
                var cur = new int[8];
                for (int i = 0; i < 8; i++)
                {
                    int total = p1[i] * last[6] + p2[i] * last[7];
                    for (int x = i - 1; x > -1; x--) total += tmp[(i - 1) - x] * p2[x];
                    cur[i] = Math.Clamp(((tmp[i] << 11) + total) >> 11, -32768, 32767);
                }
                last = cur;
                o.AddRange(cur);
            }
        }
        return o;
    }

    [Fact]
    public void VadpcmMatchesReferenceDecoder()
    {
        var data = TestRom.AdpcmFrames(30);
        var ours = Audio.DecodeVadpcm(data, 0, data.Length, new AdpcmBook { Order = 2, NPredictors = 2, Coefs = TestRom.Book });
        Assert.Equal(ReferenceVadpcm(data, TestRom.Book), ours.Select(s => (int)s).ToList());
    }

    [Fact]
    public void TextureFormatsDecode()
    {
        var rgba16 = new byte[] { 0xF8, 0x01, 0xF8, 0x01, 0xF8, 0x01, 0xF8, 0x01 };
        Assert.Equal(((byte)255, (byte)0, (byte)0, (byte)255), Texture.Decode(rgba16, 0, 2, 2, "RGBA16").Get(0, 0));
        Assert.Equal(((byte)255, (byte)255, (byte)255, (byte)0), Texture.Decode(new byte[] { 0xF0, 0xF0, 0xF0, 0xF0 }, 0, 2, 2, "IA8").Get(1, 1));
        var i4 = Texture.Decode(new byte[] { 0xF0, 0x0F }, 0, 2, 2, "I4");
        Assert.Equal(255, i4.Get(0, 0).r);
        Assert.Equal(0, i4.Get(1, 0).r);
    }

    [Fact]
    public void ScanFindsEverything()
    {
        Assert.Equal(new[] { "MIO0", "Yay0", "Yaz0", "Yaz0" }, P.Blobs.Skip(1).Select(b => b.Kind).OrderBy(k => k, StringComparer.Ordinal));
        int skelBlob = P.Blobs.Single(b => b.RomOffset == _fx.Info.SkelOff).Index;
        var models = P.Models.Where(m => m.BlobIndex != skelBlob).ToList();
        Assert.Equal(3, P.Models.Count(m => m.BlobIndex == skelBlob)); // the three limb display lists
        Assert.Equal(new[] { "F3D", "F3DEX", "F3DEX2", "F3DEX2" }, models.Select(m => m.Dl.Ucode.ToString()).OrderBy(x => x, StringComparer.Ordinal));
        foreach (var m in models)
        {
            var mesh = P.GetMesh(m);
            Assert.Contains(mesh.Triangles.Count, new[] { 12, 2 });
            Assert.Equal(new[] { 100f }, mesh.Vertices.Select(v => Math.Abs(v.X)).Distinct());
        }
        var raw = P.Models.Single(m => m.BlobIndex == 0);
        Assert.Equal(_fx.Info.RawDl, raw.Dl.Offset);
        Assert.Equal(_fx.Info.RawObjOff, raw.Dl.Base);
        Assert.Equal(new[] { "CI4", "RGBA16" }, P.Textures.Select(t => t.Fmt).Distinct().OrderBy(x => x));
        var ci = P.TextureImage(P.Textures.First(t => t.Fmt == "CI4"));
        Assert.Equal((16, 16), (ci.Width, ci.Height));
        Assert.Equal(16, ci.CountColors());
        var tex = P.GetMesh(P.Models.First(m => m.Dl.Ucode == Ucode.F3DEX2)).Materials.Single().Texture!;
        Assert.Equal(((byte)255, (byte)0, (byte)0, (byte)255), tex.Get(4, 0));
        Assert.Equal(((byte)255, (byte)255, (byte)255, (byte)255), tex.Get(0, 0));
    }

    [Fact]
    public void AudioBankAndTbl()
    {
        Assert.Single(P.BankFiles);
        Assert.Equal((0, _fx.Info.Tbl), P.LocateTbl(0));
        var refs = P.AllSounds();
        Assert.Equal(2, refs.Count);
        var (pcm, rate) = P.DecodeSound(refs[1]);
        Assert.Equal(22050, rate);
        Assert.Equal(-10000, pcm[0]);
        Assert.Equal(64, pcm.Length);
        Assert.Equal(640, P.DecodeSound(refs[0]).pcm.Length);
    }

    [Fact]
    public void CompressedMidiConverts()
    {
        var cseq = P.Sequences.Single(s => s.Kind == "CSeq");
        var midi = P.SequenceMidi(cseq)!;
        Assert.Equal("MThd"u8.ToArray(), midi[..4]);
        int noteOns = 0;
        for (int i = 0; i + 2 < midi.Length; i++) if (midi[i] == 0x90 && midi[i + 2] == 0x64) noteOns++;
        Assert.Equal(5, noteOns); // 3 literal notes + 2 copied by the back-reference
        Assert.Contains(P.Sequences, s => s.Kind == "MIDI");
    }

    [Fact]
    public void MipsDisassembly()
    {
        Assert.Equal("addiu    sp, sp, -0x18", Mips.Decode(0x27BDFFE8, 0x80000400).Text);
        Assert.Equal(0x80000420u, Mips.Decode(0x0C000108, 0x80000408).Target);
        Assert.Equal("return;", Mips.Decode(0x03E00008, 0).Pseudo);
        Assert.Contains(0x80000420u, P.Functions);
        var c = Mips.FunctionToPseudoC(P.Rom.Data, 0x1000, 0x80000400);
        Assert.Contains("func_80000420();", c);
        Assert.Contains("return;", c);
    }

    [Fact]
    public void ZeldaDmadataTable()
    {
        var payloads = Enumerable.Range(0, 25).Select(i => Enumerable.Repeat((byte)i, 0x200).Concat(System.Text.Encoding.ASCII.GetBytes($"OBJECT{i:D3}")).ToArray()).ToList();
        var rom = new List<byte>(new byte[0x1060]);
        int tableOff = rom.Count, entries = 2 + payloads.Count;
        rom.AddRange(new byte[entries * 16 + 16]);
        uint vrom = (uint)(0x1060 + entries * 16 + 16);
        var rows = new List<(uint, uint, uint, uint)> { (0, 0x1060, 0, 0), (0x1060, vrom, 0x1060, 0) };
        for (int i = 0; i < payloads.Count; i++)
        {
            var pl = payloads[i];
            uint start = (uint)rom.Count;
            if (i % 2 == 1) { var c = TestRom.Yaz0(pl); rom.AddRange(c); rows.Add((vrom, vrom + (uint)pl.Length, start, start + (uint)c.Length)); }
            else { rom.AddRange(pl); rows.Add((vrom, vrom + (uint)pl.Length, start, 0)); }
            while (rom.Count % 16 != 0) rom.Add(0);
            vrom += (uint)pl.Length;
        }
        var arr = rom.ToArray();
        for (int i = 0; i < rows.Count; i++)
        {
            BE.W32(arr, tableOff + i * 16, rows[i].Item1); BE.W32(arr, tableOff + i * 16 + 4, rows[i].Item2);
            BE.W32(arr, tableOff + i * 16 + 8, rows[i].Item3); BE.W32(arr, tableOff + i * 16 + 12, rows[i].Item4);
        }
        var files = Games.FindZeldaDmadata(arr);
        Assert.Equal(entries, files.Count);
        for (int i = 0; i < payloads.Count; i++) Assert.Equal(payloads[i], files.Single(f => f.Index == i + 2).Data);
    }

    [Fact]
    public void ModelExportersWriteValidFiles()
    {
        var mesh = P.GetMesh(P.Models.First(m => m.Dl.Ucode == Ucode.F3DEX2));
        var dir = TempDir();
        foreach (var fmt in ModelExport.Formats.Keys)
        {
            var files = ModelExport.Export(mesh, Path.Combine(dir, fmt, "cube." + fmt), fmt);
            Assert.All(files, f => Assert.True(new FileInfo(f).Length > 0, f));
        }
        var gltf = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "gltf", "cube.gltf")))!;
        Assert.Equal(mesh.Vertices.Count, (int)gltf["accessors"]![0]!["count"]!);
        Assert.Equal("glTF"u8.ToArray(), File.ReadAllBytes(Path.Combine(dir, "glb", "cube.glb"))[..4]);
        Assert.Equal("Kaydara FBX Binary  "u8.ToArray(), File.ReadAllBytes(Path.Combine(dir, "fbx", "cube.fbx"))[..20]);
        Assert.Equal(84 + 12 * 50, new FileInfo(Path.Combine(dir, "stl", "cube.stl")).Length);
        Assert.Contains("map_Kd", File.ReadAllText(Path.Combine(dir, "obj", "cube.mtl")));
    }

    [Fact]
    public void ImageAndAudioWriters()
    {
        var dir = TempDir();
        var img = P.TextureImage(P.Textures[0]);
        foreach (var ext in ImageWriter.Formats.Keys)
            Assert.True(new FileInfo(ImageWriter.Save(img, Path.Combine(dir, "t." + ext), ext)).Length > 0);
        var png = File.ReadAllBytes(Path.Combine(dir, "t.png"));
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png[..4]);
        var pcm = Enumerable.Range(0, 10000).Select(i => (short)new[] { 0, 1000, -1000, 32767, -32768 }[i % 5]).ToArray();
        foreach (var ext in SoundWriter.Native.Keys)
            Assert.True(new FileInfo(SoundWriter.Save(pcm, 22050, Path.Combine(dir, "s"), ext)).Length > 0);
        var wav = File.ReadAllBytes(Path.Combine(dir, "s.wav"));
        Assert.Equal(22050, BitConverter.ToInt32(wav, 24));
        Assert.Equal(pcm.Length * 2, BitConverter.ToInt32(wav, 40));
        var sf2 = File.ReadAllBytes(SoundFont.FromBankFile(P, 0, Path.Combine(dir, "bank.sf2")));
        Assert.Equal("RIFF"u8.ToArray(), sf2[..4]);
        Assert.Equal("sfbk"u8.ToArray(), sf2[8..12]);
    }

    [Fact]
    public void BatchExportAndUnityPackage()
    {
        var dir = TempDir();
        var report = BatchExport.ExportAll(P, dir, new BatchOptions { ModelFormat = "obj", UnityPackage = true, Code = true, RawFiles = true });
        Assert.Contains(report, r => r.StartsWith("Unity package"));
        var paths = new List<string>();
        using (var fs = File.OpenRead(Path.Combine(dir, "N64STUDIO_TEST.unitypackage")))
        using (var gz = new GZipStream(fs, CompressionMode.Decompress))
        using (var tar = new TarReader(gz))
        {
            for (var e = tar.GetNextEntry(); e != null; e = tar.GetNextEntry())
                if (e.Name.EndsWith("/pathname")) paths.Add(new StreamReader(e.DataStream!).ReadToEnd());
        }
        Assert.Contains(paths, x => x.EndsWith(".fbx"));
        Assert.Contains(paths, x => x.EndsWith(".wav"));
        Assert.All(paths, x => Assert.StartsWith("Assets/N64", x));
        Assert.Contains(paths, x => x.StartsWith("Assets/N64/N64STUDIO_TEST/Models/"));
        Assert.True(File.Exists(Path.Combine(dir, "N64STUDIO_TEST", "code_pseudo.c")));
    }

    [Fact]
    public void GameSkeletonIsRebuilt()
    {
        var s = Assert.Single(P.Skeletons);
        Assert.Equal(_fx.Info.SkelHeader, s.Offset);
        Assert.True(s.Flex);
        var m = P.GetSkeletonMesh(s);
        Assert.Equal("game", m.RigSource);
        Assert.Equal(new[] { -1, 0, 0 }, m.Bones.Select(b => b.Parent));
        Assert.Equal((0f, 150f, 0f), (m.Bones[1].X, m.Bones[1].Y, m.Bones[1].Z));
        Assert.Equal((150f, 0f, 0f), (m.Bones[2].X, m.Bones[2].Y, m.Bones[2].Z));
        Assert.Equal(38, m.Triangles.Count);
        // head limb vertices are placed at their joint (local y 0..60 -> 150..210)
        var head = m.Vertices.Where(v => v.Bone == 1).ToList();
        Assert.Equal(8, head.Count);
        Assert.Equal(150f, head.Min(v => v.Y));
        Assert.Equal(210f, head.Max(v => v.Y));
        // the two vertices the arm loads under limb 0's matrix (flex skinning) belong to limb 0, in limb 0 space
        var bridge = m.Vertices.Where(v => v.Bone == 0 && v.X == 40).ToList();
        Assert.Equal(2, bridge.Count);
        Assert.Equal(8, m.Vertices.Count(v => v.Bone == 2 && v.X >= 160));
    }

    [Fact]
    public void AutoRigSplitsRigidParts()
    {
        var a = P.GetMesh(P.Models.First(m => m.Dl.Ucode == Ucode.F3D), false);
        Assert.False(a.IsRigged);
        var b = new Mesh("b") { Vertices = a.Vertices.Select(v => { v.Y += 300; return v; }).ToList(), Triangles = a.Triangles.ToList(), Materials = a.Materials.ToList() };
        var two = a.MergedWith(b, "two");
        Rigging.AutoRig(two);
        Assert.Equal("auto", two.RigSource);
        Assert.Equal(new[] { "root", "body", "part01_upper" }, two.Bones.Select(x => x.Name));
        Assert.Equal(new[] { -1, 0, 1 }, two.Bones.Select(x => x.Parent));
        Assert.Equal((0f, 200f, 0f), (two.Bones[2].X, two.Bones[2].Y, two.Bones[2].Z)); // centre of the contact face
        Assert.All(two.Vertices.Take(8), v => Assert.Equal(1, v.Bone));
        Assert.All(two.Vertices.Skip(8), v => Assert.Equal(2, v.Bone));
        Assert.True(P.GetMesh(P.Models[0]).IsRigged); // project default: every model gets a rig
    }

    [Fact]
    public void RiggedExportsContainSkins()
    {
        var m = P.GetSkeletonMesh(P.Skeletons[0]);
        var dir = TempDir();
        var (json, _) = ModelExport.BuildGltf(m, 1.0, true);
        var skin = json["skins"]![0]!;
        Assert.Equal(3, skin["joints"]!.AsArray().Count);
        Assert.NotNull(json["meshes"]![0]!["primitives"]![0]!["attributes"]!["JOINTS_0"]);
        Assert.Equal(0, (int)json["nodes"]![0]!["skin"]!);
        ModelExport.Export(m, Path.Combine(dir, "c.fbx"), "fbx");
        var fbx = System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(dir, "c.fbx")));
        Assert.Contains("LimbNode", fbx);
        Assert.Contains("Cluster", fbx);
        Assert.Contains("BindPose", fbx);
    }

    [Fact]
    public void RendererDrawsTexturedModel()
    {
        var mesh = P.GetMesh(P.Models.First(m => m.Dl.Ucode == Ucode.F3DEX2));
        var img = new MeshView(mesh).Render(200, 200, MeshView.Mode.Textured);
        int red = 0;
        for (int i = 0; i < img.Pixels.Length; i += 4) if (img.Pixels[i] > 150 && img.Pixels[i + 1] < 60) red++;
        Assert.True(red > 500, $"only {red} red texels drawn");
    }
}
