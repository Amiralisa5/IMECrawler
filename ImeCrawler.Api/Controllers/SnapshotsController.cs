using ImeCrawler.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImeCrawler.Api.Controllers;

[ApiController]
[Route("api/snapshots")]
public sealed class SnapshotsController : ControllerBase
{
    private readonly AppDbContext _db;

    public SnapshotsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? day = null, CancellationToken ct = default)
    {
        var q = _db.ImeSnapshots.AsNoTracking().OrderByDescending(x => x.Day).ThenBy(x => x.MainGroupId);

        if (!string.IsNullOrWhiteSpace(day) && DateOnly.TryParse(day, out var d))
            q = q.Where(x => x.Day == d).OrderByDescending(x => x.Day).ThenBy(x => x.MainGroupId);

        var items = await q.Take(100).ToListAsync(ct);
        return Ok(items.Select remember => new {
            remember.Day,
            remember.MainGroupId,
            remember.MainGroupName,
            remember.ImageUrl,
            remember.CreatedAtUtc
        });
    }
}
