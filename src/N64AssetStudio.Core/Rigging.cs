namespace N64AssetStudio.Core;

public sealed class Limb
{
    public short JX, JY, JZ;
    public int Child = 0xFF, Sibling = 0xFF;
    public int DlOffset = -1; // -1 = no display list
}

/// <summary>A skeleton stored in the game's own data (Zelda 64 engine SkeletonHeader / FlexSkeletonHeader).</summary>
public sealed class SkeletonAsset
{
    public int Index, BlobIndex, Offset;
    public bool Flex;
    public List<Limb> Limbs = new();
    public string Name = "";
    public int DlCount => Limbs.Count(l => l.DlOffset >= 0);
}

/// <summary>
/// Skeleton recovery and automatic rigging.
/// <para>N64 characters are drawn as rigid parts, one matrix per part, so a rig is "one bone per part, every
/// vertex weighted 100% to its part's bone". Where the game's skeleton format is known (Zelda 64 engine) the
/// real skeleton is used; otherwise <see cref="AutoRig"/> derives one from the geometry.</para>
/// </summary>
public static class Rigging
{
    const int NoLimb = 0xFF;

    // ------------------------------------------------------------------ Zelda 64 skeletons
    static bool SegPtr(byte[] d, uint p, int size, out int off)
    {
        off = (int)(p & 0xFFFFFF);
        return (p >> 24) == 0x06 && off % 4 == 0 && off + size <= d.Length;
    }

    /// <summary>Find Zelda 64 (OoT/MM) skeleton headers in an object file (segment 0x06).</summary>
    public static List<SkeletonAsset> FindZeldaSkeletons(byte[] d, int blobIndex, ISet<int> dlOffsets)
    {
        var found = new List<SkeletonAsset>();
        var usedLimbArrays = new HashSet<int>();
        for (int i = 0; i + 8 <= d.Length; i += 4)
        {
            if (d[i] != 0x06 || d[i + 5] != 0 || d[i + 6] != 0 || d[i + 7] != 0) continue;
            int count = d[i + 4];
            if (count < 2 || count > 127) continue;
            if (!SegPtr(d, BE.U32(d, i), count * 4, out int arr) || usedLimbArrays.Contains(arr)) continue;
            var limbs = new List<Limb>();
            bool ok = true;
            for (int k = 0; k < count && ok; k++)
            {
                if (!SegPtr(d, BE.U32(d, arr + k * 4), 12, out int lo)) { ok = false; break; }
                var l = new Limb
                {
                    JX = BE.S16(d, lo), JY = BE.S16(d, lo + 2), JZ = BE.S16(d, lo + 4),
                    Child = d[lo + 6], Sibling = d[lo + 7],
                };
                uint dl = BE.U32(d, lo + 8);
                if (dl != 0)
                {
                    if (!SegPtr(d, dl, 8, out int dlo)) { ok = false; break; }
                    l.DlOffset = dlo;
                }
                if ((l.Child != NoLimb && l.Child >= count) || (l.Sibling != NoLimb && l.Sibling >= count)) ok = false;
                limbs.Add(l);
            }
            if (!ok) continue;
            // must form a tree rooted at limb 0 that reaches every limb exactly once
            var seen = new bool[count];
            var stack = new Stack<int>();
            stack.Push(0);
            int visited = 0;
            while (stack.Count > 0 && ok)
            {
                int k = stack.Pop();
                if (seen[k]) { ok = false; break; }
                seen[k] = true;
                visited++;
                if (limbs[k].Sibling != NoLimb) stack.Push(limbs[k].Sibling);
                if (limbs[k].Child != NoLimb) stack.Push(limbs[k].Child);
            }
            if (!ok || visited != count || limbs[0].Sibling != NoLimb) continue;
            var dls = limbs.Where(l => l.DlOffset >= 0).Select(l => l.DlOffset).ToList();
            if (dls.Count == 0 || dls.Count(dlOffsets.Contains) * 2 < dls.Count) continue;
            bool flex = i + 12 <= d.Length && d[i + 8] == dls.Count && d[i + 9] == 0 && d[i + 10] == 0 && d[i + 11] == 0;
            usedLimbArrays.Add(arr);
            found.Add(new SkeletonAsset
            {
                BlobIndex = blobIndex, Offset = i, Flex = flex, Limbs = limbs,
                Name = $"skeleton @ 0x{i:X} ({count} limbs{(flex ? ", flex" : "")})",
            });
        }
        return found;
    }

    /// <summary>Limbs in draw order (depth-first: child before sibling), which is also the order of the
    /// per-limb matrices the game writes to segment 0x0D.</summary>
    static List<int> DrawOrder(List<Limb> limbs)
    {
        var order = new List<int>();
        void Visit(int k)
        {
            while (k != NoLimb)
            {
                order.Add(k);
                if (limbs[k].Child != NoLimb) Visit(limbs[k].Child);
                k = limbs[k].Sibling;
            }
        }
        Visit(0);
        return order;
    }

    /// <summary>Assemble a skeleton's limb display lists into one skinned mesh in bind pose.</summary>
    public static Mesh BuildSkeletonMesh(byte[] d, SkeletonAsset s, IReadOnlyDictionary<int, DisplayList> dls, string name)
    {
        var limbs = s.Limbs;
        var bones = new Bone?[limbs.Count];
        void Place(int k, int parent, float px, float py, float pz)
        {
            while (k != NoLimb)
            {
                var l = limbs[k];
                float x = px + l.JX, y = py + l.JY, z = pz + l.JZ;
                bones[k] = new Bone($"limb_{k:D2}", parent, x, y, z);
                if (l.Child != NoLimb) Place(l.Child, k, x, y, z);
                k = l.Sibling;
            }
        }
        Place(0, -1, 0, 0, 0);

        var order = DrawOrder(limbs);
        var matrixToLimb = order.Where(k => limbs[k].DlOffset >= 0).ToList();
        var mesh = new Mesh(name) { Bones = bones.Select(b => b!).ToList(), RigSource = "game" };
        foreach (int k in order)
        {
            var l = limbs[k];
            if (l.DlOffset < 0) continue;
            var ucode = dls.TryGetValue(l.DlOffset, out var known) ? known.Ucode : Ucode.F3DEX2;
            var interp = new F3D.DisplayListInterpreter(d, ucode, 0, 0x06, null)
            {
                DefaultBone = k,
                MatrixBone = addr => (addr >> 24) == 0x0D && (int)(addr & 0xFFFFFF) / 0x40 < matrixToLimb.Count
                    ? matrixToLimb[(int)(addr & 0xFFFFFF) / 0x40] : null,
            };
            var part = interp.Run(l.DlOffset, $"limb_{k:D2}");
            int vo = mesh.Vertices.Count, mo = mesh.Materials.Count;
            foreach (var v0 in part.Vertices)
            {
                var v = v0;
                var b = mesh.Bones[v.Bone];
                v.X += b.X; v.Y += b.Y; v.Z += b.Z; // limb vertices are stored relative to their joint
                mesh.Vertices.Add(v);
            }
            mesh.Materials.AddRange(part.Materials);
            foreach (var (a, b2, c, m) in part.Triangles) mesh.Triangles.Add((a + vo, b2 + vo, c + vo, m + mo));
            mesh.HasColors |= part.HasColors;
            mesh.HasNormals |= part.HasNormals;
            mesh.Notes.AddRange(part.Notes.Select(n => $"limb {k}: {n}"));
        }
        mesh.Notes.Insert(0, $"Zelda 64 {(s.Flex ? "flex " : "")}skeleton @ 0x{s.Offset:X}: {limbs.Count} limbs, {s.DlCount} with geometry");
        return mesh;
    }

    // ------------------------------------------------------------------ automatic rigging
    sealed class Part
    {
        public List<int> Verts = new();
        public int Tris;
        public float MinX = float.MaxValue, MinY = float.MaxValue, MinZ = float.MaxValue;
        public float MaxX = float.MinValue, MaxY = float.MinValue, MaxZ = float.MinValue;
        public float CX => (MinX + MaxX) / 2;
        public float CY => (MinY + MaxY) / 2;
        public float CZ => (MinZ + MaxZ) / 2;

        public void Add(Vertex v)
        {
            MinX = Math.Min(MinX, v.X); MinY = Math.Min(MinY, v.Y); MinZ = Math.Min(MinZ, v.Z);
            MaxX = Math.Max(MaxX, v.X); MaxY = Math.Max(MaxY, v.Y); MaxZ = Math.Max(MaxZ, v.Z);
        }

        public void Absorb(Part o)
        {
            Verts.AddRange(o.Verts);
            Tris += o.Tris;
            MinX = Math.Min(MinX, o.MinX); MinY = Math.Min(MinY, o.MinY); MinZ = Math.Min(MinZ, o.MinZ);
            MaxX = Math.Max(MaxX, o.MaxX); MaxY = Math.Max(MaxY, o.MaxY); MaxZ = Math.Max(MaxZ, o.MaxZ);
        }

        /// <summary>Gap between bounding boxes (0 when they touch), with centre distance as a tie-breaker.</summary>
        public double Distance(Part o)
        {
            double dx = Math.Max(0, Math.Max(MinX - o.MaxX, o.MinX - MaxX));
            double dy = Math.Max(0, Math.Max(MinY - o.MaxY, o.MinY - MaxY));
            double dz = Math.Max(0, Math.Max(MinZ - o.MaxZ, o.MinZ - MaxZ));
            double cx = CX - o.CX, cy = CY - o.CY, cz = CZ - o.CZ;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz) + 1e-3 * Math.Sqrt(cx * cx + cy * cy + cz * cz);
        }

        public double DistanceToBox(Vertex v)
        {
            double dx = Math.Max(0, Math.Max(MinX - v.X, v.X - MaxX));
            double dy = Math.Max(0, Math.Max(MinY - v.Y, v.Y - MaxY));
            double dz = Math.Max(0, Math.Max(MinZ - v.Z, v.Z - MaxZ));
            return dx * dx + dy * dy + dz * dz;
        }
    }

    static int Find(int[] p, int x)
    {
        while (p[x] != x) { p[x] = p[p[x]]; x = p[x]; }
        return x;
    }

    /// <summary>
    /// Automatic rig: splits the mesh into its rigid parts (connected pieces after welding coincident
    /// vertices), makes the biggest part the body, links the other parts into a tree by proximity, puts each
    /// joint where a part meets its parent and binds every vertex 100% to its part's bone.
    /// Bone 0 is always a "root" bone at ground level under the model.
    /// </summary>
    public static void AutoRig(Mesh mesh, int maxBones = 64, double minPartFraction = 0.01)
    {
        if (mesh.IsRigged || mesh.Vertices.Count == 0 || mesh.Triangles.Count == 0) return;
        var vs = mesh.Vertices;
        int n = vs.Count;
        var parent = Enumerable.Range(0, n).ToArray();
        void Union(int a, int b) { a = Find(parent, a); b = Find(parent, b); if (a != b) parent[a] = b; }
        foreach (var (a, b, c, _) in mesh.Triangles) { Union(a, b); Union(b, c); }
        var weld = new Dictionary<(int, int, int), int>();
        for (int i = 0; i < n; i++)
        {
            var key = ((int)MathF.Round(vs[i].X), (int)MathF.Round(vs[i].Y), (int)MathF.Round(vs[i].Z));
            if (weld.TryGetValue(key, out int j)) Union(i, j); else weld[key] = i;
        }
        var byRoot = new Dictionary<int, Part>();
        for (int i = 0; i < n; i++)
        {
            int r = Find(parent, i);
            if (!byRoot.TryGetValue(r, out var p)) byRoot[r] = p = new Part();
            p.Verts.Add(i);
            p.Add(vs[i]);
        }
        foreach (var (a, _, _, _) in mesh.Triangles) byRoot[Find(parent, a)].Tris++;
        var parts = byRoot.Values.Where(p => p.Tris > 0).ToList();
        foreach (var orphan in byRoot.Values.Where(p => p.Tris == 0))
            if (parts.Count > 0) parts.OrderBy(p => p.Distance(orphan)).First().Absorb(orphan);

        // fold tiny pieces (eyes, buttons, decals) and any excess into their nearest neighbour
        int total = mesh.Triangles.Count;
        int minTris = Math.Max(2, (int)(total * minPartFraction));
        var big = parts.Where(p => p.Tris >= minTris).ToList();
        if (big.Count == 0) big.Add(parts.OrderByDescending(p => p.Tris).First());
        foreach (var small in parts.Where(p => !big.Contains(p)).ToList())
            big.OrderBy(p => p.Distance(small)).First().Absorb(small);
        parts = big;
        while (parts.Count > maxBones - 1)
        {
            var smallest = parts.OrderBy(p => p.Tris).First();
            parts.Remove(smallest);
            parts.OrderBy(p => p.Distance(smallest)).First().Absorb(smallest);
        }

        // hierarchy: Prim's minimum spanning tree from the biggest part (usually the torso)
        var body = parts.OrderByDescending(p => p.Tris).First();
        var order = new List<Part> { body };
        var parentOf = new Dictionary<Part, Part>();
        var rest = parts.Where(p => p != body).ToList();
        while (rest.Count > 0)
        {
            Part? bestChild = null, bestParent = null;
            double best = double.MaxValue;
            foreach (var c in rest)
                foreach (var p in order)
                {
                    double dist = c.Distance(p);
                    if (dist < best) { best = dist; bestChild = c; bestParent = p; }
                }
            parentOf[bestChild!] = bestParent!;
            order.Add(bestChild!);
            rest.Remove(bestChild!);
        }

        float minY = vs.Min(v => v.Y);
        float sizeX = Math.Max(1, vs.Max(v => v.X) - vs.Min(v => v.X)), sizeY = Math.Max(1, vs.Max(v => v.Y) - minY);
        mesh.Bones = new List<Bone> { new("root", -1, body.CX, minY, body.CZ), new("body", 0, body.CX, body.CY, body.CZ) };
        var boneOf = new Dictionary<Part, int> { [body] = 1 };
        int num = 1;
        foreach (var p in order.Skip(1))
        {
            var par = parentOf[p];
            // joint = centre of the region where this part meets its parent (vertices nearest the parent's bounds)
            var dists = p.Verts.Select(i => (v: vs[i], d: Math.Sqrt(par.DistanceToBox(vs[i])))).ToList();
            double dmin = dists.Min(x => x.d);
            double tol = 0.08 * Math.Sqrt(Math.Pow(p.MaxX - p.MinX, 2) + Math.Pow(p.MaxY - p.MinY, 2) + Math.Pow(p.MaxZ - p.MinZ, 2));
            var contact = dists.Where(x => x.d <= dmin + tol).Select(x => x.v).ToList();
            var joint = new Vertex(contact.Average(v => v.X), contact.Average(v => v.Y), contact.Average(v => v.Z));
            float dx = (p.CX - body.CX) / sizeX, dy = (p.CY - body.CY) / sizeY;
            string where = Math.Abs(dy) >= Math.Abs(dx) ? (dy >= 0 ? "upper" : "lower") : "side";
            string side = Math.Abs(dx) > 0.08 ? (dx > 0 ? "_L" : "_R") : "";
            mesh.Bones.Add(new Bone($"part{num++:D2}_{where}{side}", boneOf[par], joint.X, joint.Y, joint.Z));
            boneOf[p] = mesh.Bones.Count - 1;
        }
        foreach (var p in parts)
            foreach (var i in p.Verts)
            {
                var v = vs[i];
                v.Bone = boneOf[p];
                vs[i] = v;
            }
        mesh.RigSource = "auto";
        mesh.Notes.Add($"auto-rigged: {mesh.Bones.Count} bones from {parts.Count} rigid part(s)");
    }
}
