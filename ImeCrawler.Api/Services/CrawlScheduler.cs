using System.Globalization;
using ImeCrawler.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ImeCrawler.Api.Services;

/// <summary>
/// Service to determine what dates and groups need to be crawled.
/// Can be used for backfilling missing data or manual scheduling.
/// </summary>
public sealed class CrawlScheduler
{
    private readonly AppDbContext _db;
    private readonly ILogger<CrawlScheduler> _logger;

    public CrawlScheduler(AppDbContext db, ILogger<CrawlScheduler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Gets dates that need to be crawled (missing from database).
    /// </summary>
    public async Task<List<DateOnly>> GetMissingDatesAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default)
    {
        var crawledDates = await _db.ImeSnapshots
            .Where(x => x.Day >= startDate && x.Day <= endDate && x.MainGroupId == 0)
            .Select(x => x.Day)
            .Distinct()
            .ToListAsync(ct);

        var allDates = new List<DateOnly>();
        var current = startDate;
        while (current <= endDate)
        {
            allDates.Add(current);
            current = current.AddDays(1);
        }

        var missing = allDates.Except(crawledDates).ToList();
        _logger.LogInformation("Found {MissingCount} missing dates between {Start} and {End}", 
            missing.Count, startDate, endDate);

        return missing;
    }

    /// <summary>
    /// Converts a Gregorian date to Jalali format (yyyy/MM/dd).
    /// </summary>
    public static string ToJalali(DateOnly gregorian)
    {
        var pc = new PersianCalendar();
        var dt = gregorian.ToDateTime(TimeOnly.MinValue);
        var year = pc.GetYear(dt);
        var month = pc.GetMonth(dt);
        var day = pc.GetDayOfMonth(dt);
        return $"{year}/{month:D2}/{day:D2}";
    }

    /// <summary>
    /// Converts a Jalali date string to Gregorian DateOnly.
    /// </summary>
    public static DateOnly FromJalali(string jalali)
    {
        var parts = jalali.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
            throw new ArgumentException("Invalid jalali date format. Expected yyyy/MM/dd", nameof(jalali));

        int jy = int.Parse(parts[0]);
        int jm = int.Parse(parts[1]);
        int jd = int.Parse(parts[2]);

        var pc = new PersianCalendar();
        var dt = pc.ToDateTime(jy, jm, jd, 0, 0, 0, 0);
        return DateOnly.FromDateTime(dt);
    }
}

