using ImeCrawler.Api.Data;
using ImeCrawler.Api.Services;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Extensions.Http;

var builder = WebApplication.CreateBuilder(args);

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// DB
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer")));

// HttpClient for IME
builder.Services.AddHttpClient<ImeAuctionClient>()
    .SetHandlerLifetime(TimeSpan.FromMinutes(5))
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(3, i => TimeSpan.FromSeconds(2 * i)));

// Crawler pipeline
builder.Services.AddSingleton<ImeAuctionResponseParser>();
builder.Services.AddSingleton<HtmlReportRenderer>();
builder.Services.AddSingleton<IHtmlToImage, PlaywrightHtmlToImage>();
builder.Services.AddScoped<ImeCrawlOrchestrator>();
builder.Services.AddScoped<CrawlScheduler>();

// Background service for daily crawling
builder.Services.AddHostedService<DailyCrawlService>();

var app = builder.Build();

// Ensure the Playwright Chromium browser is present (idempotent; downloads on first run only).
// Best-effort: a locked-down/offline host will log a warning instead of crashing the app.
try
{
    var exitCode = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
    if (exitCode != 0)
        app.Logger.LogWarning("Playwright browser install returned exit code {ExitCode}; screenshots may fail.", exitCode);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Playwright browser install failed; screenshots may not work until 'playwright install chromium' is run.");
}

// Create the database/schema on startup (no migrations in this project).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseStaticFiles(); // serve wwwroot/*
app.MapControllers();

app.Run();
