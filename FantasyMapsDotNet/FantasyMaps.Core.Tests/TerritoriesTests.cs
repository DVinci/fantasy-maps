using FantasyMaps.Core;
using FantasyMaps.Core.Mesh;
using FantasyMaps.Core.Terrain;
using Xunit;

namespace FantasyMaps.Core.Tests;

public class TerritoriesTests
{
    [Fact]
    public void GetTerritories_AssignsEveryLandVertexToACity()
    {
        var mesh = MeshBuilder.GenerateGoodMesh(128);
        var h = HeightPrimitives.Mountains(mesh, 3);
        h = HeightPrimitives.Normalize(h);
        h = Erosion.SetSeaLevel(h, 0.5);
        var render = new RenderState { H = h, Params = new MapParams { Ncities = 3, Nterrs = 3 } };
        CityPlacer.PlaceCities(render);
        var terr = Territories.GetTerritories(render);
        render.Terr = terr;
        for (int i = 0; i < h.Length; i++)
            if (h[i] > 0f && !mesh.IsEdge(i))
                Assert.True(terr[i] >= 0, $"Land vertex {i} has no territory owner");
    }
}
