namespace Scraper.Worker.Orchestration.Models;

public sealed record ScrapingRunResult(
    int ScrapedCount,
    int InsertedCount,
    int SkippedCount,
    TimeSpan Duration)
{
    public static ScrapingRunResult Empty(TimeSpan duration) =>
        new(0, 0, 0, duration);
}