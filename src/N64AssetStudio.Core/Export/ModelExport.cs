using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace N64AssetStudio.Core.Export;

public static class ModelExport
{
    public static readonly Dictionary<string, string> Formats = new()
    {
        ["glb"] = "glTF 2.0 binary (.glb) - Blender, Unity, Godot, Unreal",
        ["gltf"] = "glTF 2.0 (.gltf + .bin + textures)",
        ["fbx"] = "Autodesk FBX binary (.fbx) - Blender, Unity, Unreal, Maya",
        ["obj"] = "Wavefront OBJ (.obj + .mtl + textures)",
        ["dae"] = "COLLADA (.dae)",
        ["ply"] = "Stanford PLY (.ply)",
        ["stl"] = "STL binary (.stl, geometry only)",
    };

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string N(double v) => v.ToString("0.######", Inv);

    public static string Safe(string name)
    {
        var s = Regex.Replace(name, "[^A-Za-z0-9_.-]+", "_").Trim('_');
        return s.Length == 0 ? "asset" : s;
    }

    public static string TextureFile(Material m) => Safe(m.Name) + ".png";

    static Dictionary<int, string> WriteTextures(Mesh mesh, string folder)
    {
        var o = new Dictionary<int, string>();
        for (int i = 0; i < mesh.Materials.Count; i++)
            if (mesh.Materials[i].Texture is RgbaImage t)
            {
                string fn = TextureFile(mesh.Materials[i]);
                File.WriteAllBytes(Path.Combine(folder, fn), ImageWriter.Png(t));
                o[i] = fn;
            }
        return o;
    }

    public static List<string> Export(Mesh mesh, string path, string fmt, double scale = 1.0)
    {
        fmt = fmt.ToLowerInvariant();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        return fmt switch
        {
            "obj" => Obj(mesh, path, scale), "gltf" => Gltf(mesh, path, scale), "glb" => Glb(mesh, path, scale),
            "dae" => Dae(mesh, path, scale), "ply" => Ply(mesh, path, scale), "stl" => Stl(mesh, path, scale),
            "fbx" => Fbx(mesh, path, scale),
            _ => throw new ArgumentException($"unknown model format {fmt}"),
        };
    }

    // ------------------------------------------------------------------ OBJ
    public static List<string> Obj(Mesh mesh, string path, double scale)
    {
        string folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
        string bname = Path.GetFileNameWithoutExtension(path);
        var tex = WriteTextures(mesh, folder);
        string mtl = bname + ".mtl";
        var m = new StringBuilder("# Exported by N64 Asset Studio\n");
        for (int i = 0; i < mesh.Materials.Count; i++)
        {
            var mat = mesh.Materials[i];
            m.Append($"\nnewmtl {Safe(mat.Name)}\nKa 0 0 0\nKd {N(mat.Color[0])} {N(mat.Color[1])} {N(mat.Color[2])}\nKs 0 0 0\nd {N(mat.Color[3])}\nillum 1\n");
            if (tex.TryGetValue(i, out var fn))
            {
                m.Append($"map_Kd {fn}\n");
                if (mat.HasAlpha) m.Append($"map_d {fn}\n");
            }
        }
        File.WriteAllText(Path.Combine(folder, mtl), m.ToString());
        var sb = new StringBuilder();
        sb.Append($"# Exported by N64 Asset Studio\n# {mesh.Vertices.Count} vertices, {mesh.Triangles.Count} triangles\nmtllib {mtl}\no {Safe(mesh.Name)}\n");
        foreach (var v in mesh.Vertices)
            sb.Append(mesh.HasColors
                ? $"v {N(v.X * scale)} {N(v.Y * scale)} {N(v.Z * scale)} {N(v.R)} {N(v.G)} {N(v.B)}\n"
                : $"v {N(v.X * scale)} {N(v.Y * scale)} {N(v.Z * scale)}\n");
        foreach (var v in mesh.Vertices) sb.Append($"vt {N(v.U)} {N(1 - v.V)}\n");
        foreach (var v in mesh.Vertices) sb.Append($"vn {N(v.NX)} {N(v.NY)} {N(v.NZ)}\n");
        foreach (var (mi, tris) in mesh.TrianglesByMaterial())
        {
            sb.Append($"usemtl {Safe(mesh.Materials[mi].Name)}\n");
            foreach (var (a, b, c) in tris) sb.Append($"f {a + 1}/{a + 1}/{a + 1} {b + 1}/{b + 1}/{b + 1} {c + 1}/{c + 1}/{c + 1}\n");
        }
        File.WriteAllText(path, sb.ToString());
        return new List<string> { path, Path.Combine(folder, mtl) }.Concat(tex.Values.Select(t => Path.Combine(folder, t))).ToList();
    }

    // ----------------------------------------------------------------- glTF
    static int WrapCode(string w) => w switch { "mirror" => 33648, "clamp" => 33071, _ => 10497 };

    public static (JsonObject json, byte[] bin) BuildGltf(Mesh mesh, double scale, bool embedImages, Dictionary<int, string>? uris = null)
    {
        var blob = new MemoryStream();
        var views = new JsonArray();
        var accessors = new JsonArray();

        int AddView(byte[] data, int? target)
        {
            while (blob.Length % 4 != 0) blob.WriteByte(0);
            var v = new JsonObject { ["buffer"] = 0, ["byteOffset"] = blob.Length, ["byteLength"] = data.Length };
            if (target.HasValue) v["target"] = target.Value;
            blob.Write(data);
            views.Add(v);
            return views.Count - 1;
        }
        int AddFloats(float[] arr, int comps, string type, bool minmax)
        {
            var bytes = new byte[arr.Length * 4];
            Buffer.BlockCopy(arr, 0, bytes, 0, bytes.Length);
            var acc = new JsonObject { ["bufferView"] = AddView(bytes, 34962), ["componentType"] = 5126, ["count"] = arr.Length / comps, ["type"] = type };
            if (minmax && arr.Length > 0)
            {
                var mn = new JsonArray(); var mx = new JsonArray();
                for (int c = 0; c < comps; c++)
                {
                    float lo = float.MaxValue, hi = float.MinValue;
                    for (int i = c; i < arr.Length; i += comps) { lo = Math.Min(lo, arr[i]); hi = Math.Max(hi, arr[i]); }
                    mn.Add(lo); mx.Add(hi);
                }
                acc["min"] = mn; acc["max"] = mx;
            }
            accessors.Add(acc);
            return accessors.Count - 1;
        }

        var vs = mesh.Vertices;
        var pos = vs.SelectMany(v => new[] { (float)(v.X * scale), (float)(v.Y * scale), (float)(v.Z * scale) }).ToArray();
        var nrm = vs.SelectMany(v => new[] { v.NX, v.NY, v.NZ }).ToArray();
        var uv = vs.SelectMany(v => new[] { v.U, v.V }).ToArray();
        var attrs = new JsonObject
        {
            ["POSITION"] = AddFloats(pos, 3, "VEC3", true),
            ["NORMAL"] = AddFloats(nrm, 3, "VEC3", false),
            ["TEXCOORD_0"] = AddFloats(uv, 2, "VEC2", false),
        };
        if (mesh.HasColors) attrs["COLOR_0"] = AddFloats(vs.SelectMany(v => new[] { v.R, v.G, v.B, v.A }).ToArray(), 4, "VEC4", false);

        var images = new JsonArray(); var textures = new JsonArray(); var samplers = new JsonArray(); var materials = new JsonArray();
        for (int i = 0; i < mesh.Materials.Count; i++)
        {
            var m = mesh.Materials[i];
            var pbr = new JsonObject
            {
                ["baseColorFactor"] = new JsonArray(m.Color.Select(c => (JsonNode)c).ToArray()),
                ["metallicFactor"] = 0.0, ["roughnessFactor"] = 1.0,
            };
            var gm = new JsonObject { ["name"] = Safe(m.Name), ["doubleSided"] = true, ["pbrMetallicRoughness"] = pbr };
            if (m.Texture != null)
            {
                if (embedImages)
                    images.Add(new JsonObject { ["bufferView"] = AddView(ImageWriter.Png(m.Texture), null), ["mimeType"] = "image/png", ["name"] = Safe(m.Name) });
                else
                    images.Add(new JsonObject { ["uri"] = uris != null && uris.TryGetValue(i, out var u) ? u : TextureFile(m) });
                samplers.Add(new JsonObject { ["magFilter"] = 9729, ["minFilter"] = 9729, ["wrapS"] = WrapCode(m.WrapS), ["wrapT"] = WrapCode(m.WrapT) });
                textures.Add(new JsonObject { ["source"] = images.Count - 1, ["sampler"] = samplers.Count - 1 });
                pbr["baseColorTexture"] = new JsonObject { ["index"] = textures.Count - 1 };
            }
            if (m.HasAlpha) { gm["alphaMode"] = "MASK"; gm["alphaCutoff"] = 0.5; }
            materials.Add(gm);
        }

        var prims = new JsonArray();
        foreach (var (mi, tris) in mesh.TrianglesByMaterial())
        {
            var idx = new byte[tris.Count * 12];
            for (int t = 0; t < tris.Count; t++)
            {
                BitConverter.TryWriteBytes(idx.AsSpan(t * 12), (uint)tris[t].Item1);
                BitConverter.TryWriteBytes(idx.AsSpan(t * 12 + 4), (uint)tris[t].Item2);
                BitConverter.TryWriteBytes(idx.AsSpan(t * 12 + 8), (uint)tris[t].Item3);
            }
            accessors.Add(new JsonObject { ["bufferView"] = AddView(idx, 34963), ["componentType"] = 5125, ["count"] = tris.Count * 3, ["type"] = "SCALAR" });
            prims.Add(new JsonObject { ["attributes"] = attrs.DeepClone(), ["indices"] = accessors.Count - 1, ["material"] = mi, ["mode"] = 4 });
        }
        if (prims.Count == 0) prims.Add(new JsonObject { ["attributes"] = attrs.DeepClone(), ["mode"] = 0 });
        while (blob.Length % 4 != 0) blob.WriteByte(0);

        var root = new JsonObject
        {
            ["asset"] = new JsonObject { ["version"] = "2.0", ["generator"] = "N64 Asset Studio" },
            ["scene"] = 0,
            ["scenes"] = new JsonArray(new JsonObject { ["nodes"] = new JsonArray(0) }),
            ["nodes"] = new JsonArray(new JsonObject { ["name"] = Safe(mesh.Name), ["mesh"] = 0 }),
            ["meshes"] = new JsonArray(new JsonObject { ["name"] = Safe(mesh.Name), ["primitives"] = prims }),
            ["buffers"] = new JsonArray(new JsonObject { ["byteLength"] = blob.Length }),
            ["bufferViews"] = views,
            ["accessors"] = accessors,
        };
        if (materials.Count > 0) root["materials"] = materials;
        if (images.Count > 0) { root["images"] = images; root["textures"] = textures; root["samplers"] = samplers; }
        return (root, blob.ToArray());
    }

    public static byte[] GlbBytes(Mesh mesh, double scale = 1.0)
    {
        var (json, bin) = BuildGltf(mesh, scale, true);
        var js = Encoding.UTF8.GetBytes(json.ToJsonString());
        int jsLen = (js.Length + 3) & ~3;
        var o = new MemoryStream();
        var w = new BinaryWriter(o);
        w.Write(0x46546C67u); w.Write(2u); w.Write((uint)(12 + 8 + jsLen + 8 + bin.Length));
        w.Write((uint)jsLen); w.Write(0x4E4F534Au); w.Write(js); for (int i = js.Length; i < jsLen; i++) w.Write((byte)' ');
        w.Write((uint)bin.Length); w.Write(0x004E4942u); w.Write(bin);
        return o.ToArray();
    }

    public static List<string> Glb(Mesh mesh, string path, double scale)
    {
        File.WriteAllBytes(path, GlbBytes(mesh, scale));
        return new() { path };
    }

    public static List<string> Gltf(Mesh mesh, string path, double scale)
    {
        string folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
        var tex = WriteTextures(mesh, folder);
        var (json, bin) = BuildGltf(mesh, scale, false, tex);
        string binName = Path.GetFileNameWithoutExtension(path) + ".bin";
        json["buffers"]![0]!["uri"] = binName;
        File.WriteAllBytes(Path.Combine(folder, binName), bin);
        File.WriteAllText(path, json.ToJsonString(new JsonSerializerOptions(JsonSerializerOptions.Default) { WriteIndented = true }));
        return new List<string> { path, Path.Combine(folder, binName) }.Concat(tex.Values.Select(t => Path.Combine(folder, t))).ToList();
    }

    // -------------------------------------------------------------- COLLADA
    public static List<string> Dae(Mesh mesh, string path, double scale)
    {
        string folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
        var tex = WriteTextures(mesh, folder);
        string name = Safe(mesh.Name);
        int n = mesh.Vertices.Count;
        string Floats(IEnumerable<double> xs) => string.Join(" ", xs.Select(x => x.ToString("0.######", Inv)));
        var x = new StringBuilder();
        x.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<COLLADA xmlns=\"http://www.collada.org/2005/11/COLLADASchema\" version=\"1.4.1\">\n");
        x.Append("<asset><contributor><authoring_tool>N64 Asset Studio</authoring_tool></contributor><unit name=\"meter\" meter=\"1\"/><up_axis>Y_UP</up_axis></asset>\n");
        x.Append("<library_images>");
        foreach (var (i, fn) in tex) x.Append($"<image id=\"img{i}\" name=\"img{i}\"><init_from>{System.Security.SecurityElement.Escape(fn)}</init_from></image>");
        x.Append("</library_images>\n<library_effects>");
        for (int i = 0; i < mesh.Materials.Count; i++)
        {
            var m = mesh.Materials[i];
            x.Append($"<effect id=\"fx{i}\"><profile_COMMON>");
            if (tex.ContainsKey(i))
                x.Append($"<newparam sid=\"surf{i}\"><surface type=\"2D\"><init_from>img{i}</init_from></surface></newparam>" +
                         $"<newparam sid=\"samp{i}\"><sampler2D><source>surf{i}</source></sampler2D></newparam>");
            x.Append("<technique sid=\"common\"><lambert><diffuse>");
            x.Append(tex.ContainsKey(i) ? $"<texture texture=\"samp{i}\" texcoord=\"UVSET0\"/>" : $"<color>{Floats(m.Color.Select(c => (double)c))}</color>");
            x.Append("</diffuse></lambert></technique></profile_COMMON></effect>");
        }
        x.Append("</library_effects>\n<library_materials>");
        for (int i = 0; i < mesh.Materials.Count; i++) x.Append($"<material id=\"mat{i}\" name=\"{Safe(mesh.Materials[i].Name)}\"><instance_effect url=\"#fx{i}\"/></material>");
        x.Append("</library_materials>\n<library_geometries>");
        x.Append($"<geometry id=\"geom\" name=\"{name}\"><mesh>");
        var srcs = new (string id, IEnumerable<double> data, string pars)[]
        {
            ("pos", mesh.Vertices.SelectMany(v => new double[] { v.X * scale, v.Y * scale, v.Z * scale }), "XYZ"),
            ("nrm", mesh.Vertices.SelectMany(v => new double[] { v.NX, v.NY, v.NZ }), "XYZ"),
            ("uv", mesh.Vertices.SelectMany(v => new double[] { v.U, 1 - v.V }), "ST"),
            ("col", mesh.Vertices.SelectMany(v => new double[] { v.R, v.G, v.B, v.A }), "RGBA"),
        };
        foreach (var (id, data, pars) in srcs)
        {
            int stride = pars.Length;
            x.Append($"<source id=\"{id}\"><float_array id=\"{id}-a\" count=\"{n * stride}\">{Floats(data)}</float_array>" +
                     $"<technique_common><accessor source=\"#{id}-a\" count=\"{n}\" stride=\"{stride}\">" +
                     string.Concat(pars.Select(p => $"<param name=\"{p}\" type=\"float\"/>")) + "</accessor></technique_common></source>");
        }
        x.Append("<vertices id=\"verts\"><input semantic=\"POSITION\" source=\"#pos\"/></vertices>");
        var groups = mesh.TrianglesByMaterial();
        foreach (var (mi, tris) in groups)
        {
            x.Append($"<triangles material=\"m{mi}\" count=\"{tris.Count}\"><input semantic=\"VERTEX\" source=\"#verts\" offset=\"0\"/>" +
                     "<input semantic=\"NORMAL\" source=\"#nrm\" offset=\"0\"/><input semantic=\"TEXCOORD\" source=\"#uv\" offset=\"0\" set=\"0\"/>" +
                     (mesh.HasColors ? "<input semantic=\"COLOR\" source=\"#col\" offset=\"0\" set=\"0\"/>" : "") +
                     "<p>" + string.Join(" ", tris.Select(t => $"{t.Item1} {t.Item2} {t.Item3}")) + "</p></triangles>");
        }
        x.Append("</mesh></geometry></library_geometries>\n");
        x.Append($"<library_visual_scenes><visual_scene id=\"scene\"><node id=\"{name}\" name=\"{name}\"><instance_geometry url=\"#geom\"><bind_material><technique_common>");
        foreach (var mi in groups.Keys)
            x.Append($"<instance_material symbol=\"m{mi}\" target=\"#mat{mi}\"><bind_vertex_input semantic=\"UVSET0\" input_semantic=\"TEXCOORD\" input_set=\"0\"/></instance_material>");
        x.Append("</technique_common></bind_material></instance_geometry></node></visual_scene></library_visual_scenes>\n<scene><instance_visual_scene url=\"#scene\"/></scene></COLLADA>\n");
        File.WriteAllText(path, x.ToString());
        return new List<string> { path }.Concat(tex.Values.Select(t => Path.Combine(folder, t))).ToList();
    }

    // ------------------------------------------------------------------ PLY
    public static List<string> Ply(Mesh mesh, string path, double scale)
    {
        using var fs = File.Create(path);
        var head = string.Join("\n", new[]
        {
            "ply", "format binary_little_endian 1.0", "comment N64 Asset Studio", $"element vertex {mesh.Vertices.Count}",
            "property float x", "property float y", "property float z", "property float nx", "property float ny", "property float nz",
            "property float s", "property float t", "property uchar red", "property uchar green", "property uchar blue", "property uchar alpha",
            $"element face {mesh.Triangles.Count}", "property list uchar int vertex_indices", "end_header", "",
        });
        fs.Write(Encoding.ASCII.GetBytes(head));
        var w = new BinaryWriter(fs);
        static byte C(float f) => (byte)Math.Clamp((int)(f * 255), 0, 255);
        foreach (var v in mesh.Vertices)
        {
            w.Write((float)(v.X * scale)); w.Write((float)(v.Y * scale)); w.Write((float)(v.Z * scale));
            w.Write(v.NX); w.Write(v.NY); w.Write(v.NZ); w.Write(v.U); w.Write(1 - v.V);
            w.Write(C(v.R)); w.Write(C(v.G)); w.Write(C(v.B)); w.Write(C(v.A));
        }
        foreach (var t in mesh.Triangles) { w.Write((byte)3); w.Write(t.A); w.Write(t.B); w.Write(t.C); }
        return new() { path };
    }

    // ------------------------------------------------------------------ STL
    public static List<string> Stl(Mesh mesh, string path, double scale)
    {
        using var fs = File.Create(path);
        var w = new BinaryWriter(fs);
        w.Write(Encoding.ASCII.GetBytes("N64 Asset Studio STL".PadRight(80)));
        w.Write((uint)mesh.Triangles.Count);
        foreach (var t in mesh.Triangles)
        {
            var a = mesh.Vertices[t.A]; var b = mesh.Vertices[t.B]; var c = mesh.Vertices[t.C];
            double ux = b.X - a.X, uy = b.Y - a.Y, uz = b.Z - a.Z, vx = c.X - a.X, vy = c.Y - a.Y, vz = c.Z - a.Z;
            double nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
            double l = Math.Sqrt(nx * nx + ny * ny + nz * nz); if (l == 0) l = 1;
            w.Write((float)(nx / l)); w.Write((float)(ny / l)); w.Write((float)(nz / l));
            foreach (var v in new[] { a, b, c }) { w.Write((float)(v.X * scale)); w.Write((float)(v.Y * scale)); w.Write((float)(v.Z * scale)); }
            w.Write((ushort)0);
        }
        return new() { path };
    }

    public static List<string> Fbx(Mesh mesh, string path, double scale)
    {
        string folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
        var tex = WriteTextures(mesh, folder);
        FbxWriter.Write(mesh, path, scale, tex);
        return new List<string> { path }.Concat(tex.Values.Select(t => Path.Combine(folder, t))).ToList();
    }
}
