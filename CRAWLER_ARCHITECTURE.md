# IME Crawler Architecture & Recommendations

## ✅ Recommendation: **API Adapter** (Already Implemented)

You're already using the **best approach** - an **API Adapter**. Here's why:

### Why API Adapter?
1. ✅ **The website exposes a proper API endpoint**: `/subsystems/ime/auction/auction.ashx`
2. ✅ **Returns structured JSON data** - no HTML parsing needed
3. ✅ **More reliable** - less likely to break when the website's HTML changes
4. ✅ **More efficient** - smaller payload, faster processing
5. ✅ **Respectful** - uses the same endpoint the website uses internally

### Why NOT Scraper?
- ❌ Scrapers parse HTML, which is fragile and breaks when HTML structure changes
- ❌ More complex to maintain
- ❌ Unnecessary when an API exists

### Why NOT Crawler?
- ❌ Crawlers navigate pages and follow links
- ❌ Overkill for a single data endpoint
- ❌ More resource-intensive

## Implementation Overview

### Components

1. **`ImeAuctionClient`** - Calls the IME API endpoint
2. **`ImeAuctionResponseParser`** - Parses JSON responses
3. **`ImeCrawlOrchestrator`** - Orchestrates the crawl process (fetch → parse → store → screenshot)
4. **`DailyCrawlService`** - Background service that runs daily (NEW)
5. **`CrawlScheduler`** - Helper for date conversion and finding missing dates (NEW)

### Daily Automation

The `DailyCrawlService` runs automatically:
- **Default time**: 16:30 UTC (~20:00 Tehran time, after market close)
- **Configurable**: Set `CrawlSchedule:DailyRunTime` in `appsettings.json`
- **Smart scheduling**: Calculates next run time automatically
- **Error handling**: Retries on errors, logs everything
- **Duplicate prevention**: Checks if data already exists before crawling

### API Endpoints

#### Manual Crawl
```http
POST /api/crawl/day?jalali=1404/10/14&mainGroupId=0&mainGroupName=All&m=0&c=0&s=0&p=0
POST /api/crawl/today
```

#### Check Missing Dates
```http
GET /api/crawl/missing?startJalali=1404/10/01&endJalali=1404/10/14
```

#### Statistics
```http
GET /api/crawl/stats
```

## Configuration

### appsettings.json
```json
{
  "CrawlSchedule": {
    "DailyRunTime": "16:30"  // UTC time (HH:mm format)
  }
}
```

### Time Zone Notes
- The service runs in **UTC**
- Default 16:30 UTC ≈ 20:00 Tehran time (UTC+3:30)
- Adjust `DailyRunTime` based on when you want the crawl to run

## Database Schema

### `ImeOffer` Table
- Stores individual auction offers
- Includes raw payload for traceability
- Indexed by `Day` and `SourcePk`

### `ImeSnapshot` Table
- Stores daily snapshots (screenshots)
- One record per day per main group
- Used to track what's been crawled

## How It Works

1. **Daily Trigger**: `DailyCrawlService` wakes up at the configured time
2. **Date Conversion**: Converts current UTC date to Jalali calendar
3. **Duplicate Check**: Verifies if today's data already exists
4. **API Call**: Calls `/subsystems/ime/auction/auction.ashx` with parameters
5. **Parse**: Extracts structured data from JSON response
6. **Store**: Saves offers to `ImeOffers` table
7. **Screenshot**: Generates PNG snapshot and saves to `wwwroot/reports/`
8. **Record**: Creates entry in `ImeSnapshots` table

## Parameters Explained

The API endpoint accepts these parameters:
- `fr=false` - Format flag
- `f` & `t` - From/To dates (Jalali format: yyyy/MM/dd)
- `m` - Main group ID (0 = all)
- `c` - Category ID (0 = all)
- `s` - Subcategory ID (0 = all)
- `p` - Producer ID (0 = all)
- `lang=8` - Language (8 = Persian)

## Monitoring & Maintenance

### Logs
Check application logs for:
- Daily crawl start/completion
- Errors and retries
- Number of records inserted

### Health Checks
- Use `/api/crawl/stats` to see latest crawl status
- Use `/api/crawl/missing` to find gaps in data

### Manual Backfill
If you need to backfill missing dates:
```bash
# Find missing dates
curl http://localhost:5041/api/crawl/missing?startJalali=1404/10/01&endJalali=1404/10/14

# Crawl each missing date
curl -X POST "http://localhost:5041/api/crawl/day?jalali=1404/10/01"
```

## Best Practices

1. ✅ **Keep using the API adapter** - it's the right approach
2. ✅ **Monitor logs** - check daily for errors
3. ✅ **Review snapshots** - verify data quality periodically
4. ✅ **Backup database** - ensure data persistence
5. ✅ **Set up alerts** - notify on crawl failures

## Troubleshooting

### Crawl not running?
- Check logs for `DailyCrawlService` messages
- Verify `CrawlSchedule:DailyRunTime` is set correctly
- Ensure the application is running (background service needs app to be up)

### Missing data?
- Use `/api/crawl/missing` to find gaps
- Manually trigger crawls for missing dates
- Check if API endpoint is accessible

### API errors?
- Verify network connectivity to `ime.co.ir`
- Check if API endpoint structure changed
- Review `ImeAuctionClient` retry logic

