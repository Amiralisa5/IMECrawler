using ImeCrawler.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImeCrawler.Api.Controllers;

[ApiController]
[Route("api/snapshots")]
public sealed class SnapshotsController : ControllerBase
{
    private readonly AppDbContext _db;

    public SnapshotsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? day = null, CancellationToken ct = default)
    {
        var query = _db.ImeSnapshots
            .AsNoTracking()
            .OrderByDescending(x => x.Day)
            .ThenBy(x => x.MainGroupId);

        if (!string.IsNullOrWhiteSpace(day) && DateOnly.TryParse(day, out var parsedDay))
        {
            query = query
                .Where(x => x.Day == parsedDay)
                .OrderByDescending(x => x.Day)
                .ThenBy(x => x.MainGroupId);
        }

        var items = await query.Take(100).ToListAsync(ct);

        var shaped = items.Select(x => new
        {
            Day = x.Day,
            MainGroupId = x.MainGroupId,
            MainGroupName = x.MainGroupName,
            ImageUrl = x.ImageUrl,
            CreatedAtUtc = x.CreatedAtUtc
        });

        return Ok(shaped);
    }
}
