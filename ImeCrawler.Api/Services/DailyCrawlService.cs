using ImeCrawler.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ImeCrawler.Api.Services;

/// <summary>
/// Background service that runs daily to crawl IME auction data.
/// Runs at a configured time (default: 2:00 AM UTC) every day.
/// </summary>
public sealed class DailyCrawlService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DailyCrawlService> _logger;
    private readonly TimeSpan _runTime;

    public DailyCrawlService(
        IServiceProvider serviceProvider,
        ILogger<DailyCrawlService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        // Default to 2:00 AM UTC, configurable via appsettings
        var hour = configuration.GetValue<int>("CrawlSchedule:Hour", 2);
        var minute = configuration.GetValue<int>("CrawlSchedule:Minute", 0);
        _runTime = new TimeSpan(hour, minute, 0);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DailyCrawlService started. Will run daily at {RunTime} UTC", _runTime);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var nextRun = now.Date.Add(_runTime);

            // If the scheduled time has passed today, schedule for tomorrow
            if (nextRun <= now)
            {
                nextRun = nextRun.AddDays(1);
            }

            var delay = nextRun - now;
            _logger.LogInformation("Next crawl scheduled for {NextRun} UTC (in {Delay})", nextRun, delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            if (stoppingToken.IsCancellationRequested)
                break;

            try
            {
                _logger.LogInformation("Starting daily crawl at {Time} UTC", DateTime.UtcNow);
                await RunDailyCrawlAsync(stoppingToken);
                _logger.LogInformation("Daily crawl completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during daily crawl");
            }
        }

        _logger.LogInformation("DailyCrawlService stopped");
    }

    private async Task RunDailyCrawlAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<ImeCrawlOrchestrator>();
        var categoryService = scope.ServiceProvider.GetRequiredService<ImeCategoryService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Get today's Jalali date
        var todayJalali = JalaliDateHelper.TodayJalali();
        var todayGregorian = JalaliDateHelper.JalaliToGregorian(todayJalali);

        _logger.LogInformation("Crawling data for {JalaliDate} ({GregorianDate})", todayJalali, todayGregorian);

        // Check if we've already crawled today
        // We check both ImeSnapshots (complete crawl) and ImeOffers (partial crawl)
        // because CrawlOneDayAsync saves offers BEFORE creating the snapshot.
        // If an error occurs between saving offers and snapshot, we'd otherwise insert duplicates.
        var snapshotExists = await db.ImeSnapshots
            .AnyAsync(x => x.Day == todayGregorian && x.MainGroupId == 0, ct);

        var offersExist = await db.ImeOffers
            .AnyAsync(x => x.Day == todayGregorian && x.MainGroupId == 0, ct);

        if (snapshotExists || offersExist)
        {
            if (snapshotExists)
            {
                _logger.LogInformation("Snapshot for {Date} already exists. Skipping crawl.", todayJalali);
            }
            else
            {
                _logger.LogWarning(
                    "Offers for {Date} exist but snapshot is missing. This may indicate a previous partial crawl. Skipping to avoid duplicates.",
                    todayJalali);
            }
            return;
        }

        // Fetch main groups
        var mainGroups = await categoryService.GetMainGroupsAsync(ct);
        _logger.LogInformation("Found {Count} main groups", mainGroups.Count);

        var totalInserted = 0;

        // Crawl "All" group first (m=0, c=0, s=0, p=0)
        try
        {
            var (inserted, snapshotUrl) = await orchestrator.CrawlOneDayAsync(
                todayGregorian, todayJalali, 0, "Ù‡Ù…Ù‡ Ú¯Ø±ÙˆÙ‡â€ŒÙ‡Ø§", 0, 0, 0, 0, ct);
            totalInserted += inserted;
            _logger.LogInformation("Crawled 'All Groups': {Inserted} offers, snapshot: {SnapshotUrl}", inserted, snapshotUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crawling 'All Groups'");
        }

        // Optionally crawl each main group individually
        // This gives you more granular snapshots but takes longer
        var crawlIndividualGroups = scope.ServiceProvider
            .GetRequiredService<IConfiguration>()
            .GetValue<bool>("CrawlSettings:CrawlIndividualGroups", false);

        if (crawlIndividualGroups)
        {
            foreach (var mainGroup in mainGroups)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    var (inserted, snapshotUrl) = await orchestrator.CrawlOneDayAsync(
                        todayGregorian, todayJalali, mainGroup.Code, mainGroup.Name,
                        mainGroup.Code, 0, 0, 0, ct);
                    totalInserted += inserted;
                    _logger.LogInformation(
                        "Crawled '{GroupName}' (ID: {GroupId}): {Inserted} offers, snapshot: {SnapshotUrl}",
                        mainGroup.Name, mainGroup.Code, inserted, snapshotUrl);

                    // Small delay to avoid overwhelming the server
                    await Task.Delay(TimeSpan.FromSeconds(2), ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error crawling main group '{GroupName}' (ID: {GroupId})", mainGroup.Name, mainGroup.Code);
                }
            }
        }

        _logger.LogInformation("Daily crawl completed. Total offers inserted: {TotalInserted}", totalInserted);
    }
}
