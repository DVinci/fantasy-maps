namespace FantasyMaps.Core.Terrain;

public static class Erosion
{
    public static int[] Downhill(HeightField h)
    {
        if (h.DownhillCache != null) return h.DownhillCache;
        var downs = new int[h.Length];
        for (int i = 0; i < h.Length; i++)
        {
            if (h.Mesh.IsEdge(i)) { downs[i] = -2; continue; }
            int best = -1; float bestH = h[i];
            foreach (int nb in h.Mesh.Neighbours(i))
                if (h[nb] < bestH) { bestH = h[nb]; best = nb; }
            downs[i] = best;
        }
        h.DownhillCache = downs;
        return downs;
    }

    public static HeightField FillSinks(HeightField h, float epsilon = 1e-5f)
    {
        const float Infinity = 999999f;
        var newH = HeightPrimitives.Zero(h.Mesh);
        for (int i = 0; i < h.Length; i++)
            newH.Values[i] = h.Mesh.IsNearEdge(i) ? h[i] : Infinity;

        while (true)
        {
            bool changed = false;
            for (int i = 0; i < h.Length; i++)
            {
                if (newH[i] == h[i]) continue;
                foreach (int nb in h.Mesh.Neighbours(i))
                {
                    if (h[i] >= newH[nb] + epsilon) { newH.Values[i] = h[i]; changed = true; break; }
                    float oh = newH[nb] + epsilon;
                    if (newH[i] > oh && oh > h[i]) { newH.Values[i] = oh; changed = true; }
                }
            }
            if (!changed) return newH;
        }
    }

    public static HeightField GetFlux(HeightField h)
    {
        var dh = Downhill(h);
        var flux = HeightPrimitives.Zero(h.Mesh);
        var idxs = Enumerable.Range(0, h.Length).ToArray();
        Array.Sort(idxs, (a, b) => h[b].CompareTo(h[a]));
        float init = 1f / h.Length;
        for (int i = 0; i < h.Length; i++) flux.Values[i] = init;
        foreach (int j in idxs)
            if (dh[j] >= 0) flux.Values[dh[j]] += flux[j];
        return flux;
    }

    public static (double Sx, double Sy) Trislope(HeightField h, int i)
    {
        var nbs = h.Mesh.Neighbours(i);
        if (nbs.Length != 3) return (0, 0);
        var p0 = h.Mesh.Vxs[nbs[0]]; var p1 = h.Mesh.Vxs[nbs[1]]; var p2 = h.Mesh.Vxs[nbs[2]];
        double x1 = p1[0] - p0[0], x2 = p2[0] - p0[0];
        double y1 = p1[1] - p0[1], y2 = p2[1] - p0[1];
        double det = x1 * y2 - x2 * y1;
        if (Math.Abs(det) < 1e-10) return (0, 0);
        double h1 = h[nbs[1]] - h[nbs[0]], h2 = h[nbs[2]] - h[nbs[0]];
        return ((y2 * h1 - y1 * h2) / det, (-x2 * h1 + x1 * h2) / det);
    }

    public static HeightField GetSlope(HeightField h)
    {
        var sl = HeightPrimitives.Zero(h.Mesh);
        for (int i = 0; i < h.Length; i++)
        {
            var (sx, sy) = Trislope(h, i);
            sl.Values[i] = (float)Math.Sqrt(sx * sx + sy * sy);
        }
        return sl;
    }

    public static HeightField ErosionRate(HeightField h)
    {
        var flux = GetFlux(h); var sl = GetSlope(h);
        var result = HeightPrimitives.Zero(h.Mesh);
        for (int i = 0; i < h.Length; i++)
        {
            float river = (float)(Math.Sqrt(flux[i]) * sl[i]);
            float creep = sl[i] * sl[i];
            result.Values[i] = Math.Min(1000f * river + creep, 200f);
        }
        return result;
    }

    public static HeightField Erode(HeightField h, float amount)
    {
        var er = ErosionRate(h);
        float maxR = er.Values.Max();
        if (maxR < 1e-9f) return h.Clone();
        var result = HeightPrimitives.Zero(h.Mesh);
        for (int i = 0; i < h.Length; i++)
            result.Values[i] = h[i] - amount * (er[i] / maxR);
        return result;
    }

    public static HeightField DoErosion(HeightField h, float amount, int n = 1)
    {
        h = FillSinks(h);
        for (int i = 0; i < n; i++) { h = Erode(h, amount); h = FillSinks(h); }
        return h;
    }

    public static HeightField SetSeaLevel(HeightField h, double q)
    {
        float delta = HeightPrimitives.Quantile(h, q);
        var result = HeightPrimitives.Zero(h.Mesh);
        for (int i = 0; i < h.Length; i++) result.Values[i] = h[i] - delta;
        return result;
    }

    public static HeightField CleanCoast(HeightField h, int iters = 1)
    {
        for (int iter = 0; iter < iters; iter++)
        {
            var newH = h.Clone();
            for (int i = 0; i < h.Length; i++)
            {
                if (h[i] <= 0f) continue;
                var nbs = h.Mesh.Neighbours(i);
                if (nbs.Length != 3) continue;
                int landCount = 0; float bestOcean = -999999f;
                foreach (int nb in nbs)
                    if (h[nb] > 0f) landCount++; else if (h[nb] > bestOcean) bestOcean = h[nb];
                if (landCount > 1) continue;
                newH.Values[i] = bestOcean / 2f;
            }
            h = newH;
            newH = h.Clone();
            for (int i = 0; i < h.Length; i++)
            {
                if (h[i] > 0f) continue;
                var nbs = h.Mesh.Neighbours(i);
                if (nbs.Length != 3) continue;
                int oceanCount = 0; float bestLand = 999999f;
                foreach (int nb in nbs)
                    if (h[nb] <= 0f) oceanCount++; else if (h[nb] < bestLand) bestLand = h[nb];
                if (oceanCount > 1) continue;
                newH.Values[i] = bestLand / 2f;
            }
            h = newH;
        }
        return h;
    }
}
