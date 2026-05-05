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
    /// Full antique-style map: light ocean wash, rivers, coast, territory borders,
    /// slope hatch, cities, and labels. Matches the original JS drawMap() style.
    /// </summary>
    public static string DrawMap(RenderState render, LanguageModel? lang = null)
    {
        PrepareRender(render);
        var sb = new StringBuilder();

        // SVG defs: ocean wave pattern + vignette gradient
        sb.Append(BuildAntiqueDefs());

        // Parchment background — land cells are not explicitly filled so they show through as parchment
        sb.Append("<rect x=\"-500\" y=\"-500\" width=\"1000\" height=\"1000\" fill=\"#f4e4c1\"/>");

        // Ocean: flat base color, then wave texture overlay
        RenderOceanFill(render.H, sb);
        RenderOceanWaves(render.H, sb);

        // Coast inner shadow: drawn before the coast line for a land-depth effect
        foreach (var path in render.Coasts)
        {
            string d = SvgBuilder.MakePath(path);
            sb.AppendLine($"<path d=\"{d}\" fill=\"none\" style=\"stroke:#553311;stroke-width:9;stroke-linecap:round;stroke-linejoin:round;opacity:0.15\"/>");
        }

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

        // Vignette overlay — last, so it darkens everything including labels
        sb.Append("<rect x=\"-500\" y=\"-500\" width=\"1000\" height=\"1000\" fill=\"url(#vignette)\"/>");

        return SvgBuilder.WrapSvg(sb.ToString());
    }

    private static string BuildAntiqueDefs() => """
        <defs>
          <pattern id="oceanWave" x="0" y="0" width="80" height="20" patternUnits="userSpaceOnUse">
            <path d="M0,10 Q20,3 40,10 Q60,17 80,10" fill="none" stroke="#7a9fb5" stroke-width="1.2" stroke-linecap="round" opacity="0.6"/>
          </pattern>
          <radialGradient id="vignette" cx="50%" cy="50%" r="70%">
            <stop offset="25%" stop-color="black" stop-opacity="0"/>
            <stop offset="100%" stop-color="black" stop-opacity="0.38"/>
          </radialGradient>
        </defs>
        """;

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

    private static void RenderOceanFill(HeightField h, StringBuilder sb)
    {
        for (int i = 0; i < h.Mesh.Vxs.Length; i++)
        {
            if (h[i] > 0f) continue;
            var triPts = h.Mesh.Tris[i];
            if (triPts == null || triPts.Length < 3) continue;
            sb.AppendLine(FormattableString.Invariant(
                $"<path d=\"{SvgBuilder.MakePath(triPts)}\" fill=\"#c8dce8\" />"));
        }
    }

    private static void RenderOceanWaves(HeightField h, StringBuilder sb)
    {
        for (int i = 0; i < h.Mesh.Vxs.Length; i++)
        {
            if (h[i] > 0f) continue;
            var triPts = h.Mesh.Tris[i];
            if (triPts == null || triPts.Length < 3) continue;
            sb.AppendLine(FormattableString.Invariant(
                $"<path d=\"{SvgBuilder.MakePath(triPts)}\" fill=\"url(#oceanWave)\" />"));
        }
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
        for (int i = 0; i < @params.RelaxPasses; i++)
            h = HeightPrimitives.Relax(h);
        h = HeightPrimitives.Peaky(h);
        h = Erosion.DoErosion(h, 0.05f, 5);
        h = Erosion.SetSeaLevel(h, 0.5);
        h = Erosion.CleanCoast(h, 3);

        var render = new RenderState { H = h, Params = @params };
        CityPlacer.PlaceCities(render);
        return render;
    }
}
