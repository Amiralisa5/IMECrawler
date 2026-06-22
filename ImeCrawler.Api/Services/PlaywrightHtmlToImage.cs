using Microsoft.Playwright;

namespace ImeCrawler.Api.Services;

public sealed class PlaywrightHtmlToImage : IHtmlToImage, IAsyncDisposable
{
    // Launching a browser is expensive, so we create one Playwright + Browser instance
    // and reuse it across renders (a fresh, isolated context/page per render).
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    private async Task<IBrowser> GetBrowserAsync()
    {
        if (_browser is { IsConnected: true }) return _browser;

        await _gate.WaitAsync();
        try
        {
            if (_browser is { IsConnected: true }) return _browser;

            // Drop a stale/disconnected browser before relaunching.
            if (_browser is not null)
            {
                try { await _browser.DisposeAsync(); } catch { /* best effort */ }
                _browser = null;
            }

            _playwright ??= await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
            return _browser;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<byte[]> RenderPngAsync(string html, CancellationToken ct)
    {
        var browser = await GetBrowserAsync();
        await using var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.SetContentAsync(html, new() { WaitUntil = WaitUntilState.NetworkIdle });
        return await page.ScreenshotAsync(new() { FullPage = true, Type = ScreenshotType.Png });
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            try { await _browser.DisposeAsync(); } catch { /* best effort */ }
        }
        _playwright?.Dispose();
        _gate.Dispose();
    }
}
