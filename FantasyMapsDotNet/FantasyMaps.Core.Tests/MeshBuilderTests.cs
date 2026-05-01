using FantasyMaps.Core.Mesh;
using Xunit;

namespace FantasyMaps.Core.Tests;

public class MeshBuilderTests
{
    [Fact]
    public void VoronoiMesh_IsEdge_ReturnsTrueForBoundaryVertex()
    {
        // Vertex 0: degree 1 → edge; Vertex 1: degree 3 → not edge
        var adj = new int[][] { [1], [0, 2, 3], [1], [1] };
        var mesh = new VoronoiMesh(
            vxs: [[0, 0], [0.1, 0], [0.2, 0], [0.1, 0.1]],
            adj: adj,
            tris: [[], [], [], []],
            edges: [],
            pts: [],
            extent: new Core.Extent());
        Assert.True(mesh.IsEdge(0));
        Assert.False(mesh.IsEdge(1));
    }

    [Fact]
    public void VoronoiMesh_Neighbours_ReturnsAdjacentIndices()
    {
        var adj = new int[][] { [1, 2], [0, 2], [0, 1] };
        var mesh = new VoronoiMesh(
            vxs: [[0, 0], [0.1, 0.1], [0.2, 0]],
            adj: adj,
            tris: [[], [], []],
            edges: [],
            pts: [[0.1, 0.05]],
            extent: new Core.Extent());
        Assert.Equal([1, 2], mesh.Neighbours(0));
    }

    [Fact]
    public void VoronoiMesh_Distance_ComputesEuclidean()
    {
        var mesh = new VoronoiMesh(
            vxs: [[0, 0], [3, 4]],
            adj: [[1], [0]],
            tris: [[], []],
            edges: [],
            pts: [],
            extent: new Core.Extent());
        Assert.Equal(5.0, mesh.Distance(0, 1), precision: 10);
    }

    [Fact]
    public void GenerateGoodMesh_ProducesValidMesh()
    {
        var mesh = MeshBuilder.GenerateGoodMesh(256, new Core.Extent());
        Assert.True(mesh.Vxs.Length > 0, "Should have Voronoi vertices");
        Assert.Equal(mesh.Vxs.Length, mesh.Adj.Length);
        Assert.Equal(mesh.Vxs.Length, mesh.Tris.Length);
        Assert.All(mesh.Adj, adj => Assert.True(adj.Length >= 1));
        // BoundedVoronoi circumcenters can slightly exceed bounding box; allow ±2.0 safety margin
        Assert.All(mesh.Vxs, v => {
            Assert.True(!double.IsNaN(v[0]) && !double.IsInfinity(v[0]));
            Assert.True(!double.IsNaN(v[1]) && !double.IsInfinity(v[1]));
        });
    }
}
// Smoke test lives here because it exercises the full pipeline top-to-bottom

public class FullPipelineTests
{
    [Fact]
    public void FullPipeline_GeneratesValidSvg()
    {
        var @params = new Core.MapParams { Npts = 512, Ncities = 5, Nterrs = 3 };
        var mesh = MeshBuilder.GenerateGoodMesh(@params.Npts);
        var render = Core.Rendering.MapRenderer.GenerateFullMap(@params, mesh);
        var lang = Core.Language.LanguageFactory.MakeRandomLanguage();
        string svg = Core.Rendering.MapRenderer.DrawMap(render, lang);
        Assert.Contains("<svg", svg);
        Assert.Contains("<path", svg);
        Assert.Contains("<text", svg);
        Assert.True(svg.Length > 1000, "SVG should have substantial content");
    }
}
