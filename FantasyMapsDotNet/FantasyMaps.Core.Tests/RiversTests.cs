using FantasyMaps.Core.Mesh;
using FantasyMaps.Core.Terrain;
using Xunit;

namespace FantasyMaps.Core.Tests;

public class RiversTests
{
    private static HeightField MakeTestTerrain()
    {
        var mesh = MeshBuilder.GenerateGoodMesh(256);
        var h = HeightPrimitives.Mountains(mesh, 3);
        h = HeightPrimitives.Normalize(h);
        h = Erosion.FillSinks(h);
        return Erosion.SetSeaLevel(h, 0.5);
    }

    [Fact]
    public void Contour_ReturnsPathsAtSeaLevel()
    {
        var h = MakeTestTerrain();
        var coasts = Rivers.Contour(h, 0f);
        Assert.True(coasts.Count > 0, "Should have at least one coastline path");
        Assert.All(coasts, path => Assert.True(path.Length >= 2));
    }

    [Fact]
    public void GetRivers_ReturnsPathsAboveSeaLevel()
    {
        var h = MakeTestTerrain();
        var riverPaths = Rivers.GetRivers(h, 0.01f);
        foreach (var path in riverPaths)
            foreach (var pt in path)
                Assert.Equal(2, pt.Length);
    }
}
