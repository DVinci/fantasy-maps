using FantasyMaps.Core.Mesh;

namespace FantasyMaps.Core.Terrain;

public static class HeightPrimitives
{
    public static HeightField Zero(VoronoiMesh mesh)
        => new(new float[mesh.Vxs.Length], mesh);

    public static HeightField Map(HeightField h, Func<double, double, int, float> f)
    {
        var result = Zero(h.Mesh);
        for (int i = 0; i < h.Length; i++)
        {
            var vx = h.Mesh.Vxs[i];
            result.Values[i] = f(vx[0], vx[1], i);
        }
        return result;
    }

    public static HeightField Map(HeightField h, Func<float, float> f)
    {
        var result = Zero(h.Mesh);
        for (int i = 0; i < h.Length; i++) result.Values[i] = f(h.Values[i]);
        return result;
    }

    public static HeightField Slope(VoronoiMesh mesh, double[] direction)
        => Map(Zero(mesh), (x, y, _) => (float)(x * direction[0] + y * direction[1]));

    public static HeightField Cone(VoronoiMesh mesh, double strength)
        => Map(Zero(mesh), (x, y, _) => (float)(Math.Sqrt(x * x + y * y) * strength));

    public static HeightField Mountains(VoronoiMesh mesh, int n, double r = 0.05)
    {
        var result = Zero(mesh);
        for (int k = 0; k < n; k++)
        {
            double cx = (Random.Shared.NextDouble() - 0.5) * mesh.Extent.Width;
            double cy = (Random.Shared.NextDouble() - 0.5) * mesh.Extent.Height;
            for (int i = 0; i < mesh.Vxs.Length; i++)
            {
                double dx = mesh.Vxs[i][0] - cx, dy = mesh.Vxs[i][1] - cy;
                result.Values[i] += (float)Math.Exp(-(dx * dx + dy * dy) / (2 * r * r));
            }
        }
        return result;
    }

    public static HeightField Normalize(HeightField h)
    {
        float lo = h.Values.Min(), hi = h.Values.Max();
        float range = hi - lo;
        if (range < 1e-9f) return h.Clone();
        return Map(h, v => (v - lo) / range);
    }

    public static HeightField Peaky(HeightField h)
        => Map(h, v => Math.Abs(v));

    public static HeightField Add(params HeightField[] fields)
    {
        var result = Zero(fields[0].Mesh);
        foreach (var h in fields)
            for (int i = 0; i < h.Length; i++) result.Values[i] += h.Values[i];
        return result;
    }

    public static HeightField Relax(HeightField h)
    {
        var result = Zero(h.Mesh);
        for (int i = 0; i < h.Length; i++)
        {
            var nbs = h.Mesh.Neighbours(i);
            if (nbs.Length == 0) { result.Values[i] = h.Values[i]; continue; }
            float sum = h.Values[i];
            foreach (int nb in nbs) sum += h.Values[nb];
            result.Values[i] = sum / (nbs.Length + 1);
        }
        return result;
    }

    public static float Quantile(HeightField h, double q)
    {
        var sorted = h.Values.OrderBy(v => v).ToArray();
        int idx = Math.Clamp((int)(q * sorted.Length), 0, sorted.Length - 1);
        return sorted[idx];
    }
}
