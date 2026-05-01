using System.Text;

namespace FantasyMaps.Core.Rendering;

public static class SvgBuilder
{
    public const double Scale = 1000.0;

    public static string MakePath(double[][] points)
    {
        if (points.Length == 0) return "";
        var sb = new StringBuilder();
        sb.Append($"M{points[0][0] * Scale:F2},{points[0][1] * Scale:F2}");
        for (int i = 1; i < points.Length; i++)
            sb.Append($"L{points[i][0] * Scale:F2},{points[i][1] * Scale:F2}");
        return sb.ToString();
    }

    public static string FilledPath(double[][] points, string fill, string? cssClass = null)
    {
        string cls = cssClass != null ? $" class=\"{cssClass}\"" : "";
        return $"<path{cls} d=\"{MakePath(points)}\" fill=\"{fill}\" />";
    }

    public static string StrokedPath(double[][] points, string cssClass, string style = "")
    {
        string styleAttr = style.Length > 0 ? $" style=\"{style}\"" : "";
        return $"<path class=\"{cssClass}\"{styleAttr} d=\"{MakePath(points)}\" fill=\"none\" />";
    }

    public static string Circle(double x, double y, double r, string cssClass, string style = "")
    {
        string styleAttr = style.Length > 0 ? $" style=\"{style}\"" : "";
        return $"<circle class=\"{cssClass}\"{styleAttr} cx=\"{x * Scale:F2}\" cy=\"{y * Scale:F2}\" r=\"{r}\" />";
    }

    public static string Line(double x1, double y1, double x2, double y2, string cssClass, string style = "")
    {
        string styleAttr = style.Length > 0 ? $" style=\"{style}\"" : "";
        return $"<line class=\"{cssClass}\"{styleAttr} x1=\"{x1 * Scale:F2}\" y1=\"{y1 * Scale:F2}\" x2=\"{x2 * Scale:F2}\" y2=\"{y2 * Scale:F2}\" />";
    }

    public static string Text(double x, double y, string content, string cssClass, string style = "")
    {
        string styleAttr = style.Length > 0 ? $" style=\"{style}\"" : "";
        string escaped = content
            .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
            .Replace("\"", "&quot;");
        return $"<text class=\"{cssClass}\"{styleAttr} x=\"{x * Scale:F2}\" y=\"{y * Scale:F2}\">{escaped}</text>";
    }

    public static string WrapSvg(string content, string viewBox = "-500 -500 1000 1000", string style = "width:800px;height:800px")
        => $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"{viewBox}\" style=\"{style}\">{content}</svg>";
}
