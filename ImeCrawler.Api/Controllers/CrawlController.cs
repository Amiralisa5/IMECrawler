using ImeCrawler.Api.Data;
using ImeCrawler.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ImeCrawler.Api.Controllers;

[ApiController]
[Route("api/crawl")]
public sealed class CrawlController : ControllerBase
{
    private readonly ImeCrawlOrchestrator _orchestrator;
    private readonly CrawlScheduler _scheduler;
    private readonly AppDbContext _db;
    private readonly ImeOptions _ime;

    public CrawlController(
        ImeCrawlOrchestrator orchestrator,
        CrawlScheduler scheduler,
        AppDbContext db,
        IOptions<ImeOptions> ime)
    {
        _orchestrator = orchestrator;
        _scheduler = scheduler;
        _db = db;
        _ime = ime.Value;
    }

    /// <summary>
    /// Crawl the petrochemical hall for the NEXT day ("عرضه‌های تالار پتروشیمی در روز بعد") and export
    /// Excel + PDF + PNG. This is what the daily background job runs automatically.
    /// Example: POST /api/crawl/petrochemical/next-day
    /// </summary>
    [HttpPost("petrochemical/next-day")]
    public async Task<IActionResult> CrawlPetrochemicalNextDay(CancellationToken ct = default)
    {
        var iranDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3.5));
        var greg = iranDate.AddDays(_ime.DaysAhead);
        var jalali = CrawlScheduler.ToJalali(greg);

        var result = await _orchestrator.CrawlPetrochemicalAsync(greg, jalali, ct);
        return Ok(new { hall = _ime.MainGroupName, jalali, gregorian = greg.ToString("yyyy-MM-dd"), result });
    }

    /// <summary>
    /// Crawl the petrochemical hall for a specific Jalali day and export Excel + PDF + PNG.
    /// Example: POST /api/crawl/petrochemical/day?jalali=1405/04/08
    /// </summary>
    [HttpPost("petrochemical/day")]
    public async Task<IActionResult> CrawlPetrochemicalDay([FromQuery] string jalali, CancellationToken ct = default)
    {
        var greg = CrawlScheduler.FromJalali(jalali);
        var result = await _orchestrator.CrawlPetrochemicalAsync(greg, jalali, ct);
        return Ok(new { hall = _ime.MainGroupName, jalali, gregorian = greg.ToString("yyyy-MM-dd"), result });
    }

    /// <summary>
    /// Run one day (you pass jalali date string exactly like the site)
    /// Example: POST /api/crawl/day?jalali=1404/10/02&mainGroupId=0&mainGroupName=All&m=0&c=0&s=0&p=0
    /// </summary>
    [HttpPost("day")]
    public async Task<IActionResult> CrawlDay(
        [FromQuery] string jalali,
        [FromQuery] int mainGroupId = 0,
        [FromQuery] string mainGroupName = "All",
        [FromQuery] int m = 0, [FromQuery] int c = 0, [FromQuery] int s = 0, [FromQuery] int p = 0,
        CancellationToken ct = default)
    {
        var greg = CrawlScheduler.FromJalali(jalali);

        var (inserted, snapshotUrl) = await _orchestrator.CrawlOneDayAsync(
            greg, jalali, mainGroupId, mainGroupName, m, c, s, p, ct);

        return Ok(new { jalali, gregorian = greg.ToString("yyyy-MM-dd"), inserted, snapshotUrl });
    }

    /// <summary>
    /// Crawl today's data (uses current date in Jalali calendar)
    /// Example: POST /api/crawl/today
    /// </summary>
    [HttpPost("today")]
    public async Task<IActionResult> CrawlToday(
        [FromQuery] int mainGroupId = 0,
        [FromQuery] string mainGroupName = "All",
        [FromQuery] int m = 0, [FromQuery] int c = 0, [FromQuery] int s = 0, [FromQuery] int p = 0,
        CancellationToken ct = default)
    {
        var todayGregorian = DateOnly.FromDateTime(DateTime.UtcNow);
        var todayJalali = CrawlScheduler.ToJalali(todayGregorian);

        var (inserted, snapshotUrl) = await _orchestrator.CrawlOneDayAsync(
            todayGregorian, todayJalali, mainGroupId, mainGroupName, m, c, s, p, ct);

        return Ok(new { jalali = todayJalali, gregorian = todayGregorian.ToString("yyyy-MM-dd"), inserted, snapshotUrl });
    }

    /// <summary>
    /// Get missing dates that need to be crawled
    /// Example: GET /api/crawl/missing?startJalali=1404/10/01&endJalali=1404/10/14
    /// </summary>
    [HttpGet("missing")]
    public async Task<IActionResult> GetMissingDates(
        [FromQuery] string? startJalali = null,
        [FromQuery] string? endJalali = null,
        CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = startJalali != null ? CrawlScheduler.FromJalali(startJalali) : today.AddDays(-30);
        var end = endJalali != null ? CrawlScheduler.FromJalali(endJalali) : today;

        var missing = await _scheduler.GetMissingDatesAsync(start, end, ct);

        return Ok(new
        {
            startDate = start.ToString("yyyy-MM-dd"),
            endDate = end.ToString("yyyy-MM-dd"),
            missingCount = missing.Count,
            missingDates = missing.Select(d => new
            {
                gregorian = d.ToString("yyyy-MM-dd"),
                jalali = CrawlScheduler.ToJalali(d)
            })
        });
    }

    /// <summary>
    /// Get crawl statistics
    /// Example: GET /api/crawl/stats
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct = default)
    {
        var totalOffers = await _db.ImeOffers.CountAsync(ct);
        var totalSnapshots = await _db.ImeSnapshots.CountAsync(ct);
        var latestSnapshot = await _db.ImeSnapshots
            .OrderByDescending(x => x.Day)
            .ThenByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        return Ok(new
        {
            totalOffers,
            totalSnapshots,
            latestSnapshot = latestSnapshot != null ? new
            {
                day = latestSnapshot.Day.ToString("yyyy-MM-dd"),
                jalali = CrawlScheduler.ToJalali(latestSnapshot.Day),
                mainGroupName = latestSnapshot.MainGroupName,
                offerCount = latestSnapshot.OfferCount,
                imageUrl = latestSnapshot.ImageUrl,
                pdfUrl = latestSnapshot.PdfUrl,
                excelUrl = latestSnapshot.ExcelUrl,
                createdAt = latestSnapshot.CreatedAtUtc
            } : null
        });
    }
}
