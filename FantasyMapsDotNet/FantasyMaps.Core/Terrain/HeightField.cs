using FantasyMaps.Core.Mesh;

namespace FantasyMaps.Core.Terrain;

public class HeightField
{
    public float[] Values { get; }
    public VoronoiMesh Mesh { get; }
    public int[]? DownhillCache { get; set; }

    public HeightField(float[] values, VoronoiMesh mesh)
    {
        Values = values;
        Mesh = mesh;
    }

    public int Length => Values.Length;

    public float this[int i]
    {
        get => Values[i];
        set { Values[i] = value; DownhillCache = null; }
    }

    public HeightField Clone()
    {
        var copy = new float[Values.Length];
        Values.CopyTo(copy, 0);
        return new HeightField(copy, Mesh);
    }
}
