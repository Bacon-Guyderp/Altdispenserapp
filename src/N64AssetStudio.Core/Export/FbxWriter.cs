using System.IO.Compression;
using System.Text;

namespace N64AssetStudio.Core.Export;

/// <summary>Minimal binary FBX 7.4 writer for static textured meshes.</summary>
public static class FbxWriter
{
    const int Version = 7400;
    static readonly byte[] Head = Encoding.ASCII.GetBytes("Kaydara FBX Binary  \0\x1a\0");
    static readonly byte[] FileId = { 0x28, 0xb3, 0x2a, 0xeb, 0xb6, 0x24, 0xcc, 0xc2, 0xbf, 0xc8, 0xb0, 0x2a, 0xa9, 0x2b, 0xfc, 0xf1 };
    static readonly byte[] FootId = { 0xfa, 0xbc, 0xab, 0x09, 0xd0, 0xc8, 0xd4, 0x66, 0xb1, 0x76, 0xfb, 0x83, 0x1c, 0xf7, 0x26, 0x7e };
    static readonly byte[] FootMagic = { 0xf8, 0x5a, 0x8c, 0x6a, 0xde, 0xf5, 0xd9, 0x7e, 0xec, 0xe9, 0x0c, 0xe3, 0x75, 0x8f, 0x29, 0x0b };

    /// <summary>Typed FBX property values.</summary>
    public abstract record P;
    public sealed record PI(int V) : P;
    public sealed record PL(long V) : P;
    public sealed record PD(double V) : P;
    public sealed record PC(bool V) : P;
    public sealed record PS(byte[] V) : P { public PS(string s) : this(Encoding.UTF8.GetBytes(s)) { } }
    public sealed record PR(byte[] V) : P;
    public sealed record PAi(int[] V) : P;
    public sealed record PAd(double[] V) : P;

    public sealed class Node
    {
        public string Name;
        public List<P> Props;
        public List<Node> Children = new();
        public Node(string name, params P[] props) { Name = name; Props = props.ToList(); }
        public Node Add(string name, params P[] props) { var n = new Node(name, props); Children.Add(n); return n; }

        static void WriteArray(BinaryWriter w, char code, byte[] raw, int count)
        {
            w.Write((byte)code);
            w.Write(count);
            if (raw.Length > 128)
            {
                var ms = new MemoryStream();
                using (var z = new ZLibStream(ms, CompressionLevel.Optimal, true)) z.Write(raw);
                var c = ms.ToArray();
                w.Write(1); w.Write(c.Length); w.Write(c);
            }
            else { w.Write(0); w.Write(raw.Length); w.Write(raw); }
        }

        static void WriteProp(BinaryWriter w, P p)
        {
            switch (p)
            {
                case PI x: w.Write((byte)'I'); w.Write(x.V); break;
                case PL x: w.Write((byte)'L'); w.Write(x.V); break;
                case PD x: w.Write((byte)'D'); w.Write(x.V); break;
                case PC x: w.Write((byte)'C'); w.Write((byte)(x.V ? 1 : 0)); break;
                case PS x: w.Write((byte)'S'); w.Write(x.V.Length); w.Write(x.V); break;
                case PR x: w.Write((byte)'R'); w.Write(x.V.Length); w.Write(x.V); break;
                case PAi x: { var b = new byte[x.V.Length * 4]; Buffer.BlockCopy(x.V, 0, b, 0, b.Length); WriteArray(w, 'i', b, x.V.Length); break; }
                case PAd x: { var b = new byte[x.V.Length * 8]; Buffer.BlockCopy(x.V, 0, b, 0, b.Length); WriteArray(w, 'd', b, x.V.Length); break; }
            }
        }

        public void Write(BinaryWriter w)
        {
            var s = w.BaseStream;
            long start = s.Position;
            var pm = new MemoryStream();
            var pw = new BinaryWriter(pm);
            foreach (var p in Props) WriteProp(pw, p);
            var name = Encoding.ASCII.GetBytes(Name);
            w.Write(0u); w.Write((uint)Props.Count); w.Write((uint)pm.Length);
            w.Write((byte)name.Length); w.Write(name); w.Write(pm.ToArray());
            if (Children.Count > 0 || Props.Count == 0)
            {
                foreach (var c in Children) c.Write(w);
                w.Write(new byte[13]);
            }
            long end = s.Position;
            s.Position = start;
            w.Write((uint)end);
            s.Position = end;
        }
    }

    static PS ClassName(string n, string cls) => new(Encoding.UTF8.GetBytes(n).Concat(new byte[] { 0, 1 }).Concat(Encoding.ASCII.GetBytes(cls)).ToArray());

    static Node P70(Node parent, params P[][] rows)
    {
        var p = parent.Add("Properties70");
        foreach (var r in rows) p.Add("P", r);
        return p;
    }

    static P[] Row(string name, string type, string sub, string flags, params P[] vals) =>
        new P[] { new PS(name), new PS(type), new PS(sub), new PS(flags) }.Concat(vals).ToArray();

    static PAd Translation(double x, double y, double z) =>
        new(new double[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, x, y, z, 1 });

    /// <summary>Armature: one LimbNode per bone, a Skin deformer with one Cluster per bone (rigid weights)
    /// and a BindPose. Bind rotations are identity, so all bind matrices are pure translations.</summary>
    static void WriteSkeleton(Mesh mesh, double scale, Node objects, Node conns, Func<long> newId, long meshId, long geoId)
    {
        var bones = mesh.Bones;
        var boneIds = new long[bones.Count];
        for (int i = 0; i < bones.Count; i++)
        {
            var b = bones[i];
            double px = 0, py = 0, pz = 0;
            if (b.Parent >= 0) { px = bones[b.Parent].X; py = bones[b.Parent].Y; pz = bones[b.Parent].Z; }
            long attrId = newId();
            var attr = objects.Add("NodeAttribute", new PL(attrId), ClassName(b.Name, "NodeAttribute"), new PS("LimbNode"));
            P70(attr, Row("Size", "double", "Number", "", new PD(1.0)));
            attr.Add("TypeFlags", new PS("Skeleton"));
            boneIds[i] = newId();
            var model = objects.Add("Model", new PL(boneIds[i]), ClassName(b.Name, "Model"), new PS("LimbNode"));
            model.Add("Version", new PI(232));
            P70(model,
                Row("Lcl Translation", "Lcl Translation", "", "A", new PD((b.X - px) * scale), new PD((b.Y - py) * scale), new PD((b.Z - pz) * scale)),
                Row("Lcl Rotation", "Lcl Rotation", "", "A", new PD(0), new PD(0), new PD(0)),
                Row("Lcl Scaling", "Lcl Scaling", "", "A", new PD(1), new PD(1), new PD(1)));
            model.Add("Shading", new PC(true));
            model.Add("Culling", new PS("CullingOff"));
            conns.Add("C", new PS("OO"), new PL(attrId), new PL(boneIds[i]));
        }
        for (int i = 0; i < bones.Count; i++)
            conns.Add("C", new PS("OO"), new PL(boneIds[i]), new PL(bones[i].Parent >= 0 ? boneIds[bones[i].Parent] : 0));

        long skinId = newId();
        var skin = objects.Add("Deformer", new PL(skinId), ClassName("Armature", "Deformer"), new PS("Skin"));
        skin.Add("Version", new PI(101));
        skin.Add("Link_DeformAcuracy", new PD(50.0));
        conns.Add("C", new PS("OO"), new PL(skinId), new PL(geoId));
        for (int i = 0; i < bones.Count; i++)
        {
            var idx = Enumerable.Range(0, mesh.Vertices.Count).Where(v => mesh.Vertices[v].Bone == i).ToArray();
            long clId = newId();
            var cl = objects.Add("Deformer", new PL(clId), ClassName(bones[i].Name, "SubDeformer"), new PS("Cluster"));
            cl.Add("Version", new PI(100));
            cl.Add("UserData", new PS(""), new PS(""));
            cl.Add("Indexes", new PAi(idx));
            cl.Add("Weights", new PAd(idx.Select(_ => 1.0).ToArray()));
            double bx = bones[i].X * scale, by = bones[i].Y * scale, bz = bones[i].Z * scale;
            cl.Add("Transform", Translation(-bx, -by, -bz));     // mesh space -> bone space at bind time
            cl.Add("TransformLink", Translation(bx, by, bz));    // bone's global bind matrix
            conns.Add("C", new PS("OO"), new PL(clId), new PL(skinId));
            conns.Add("C", new PS("OO"), new PL(boneIds[i]), new PL(clId));
        }

        var pose = objects.Add("Pose", new PL(newId()), ClassName("BindPose", "Pose"), new PS("BindPose"));
        pose.Add("Type", new PS("BindPose"));
        pose.Add("Version", new PI(100));
        pose.Add("NbPoseNodes", new PI(bones.Count + 1));
        var pn = pose.Add("PoseNode");
        pn.Add("Node", new PL(meshId));
        pn.Add("Matrix", Translation(0, 0, 0));
        for (int i = 0; i < bones.Count; i++)
        {
            var n = pose.Add("PoseNode");
            n.Add("Node", new PL(boneIds[i]));
            n.Add("Matrix", Translation(bones[i].X * scale, bones[i].Y * scale, bones[i].Z * scale));
        }
    }

    public static void Write(Mesh mesh, string path, double scale, Dictionary<int, string> textures)
    {
        long uid = 100000;
        long NewId() => ++uid;
        var root = new List<Node>();

        var hx = new Node("FBXHeaderExtension");
        hx.Add("FBXHeaderVersion", new PI(1003));
        hx.Add("FBXVersion", new PI(Version));
        hx.Add("EncryptionType", new PI(0));
        var ts = hx.Add("CreationTimeStamp");
        foreach (var (k, v) in new[] { ("Version", 1000), ("Year", 1970), ("Month", 1), ("Day", 1), ("Hour", 10), ("Minute", 0), ("Second", 0), ("Millisecond", 0) })
            ts.Add(k, new PI(v));
        hx.Add("Creator", new PS("N64 Asset Studio"));
        root.Add(hx);
        root.Add(new Node("FileId", new PR(FileId)));
        root.Add(new Node("CreationTime", new PS("1970-01-01 10:00:00:000")));
        root.Add(new Node("Creator", new PS("N64 Asset Studio")));

        var gs = new Node("GlobalSettings");
        gs.Add("Version", new PI(1000));
        P70(gs,
            Row("UpAxis", "int", "Integer", "", new PI(1)), Row("UpAxisSign", "int", "Integer", "", new PI(1)),
            Row("FrontAxis", "int", "Integer", "", new PI(2)), Row("FrontAxisSign", "int", "Integer", "", new PI(1)),
            Row("CoordAxis", "int", "Integer", "", new PI(0)), Row("CoordAxisSign", "int", "Integer", "", new PI(1)),
            Row("OriginalUpAxis", "int", "Integer", "", new PI(1)), Row("OriginalUpAxisSign", "int", "Integer", "", new PI(1)),
            // 100 = file units are metres, so 1 N64 unit == 1 m like the glTF/OBJ exports
            Row("UnitScaleFactor", "double", "Number", "", new PD(100.0)),
            Row("OriginalUnitScaleFactor", "double", "Number", "", new PD(100.0)));
        root.Add(gs);

        var docs = new Node("Documents");
        docs.Add("Count", new PI(1));
        var doc = docs.Add("Document", new PL(NewId()), new PS("Scene"), new PS("Scene"));
        P70(doc);
        doc.Add("RootNode", new PL(0));
        root.Add(docs);
        root.Add(new Node("References"));

        int nTex = textures.Count, nMat = mesh.Materials.Count;
        var defs = new Node("Definitions");
        defs.Add("Version", new PI(100));
        int nBones = mesh.Bones.Count, rigged = nBones > 0 ? 1 : 0;
        defs.Add("Count", new PI(3 + nMat + 2 * nTex + nBones * 3 + rigged * 2));
        foreach (var (typ, cnt) in new[] { ("GlobalSettings", 1), ("Model", 1 + nBones), ("Geometry", 1), ("Material", nMat), ("Texture", nTex),
                     ("Video", nTex), ("NodeAttribute", nBones), ("Deformer", nBones + rigged), ("Pose", rigged) })
            if (cnt > 0) defs.Add("ObjectType", new PS(typ)).Add("Count", new PI(cnt));
        root.Add(defs);

        var objects = new Node("Objects");
        var conns = new Node("Connections");

        var verts = mesh.Vertices.SelectMany(v => new[] { v.X * scale, v.Y * scale, v.Z * scale }).ToArray();
        var flat = mesh.Triangles.SelectMany(t => new[] { t.A, t.B, t.C }).ToArray();
        var pvi = (int[])flat.Clone();
        for (int i = 2; i < pvi.Length; i += 3) pvi[i] = -pvi[i] - 1;
        var normals = flat.SelectMany(i => new double[] { mesh.Vertices[i].NX, mesh.Vertices[i].NY, mesh.Vertices[i].NZ }).ToArray();
        var uvs = mesh.Vertices.SelectMany(v => new double[] { v.U, 1 - v.V }).ToArray();
        var cols = mesh.Vertices.SelectMany(v => new double[] { v.R, v.G, v.B, v.A }).ToArray();

        long geoId = NewId();
        var geo = objects.Add("Geometry", new PL(geoId), ClassName(mesh.Name, "Geometry"), new PS("Mesh"));
        P70(geo);
        geo.Add("GeometryVersion", new PI(124));
        geo.Add("Vertices", new PAd(verts));
        geo.Add("PolygonVertexIndex", new PAi(pvi));
        var ln = geo.Add("LayerElementNormal", new PI(0));
        ln.Add("Version", new PI(101)); ln.Add("Name", new PS(""));
        ln.Add("MappingInformationType", new PS("ByPolygonVertex")); ln.Add("ReferenceInformationType", new PS("Direct"));
        ln.Add("Normals", new PAd(normals));
        var lu = geo.Add("LayerElementUV", new PI(0));
        lu.Add("Version", new PI(101)); lu.Add("Name", new PS("UVMap"));
        lu.Add("MappingInformationType", new PS("ByPolygonVertex")); lu.Add("ReferenceInformationType", new PS("IndexToDirect"));
        lu.Add("UV", new PAd(uvs)); lu.Add("UVIndex", new PAi(flat));
        if (mesh.HasColors)
        {
            var lc = geo.Add("LayerElementColor", new PI(0));
            lc.Add("Version", new PI(101)); lc.Add("Name", new PS("Col"));
            lc.Add("MappingInformationType", new PS("ByPolygonVertex")); lc.Add("ReferenceInformationType", new PS("IndexToDirect"));
            lc.Add("Colors", new PAd(cols)); lc.Add("ColorIndex", new PAi(flat));
        }
        var lm = geo.Add("LayerElementMaterial", new PI(0));
        lm.Add("Version", new PI(101)); lm.Add("Name", new PS(""));
        lm.Add("MappingInformationType", new PS(nMat > 1 ? "ByPolygon" : "AllSame"));
        lm.Add("ReferenceInformationType", new PS("IndexToDirect"));
        lm.Add("Materials", new PAi(nMat > 1 ? mesh.Triangles.Select(t => t.Mat).ToArray() : new[] { 0 }));
        var layer = geo.Add("Layer", new PI(0));
        layer.Add("Version", new PI(100));
        var elems = new List<string> { "LayerElementNormal", "LayerElementUV", "LayerElementMaterial" };
        if (mesh.HasColors) elems.Add("LayerElementColor");
        foreach (var e in elems)
        {
            var le = layer.Add("LayerElement");
            le.Add("Type", new PS(e));
            le.Add("TypedIndex", new PI(0));
        }

        long modelId = NewId();
        var model = objects.Add("Model", new PL(modelId), ClassName(mesh.Name, "Model"), new PS("Mesh"));
        model.Add("Version", new PI(232));
        P70(model,
            Row("Lcl Translation", "Lcl Translation", "", "A", new PD(0), new PD(0), new PD(0)),
            Row("Lcl Rotation", "Lcl Rotation", "", "A", new PD(0), new PD(0), new PD(0)),
            Row("Lcl Scaling", "Lcl Scaling", "", "A", new PD(1), new PD(1), new PD(1)),
            Row("DefaultAttributeIndex", "int", "Integer", "", new PI(0)));
        model.Add("Shading", new PC(true));
        model.Add("Culling", new PS("CullingOff"));
        conns.Add("C", new PS("OO"), new PL(modelId), new PL(0));
        conns.Add("C", new PS("OO"), new PL(geoId), new PL(modelId));
        if (nBones > 0) WriteSkeleton(mesh, scale, objects, conns, NewId, modelId, geoId);

        string folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
        for (int i = 0; i < nMat; i++)
        {
            var m = mesh.Materials[i];
            long matId = NewId();
            var mat = objects.Add("Material", new PL(matId), ClassName(m.Name, "Material"), new PS(""));
            mat.Add("Version", new PI(102));
            mat.Add("ShadingModel", new PS("lambert"));
            mat.Add("MultiLayer", new PI(0));
            P70(mat,
                Row("DiffuseColor", "Color", "", "A", new PD(m.Color[0]), new PD(m.Color[1]), new PD(m.Color[2])),
                Row("Diffuse", "Vector3D", "Vector", "", new PD(m.Color[0]), new PD(m.Color[1]), new PD(m.Color[2])),
                Row("Opacity", "double", "Number", "", new PD(m.Color[3])));
            conns.Add("C", new PS("OO"), new PL(matId), new PL(modelId));
            if (!textures.TryGetValue(i, out var fn)) continue;
            string full = Path.Combine(folder, fn);
            long texId = NewId(), vidId = NewId();
            var vid = objects.Add("Video", new PL(vidId), ClassName(m.Name, "Video"), new PS("Clip"));
            vid.Add("Type", new PS("Clip"));
            P70(vid, Row("Path", "KString", "XRefUrl", "", new PS(full)));
            vid.Add("UseMipMap", new PI(0));
            vid.Add("Filename", new PS(full));
            vid.Add("RelativeFilename", new PS(fn));
            var tex = objects.Add("Texture", new PL(texId), ClassName(m.Name, "Texture"), new PS(""));
            tex.Add("Type", new PS("TextureVideoClip"));
            tex.Add("Version", new PI(202));
            tex.Add("TextureName", ClassName(m.Name, "Texture"));
            P70(tex, Row("UseMaterial", "bool", "", "", new PI(1)));
            tex.Add("Media", ClassName(m.Name, "Video"));
            tex.Add("FileName", new PS(full));
            tex.Add("RelativeFilename", new PS(fn));
            tex.Add("ModelUVTranslation", new PD(0), new PD(0));
            tex.Add("ModelUVScaling", new PD(1), new PD(1));
            tex.Add("Texture_Alpha_Source", new PS("None"));
            tex.Add("Cropping", new PI(0), new PI(0), new PI(0), new PI(0));
            conns.Add("C", new PS("OO"), new PL(vidId), new PL(texId));
            conns.Add("C", new PS("OP"), new PL(texId), new PL(matId), new PS("DiffuseColor"));
        }
        root.Add(objects);
        root.Add(conns);
        var takes = new Node("Takes");
        takes.Add("Current", new PS(""));
        root.Add(takes);

        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        w.Write(Head);
        w.Write(Version);
        foreach (var n in root) n.Write(w);
        w.Write(new byte[13]);
        w.Write(FootId);
        w.Write(new byte[4]);
        int ofs = (int)(ms.Position & 0x0F);
        int pad = ((ofs + 15) & ~0x0F) - ofs;
        w.Write(new byte[pad == 0 ? 16 : pad]);
        w.Write(Version);
        w.Write(new byte[120]);
        w.Write(FootMagic);
        File.WriteAllBytes(path, ms.ToArray());
    }
}
