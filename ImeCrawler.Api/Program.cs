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

app.UseSwagger();
app.UseSwaggerUI();

app.UseStaticFiles(); // serve wwwroot/*
app.MapControllers();

app.Run();
