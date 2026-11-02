namespace N64AssetStudio.Core.Render;

/// <summary>Small z-buffered software rasteriser for model previews (no GPU / OpenGL needed).</summary>
public sealed class MeshView
{
    public enum Mode { Textured, Solid, Wire }

    readonly Mesh _mesh;
    readonly double[] _cx = new double[3];
    readonly double _radius;
    readonly double[][] _matColor;
    public double Yaw = 0.61, Pitch = -0.35, Zoom = 1.0, PanX, PanY;

    static readonly byte[] Bg = { 40, 42, 48 };

    public MeshView(Mesh mesh)
    {
        _mesh = mesh;
        if (mesh.Vertices.Count > 0)
        {
            double[] lo = { double.MaxValue, double.MaxValue, double.MaxValue }, hi = { double.MinValue, double.MinValue, double.MinValue };
            foreach (var v in mesh.Vertices)
            {
                lo[0] = Math.Min(lo[0], v.X); lo[1] = Math.Min(lo[1], v.Y); lo[2] = Math.Min(lo[2], v.Z);
                hi[0] = Math.Max(hi[0], v.X); hi[1] = Math.Max(hi[1], v.Y); hi[2] = Math.Max(hi[2], v.Z);
            }
            for (int i = 0; i < 3; i++) _cx[i] = (lo[i] + hi[i]) / 2;
            _radius = Math.Sqrt(Enumerable.Range(0, 3).Sum(i => (hi[i] - lo[i]) * (hi[i] - lo[i]))) / 2;
        }
        if (_radius <= 0) _radius = 1;
        _matColor = mesh.Materials.Select(m =>
        {
            if (m.Texture == null) return new double[] { m.Color[0], m.Color[1], m.Color[2] };
            double r = 0, g = 0, b = 0, a = 0;
            var p = m.Texture.Pixels;
            for (int i = 0; i < p.Length; i += 4) { double al = p[i + 3] / 255.0; r += p[i] * al; g += p[i + 1] * al; b += p[i + 2] * al; a += al; }
            a = Math.Max(a, 1e-6);
            return new[] { r / a / 255 * m.Color[0], g / a / 255 * m.Color[1], b / a / 255 * m.Color[2] };
        }).ToArray();
    }

    public void Reset() { Yaw = 0.61; Pitch = -0.35; Zoom = 1; PanX = PanY = 0; }

    public RgbaImage Render(int w, int h, Mode mode, bool edges = false)
    {
        w = Math.Max(w, 16); h = Math.Max(h, 16);
        var img = new RgbaImage(w, h);
        var px = img.Pixels;
        for (int i = 0; i < px.Length; i += 4) { px[i] = Bg[0]; px[i + 1] = Bg[1]; px[i + 2] = Bg[2]; px[i + 3] = 255; }
        var vs = _mesh.Vertices;
        if (_mesh.Triangles.Count == 0) return img;

        double cy = Math.Cos(Yaw), sy = Math.Sin(Yaw), cp = Math.Cos(Pitch), sp = Math.Sin(Pitch);
        double s = Math.Min(w, h) * 0.45 / _radius * Zoom;
        int n = vs.Count;
        var sx = new double[n]; var syy = new double[n]; var depth = new double[n];
        var rx = new double[n]; var ry = new double[n]; var rz = new double[n];
        for (int i = 0; i < n; i++)
        {
            double x = vs[i].X - _cx[0], y = vs[i].Y - _cx[1], z = vs[i].Z - _cx[2];
            double x1 = cy * x + sy * z, z1 = -sy * x + cy * z;
            double y2 = cp * y - sp * z1, z2 = sp * y + cp * z1;
            rx[i] = x1; ry[i] = y2; rz[i] = z2;
            sx[i] = x1 * s + w / 2.0 + PanX;
            syy[i] = -y2 * s + h / 2.0 + PanY;
            depth[i] = z2;
        }

        if (mode == Mode.Wire)
        {
            foreach (var t in _mesh.Triangles) DrawTri(img, sx, syy, t.A, t.B, t.C, 120, 200, 255);
            return img;
        }

        var zbuf = new double[w * h];
        Array.Fill(zbuf, double.MaxValue);
        double lx = 0.3, ly = 0.5, lz = -0.8, ll = Math.Sqrt(lx * lx + ly * ly + lz * lz);
        lx /= ll; ly /= ll; lz /= ll;

        foreach (var (a, b, c, m) in _mesh.Triangles)
        {
            double ux = rx[b] - rx[a], uy = ry[b] - ry[a], uz = rz[b] - rz[a];
            double wx = rx[c] - rx[a], wy = ry[c] - ry[a], wz = rz[c] - rz[a];
            double nx = uy * wz - uz * wy, ny = uz * wx - ux * wz, nz = ux * wy - uy * wx;
            double nl = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            double light = nl > 0 ? 0.35 + 0.65 * Math.Abs((nx * lx + ny * ly + nz * lz) / nl) : 0.35;

            double x0 = sx[a], y0 = syy[a], x1 = sx[b], y1 = syy[b], x2 = sx[c], y2 = syy[c];
            double area = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0);
            if (Math.Abs(area) < 1e-9) continue;
            int minX = Math.Max(0, (int)Math.Floor(Math.Min(x0, Math.Min(x1, x2))));
            int maxX = Math.Min(w - 1, (int)Math.Ceiling(Math.Max(x0, Math.Max(x1, x2))));
            int minY = Math.Max(0, (int)Math.Floor(Math.Min(y0, Math.Min(y1, y2))));
            int maxY = Math.Min(h - 1, (int)Math.Ceiling(Math.Max(y0, Math.Max(y1, y2))));
            if (minX > maxX || minY > maxY) continue;

            var mat = m < _mesh.Materials.Count ? _mesh.Materials[m] : null;
            var tex = mode == Mode.Textured ? mat?.Texture : null;
            var flat = m < _matColor.Length ? _matColor[m] : new[] { 0.8, 0.8, 0.8 };
            Vertex va = vs[a], vb = vs[b], vc = vs[c];

            for (int py = minY; py <= maxY; py++)
            {
                double fy = py + 0.5;
                for (int pxl = minX; pxl <= maxX; pxl++)
                {
                    double fx = pxl + 0.5;
                    double w0 = ((x1 - fx) * (y2 - fy) - (x2 - fx) * (y1 - fy)) / area;
                    double w1 = ((x2 - fx) * (y0 - fy) - (x0 - fx) * (y2 - fy)) / area;
                    double w2 = 1 - w0 - w1;
                    if (w0 < -1e-6 || w1 < -1e-6 || w2 < -1e-6) continue;
                    double z = w0 * depth[a] + w1 * depth[b] + w2 * depth[c];
                    int zi = py * w + pxl;
                    if (z >= zbuf[zi]) continue;
                    double r, g, bl;
                    double vr = w0 * va.R + w1 * vb.R + w2 * vc.R, vg = w0 * va.G + w1 * vb.G + w2 * vc.G, vb2 = w0 * va.B + w1 * vb.B + w2 * vc.B;
                    if (tex != null)
                    {
                        double u = w0 * va.U + w1 * vb.U + w2 * vc.U, v = w0 * va.V + w1 * vb.V + w2 * vc.V;
                        int tx = (int)Math.Floor(u * tex.Width) % tex.Width; if (tx < 0) tx += tex.Width;
                        int ty = (int)Math.Floor(v * tex.Height) % tex.Height; if (ty < 0) ty += tex.Height;
                        int ti = (ty * tex.Width + tx) * 4;
                        if (tex.Pixels[ti + 3] < 77) continue;
                        r = tex.Pixels[ti] / 255.0 * mat!.Color[0]; g = tex.Pixels[ti + 1] / 255.0 * mat.Color[1]; bl = tex.Pixels[ti + 2] / 255.0 * mat.Color[2];
                    }
                    else { r = flat[0]; g = flat[1]; bl = flat[2]; }
                    zbuf[zi] = z;
                    int o = zi * 4;
                    px[o] = (byte)Math.Clamp(r * vr * light * 255, 0, 255);
                    px[o + 1] = (byte)Math.Clamp(g * vg * light * 255, 0, 255);
                    px[o + 2] = (byte)Math.Clamp(bl * vb2 * light * 255, 0, 255);
                }
            }
        }
        if (edges) foreach (var t in _mesh.Triangles) DrawTri(img, sx, syy, t.A, t.B, t.C, 20, 20, 20);
        return img;
    }

    static void DrawTri(RgbaImage img, double[] x, double[] y, int a, int b, int c, byte r, byte g, byte bl)
    {
        Line(img, x[a], y[a], x[b], y[b], r, g, bl);
        Line(img, x[b], y[b], x[c], y[c], r, g, bl);
        Line(img, x[c], y[c], x[a], y[a], r, g, bl);
    }

    public static void Line(RgbaImage img, double x0, double y0, double x1, double y1, byte r, byte g, byte b)
    {
        double dx = x1 - x0, dy = y1 - y0;
        int steps = (int)Math.Min(4000, Math.Max(Math.Abs(dx), Math.Abs(dy))) + 1;
        for (int i = 0; i <= steps; i++)
        {
            int px = (int)(x0 + dx * i / steps), py = (int)(y0 + dy * i / steps);
            if (px >= 0 && py >= 0 && px < img.Width && py < img.Height) img.Set(px, py, r, g, b, 255);
        }
    }

    public static RgbaImage Waveform(short[] pcm, int w, int h)
    {
        w = Math.Max(16, w); h = Math.Max(16, h);
        var img = new RgbaImage(w, h);
        for (int i = 0; i < img.Pixels.Length; i += 4) { img.Pixels[i] = 24; img.Pixels[i + 1] = 26; img.Pixels[i + 2] = 30; img.Pixels[i + 3] = 255; }
        Line(img, 0, h / 2, w, h / 2, 60, 60, 70);
        if (pcm.Length == 0) return img;
        for (int x = 0; x < w; x++)
        {
            int a = (int)((long)pcm.Length * x / w), b = Math.Max(a + 1, (int)((long)pcm.Length * (x + 1) / w));
            short lo = short.MaxValue, hi = short.MinValue;
            for (int i = a; i < Math.Min(b, pcm.Length); i++) { lo = Math.Min(lo, pcm[i]); hi = Math.Max(hi, pcm[i]); }
            Line(img, x, h / 2.0 - hi / 32768.0 * h / 2 * 0.95, x, h / 2.0 - lo / 32768.0 * h / 2 * 0.95, 90, 200, 140);
        }
        return img;
    }

    public static RgbaImage Checker(RgbaImage img, int cell = 8)
    {
        var o = new RgbaImage(img.Width, img.Height);
        for (int y = 0; y < img.Height; y++)
            for (int x = 0; x < img.Width; x++)
            {
                int i = (y * img.Width + x) * 4;
                byte bg = ((x / cell + y / cell) & 1) == 0 ? (byte)0xCC : (byte)0x99;
                int a = img.Pixels[i + 3];
                for (int k = 0; k < 3; k++) o.Pixels[i + k] = (byte)((img.Pixels[i + k] * a + bg * (255 - a)) / 255);
                o.Pixels[i + 3] = 255;
            }
        return o;
    }
}
