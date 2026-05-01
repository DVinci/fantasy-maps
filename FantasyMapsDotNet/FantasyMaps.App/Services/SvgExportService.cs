using CommunityToolkit.Maui.Storage;
using System.Text;

namespace FantasyMaps.App.Services;

public class SvgExportService
{
    public async Task SaveAsync(string svgContent, CancellationToken ct = default)
    {
        var bytes = Encoding.UTF8.GetBytes(svgContent);
        using var stream = new MemoryStream(bytes);
        await FileSaver.Default.SaveAsync("fantasy-map.svg", stream, ct);
    }
}
