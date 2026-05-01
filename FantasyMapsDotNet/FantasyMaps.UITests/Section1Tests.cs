using Microsoft.Playwright;

namespace FantasyMaps.UITests;

[TestFixture]
public class Section1Tests : PlaywrightFixture
{
    [Test]
    public async Task GenerateRandomPoints_ShowsCirclesInSvg()
    {
        await GotoAndWaitForBlazor();

        await Page.ClickAsync("[data-testid='btn-generate-points']");

        var circle = Page.Locator("[data-testid='section1-svg'] svg circle").First;
        await Expect(circle).ToBeVisibleAsync(new() { Timeout = ShortTimeout });

        var error = Page.Locator("[data-testid='section1-error']");
        await Expect(error).Not.ToBeVisibleAsync();
    }
}
