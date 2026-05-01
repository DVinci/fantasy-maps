namespace FantasyMaps.Core.Terrain;

public static class Rivers
{
    public static List<double[][]> Contour(HeightField h, float level = 0f)
    {
        var segments = new List<double[][]>();
        foreach (var (v0, v1, left, right) in h.Mesh.Edges)
        {
            if (right == null) continue;
            if (h.Mesh.IsNearEdge(v0) || h.Mesh.IsNearEdge(v1)) continue;
            bool v0Above = h[v0] > level, v1Above = h[v1] > level;
            if (v0Above != v1Above && left != null)
                segments.Add([left, right]);
        }
        return MergeSegments(segments);
    }

    public static List<double[][]> GetRivers(HeightField h, float limit)
    {
        var dh = Erosion.Downhill(h);
        var flux = Erosion.GetFlux(h);
        int aboveCount = h.Values.Count(v => v > 0f);
        float adjustedLimit = limit * aboveCount / h.Length;

        var links = new List<double[][]>();
        for (int i = 0; i < h.Length; i++)
        {
            if (h.Mesh.IsNearEdge(i)) continue;
            if (flux[i] > adjustedLimit && h[i] > 0f && dh[i] >= 0)
            {
                var up = h.Mesh.Vxs[i];
                var downVx = h.Mesh.Vxs[dh[i]];
                links.Add(h[dh[i]] > 0f
                    ? [up, downVx]
                    : [up, [(up[0] + downVx[0]) / 2, (up[1] + downVx[1]) / 2]]);
            }
        }
        return MergeSegments(links).Select(RelaxPath).ToList();
    }

    public static List<double[][]> MergeSegments(List<double[][]> segs)
    {
        var adj = new Dictionary<string, List<string>>();
        var coordMap = new Dictionary<string, double[]>();

        string Key(double[] pt) => $"{pt[0]:R},{pt[1]:R}";
        void AddAdj(double[] a, double[] b)
        {
            string ka = Key(a), kb = Key(b);
            coordMap[ka] = a; coordMap[kb] = b;
            if (!adj.ContainsKey(ka)) adj[ka] = [];
            if (!adj.ContainsKey(kb)) adj[kb] = [];
            adj[ka].Add(kb); adj[kb].Add(ka);
        }
        foreach (var seg in segs) { if (seg.Length >= 2) AddAdj(seg[0], seg[^1]); }

        var done = new bool[segs.Count];
        var paths = new List<double[][]>();
        List<string>? path = null;

        while (true)
        {
            if (path == null)
            {
                int idx = Array.FindIndex(done, d => !d);
                if (idx < 0) break;
                done[idx] = true;
                path = [Key(segs[idx][0]), Key(segs[idx][^1])];
            }
            bool changed = false;
            for (int i = 0; i < segs.Count; i++)
            {
                if (done[i]) continue;
                string s0 = Key(segs[i][0]), s1 = Key(segs[i][^1]);
                string head = path[0], tail = path[^1];
                if (adj.TryGetValue(head, out var headAdj) && headAdj.Count == 2 && s1 == head)
                    { path.Insert(0, s0); done[i] = true; changed = true; break; }
                if (adj.TryGetValue(head, out headAdj) && headAdj.Count == 2 && s0 == head)
                    { path.Insert(0, s1); done[i] = true; changed = true; break; }
                if (adj.TryGetValue(tail, out var tailAdj) && tailAdj.Count == 2 && s0 == tail)
                    { path.Add(s1); done[i] = true; changed = true; break; }
                if (adj.TryGetValue(tail, out tailAdj) && tailAdj.Count == 2 && s1 == tail)
                    { path.Add(s0); done[i] = true; changed = true; break; }
            }
            if (!changed)
            {
                paths.Add(path.Select(k => coordMap[k]).ToArray());
                path = null;
            }
        }
        return paths;
    }

    public static double[][] RelaxPath(double[][] path)
    {
        if (path.Length < 3) return path;
        var result = new double[path.Length][];
        result[0] = path[0];
        for (int i = 1; i < path.Length - 1; i++)
            result[i] = [
                0.25 * path[i-1][0] + 0.5 * path[i][0] + 0.25 * path[i+1][0],
                0.25 * path[i-1][1] + 0.5 * path[i][1] + 0.25 * path[i+1][1]];
        result[^1] = path[^1];
        return result;
    }
}
