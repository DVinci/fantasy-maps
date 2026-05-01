using TriangleNet.Geometry;
using TriangleNet.Meshing;
using TriangleNet.Voronoi;

namespace FantasyMaps.Core.Mesh;

public static class MeshBuilder
{
    public static VoronoiMesh GenerateGoodMesh(int n, Extent? extent = null)
    {
        extent ??= new Extent();
        var pts = GenerateGoodPoints(n, extent);
        return BuildMesh(pts, extent);
    }

    private static double[][] GeneratePoints(int n, Extent extent)
    {
        var pts = new double[n][];
        for (int i = 0; i < n; i++)
            pts[i] = [
                (Random.Shared.NextDouble() - 0.5) * extent.Width,
                (Random.Shared.NextDouble() - 0.5) * extent.Height
            ];
        return pts;
    }

    private static double[][] GenerateGoodPoints(int n, Extent extent)
    {
        var pts = GeneratePoints(n, extent);
        pts = [.. pts.OrderBy(p => p[0])];
        return ImprovePoints(pts, extent);
    }

    // One Lloyd relaxation: replace each point with the centroid of its Voronoi cell.
    private static double[][] ImprovePoints(double[][] pts, Extent extent)
    {
        var polygon = PtsToPolygon(pts, extent);
        var delaunay = (TriangleNet.Mesh)polygon.Triangulate(new ConstraintOptions(), new QualityOptions());
        var voronoi = new BoundedVoronoi(delaunay);

        var newPts = new List<double[]>(pts.Length);
        foreach (var face in voronoi.Faces)
        {
            if (face.Edge == null) continue;
            var verts = new List<double[]>();
            var start = face.Edge;
            var he = start;
            do {
                if (he.Origin != null)
                    verts.Add([he.Origin.X, he.Origin.Y]);
                he = he.Next;
            } while (he != null && he != start);

            if (verts.Count == 0) continue;
            newPts.Add([verts.Average(v => v[0]), verts.Average(v => v[1])]);
        }

        while (newPts.Count < pts.Length)
            newPts.Add(pts[newPts.Count]);
        return [.. newPts.Take(pts.Length)];
    }

    private static Polygon PtsToPolygon(double[][] pts, Extent extent)
    {
        double hw = extent.Width / 2.0, hh = extent.Height / 2.0;
        var polygon = new Polygon();
        foreach (var pt in pts)
            polygon.Add(new Vertex(pt[0], pt[1]));
        // Corner anchors ensure BoundedVoronoi has a closed rectangular boundary
        polygon.Add(new Vertex(-hw, -hh));
        polygon.Add(new Vertex( hw, -hh));
        polygon.Add(new Vertex( hw,  hh));
        polygon.Add(new Vertex(-hw,  hh));
        return polygon;
    }

    private static VoronoiMesh BuildMesh(double[][] pts, Extent extent)
    {
        var polygon = PtsToPolygon(pts, extent);
        var delaunay = (TriangleNet.Mesh)polygon.Triangulate(new ConstraintOptions(), new QualityOptions());
        var voronoi = new BoundedVoronoi(delaunay);

        int n = voronoi.Vertices.Count;

        // Map DCEL vertex ID → contiguous array index
        var idToIdx = new Dictionary<int, int>(n);
        var vxs = new double[n][];
        for (int i = 0; i < n; i++)
        {
            var v = voronoi.Vertices[i];
            idToIdx[v.ID] = i;
            vxs[i] = [v.X, v.Y];
        }

        var adjSets = new HashSet<int>[n];
        var triLists = new List<double[]>[n];
        for (int i = 0; i < n; i++) { adjSets[i] = []; triLists[i] = []; }

        var edges = new List<(int V0, int V1, double[]? Left, double[]? Right)>();

        foreach (var he in voronoi.HalfEdges)
        {
            if (he.Origin == null || he.Twin?.Origin == null) continue;
            // Process each undirected edge once
            if (he.ID >= he.Twin.ID) continue;

            if (!idToIdx.TryGetValue(he.Origin.ID, out int e0)) continue;
            if (!idToIdx.TryGetValue(he.Twin.Origin.ID, out int e1)) continue;

            adjSets[e0].Add(e1);
            adjSets[e1].Add(e0);

            double[]? left = he.Face?.Generator != null
                ? [he.Face.Generator.X, he.Face.Generator.Y] : null;
            double[]? right = he.Twin.Face?.Generator != null
                ? [he.Twin.Face.Generator.X, he.Twin.Face.Generator.Y] : null;

            if (left != null) { AddIfAbsent(triLists[e0], left); AddIfAbsent(triLists[e1], left); }
            if (right != null) { AddIfAbsent(triLists[e0], right); AddIfAbsent(triLists[e1], right); }

            edges.Add((e0, e1, left, right));
        }

        return new VoronoiMesh(
            vxs: vxs,
            adj: adjSets.Select(s => s.ToArray()).ToArray(),
            tris: triLists.Select(t => t.ToArray()).ToArray(),
            edges: [.. edges],
            pts: pts,
            extent: extent);
    }

    private static void AddIfAbsent(List<double[]> list, double[] pt)
    {
        foreach (var p in list)
            if (p[0] == pt[0] && p[1] == pt[1]) return;
        list.Add(pt);
    }
}
