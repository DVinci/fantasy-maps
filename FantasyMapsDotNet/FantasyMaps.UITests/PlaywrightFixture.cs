using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace FantasyMaps.UITests;

/// <summary>
/// Base class for all section tests. Each test gets its own page,
/// navigated to the home page and scrolled to the section under test.
/// </summary>
public class PlaywrightFixture : PageTest
{
    protected const string BaseUrl = WebServerFixture.BaseUrl;

    // Long timeouts: some sections run heavy C# computation (erosion, full map)
    protected static readonly float ShortTimeout = 20_000;
    protected static readonly float MediumTimeout = 45_000;
    protected static readonly float LongTimeout   = 90_000;

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        BaseURL = BaseUrl,
        ViewportSize = new() { Width = 1280, Height = 900 }
    };

    /// <summary>
    /// Navigate to the page and wait for Blazor's SignalR circuit to connect
    /// before letting tests click buttons. Without this, clicks issued
    /// immediately after navigation are lost on cold-start circuits.
    /// BlazorReadySignal renders #blazor-connected after OnAfterRenderAsync(firstRender),
    /// which only fires once the interactive circuit is established.
    /// </summary>
    protected async Task GotoAndWaitForBlazor(string path = "/")
    {
        await Page.GotoAsync(path);
        // Wait for the span to be attached to the DOM (it's display:none, so use Attached not Visible)
        await Page.WaitForSelectorAsync("#blazor-connected",
            new() { State = WaitForSelectorState.Attached, Timeout = 15_000 });
    }
}
