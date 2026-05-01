namespace FantasyMaps.Core;

public class MapParams
{
    public int Npts { get; set; } = 4096;
    public int Ncities { get; set; } = 15;
    public int Nterrs { get; set; } = 5;
    public double[] Fontsizes { get; set; } = [25, 18, 15];
    public Extent Extent { get; set; } = new();
}
