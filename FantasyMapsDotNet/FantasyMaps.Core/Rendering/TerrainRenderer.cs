using System.Text;
using FantasyMaps.Core.Terrain;

namespace FantasyMaps.Core.Rendering;

public static class TerrainRenderer
{
    public static string VisualizeVoronoi(HeightField field, float? lo = null, float? hi = null)
    {
        float loVal = lo ?? field.Values.Min() - 1e-9f;
        float hiVal = hi ?? field.Values.Max() + 1e-9f;
        float range = hiVal - loVal;
        var sb = new StringBuilder();

        for (int i = 0; i < field.Mesh.Vxs.Length; i++)
        {
            var triPts = field.Mesh.Tris[i];
            if (triPts == null || triPts.Length < 3) continue;
            float t = range > 1e-9f ? Math.Clamp((field[i] - loVal) / range, 0f, 1f) : 0f;
            string color = ViridisColor.Interpolate(t);
            sb.AppendLine(SvgBuilder.FilledPath(triPts, color, "field"));
        }
        return sb.ToString();
    }

    public static string DrawPaths(List<double[][]> paths, string cssClass, string style = "")
    {
        var sb = new StringBuilder();
        foreach (var path in paths)
            sb.AppendLine(SvgBuilder.StrokedPath(path, cssClass, style));
        return sb.ToString();
    }

    public static string VisualizeSlopes(HeightField h)
    {
        var sb = new StringBuilder();
        double r = 0.25 / Math.Sqrt(h.Length);

        for (int i = 0; i < h.Length; i++)
        {
            if (h[i] <= 0f || h.Mesh.IsNearEdge(i)) continue;
            var nbs = h.Mesh.Neighbours(i).Concat([i]).ToArray();
            double s = 0, s2 = 0;
            foreach (int nb in nbs)
            {
                var (sx, sy) = Erosion.Trislope(h, nb);
                s += sx / 10; s2 += sy;
            }
            s /= nbs.Length; s2 /= nbs.Length;
            double absS = Math.Abs(s);
            double threshold = 0.1 + Random.Shared.NextDouble() * 0.3;
            if (absS < threshold) continue;

            double l = r * (1 + Random.Shared.NextDouble()) * (1 - 0.2 * Math.Pow(Math.Atan(s), 2))
                       * Math.Exp(s2 / 100);
            double x = h.Mesh.Vxs[i][0], y = h.Mesh.Vxs[i][1];

            if (Math.Abs(l * s) > 2 * r)
            {
                int n = Math.Min((int)Math.Abs(l * s / r), 4);
                l /= n;
                for (int j = 0; j < n; j++)
                {
                    double u = Rand.Normal() * r, v = Rand.Normal() * r;
                    sb.AppendLine(SvgBuilder.Line(x + u - l, y + v + l * s, x + u + l, y + v - l * s,
                        "slope", "stroke:#797;stroke-width:1;stroke-linecap:round"));
                }
            }
            else
            {
                sb.AppendLine(SvgBuilder.Line(x - l, y + l * s, x + l, y - l * s,
                    "slope", "stroke:#797;stroke-width:1;stroke-linecap:round"));
            }
        }
        return sb.ToString();
    }

    public static string VisualizeCities(RenderState render)
    {
        var sb = new StringBuilder();
        int n = render.Params.Nterrs;
        for (int idx = 0; idx < render.Cities.Count; idx++)
        {
            int city = render.Cities[idx];
            var vx = render.H.Mesh.Vxs[city];
            double radius = idx < n ? 10 : 4;
            sb.AppendLine(SvgBuilder.Circle(vx[0], vx[1], radius, "city",
                "fill:white;stroke:black;stroke-width:5;stroke-linecap:round"));
        }
        return sb.ToString();
    }
}
