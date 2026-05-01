using FantasyMaps.Core;
using FantasyMaps.Core.Mesh;
using FantasyMaps.Core.Terrain;
using Xunit;

namespace FantasyMaps.Core.Tests;

public class CityPlacerTests
{
    [Fact]
    public void PlaceCities_PlacesOnLandNotNearEdge()
    {
        var mesh = MeshBuilder.GenerateGoodMesh(256);
        var h = HeightPrimitives.Mountains(mesh, 3);
        h = HeightPrimitives.Normalize(h);
        h = Erosion.SetSeaLevel(h, 0.5);
        var render = new RenderState { H = h, Params = new MapParams { Ncities = 5 } };
        CityPlacer.PlaceCities(render);
        Assert.Equal(5, render.Cities.Count);
        foreach (int city in render.Cities)
        {
            Assert.True(h[city] > 0f, $"City {city} is in ocean");
            Assert.False(mesh.IsNearEdge(city), $"City {city} is near edge");
        }
    }
}
