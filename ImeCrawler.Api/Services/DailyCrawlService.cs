using System.Globalization;
using ImeCrawler.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ImeCrawler.Api.Services;

/// <summary>
/// Background service that runs daily to crawl IME auction data.
/// Runs at a configured time (default: 16:30 UTC, which is ~20:00 Tehran time).
/// </summary>
public sealed class DailyCrawlService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DailyCrawlService> _logger;
    private readonly TimeSpan _runTime; // Time of day to run (UTC)

    public DailyCrawlService(
        IServiceProvider serviceProvider,
        ILogger<DailyCrawlService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        
        // Default: 16:30 UTC (~20:00 Tehran time, after market close)
        var configuredTime = configuration["CrawlSchedule:DailyRunTime"] ?? "16:30";
        if (TimeSpan.TryParse(configuredTime, out var ts))
            _runTime = ts;
        else
            _runTime = new TimeSpan(16, 30, 0);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DailyCrawlService started. Will run daily at {RunTime} UTC", _runTime);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                var nextRun = CalculateNextRunTime(now);
                var delay = nextRun - now;

                _logger.LogInformation("Next crawl scheduled for {NextRun} UTC (in {Delay})", 
                    nextRun, delay);

                await Task.Delay(delay, stoppingToken);

                if (stoppingToken.IsCancellationRequested)
                    break;

                await PerformDailyCrawlAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("DailyCrawlService is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DailyCrawlService. Will retry on next scheduled time.");
                // Wait 1 hour before retrying on error
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }

    private DateTime CalculateNextRunTime(DateTime now)
    {
        var today = now.Date.Add(_runTime);
        
        // If we've already passed today's run time, schedule for tomorrow
        if (now >= today)
            return today.AddDays(1);
        
        return today;
    }

    private async Task PerformDailyCrawlAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting daily crawl at {Time} UTC", DateTime.UtcNow);

        using var scope = _serviceProvider.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<ImeCrawlOrchestrator>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Get today's date in both calendars
        var todayGregorian = DateOnly.FromDateTime(DateTime.UtcNow);
        var todayJalali = ConvertToJalali(todayGregorian);

        _logger.LogInformation("Crawling data for Jalali date: {Jalali} (Gregorian: {Gregorian})", 
            todayJalali, todayGregorian);

        // Check if we've already crawled today
        var alreadyCrawled = await db.ImeSnapshots
            .AnyAsync(x => x.Day == todayGregorian && x.MainGroupId == 0, ct);

        if (alreadyCrawled)
        {
            _logger.LogInformation("Data for {Date} already exists. Skipping crawl.", todayJalali);
            return;
        }

        // Crawl all groups (mainGroupId=0 means all)
        // Parameters: m=0, c=0, s=0, p=0 means "all groups"
        var (inserted, snapshotUrl) = await orchestrator.CrawlOneDayAsync(
            todayGregorian,
            todayJalali,
            mainGroupId: 0,
            mainGroupName: "All",
            m: 0, c: 0, s: 0, p: 0,
            ct);

        _logger.LogInformation("Daily crawl completed. Inserted {Count} offers. Snapshot: {SnapshotUrl}", 
            inserted, snapshotUrl);
    }

    private static string ConvertToJalali(DateOnly gregorian)
    {
        var pc = new PersianCalendar();
        var dt = gregorian.ToDateTime(TimeOnly.MinValue);
        var year = pc.GetYear(dt);
        var month = pc.GetMonth(dt);
        var day = pc.GetDayOfMonth(dt);
        return $"{year}/{month:D2}/{day:D2}";
    }
}

