using ImeCrawler.Api.Data;
using ImeCrawler.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ImeCrawler.Api.Services;

public sealed record PetrochemicalCrawlResult(
    int inserted, int offerCount, string imageUrl, string pdfUrl, string excelUrl);

public sealed class ImeCrawlOrchestrator
{
    private readonly ImeAuctionClient _client;
    private readonly ImeAuctionResponseParser _parser;
    private readonly HtmlReportRenderer _renderer;
    private readonly IHtmlToImage _htmlToImage;
    private readonly ExcelReportExporter _excel;
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ImeOptions _ime;

    public ImeCrawlOrchestrator(
        ImeAuctionClient client,
        ImeAuctionResponseParser parser,
        HtmlReportRenderer renderer,
        IHtmlToImage htmlToImage,
        ExcelReportExporter excel,
        AppDbContext db,
        IWebHostEnvironment env,
        IOptions<ImeOptions> ime)
    {
        _client = client;
        _parser = parser;
        _renderer = renderer;
        _htmlToImage = htmlToImage;
        _excel = excel;
        _db = db;
        _env = env;
        _ime = ime.Value;
    }

    public async Task<(int inserted, string snapshotUrl)> CrawlOneDayAsync(
        DateOnly dayGregorian,
        string jalaliDate,
        int mainGroupId,
        string mainGroupName,
        int m, int c, int s, int p,
        CancellationToken ct)
    {
        // 1) fetch
        var raw = await _client.FetchAsync(jalaliDate, m, c, s, p, ct);

        // 2) parse
        var parsed = _parser.Parse(raw);

        // 3) store rows (sorted later for screenshot)
        // If parse failed, we still store one row with raw payload.
        var entities = parsed.Select(x => new ImeOffer
        {
            Day = dayGregorian,
            MainGroupId = mainGroupId,
            MainGroupName = mainGroupName,
            SourcePk = x.SourcePk,
            ProductName = x.ProductName,
            Symbol = x.Symbol,
            Talar = x.Talar,
            Broker = x.Broker,
            RawPayload = x.RawPayload
        }).ToList();

        _db.ImeOffers.AddRange(entities);
        var inserted = await _db.SaveChangesAsync(ct);

        // 4) beautify + sort + screenshot
        var sorted = parsed
            .OrderBy(x => x.ProductName ?? "")
            .ThenBy(x => x.Symbol ?? "")
            .ToList();

        var html = _renderer.Render($"IME - {mainGroupName} - {jalaliDate}", sorted);
        var png = await _htmlToImage.RenderPngAsync(html, ct);

        // 5) write to wwwroot
        var absDir = EnsureReportDir(dayGregorian, out var relDir);
        var fileName = $"ime_{Slug(mainGroupName)}_{dayGregorian:yyyyMMdd}.png";
        await File.WriteAllBytesAsync(Path.Combine(absDir, fileName), png, ct);

        var snapshotUrl = ToUrl(relDir, fileName);

        // 6) store snapshot record
        _db.ImeSnapshots.Add(new ImeSnapshot
        {
            Day = dayGregorian,
            MainGroupId = mainGroupId,
            MainGroupName = mainGroupName,
            ImageUrl = snapshotUrl,
            OfferCount = parsed.Count
        });
        await _db.SaveChangesAsync(ct);

        return (inserted, snapshotUrl);
    }

    /// <summary>
    /// Crawl all offerings for a single day, keep only the petrochemical hall (تالار پتروشیمی) via the
    /// configured Talar keyword, store them, and export PNG + PDF + Excel to wwwroot.
    /// </summary>
    public async Task<PetrochemicalCrawlResult> CrawlPetrochemicalAsync(
        DateOnly dayGregorian, string jalaliDate, CancellationToken ct)
    {
        // 1) fetch the day's offerings (optionally narrowed by configured group ids)
        var raw = await _client.FetchAsync(jalaliDate, _ime.MainGroupId, _ime.CategoryId, _ime.SubCategoryId, _ime.ProducerId, ct);

        // 2) parse, then keep only the petrochemical hall by Talar keyword (client-side filter)
        var parsedAll = _parser.Parse(raw);
        var parsed = string.IsNullOrWhiteSpace(_ime.TalarKeyword)
            ? parsedAll.ToList()
            : parsedAll.Where(x => (x.Talar ?? "").Contains(_ime.TalarKeyword)).ToList();

        // 3) store offers
        var entities = parsed.Select(x => new ImeOffer
        {
            Day = dayGregorian,
            MainGroupId = _ime.MainGroupId,
            MainGroupName = _ime.MainGroupName,
            SourcePk = x.SourcePk,
            ProductName = x.ProductName,
            Symbol = x.Symbol,
            Talar = x.Talar,
            Broker = x.Broker,
            RawPayload = x.RawPayload
        }).ToList();
        _db.ImeOffers.AddRange(entities);
        var inserted = await _db.SaveChangesAsync(ct);

        // 4) build one HTML report and export it three ways
        var sorted = parsed
            .OrderBy(x => x.ProductName ?? "")
            .ThenBy(x => x.Symbol ?? "")
            .ToList();

        var title = $"عرضه‌های {_ime.MainGroupName} — {jalaliDate}";
        var html = _renderer.Render(title, sorted);

        var absDir = EnsureReportDir(dayGregorian, out var relDir);
        // Fixed ASCII base name keeps the served URLs clean (the hall is always petrochemical here).
        var baseName = $"ime_petrochemical_{dayGregorian:yyyyMMdd}";

        var png = await _htmlToImage.RenderPngAsync(html, ct);
        await File.WriteAllBytesAsync(Path.Combine(absDir, baseName + ".png"), png, ct);

        var pdf = await _htmlToImage.RenderPdfAsync(html, ct);
        await File.WriteAllBytesAsync(Path.Combine(absDir, baseName + ".pdf"), pdf, ct);

        var xlsx = _excel.Build(title, sorted);
        await File.WriteAllBytesAsync(Path.Combine(absDir, baseName + ".xlsx"), xlsx, ct);

        var imageUrl = ToUrl(relDir, baseName + ".png");
        var pdfUrl = ToUrl(relDir, baseName + ".pdf");
        var excelUrl = ToUrl(relDir, baseName + ".xlsx");

        // 5) record snapshot
        _db.ImeSnapshots.Add(new ImeSnapshot
        {
            Day = dayGregorian,
            MainGroupId = _ime.MainGroupId,
            MainGroupName = _ime.MainGroupName,
            ImageUrl = imageUrl,
            PdfUrl = pdfUrl,
            ExcelUrl = excelUrl,
            OfferCount = parsed.Count
        });
        await _db.SaveChangesAsync(ct);

        return new PetrochemicalCrawlResult(inserted, parsed.Count, imageUrl, pdfUrl, excelUrl);
    }

    private string EnsureReportDir(DateOnly day, out string relDir)
    {
        var webRoot = _env.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            // webapi template sometimes lacks wwwroot until created
            webRoot = Path.Combine(_env.ContentRootPath, "wwwroot");
            Directory.CreateDirectory(webRoot);
        }
        relDir = Path.Combine("reports", "ime", day.Year.ToString(), day.Month.ToString("D2"));
        var absDir = Path.Combine(webRoot, relDir);
        Directory.CreateDirectory(absDir);
        return absDir;
    }

    private static string ToUrl(string relDir, string fileName)
        => "/" + Path.Combine(relDir, fileName).Replace("\\", "/");

    private static string Slug(string s)
    {
        // ASCII-only slug so served file URLs stay clean (non-ASCII, e.g. Persian, is dropped).
        var cleaned = new string(s.Where(ch =>
            (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9')
            || ch == ' ' || ch == '-' || ch == '_').ToArray());
        cleaned = cleaned.Trim().Replace(' ', '_');
        if (string.IsNullOrWhiteSpace(cleaned)) cleaned = "group";
        return cleaned.Length > 60 ? cleaned[..60] : cleaned;
    }
}
