using Microsoft.Extensions.Options;
using Scraper.Worker.Options;
using Scraper.Worker.Orchestration;

namespace Scraper.Worker;

public sealed class ScraperWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WorkerOptions        _options;
    private readonly ILogger<ScraperWorker> _logger;

    public ScraperWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<WorkerOptions> options,
        ILogger<ScraperWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options      = options.Value;
        _logger       = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "ScraperWorker started — interval: {Interval}",
            _options.Interval);

        // Run immediately on startup, then on each timer tick
        await RunSafeAsync(stoppingToken);

        using var timer = new PeriodicTimer(_options.Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RunSafeAsync(stoppingToken);
    }
    
    private async Task RunSafeAsync(CancellationToken ct)
    {
        // New scope per run — Scoped services (repository, cache) resolved fresh
        await using var scope = _scopeFactory.CreateAsyncScope();

        try
        {
            var orchestrator = scope.ServiceProvider
                .GetRequiredService<ScrapingOrchestrator>();

            await orchestrator.RunAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Graceful shutdown — not an error
        }
        catch (Exception ex)
        {
            // Log and survive — worker must not crash on single run failure
            _logger.LogError(ex, "Scraping run failed — will retry on next tick");
        }
    }
}