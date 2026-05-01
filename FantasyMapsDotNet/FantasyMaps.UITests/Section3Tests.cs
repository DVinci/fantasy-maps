using Microsoft.Playwright;

namespace FantasyMaps.UITests;

[TestFixture]
public class Section3Tests : PlaywrightFixture
{
    [Test]
    public async Task AddSlope_ShowsColoredVoronoiCells()
    {
        await GotoAndWaitForBlazor();

        await Page.ClickAsync("[data-testid='btn-add-slope']");

        var path = Page.Locator("[data-testid='section3-svg'] svg path.field").First;
        await Expect(path).ToBeVisibleAsync(new() { Timeout = ShortTimeout });

        var error = Page.Locator("[data-testid='section3-error']");
        await Expect(error).Not.ToBeVisibleAsync();
    }

    [Test]
    public async Task AddMountainsThenNormalize_ShowsColoredCells()
    {
        await GotoAndWaitForBlazor();

        await Page.ClickAsync("[data-testid='btn-add-mountains']");
        var path = Page.Locator("[data-testid='section3-svg'] svg path.field").First;
        await Expect(path).ToBeVisibleAsync(new() { Timeout = ShortTimeout });

        await Page.ClickAsync("[data-testid='btn-normalize']");
        await Expect(path).ToBeVisibleAsync(new() { Timeout = ShortTimeout });

        var error = Page.Locator("[data-testid='section3-error']");
        await Expect(error).Not.ToBeVisibleAsync();
    }
}
