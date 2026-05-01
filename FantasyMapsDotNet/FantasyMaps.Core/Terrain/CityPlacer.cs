namespace FantasyMaps.Core.Terrain;

public static class CityPlacer
{
    public static float[] CityScore(HeightField h, List<int> cities)
    {
        var flux = Erosion.GetFlux(h);
        var score = new float[h.Length];
        for (int i = 0; i < h.Length; i++) score[i] = (float)Math.Sqrt(flux[i]);

        for (int i = 0; i < h.Length; i++)
        {
            if (h[i] <= 0f || h.Mesh.IsNearEdge(i)) { score[i] = -999999f; continue; }
            double vx = h.Mesh.Vxs[i][0], vy = h.Mesh.Vxs[i][1];
            score[i] += (float)(0.01 / (1e-9 + Math.Abs(vx) - h.Mesh.Extent.Width / 2));
            score[i] += (float)(0.01 / (1e-9 + Math.Abs(vy) - h.Mesh.Extent.Height / 2));
            foreach (int city in cities)
                score[i] -= (float)(0.02 / (h.Mesh.Distance(city, i) + 1e-9));
        }
        return score;
    }

    public static void PlaceCity(RenderState render)
    {
        var score = CityScore(render.H, render.Cities);
        int newCity = Array.IndexOf(score, score.Max());
        render.Cities.Add(newCity);
    }

    public static void PlaceCities(RenderState render)
    {
        for (int i = 0; i < render.Params.Ncities; i++) PlaceCity(render);
    }
}
