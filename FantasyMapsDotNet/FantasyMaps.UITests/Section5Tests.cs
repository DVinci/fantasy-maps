using Microsoft.Playwright;

namespace FantasyMaps.UITests;

[TestFixture]
public class Section5Tests : PlaywrightFixture
{
    [Test]
    public async Task ToggleCoast_ShowsCoastPath()
    {
        await GotoAndWaitForBlazor();

        await Page.ClickAsync("[data-testid='btn-toggle-coast']");

        var coast = Page.Locator("[data-testid='section5-svg'] svg path.coast").First;
        await Expect(coast).ToBeVisibleAsync(new() { Timeout = MediumTimeout });

        var error = Page.Locator("[data-testid='section5-error']");
        await Expect(error).Not.ToBeVisibleAsync();
    }

    [Test]
    public async Task ToggleRivers_ShowsRiverPath()
    {
        await GotoAndWaitForBlazor();

        await Page.ClickAsync("[data-testid='btn-toggle-rivers']");

        var river = Page.Locator("[data-testid='section5-svg'] svg path.river").First;
        await Expect(river).ToBeVisibleAsync(new() { Timeout = MediumTimeout });
    }
}
