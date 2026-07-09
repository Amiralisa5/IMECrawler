using System.Globalization;
using ImeCrawler.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ImeCrawler.Api.Services;

/// <summary>
/// Background service that runs daily and crawls the NEXT day's petrochemical-hall offerings
/// (عرضه‌های تالار پتروشیمی در روز بعد), exporting PNG + PDF + Excel.
/// Runs at a configured time (default: 16:30 UTC, which is ~20:00 Tehran time).
/// </summary>
public sealed class DailyCrawlService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DailyCrawlService> _logger;
    private readonly ImeOptions _ime;
    private readonly TimeSpan _runTime; // Time of day to run (UTC)

    public DailyCrawlService(
        IServiceProvider serviceProvider,
        ILogger<DailyCrawlService> logger,
        IConfiguration configuration,
        IOptions<ImeOptions> ime)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _ime = ime.Value;

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

                _logger.LogInformation("Next crawl scheduled for {NextRun} UTC (in {Delay})", nextRun, delay);

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
        _logger.LogInformation("Starting daily petrochemical crawl at {Time} UTC", DateTime.UtcNow);

        using var scope = _serviceProvider.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<ImeCrawlOrchestrator>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Target the NEXT day ("روز بعد"). Approximate Iran local date via UTC+3:30, then look ahead.
        var iranDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3.5));
        var targetGregorian = iranDate.AddDays(_ime.DaysAhead);
        var targetJalali = ConvertToJalali(targetGregorian);

        _logger.LogInformation("Crawling {Hall} offerings for Jalali {Jalali} (Gregorian {Gregorian})",
            _ime.MainGroupName, targetJalali, targetGregorian);

        // Skip if we've already captured this day's petrochemical snapshot.
        var alreadyCrawled = await db.ImeSnapshots
            .AnyAsync(x => x.Day == targetGregorian && x.MainGroupId == _ime.MainGroupId, ct);
        if (alreadyCrawled)
        {
            _logger.LogInformation("Data for {Date} already exists. Skipping crawl.", targetJalali);
            return;
        }

        var result = await orchestrator.CrawlPetrochemicalAsync(targetGregorian, targetJalali, ct);

        _logger.LogInformation(
            "Daily crawl completed. Offerings: {Count}. Excel: {Excel} · PDF: {Pdf} · PNG: {Png}",
            result.offerCount, result.excelUrl, result.pdfUrl, result.imageUrl);
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
