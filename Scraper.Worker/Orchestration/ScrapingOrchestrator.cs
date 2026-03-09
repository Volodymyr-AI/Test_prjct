using Scraper.Core.CoreEntitities;
using Scraper.Core.Interfaces;
using Scraper.Worker.Orchestration.Models;

namespace Scraper.Worker.Orchestration;

/// <summary>
/// Coordinates the full scraping pipeline:
///   1. Scrape — Playwright scrolls the page, returns ScrapedItem[]
///   2. Filter — Redis removes already-known URLs in one round-trip
///   3. Map    — ScrapedItem → Article domain entity
///   4. Persist — ClickHouse bulk insert
///   5. Cache  — mark new URLs as known in Redis 
/// </summary>
public sealed class ScrapingOrchestrator
{
    private readonly IScraperService    _scraper;
    private readonly IUrlCache          _urlCache;
    private readonly IArticleRepository _repository;
    private readonly ILogger<ScrapingOrchestrator> _logger;

    public ScrapingOrchestrator(
        IScraperService scraper,
        IUrlCache urlCache,
        IArticleRepository repository,
        ILogger<ScrapingOrchestrator> logger)
    {
        _scraper = scraper;
        _urlCache = urlCache;
        _repository = repository;
        _logger = logger;
    }
    
    public async Task<ScrapingRunResult> RunAsync(CancellationToken ct = default)
    {
        var startedAt = DateTimeOffset.UtcNow;

        _logger.LogInformation("═══ Scraping run started at {Time:HH:mm:ss} ═══", startedAt);

        // === Step 1: Scrape ===
        await using var scraper = _scraper;
        var scraped = await _scraper.ScrapeAsync(ct);

        _logger.LogInformation("Scraped {Count} total articles", scraped.Count);

        if (scraped.Count == 0)
            return ScrapingRunResult.Empty(DateTimeOffset.UtcNow - startedAt);

        // === Step 2: Filter via Redis — single round-trip ===
        var newUrls    = await _urlCache.FilterNewUrlsAsync(scraped.Select(i => i.Url), ct);
        var newUrlsSet = newUrls.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var skipped = scraped.Count - newUrlsSet.Count;

        _logger.LogInformation(
            "Redis filter: {New} new, {Skipped} already known",
            newUrlsSet.Count, skipped);

        if (newUrlsSet.Count == 0)
            return new ScrapingRunResult(
                ScrapedCount:  scraped.Count,
                InsertedCount: 0,
                SkippedCount:  skipped,
                Duration:      DateTimeOffset.UtcNow - startedAt);

        // === Step 3: Map ScrapedItem → Article ===
        var articles = scraped
            .Where(i => newUrlsSet.Contains(i.Url))
            .Select(i => Article.Create(
                i.Url,
                i.Title,
                i.Summary,
                i.Source,
                i.PublishedAt))
            .ToList();

        // ===Step 4: Persist to ClickHouse ===
        var inserted = await _repository.InsertNewOnlyAsync(articles, ct);

        // === Step 5: Mark as known in Redis ===
        await _urlCache.MarkAsStoredAsync(newUrlsSet, ct);

        var result = new ScrapingRunResult(
            ScrapedCount:  scraped.Count,
            InsertedCount: inserted,
            SkippedCount:  skipped,
            Duration:      DateTimeOffset.UtcNow - startedAt);

        _logger.LogInformation(
            "═══ Run complete in {Duration:ss\\.fff}s — " +
            "scraped={Scraped}, inserted={Inserted}, skipped={Skipped} ═══",
            result.Duration,
            result.ScrapedCount,
            result.InsertedCount,
            result.SkippedCount);

        return result;
    }
}