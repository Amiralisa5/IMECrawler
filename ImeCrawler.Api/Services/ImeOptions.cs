namespace ImeCrawler.Api.Services;

/// <summary>
/// Configuration for the petrochemical-hall daily crawl (عرضه‌های تالار پتروشیمی).
/// Bound from the "Ime" section of appsettings.json.
/// </summary>
public sealed class ImeOptions
{
    public const string Section = "Ime";

    /// <summary>
    /// Client-side hall filter: keep only offerings whose Talar (تالار) text contains this keyword.
    /// Empty = keep every row returned for the day. Default targets the petrochemical hall.
    /// </summary>
    public string TalarKeyword { get; set; } = "پتروشیمی";

    /// <summary>Human-readable label used in report titles/filenames and stored on the snapshot.</summary>
    public string MainGroupName { get; set; } = "تالار پتروشیمی";

    // Optional server-side filters forwarded to auction.ashx (0 = all). If you know the numeric
    // group/category ids for the petrochemical hall you can set them here to narrow the request;
    // otherwise leave 0 and rely on the TalarKeyword client-side filter above.
    public int MainGroupId { get; set; }
    public int CategoryId { get; set; }
    public int SubCategoryId { get; set; }
    public int ProducerId { get; set; }

    /// <summary>How many days ahead to crawl. 1 = "روز بعد" (tomorrow's offering announcements).</summary>
    public int DaysAhead { get; set; } = 1;
}
