namespace FantasyMaps.Core;

public class RenderState
{
    // HeightField is defined in Task 5 (Terrain/HeightField.cs)
    public object H { get; set; } = null!;
    public List<int> Cities { get; set; } = [];
    public MapParams Params { get; set; } = new();
    // Each path is a list of [x,y] coordinate pairs forming a connected line
    public List<double[][]> Rivers { get; set; } = [];
    public List<double[][]> Coasts { get; set; } = [];
    public List<double[][]> Borders { get; set; } = [];
    // Territory map: Voronoi vertex index → owning city vertex index (-1 = unowned)
    public int[] Terr { get; set; } = [];
    // Language is defined in Task 10 (Language/LanguageModel.cs) — typed as object for now
    public object? Language { get; set; }
}
