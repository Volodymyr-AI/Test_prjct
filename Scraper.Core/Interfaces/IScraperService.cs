using Scraper.Core.Models;

namespace Scraper.Core.Interfaces;

public interface IScraperService : IAsyncDisposable
{
    Task<IReadOnlyList<ScrapedItem>> ScrapeAsync(CancellationToken ct = default);
}