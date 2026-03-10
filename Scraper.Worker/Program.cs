using Serilog;
using Serilog.Events;
using Scraper.Persistence.Extensions;
using Scraper.Redis.Extensions;
using Scraper.Scraping.Extensions;
using Scraper.Worker;
using Scraper.Worker.Options;
using Scraper.Worker.Orchestration;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/scraper-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateBootstrapLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    // === Serilog ===
    builder.Services.AddSerilog((_, lc) => lc
        .ReadFrom.Configuration(builder.Configuration));

    // === Options ===
    builder.Services
        .Configure<WorkerOptions>(
            builder.Configuration.GetSection(WorkerOptions.SectionName));

    // === Infrastructure layers (each registers its own interface) ===
    builder.Services
        .AddClickHousePersistence(builder.Configuration)   // IArticleRepository
        .AddRedisUrlCache(builder.Configuration)           // IUrlCache
        .AddPlaywrightScraper(builder.Configuration);      // IScraperService

    // === Orchestrator + Worker ===
    builder.Services.AddScoped<ScrapingOrchestrator>();
    builder.Services.AddHostedService<ScraperWorker>();

    // === Install Playwright Chromium if needed ===
    Log.Information("Ensuring Playwright Chromium is installed…");
    var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
    Log.Information("Playwright install exit code: {Code}", exitCode);

    var host = builder.Build();
    await host.RunAsync();

    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}