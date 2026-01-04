using System.Text.Json;
using ImeCrawler.Api.Data;
using ImeCrawler.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ImeCrawler.Api.Services;

public sealed class ImeCrawlOrchestrator
{
    private readonly ImeAuctionClient _client;
    private readonly ImeAuctionResponseParser _parser;
    private readonly HtmlReportRenderer _renderer;
    private readonly IHtmlToImage _htmlToImage;
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private const string DebugLogPath = @"c:\\Users\\Amirali\\source\\repos\\ImeCrawler\\IMECrawler\\.cursor\\debug.log";

    public ImeCrawlOrchestrator(
        ImeAuctionClient client,
        ImeAuctionResponseParser parser,
        HtmlReportRenderer renderer,
        IHtmlToImage htmlToImage,
        AppDbContext db,
        IWebHostEnvironment env)
    {
        _client = client;
        _parser = parser;
        _renderer = renderer;
        _htmlToImage = htmlToImage;
        _db = db;
        _env = env;
    }

    public async Task<(int inserted, string snapshotUrl)> CrawlOneDayAsync(
        DateOnly dayGregorian,
        string jalaliDate,
        int mainGroupId,
        string mainGroupName,
        int m, int c, int s, int p,
        CancellationToken ct)
    {
        #region agent log
        System.IO.File.AppendAllText(DebugLogPath,
            JsonSerializer.Serialize(new
            {
                sessionId = "debug-session",
                runId = "pre-fix",
                hypothesisId = "H4",
                location = "ImeCrawlOrchestrator.CrawlOneDayAsync:start",
                message = "Starting crawl",
                data = new
                {
                    dayGregorian = dayGregorian.ToString("yyyy-MM-dd"),
                    jalaliDate,
                    mainGroupId,
                    mainGroupName,
                    m,
                    c,
                    s,
                    p
                },
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            }) + Environment.NewLine);
        #endregion

        // 1) fetch
        var raw = await _client.FetchAsync(jalaliDate, m, c, s, p, ct);

        // 2) parse
        var parsed = _parser.Parse(raw);

        #region agent log
        System.IO.File.AppendAllText(DebugLogPath,
            JsonSerializer.Serialize(new
            {
                sessionId = "debug-session",
                runId = "pre-fix",
                hypothesisId = "H4",
                location = "ImeCrawlOrchestrator.CrawlOneDayAsync:parse",
                message = "Parsed offers",
                data = new
                {
                    parsedCount = parsed.Count,
                    firstPk = parsed.FirstOrDefault()?.SourcePk
                },
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            }) + Environment.NewLine);
        #endregion

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
        var webRoot = _env.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            // webapi template sometimes lacks wwwroot until created
            webRoot = Path.Combine(_env.ContentRootPath, "wwwroot");
            Directory.CreateDirectory(webRoot);
        }

        var relDir = Path.Combine("reports", "ime", dayGregorian.Year.ToString(), dayGregorian.Month.ToString("D2"));
        var absDir = Path.Combine(webRoot, relDir);
        Directory.CreateDirectory(absDir);

        var fileName = $"ime_{Slug(mainGroupName)}_{dayGregorian:yyyyMMdd}.png";
        var absPath = Path.Combine(absDir, fileName);
        await File.WriteAllBytesAsync(absPath, png, ct);

        var snapshotUrl = "/" + Path.Combine(relDir, fileName).Replace("\\", "/");

        // 6) store snapshot record
        _db.ImeSnapshots.Add(new ImeSnapshot
        {
            Day = dayGregorian,
            MainGroupId = mainGroupId,
            MainGroupName = mainGroupName,
            ImageUrl = snapshotUrl
        });
        await _db.SaveChangesAsync(ct);

        #region agent log
        System.IO.File.AppendAllText(DebugLogPath,
            JsonSerializer.Serialize(new
            {
                sessionId = "debug-session",
                runId = "pre-fix",
                hypothesisId = "H5",
                location = "ImeCrawlOrchestrator.CrawlOneDayAsync:store",
                message = "Stored crawl results",
                data = new
                {
                    entitiesCount = entities.Count,
                    inserted,
                    snapshotUrl
                },
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            }) + Environment.NewLine);
        #endregion

        return (inserted, snapshotUrl);
    }

    private static string Slug(string s)
    {
        // Minimal safe slug. Good enough for filenames.
        var cleaned = new string(s.Where(ch => char.IsLetterOrDigit(ch) || ch == ' ' || ch == '-' || ch == '_').ToArray());
        cleaned = cleaned.Trim().Replace(' ', '_');
        if (string.IsNullOrWhiteSpace(cleaned)) cleaned = "group";
        return cleaned.Length > 60 ? cleaned[..60] : cleaned;
    }
}
