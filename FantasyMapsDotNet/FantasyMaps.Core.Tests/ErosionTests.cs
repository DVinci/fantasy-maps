using FantasyMaps.Core.Mesh;
using FantasyMaps.Core.Terrain;
using Xunit;

namespace FantasyMaps.Core.Tests;

public class ErosionTests
{
    [Fact]
    public void GetFlux_IsNonNegativeEverywhere()
    {
        var mesh = MeshBuilder.GenerateGoodMesh(64);
        var h = HeightPrimitives.Cone(mesh, -0.5f);
        h = HeightPrimitives.Normalize(h);
        var flux = Erosion.GetFlux(h);
        Assert.All(flux.Values, v => Assert.True(v >= 0f));
    }

    [Fact]
    public void Erode_ReducesMeanLandHeight()
    {
        var mesh = MeshBuilder.GenerateGoodMesh(64);
        var h = HeightPrimitives.Mountains(mesh, 3);
        h = HeightPrimitives.Normalize(h);
        float before = h.Values.Average();
        var eroded = Erosion.Erode(h, 0.1f);
        Assert.True(eroded.Values.Average() < before);
    }

    [Fact]
    public void FillSinks_EliminatesInteriorMinima()
    {
        var mesh = MeshBuilder.GenerateGoodMesh(64);
        var h = HeightPrimitives.Mountains(mesh, 5);
        h = HeightPrimitives.Normalize(h);
        var filled = Erosion.FillSinks(h);
        for (int i = 0; i < filled.Length; i++)
        {
            if (filled.Mesh.IsEdge(i)) continue;
            var nbs = filled.Mesh.Neighbours(i);
            bool hasDownhill = nbs.Any(nb => filled[nb] < filled[i] + 1e-4f);
            Assert.True(hasDownhill || filled.Mesh.IsEdge(i),
                $"Vertex {i} appears to be an interior minimum after fillSinks");
        }
    }
}
