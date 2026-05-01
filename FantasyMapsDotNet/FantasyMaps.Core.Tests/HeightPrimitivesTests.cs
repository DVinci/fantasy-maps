using FantasyMaps.Core.Mesh;
using FantasyMaps.Core.Terrain;
using Xunit;

namespace FantasyMaps.Core.Tests;

public class HeightPrimitivesTests
{
    private static VoronoiMesh MakeSmallMesh()
    {
        var vxs = new double[][] { [-0.2, 0.0], [0.0, 0.0], [0.2, 0.0] };
        var adj = new int[][] { [1], [0, 2], [1] };
        var tris = new double[][][] { [], [], [] };
        return new VoronoiMesh(vxs, adj, tris, [], [], new Core.Extent());
    }

    [Fact]
    public void Zero_ReturnsAllZeros()
    {
        var mesh = MakeSmallMesh();
        var h = HeightPrimitives.Zero(mesh);
        Assert.Equal(3, h.Length);
        Assert.All(h.Values, v => Assert.Equal(0f, v));
        Assert.Same(mesh, h.Mesh);
    }

    [Fact]
    public void Normalize_ScalesToZeroOne()
    {
        var mesh = MakeSmallMesh();
        var h = HeightPrimitives.Zero(mesh);
        h.Values[0] = -1f; h.Values[1] = 0f; h.Values[2] = 3f;
        var norm = HeightPrimitives.Normalize(h);
        Assert.Equal(0f, norm.Values[0], precision: 5);
        Assert.Equal(1f, norm.Values[2], precision: 5);
        Assert.True(norm.Values[1] > 0f && norm.Values[1] < 1f);
    }

    [Fact]
    public void Add_SumsHeightFields()
    {
        var mesh = MakeSmallMesh();
        var h1 = HeightPrimitives.Zero(mesh); h1.Values[0] = 1f;
        var h2 = HeightPrimitives.Zero(mesh); h2.Values[0] = 2f;
        var sum = HeightPrimitives.Add(h1, h2);
        Assert.Equal(3f, sum.Values[0], precision: 5);
    }

    [Fact]
    public void Slope_ProducesGradient()
    {
        var mesh = MakeSmallMesh();
        var h = HeightPrimitives.Slope(mesh, [1.0, 0.0]);
        Assert.True(h.Values[0] < h.Values[2]);
    }
}
