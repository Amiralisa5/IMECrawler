using ImeCrawler.Api.Data;
using ImeCrawler.Api.Services;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Extensions.Http;
using System.IO;
using System.Text.Json;

public partial class Program
{
    private static void Main(string[] args)
    {
        // #region agent log
        const string AgentLogPath = @"c:\Users\Amirali\source\repos\ImeCrawler\IMECrawler\.cursor\debug.log";
        void AgentLog(string hypothesisId, string location, string message, object data)
        {
            var payload = new
            {
                sessionId = "debug-session",
                runId = "pre-fix",
                hypothesisId,
                location,
                message,
                data,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            File.AppendAllText(AgentLogPath, JsonSerializer.Serialize(payload) + Environment.NewLine);
        }
        // #endregion

        AgentLog("H1", "Program.Main:pre-check", "Checking appsettings.json presence and head", new
        {
            exists = File.Exists("appsettings.json"),
            length = File.Exists("appsettings.json") ? new FileInfo("appsettings.json").Length : 0,
            head = File.Exists("appsettings.json") ? File.ReadLines("appsettings.json").Take(12).ToArray() : Array.Empty<string>()
        });

        WebApplicationBuilder builder;
        try
        {
            AgentLog("H2", "Program.Main:before-builder", "Calling WebApplication.CreateBuilder", new { argsLength = args?.Length ?? 0 });
            builder = WebApplication.CreateBuilder(args);
            AgentLog("H2", "Program.Main:after-builder", "Builder created successfully", new { configSources = builder.Configuration.AsEnumerable().Count() });
        }
        catch (Exception ex)
        {
            AgentLog("H1", "Program.Main:builder-exception", "CreateBuilder failed", new { ex.Message, Type = ex.GetType().FullName, ex.StackTrace });
            throw;
        }

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

        builder.Services.AddHttpClient<ImeCategoryService>()
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

        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseStaticFiles(); // serve wwwroot/*
        app.MapControllers();

        app.Run();
    }
}