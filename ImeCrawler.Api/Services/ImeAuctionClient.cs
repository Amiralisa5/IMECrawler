using System.Net.Http.Headers;
using System.Text.Json;

namespace ImeCrawler.Api.Services;

public sealed class ImeAuctionClient
{
    private readonly HttpClient _http;
    private const string DebugLogPath = @"c:\Users\Amirali\source\repos\ImeCrawler\IMECrawler\.cursor\debug.log";

    // temp-check
    public ImeAuctionClient(HttpClient http)
    {
        _http = http;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");
        _http.DefaultRequestHeaders.Referrer = new Uri("https://www.ime.co.ir/arze.html");
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
    }

    public async Task<string> FetchAsync(
        string jalaliDate,
        int m, int c, int s, int p,
        CancellationToken ct)
    {
        var url =
            "https://www.ime.co.ir/subsystems/ime/auction/auction.ashx" +
            $"?fr=false&f={Uri.EscapeDataString(jalaliDate)}&t={Uri.EscapeDataString(jalaliDate)}" +
            $"&m={m}&c={c}&s={s}&p={p}&lang=8";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        var content = await res.Content.ReadAsStringAsync(ct);

        #region agent log
        System.IO.File.AppendAllText(DebugLogPath,
            JsonSerializer.Serialize(new
            {
                sessionId = "debug-session",
                runId = "pre-fix",
                hypothesisId = "H3",
                location = "ImeAuctionClient.FetchAsync",
                message = "IME fetch response",
                data = new { url, statusCode = (int)res.StatusCode, length = content?.Length ?? 0 },
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            }) + Environment.NewLine);
        #endregion

        return content;
    }
}
