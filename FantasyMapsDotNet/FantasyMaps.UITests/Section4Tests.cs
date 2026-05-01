using Microsoft.Playwright;

namespace FantasyMaps.UITests;

[TestFixture]
public class Section4Tests : PlaywrightFixture
{
    [Test]
    public async Task GenCoast_ShowsTerrainColors()
    {
        await GotoAndWaitForBlazor();

        await Page.ClickAsync("[data-testid='btn-gen-coast']");

        var path = Page.Locator("[data-testid='section4-svg'] svg path.field").First;
        await Expect(path).ToBeVisibleAsync(new() { Timeout = ShortTimeout });

        var error = Page.Locator("[data-testid='section4-error']");
        await Expect(error).Not.ToBeVisibleAsync();
    }

    [Test]
    public async Task GenCoastThenErode_ShowsErodedTerrain()
    {
        await GotoAndWaitForBlazor();

        await Page.ClickAsync("[data-testid='btn-gen-coast']");
        var path = Page.Locator("[data-testid='section4-svg'] svg path.field").First;
        await Expect(path).ToBeVisibleAsync(new() { Timeout = ShortTimeout });

        await Page.ClickAsync("[data-testid='btn-erode']");
        await Expect(path).ToBeVisibleAsync(new() { Timeout = MediumTimeout });

        var error = Page.Locator("[data-testid='section4-error']");
        await Expect(error).Not.ToBeVisibleAsync();
    }
}
