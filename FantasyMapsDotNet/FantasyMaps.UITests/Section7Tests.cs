using Microsoft.Playwright;

namespace FantasyMaps.UITests;

[TestFixture]
public class Section7Tests : PlaywrightFixture
{
    [Test]
    public async Task ShowTerritories_ShowsColoredRegionsAndBorders()
    {
        await GotoAndWaitForBlazor();

        await Page.ClickAsync("[data-testid='btn-show-territories']");

        // Territory cells show as filled paths
        var cell = Page.Locator("[data-testid='section7-svg'] svg path.field").First;
        await Expect(cell).ToBeVisibleAsync(new() { Timeout = MediumTimeout });

        // Border paths should also appear
        var border = Page.Locator("[data-testid='section7-svg'] svg path.border").First;
        await Expect(border).ToBeVisibleAsync(new() { Timeout = MediumTimeout });

        var error = Page.Locator("[data-testid='section7-error']");
        await Expect(error).Not.ToBeVisibleAsync();
    }
}
