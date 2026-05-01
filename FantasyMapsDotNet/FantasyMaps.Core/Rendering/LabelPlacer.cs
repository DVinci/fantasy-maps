using System.Text;
using FantasyMaps.Core.Language;
using FantasyMaps.Core.Terrain;

namespace FantasyMaps.Core.Rendering;

public static class LabelPlacer
{
    private static double LabelPenalty(
        double lx, double ly, double w, double h,
        List<double[][]> paths, List<(double Lx, double Ly, double W, double H)> existing)
    {
        double penalty = 0;
        const double Scale = SvgBuilder.Scale;

        if (lx < -0.45 || lx + w / Scale > 0.45 || ly < -0.45 || ly + h / Scale > 0.45)
            penalty += 10000;

        foreach (var path in paths)
            foreach (var pt in path)
            {
                double dx = pt[0] - lx - w / (2 * Scale);
                double dy = pt[1] - ly - h / (2 * Scale);
                double dist2 = dx * dx + dy * dy;
                if (dist2 < 1e-9) penalty += 500;
                else penalty += Math.Max(0, 0.01 - dist2) * 200;
            }

        foreach (var (ex, ey, ew, eh) in existing)
        {
            double overlapX = Math.Max(0, Math.Min(lx + w / Scale, ex + ew / Scale) - Math.Max(lx, ex));
            double overlapY = Math.Max(0, Math.Min(ly + h / Scale, ey + eh / Scale) - Math.Max(ly, ey));
            if (overlapX > 0 && overlapY > 0) penalty += 1000 * overlapX * overlapY;
        }

        return penalty;
    }

    public static string DrawLabels(RenderState render, LanguageModel lang)
    {
        var sb = new StringBuilder();
        var placed = new List<(double Lx, double Ly, double W, double H)>();
        var allPaths = render.Coasts.Concat(render.Rivers).Concat(render.Borders).ToList();
        var h = render.H;
        var cities = render.Cities;
        var fontsizes = render.Params.Fontsizes;
        int nterrs = render.Params.Nterrs;

        for (int ci = 0; ci < cities.Count; ci++)
        {
            int city = cities[ci];
            var vx = h.Mesh.Vxs[city];
            string name = NameGenerator.MakeName(lang, $"city{ci}");
            double fontSize = ci < nterrs ? fontsizes[0] : fontsizes[1];
            double approxW = name.Length * fontSize * 0.6;
            double approxH = fontSize;

            (double dx, double dy)[] offsets =
            [
                (10 / SvgBuilder.Scale, 0),
                (-approxW / SvgBuilder.Scale - 10 / SvgBuilder.Scale, 0),
                (-approxW / (2 * SvgBuilder.Scale), -approxH / SvgBuilder.Scale),
                (-approxW / (2 * SvgBuilder.Scale), approxH / SvgBuilder.Scale),
            ];

            double bestPenalty = double.MaxValue; int bestIdx = 0;
            for (int k = 0; k < offsets.Length; k++)
            {
                double penalty = LabelPenalty(vx[0] + offsets[k].dx, vx[1] + offsets[k].dy,
                    approxW, approxH, allPaths, placed);
                if (penalty < bestPenalty) { bestPenalty = penalty; bestIdx = k; }
            }

            double lx = vx[0] + offsets[bestIdx].dx;
            double ly = vx[1] + offsets[bestIdx].dy;
            placed.Add((lx, ly, approxW, approxH));

            string textStyle = $"font-family:'Palatino Linotype',Palatino,Georgia,serif;font-size:{fontSize}px;" +
                "fill:#000;stroke:white;stroke-width:3;paint-order:stroke;text-anchor:start";
            sb.AppendLine(SvgBuilder.Text(lx, ly, name, "city", textStyle));
        }

        for (int ti = 0; ti < Math.Min(nterrs, cities.Count); ti++)
        {
            int cityVx = cities[ti];
            string regionName = NameGenerator.MakeName(lang, $"region{ti}").ToUpper();
            double fontSize = fontsizes[2];
            double approxW = regionName.Length * fontSize * 0.6;
            double approxH = fontSize;

            double bestPenalty = double.MaxValue; double bx = 0, by = 0;
            for (int i = 0; i < h.Length; i++)
            {
                if (render.Terr.Length > 0 && render.Terr[i] != cityVx) continue;
                if (h[i] <= 0f) continue;
                var vx = h.Mesh.Vxs[i];
                double penalty = LabelPenalty(vx[0] - approxW / (2 * SvgBuilder.Scale),
                    vx[1] - approxH / (2 * SvgBuilder.Scale), approxW, approxH, allPaths, placed);
                if (penalty < bestPenalty) { bestPenalty = penalty; bx = vx[0]; by = vx[1]; }
            }

            double lx2 = bx - approxW / (2 * SvgBuilder.Scale);
            double ly2 = by - approxH / (2 * SvgBuilder.Scale);
            placed.Add((lx2, ly2, approxW, approxH));
            string regionStyle = $"font-family:'Palatino Linotype',Palatino,Georgia,serif;font-size:{fontSize}px;" +
                "fill:#8a4;font-style:italic;stroke:white;stroke-width:2;paint-order:stroke;text-anchor:middle";
            sb.AppendLine(SvgBuilder.Text(bx, by, regionName, "region", regionStyle));
        }
        return sb.ToString();
    }
}
