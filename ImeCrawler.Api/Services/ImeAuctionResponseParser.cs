using System.Text.Json;

namespace ImeCrawler.Api.Services;

public sealed record ParsedOffer(
    long? SourcePk,
    string? ProductName,
    string? Symbol,
    string? Talar,
    string? Broker,
    string RawPayload);

public sealed class ImeAuctionResponseParser
{
    // Keys from the site JS (best-effort mapping)
    private const string K_SourcePk = "bArzehRadifPK";
    private const string K_Product = "xKalaNamadKala";
    private const string K_Symbol = "bArzehRadifNamadKala";
    private const string K_Talar = "Talar";
    private const string K_Broker = "cBrokerSpcName";

    public IReadOnlyList<ParsedOffer> Parse(string raw)
    {
        raw = raw?.Trim() ?? "";
        if (raw.Length == 0) return Array.Empty<ParsedOffer>();

        // JSON
        if (raw.StartsWith("{") || raw.StartsWith("["))
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                return ParseJson(doc.RootElement, raw);
            }
            catch
            {
                // fall through
            }
        }

        // HTML (rare)
        if (raw.Contains("<table", StringComparison.OrdinalIgnoreCase))
        {
            // For now: store as one "raw" row; you can later add HtmlAgilityPack parsing if needed
            return new[] { new ParsedOffer(null, null, null, null, null, raw) };
        }

        // Delimited (unknown format): store raw only
        return new[] { new ParsedOffer(null, null, null, null, null, raw) };
    }

    private static IReadOnlyList<ParsedOffer> ParseJson(JsonElement root, string raw)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            // array of objects
            if (root.GetArrayLength() > 0 && root[0].ValueKind == JsonValueKind.Object)
            {
                return root.EnumerateArray().Select(o =>
                    new ParsedOffer(
                        TryGetLong(o, K_SourcePk),
                        TryGetString(o, K_Product),
                        TryGetString(o, K_Symbol),
                        TryGetString(o, K_Talar),
                        TryGetString(o, K_Broker),
                        raw
                    )).ToList();
            }

            // array of arrays => store raw rows only (we can map later if you paste a sample)
            return new[] { new ParsedOffer(null, null, null, null, null, raw) };
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            // common wrappers: rows/data/items/result
            foreach (var k in new[] { "rows", "data", "items", "result" })
            {
                if (root.TryGetProperty(k, out var arr) && arr.ValueKind == JsonValueKind.Array)
                    return ParseJson(arr, raw);
            }

            // single object
            return new[]
            {
                new ParsedOffer(
                    TryGetLong(root, K_SourcePk),
                    TryGetString(root, K_Product),
                    TryGetString(root, K_Symbol),
                    TryGetString(root, K_Talar),
                    TryGetString(root, K_Broker),
                    raw
                )
            };
        }

        return new[] { new ParsedOffer(null, null, null, null, null, raw) };
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

    private static long? TryGetLong(JsonElement obj, string key)
    {
        if (!obj.TryGetProperty(key, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n)) return n;
        if (v.ValueKind == JsonValueKind.String && long.TryParse(v.GetString(), out var ns)) return ns;
        return null;
    }
}
