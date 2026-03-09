namespace Scraper.Core.Interfaces;

public interface IUrlCache
{
    /// <summary>
    /// Filters the input list, returning only unique URLs
    /// </summary>
    /// <param name="urls"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<IReadOnlyList<string>> FilterNewUrlsAsync(
        IEnumerable<string> urls,
        CancellationToken ct = default);

    /// <summary>
    /// Marks URLs as known after successful DB insert
    /// </summary>
    /// <param name="urls"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task MarkAsStoredAsync(
        IEnumerable<string> urls,
        CancellationToken ct = default);
}