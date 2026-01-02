using System.Net.Http.Headers;
using System.Text.Json;

namespace ImeCrawler.Api.Services;

public sealed record CategoryGroup(int Code, string Name);
public sealed record Producer(int Code, string Name);

public sealed class ImeCategoryService
{
    private readonly HttpClient _http;

    public ImeCategoryService(HttpClient http)
    {
        _http = http;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");
        _http.DefaultRequestHeaders.Referrer = new Uri("https://www.ime.co.ir/arze.html");
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _http.DefaultRequestHeaders.ContentType = new MediaTypeHeaderValue("application/json");
    }

    public async Task<IReadOnlyList<CategoryGroup>> GetMainGroupsAsync(CancellationToken ct = default)
    {
        var url = "https://www.ime.co.ir/subsystems/ime/services/home/imedata.asmx/GetMainGroups";
        var payload = JsonSerializer.Serialize(new { Language = 8 });

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
        };

        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadAsStringAsync(ct);

        // Response is typically wrapped: {"d": [...]}
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("d", out var d) && d.ValueKind == JsonValueKind.Array)
        {
            return d.EnumerateArray()
                .Select(x => new CategoryGroup(
                    TryGetInt(x, "code"),
                    TryGetString(x, "Name") ?? ""))
                .ToList();
        }

        return Array.Empty<CategoryGroup>();
    }

    public async Task<IReadOnlyList<CategoryGroup>> GetCategoriesAsync(int mainCat, CancellationToken ct = default)
    {
        var url = "https://www.ime.co.ir/subsystems/ime/services/home/imedata.asmx/GetCatGroups";
        var payload = JsonSerializer.Serialize(new { Language = 8, MainCat = mainCat });

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
        };

        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadAsStringAsync(ct);

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("d", out var d) && d.ValueKind == JsonValueKind.Array)
        {
            return d.EnumerateArray()
                .Select(x => new CategoryGroup(
                    TryGetInt(x, "code"),
                    TryGetString(x, "name") ?? ""))
                .ToList();
        }

        return Array.Empty<CategoryGroup>();
    }

    public async Task<IReadOnlyList<CategoryGroup>> GetSubCategoriesAsync(int mainCat, int cat, CancellationToken ct = default)
    {
        var url = "https://www.ime.co.ir/subsystems/ime/services/home/imedata.asmx/GetSubCatGroups";
        var payload = JsonSerializer.Serialize(new { Language = 8, MainCat = mainCat, Cat = cat });

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
        };

        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadAsStringAsync(ct);

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("d", out var d) && d.ValueKind == JsonValueKind.Array)
        {
            return d.EnumerateArray()
                .Select(x => new CategoryGroup(
                    TryGetInt(x, "code"),
                    TryGetString(x, "name") ?? ""))
                .ToList();
        }

        return Array.Empty<CategoryGroup>();
    }

    public async Task<IReadOnlyList<Producer>> GetProducersAsync(CancellationToken ct = default)
    {
        var url = "https://www.ime.co.ir/subsystems/ime/services/home/imedata.asmx/GetProducers";
        var payload = JsonSerializer.Serialize(new { Language = 8 });

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
        };

        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadAsStringAsync(ct);

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("d", out var d) && d.ValueKind == JsonValueKind.Array)
        {
            return d.EnumerateArray()
                .Select(x => new Producer(
                    TryGetInt(x, "code"),
                    TryGetString(x, "name") ?? ""))
                .ToList();
        }

        return Array.Empty<Producer>();
    }

    private static int TryGetInt(JsonElement obj, string key)
    {
        if (!obj.TryGetProperty(key, out var v)) return 0;
        return v.ValueKind switch
        {
            JsonValueKind.Number when v.TryGetInt32(out var n) => n,
            JsonValueKind.String when int.TryParse(v.GetString(), out var ns) => ns,
            _ => 0
        };
    }

    private static string? TryGetString(JsonElement obj, string key)
    {
        if (!obj.TryGetProperty(key, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            _ => v.ToString()
        };
    }
}

