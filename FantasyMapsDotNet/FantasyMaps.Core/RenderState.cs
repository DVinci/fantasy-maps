using FantasyMaps.Core.Terrain;

namespace FantasyMaps.Core;

public class RenderState
{
    public HeightField H { get; set; } = null!;
    public List<int> Cities { get; set; } = [];
    public MapParams Params { get; set; } = new();
    public List<double[][]> Rivers { get; set; } = [];
    public List<double[][]> Coasts { get; set; } = [];
    public List<double[][]> Borders { get; set; } = [];
    public int[] Terr { get; set; } = [];
    // Typed as object until LanguageModel is defined in Task 10
    public object? Language { get; set; }
}
