namespace FantasyMaps.Core.Mesh;

public class VoronoiMesh
{
    public double[][] Vxs { get; }
    public int[][] Adj { get; }
    public double[][][] Tris { get; }
    public (int V0, int V1, double[]? Left, double[]? Right)[] Edges { get; }
    public double[][] Pts { get; }
    public Extent Extent { get; }

    public VoronoiMesh(
        double[][] vxs,
        int[][] adj,
        double[][][] tris,
        (int, int, double[]?, double[]?)[] edges,
        double[][] pts,
        Extent extent)
    {
        Vxs = vxs;
        Adj = adj;
        Tris = tris;
        Edges = edges;
        Pts = pts;
        Extent = extent;
    }

    public bool IsEdge(int i) => Adj[i].Length < 3;

    public bool IsNearEdge(int i)
    {
        double x = Vxs[i][0], y = Vxs[i][1];
        return x < -0.45 * Extent.Width || x > 0.45 * Extent.Width
            || y < -0.45 * Extent.Height || y > 0.45 * Extent.Height;
    }

    public int[] Neighbours(int i) => Adj[i];

    public double Distance(int i, int j)
    {
        double dx = Vxs[i][0] - Vxs[j][0];
        double dy = Vxs[i][1] - Vxs[j][1];
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
