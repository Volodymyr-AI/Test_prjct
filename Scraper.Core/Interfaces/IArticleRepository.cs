using Scraper.Core.CoreEntitities;

namespace Scraper.Core.Interfaces;

public interface IArticleRepository
{
    /// <summary>
    /// Bulk-inserts only articles not yet present in the storage
    /// </summary>
    /// <param name="articles"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<int> InsertNewOnlyAsync(
        IReadOnlyList<Article> articles,
        CancellationToken ct = default);
}