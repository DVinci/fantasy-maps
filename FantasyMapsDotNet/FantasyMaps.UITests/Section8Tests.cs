using Microsoft.Playwright;

namespace FantasyMaps.UITests;

[TestFixture]
public class Section8Tests : PlaywrightFixture
{
    [Test]
    public async Task GenerateFullMap_ShowsSvgWithCoastAndLabels()
    {
        await GotoAndWaitForBlazor();

        await Page.ClickAsync("[data-testid='btn-generate-map']");

        // Full pipeline is the heaviest — allow 90 seconds
        var svg = Page.Locator("[data-testid='section8-svg'] svg");
        await Expect(svg).ToBeVisibleAsync(new() { Timeout = LongTimeout });

        // Must have a coast line
        var coast = Page.Locator("[data-testid='section8-svg'] svg path.coast").First;
        await Expect(coast).ToBeVisibleAsync(new() { Timeout = 5_000 });

        // Must have at least one city label
        var cityLabel = Page.Locator("[data-testid='section8-svg'] svg text.city").First;
        await Expect(cityLabel).ToBeVisibleAsync(new() { Timeout = 5_000 });

        // No error banner
        var error = Page.Locator("[data-testid='section8-error']");
        await Expect(error).Not.ToBeVisibleAsync();
    }
}
