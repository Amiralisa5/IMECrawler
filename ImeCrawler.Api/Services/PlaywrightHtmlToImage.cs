using Microsoft.Playwright;

namespace ImeCrawler.Api.Services;

public sealed class PlaywrightHtmlToImage : IHtmlToImage
{
    public async Task<byte[]> RenderPngAsync(string html, CancellationToken ct)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(html, new() { WaitUntil = WaitUntilState.NetworkIdle });
        return await page.ScreenshotAsync(new() { FullPage = true, Type = ScreenshotType.Png });
    }
}
