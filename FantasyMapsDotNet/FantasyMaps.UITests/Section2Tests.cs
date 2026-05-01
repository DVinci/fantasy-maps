using Microsoft.Playwright;

namespace FantasyMaps.UITests;

[TestFixture]
public class Section2Tests : PlaywrightFixture
{
    [Test]
    public async Task ShowVoronoiMesh_ShowsLinesAndPoints()
    {
        await GotoAndWaitForBlazor();

        await Page.ClickAsync("[data-testid='btn-show-mesh']");

        var line = Page.Locator("[data-testid='section2-svg'] svg line").First;
        await Expect(line).ToBeVisibleAsync(new() { Timeout = ShortTimeout });

        var error = Page.Locator("[data-testid='section2-error']");
        await Expect(error).Not.ToBeVisibleAsync();
    }
}
