using System.Net.Http.Headers;

namespace ImeCrawler.Api.Services;

public sealed class ImeAuctionClient
{
    private readonly HttpClient _http;

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
        return await res.Content.ReadAsStringAsync(ct);
    }
}
