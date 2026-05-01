using System.Text;
using FantasyMaps.Core.Language;
using FantasyMaps.Core.Terrain;

namespace FantasyMaps.Core.Rendering;

public static class MapRenderer
{
    public static void PrepareRender(RenderState render)
    {
        var h = render.H;
        render.Rivers = Rivers.GetRivers(h, 0.01f);
        render.Coasts = Rivers.Contour(h, 0f);
        render.Terr = Territories.GetTerritories(render);
        render.Borders = Territories.GetBorders(render);
    }

    public static string DrawMap(RenderState render, LanguageModel? lang = null)
    {
        PrepareRender(render);
        var sb = new StringBuilder();

        sb.Append(TerrainRenderer.VisualizeVoronoi(render.H));
        sb.Append(TerrainRenderer.DrawPaths(render.Coasts, "coast",
            "stroke:#000;stroke-width:3;stroke-linecap:round;stroke-linejoin:round"));
        sb.Append(TerrainRenderer.DrawPaths(render.Rivers, "river",
            "stroke:#36a;stroke-width:2;stroke-linecap:round;stroke-linejoin:round"));
        sb.Append(TerrainRenderer.VisualizeSlopes(render.H));
        RenderTerritories(render, sb);
        sb.Append(TerrainRenderer.DrawPaths(render.Borders, "border",
            "stroke:#a33;stroke-width:2.5;stroke-dasharray:6,6;stroke-linecap:round;stroke-linejoin:round"));
        sb.Append(TerrainRenderer.VisualizeCities(render));

        if (lang != null)
            sb.Append(LabelPlacer.DrawLabels(render, lang));

        return SvgBuilder.WrapSvg(sb.ToString());
    }

    public static string DrawTerritories(RenderState render)
    {
        var sb = new StringBuilder();
        sb.Append(TerrainRenderer.VisualizeVoronoi(render.H));
        RenderTerritories(render, sb);
        sb.Append(TerrainRenderer.DrawPaths(render.Borders, "border",
            "stroke:#a33;stroke-width:2.5;stroke-dasharray:6,6;stroke-linecap:round;stroke-linejoin:round"));
        sb.Append(TerrainRenderer.VisualizeCities(render));
        return SvgBuilder.WrapSvg(sb.ToString());
    }

    private static void RenderTerritories(RenderState render, StringBuilder sb)
    {
        if (render.Terr.Length == 0) return;
        var h = render.H;
        for (int i = 0; i < h.Mesh.Vxs.Length; i++)
        {
            var triPts = h.Mesh.Tris[i];
            if (triPts == null || triPts.Length < 3) continue;
            if (h[i] <= 0f) continue;
            int terrOwner = i < render.Terr.Length ? render.Terr[i] : -1;
            if (terrOwner < 0) continue;
            int cityIdx = render.Cities.IndexOf(terrOwner);
            string color = ColorPalette.Category10[cityIdx % ColorPalette.Category10.Length];
            sb.AppendLine($"<path class=\"field\" d=\"{SvgBuilder.MakePath(triPts)}\" fill=\"{color}\" fill-opacity=\"0.5\" />");
        }
    }

    public static RenderState GenerateFullMap(MapParams @params, Mesh.VoronoiMesh mesh)
    {
        var h = HeightPrimitives.Mountains(mesh, 5);
        h = HeightPrimitives.Add(h, HeightPrimitives.Slope(mesh,
            [Random.Shared.NextDouble() * 4 - 2, Random.Shared.NextDouble() * 4 - 2]));
        h = HeightPrimitives.Peaky(h);
        h = Erosion.DoErosion(h, 0.05f, 5);
        h = Erosion.SetSeaLevel(h, 0.5);
        h = Erosion.CleanCoast(h, 3);

        var render = new RenderState { H = h, Params = @params };
        CityPlacer.PlaceCities(render);
        return render;
    }
}
