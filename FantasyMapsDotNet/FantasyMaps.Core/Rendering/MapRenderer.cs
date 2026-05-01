using System.Globalization;
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

    /// <summary>
    /// Full antique-style map: rivers, coast, territory borders, slope hatch,
    /// cities, and labels — all strokes on a white background (no filled terrain cells).
    /// Matches the original JS drawMap() exactly.
    /// </summary>
    public static string DrawMap(RenderState render, LanguageModel? lang = null)
    {
        PrepareRender(render);
        var sb = new StringBuilder();

        sb.Append(TerrainRenderer.DrawPaths(render.Rivers, "river",
            "stroke:#36a;stroke-width:2;fill:none;stroke-linecap:round;stroke-linejoin:round"));
        sb.Append(TerrainRenderer.DrawPaths(render.Coasts, "coast",
            "stroke:#333;stroke-width:3;fill:none;stroke-linecap:round;stroke-linejoin:round"));
        sb.Append(TerrainRenderer.DrawPaths(render.Borders, "border",
            "stroke:#a33;stroke-width:2.5;fill:none;stroke-dasharray:6,6;stroke-linecap:round;stroke-linejoin:round"));
        sb.Append(TerrainRenderer.VisualizeSlopes(render.H));
        sb.Append(TerrainRenderer.VisualizeCities(render));

        if (lang != null)
            sb.Append(LabelPlacer.DrawLabels(render, lang));

        return SvgBuilder.WrapSvg(sb.ToString());
    }

    /// <summary>
    /// Territory view: ocean in light blue, land cells colored by territory at
    /// 50% opacity, then coast and border strokes on top.
    /// Matches the original JS doShowTerritories() exactly.
    /// </summary>
    public static string DrawTerritories(RenderState render)
    {
        var sb = new StringBuilder();
        RenderTerritoryFill(render, sb);
        sb.Append(TerrainRenderer.DrawPaths(render.Coasts, "coast",
            "stroke:#333;stroke-width:3;fill:none;stroke-linecap:round;stroke-linejoin:round"));
        sb.Append(TerrainRenderer.DrawPaths(render.Borders, "border",
            "stroke:#a33;stroke-width:2.5;fill:none;stroke-dasharray:6,6;stroke-linecap:round;stroke-linejoin:round"));
        sb.Append(TerrainRenderer.VisualizeCities(render));
        return SvgBuilder.WrapSvg(sb.ToString());
    }

    private static void RenderTerritoryFill(RenderState render, StringBuilder sb)
    {
        var h = render.H;
        var terr = render.Terr;
        for (int i = 0; i < h.Mesh.Vxs.Length; i++)
        {
            var triPts = h.Mesh.Tris[i];
            if (triPts == null || triPts.Length < 3) continue;

            string fill;
            if (h[i] <= 0f)
            {
                fill = "#a5bfdd"; // ocean
            }
            else
            {
                int terrOwner = i < terr.Length ? terr[i] : -1;
                if (terrOwner >= 0)
                {
                    int cityIdx = render.Cities.IndexOf(terrOwner);
                    fill = ColorPalette.Category10[cityIdx % ColorPalette.Category10.Length];
                }
                else
                {
                    fill = "#ddd"; // unowned land
                }
            }

            sb.AppendLine(FormattableString.Invariant(
                $"<path class=\"field\" d=\"{SvgBuilder.MakePath(triPts)}\" fill=\"{fill}\" opacity=\"0.5\" />"));
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
