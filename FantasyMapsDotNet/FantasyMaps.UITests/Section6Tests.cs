using Microsoft.Playwright;

namespace FantasyMaps.UITests;

[TestFixture]
public class Section6Tests : PlaywrightFixture
{
    [Test]
    public async Task AddCity_ShowsCityCircle()
    {
        await GotoAndWaitForBlazor();

        await Page.ClickAsync("[data-testid='btn-add-city']");

        var city = Page.Locator("[data-testid='section6-svg'] svg circle.city").First;
        await Expect(city).ToBeVisibleAsync(new() { Timeout = MediumTimeout });

        var error = Page.Locator("[data-testid='section6-error']");
        await Expect(error).Not.ToBeVisibleAsync();
    }

    [Test]
    public async Task AddMultipleCities_ShowsAllCityCircles()
    {
        await GotoAndWaitForBlazor();

        await Page.ClickAsync("[data-testid='btn-add-city']");
        await Page.WaitForSelectorAsync("[data-testid='section6-svg'] svg circle.city",
            new() { Timeout = MediumTimeout });

        await Page.ClickAsync("[data-testid='btn-add-city']");
        await Page.ClickAsync("[data-testid='btn-add-city']");

        var cities = Page.Locator("[data-testid='section6-svg'] svg circle.city");
        await Expect(cities).ToHaveCountAsync(3, new() { Timeout = MediumTimeout });
    }
}
