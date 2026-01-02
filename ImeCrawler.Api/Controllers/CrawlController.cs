using ImeCrawler.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ImeCrawler.Api.Controllers;

[ApiController]
[Route("api/crawl")]
public sealed class CrawlController : ControllerBase
{
    private readonly ImeCrawlOrchestrator _orchestrator;

    public CrawlController(ImeCrawlOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    // Run one day (you pass jalali date string exactly like the site)
    // Example: POST /api/crawl/day?jalali=1404/10/02&mainGroupId=0&mainGroupName=All&m=0&c=0&s=0&p=0
    [HttpPost("day")]
    public async Task<IActionResult> CrawlDay(
        [FromQuery] string jalali,
        [FromQuery] int mainGroupId = 0,
        [FromQuery] string mainGroupName = "All",
        [FromQuery] int m = 0, [FromQuery] int c = 0, [FromQuery] int s = 0, [FromQuery] int p = 0,
        CancellationToken ct = default)
    {
        // We need a Gregorian DateOnly for DB partitioning and folder structure.
        // Convert Jalali -> Gregorian using JalaliDateHelper.
        var greg = JalaliDateHelper.JalaliToGregorian(jalali);

        var (inserted, snapshotUrl) = await _orchestrator.CrawlOneDayAsync(
            greg, jalali, mainGroupId, mainGroupName, m, c, s, p, ct);

        return Ok(new { jalali, gregorian = greg.ToString("yyyy-MM-dd"), inserted, snapshotUrl });
    }
}
