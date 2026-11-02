namespace N64AssetStudio.Core;

public sealed class Material
{
    public string Name = "";
    public RgbaImage? Texture;
    public float[] Color = { 1, 1, 1, 1 };
    public string WrapS = "repeat", WrapT = "repeat"; // repeat | mirror | clamp
    public string TextureInfo = "";

    public bool HasAlpha => Texture?.HasAlpha ?? Color[3] < 1f;
}

public struct Vertex
{
    public float X, Y, Z, U, V;
    public float R, G, B, A;
    public float NX, NY, NZ;

    public Vertex(float x, float y, float z, float u = 0, float v = 0)
    {
        X = x; Y = y; Z = z; U = u; V = v;
        R = G = B = A = 1;
        NX = 0; NY = 1; NZ = 0;
    }
}

public sealed class Mesh
{
    public string Name;
    public List<Vertex> Vertices = new();
    public List<(int A, int B, int C, int Mat)> Triangles = new();
    public List<Material> Materials = new();
    public bool HasNormals, HasColors;
    public List<string> Notes = new();

    public Mesh(string name) { Name = name; }

    public SortedDictionary<int, List<(int, int, int)>> TrianglesByMaterial()
    {
        var g = new SortedDictionary<int, List<(int, int, int)>>();
        foreach (var t in Triangles)
        {
            if (!g.TryGetValue(t.Mat, out var l)) g[t.Mat] = l = new();
            l.Add((t.A, t.B, t.C));
        }
        return g;
    }

    public void ComputeNormals()
    {
        var acc = new double[Vertices.Count * 3];
        foreach (var (a, b, c, _) in Triangles)
        {
            var va = Vertices[a]; var vb = Vertices[b]; var vc = Vertices[c];
            double ux = vb.X - va.X, uy = vb.Y - va.Y, uz = vb.Z - va.Z;
            double wx = vc.X - va.X, wy = vc.Y - va.Y, wz = vc.Z - va.Z;
            double nx = uy * wz - uz * wy, ny = uz * wx - ux * wz, nz = ux * wy - uy * wx;
            foreach (var i in new[] { a, b, c }) { acc[i * 3] += nx; acc[i * 3 + 1] += ny; acc[i * 3 + 2] += nz; }
        }
        for (int i = 0; i < Vertices.Count; i++)
        {
            double l = Math.Sqrt(acc[i * 3] * acc[i * 3] + acc[i * 3 + 1] * acc[i * 3 + 1] + acc[i * 3 + 2] * acc[i * 3 + 2]);
            if (l == 0) l = 1;
            var v = Vertices[i];
            v.NX = (float)(acc[i * 3] / l); v.NY = (float)(acc[i * 3 + 1] / l); v.NZ = (float)(acc[i * 3 + 2] / l);
            Vertices[i] = v;
        }
        HasNormals = true;
    }

    public Mesh MergedWith(Mesh other, string? name = null)
    {
        var m = new Mesh(name ?? Name)
        {
            Vertices = new(Vertices), Triangles = new(Triangles), Materials = new(Materials),
            HasNormals = HasNormals && other.HasNormals, HasColors = HasColors || other.HasColors,
            Notes = Notes.Concat(other.Notes).ToList(),
        };
        int vo = m.Vertices.Count, mo = m.Materials.Count;
        m.Vertices.AddRange(other.Vertices);
        m.Materials.AddRange(other.Materials);
        foreach (var (a, b, c, mat) in other.Triangles) m.Triangles.Add((a + vo, b + vo, c + vo, mat + mo));
        return m;
    }
}
