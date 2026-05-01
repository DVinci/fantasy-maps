namespace FantasyMaps.Core.Terrain;

public static class Territories
{
    public static int[] GetTerritories(RenderState render)
    {
        var h = render.H;
        var cities = render.Cities;
        int n = Math.Min(render.Params.Nterrs, cities.Count);
        var flux = Erosion.GetFlux(h);
        var terr = new int[h.Length];
        Array.Fill(terr, -1);

        var queue = new PriorityQueue<(float Score, int City, int Vx), float>();

        float Weight(int u, int v)
        {
            double horiz = h.Mesh.Distance(u, v);
            float vert = h[v] - h[u];
            if (vert > 0f) vert /= 10f;
            float diff = 1f + 0.25f * (float)Math.Pow(vert / horiz, 2);
            diff += 100f * (float)Math.Sqrt(flux[u]);
            if (h[u] <= 0f) diff = 100f;
            if ((h[u] > 0f) != (h[v] > 0f)) return 1000f;
            return (float)(horiz * diff);
        }

        for (int i = 0; i < n; i++)
        {
            int city = cities[i];
            terr[city] = city;
            foreach (int nb in h.Mesh.Neighbours(city))
            {
                float w = Weight(city, nb);
                queue.Enqueue((w, city, nb), w);
            }
        }

        while (queue.Count > 0)
        {
            var (score, city, vx) = queue.Dequeue();
            if (terr[vx] >= 0) continue;
            terr[vx] = city;
            foreach (int nb in h.Mesh.Neighbours(vx))
            {
                if (terr[nb] >= 0) continue;
                float w = score + Weight(vx, nb);
                queue.Enqueue((w, city, nb), w);
            }
        }
        return terr;
    }

    public static List<double[][]> GetBorders(RenderState render)
    {
        var terr = render.Terr;
        var h = render.H;
        var segments = new List<double[][]>();

        foreach (var (v0, v1, left, right) in h.Mesh.Edges)
        {
            if (right == null) continue;
            if (h.Mesh.IsNearEdge(v0) || h.Mesh.IsNearEdge(v1)) continue;
            if (h[v0] < 0f || h[v1] < 0f) continue;
            if (terr[v0] != terr[v1] && left != null)
                segments.Add([left, right]);
        }
        return Rivers.MergeSegments(segments).Select(Rivers.RelaxPath).ToList();
    }
}
