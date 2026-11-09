// Runs the real XenoDialAliens generator and writes every alien's triangles for render.py.
using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

static class Preview
{
    static void Main(string[] args)
    {
        string roster = args[0], outPath = args[1];
        string err;
        var aliens = XenoDialAliens.LoadRoster(out err, roster);
        if (err != null) { Console.Error.WriteLine(err); Environment.Exit(1); }
        var ci = CultureInfo.InvariantCulture;
        var sb = new StringBuilder("[");
        for (int i = 0; i < aliens.Count; i++)
        {
            var a = aliens[i];
            if (i > 0) sb.Append(',');
            sb.Append("{\"name\":\"").Append(a.name).Append("\",\"desc\":\"").Append(a.Description).Append("\",\"size\":\"")
              .Append(a.sizeName).Append("\",\"pieces\":[");
            var pieces = XenoDialAliens.Generate(a);
            for (int p = 0; p < pieces.Count; p++)
            {
                var pc = pieces[p];
                var m = pc.mesh;
                if (p > 0) sb.Append(',');
                sb.Append("{\"name\":\"").Append(pc.name).Append("\",\"v\":[");
                for (int k = 0; k < m.vertices.Length; k++)
                {
                    var v = m.vertices[k] + pc.origin; var n = m.normals[k]; var c = m.colors[k];
                    if (k > 0) sb.Append(',');
                    sb.Append(string.Format(ci, "{0:0.####},{1:0.####},{2:0.####},{3:0.###},{4:0.###},{5:0.###},{6:0.###},{7:0.###},{8:0.###},{9:0.##}",
                        v.x, v.y, v.z, n.x, n.y, n.z, c.r, c.g, c.b, c.a));
                }
                sb.Append("],\"t\":[").Append(string.Join(",", m.triangles)).Append("]}");
            }
            sb.Append("]}");
        }
        sb.Append(']');
        File.WriteAllText(outPath, sb.ToString());
        Console.WriteLine($"{aliens.Count} aliens -> {outPath}");
    }
}
