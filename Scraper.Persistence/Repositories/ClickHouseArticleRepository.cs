using ClickHouse.Client.ADO;
using ClickHouse.Client.Copy;
using Microsoft.Extensions.Logging;
using Scraper.Core.CoreEntitities;
using Scraper.Core.Interfaces;

namespace Scraper.Persistence.Repositories;

public sealed class ClickHouseArticleRepository(string connectionString, ILogger<ClickHouseArticleRepository> logger)
    : IArticleRepository
{
    public async Task<int> InsertNewOnlyAsync(
        IReadOnlyList<Article> articles,
        CancellationToken ct = default)
    {
        if (articles.Count == 0)
            return 0;

        await using var connection = new ClickHouseConnection(connectionString);
        await connection.OpenAsync(ct);
        
        var bulkCopy = new ClickHouseBulkCopy(connection)
        {
            DestinationTableName = "scraper.articles",
            BatchSize            = 1000,
            ColumnNames          =
            [
                "url", "title", "summary",
                "source", "published_at", "scraped_at"
            ]
        };

        var rows = articles.Select(a => new object?[]
        {
            a.Url,
            a.Title,
            a.Summary,
            a.Source,
            a.PublishedAt?.UtcDateTime,
            a.ScrapedAt.UtcDateTime
        });

        await bulkCopy.WriteToServerAsync(rows, ct);

        logger.LogInformation(
            "Bulk inserted {Count} articles into ClickHouse", articles.Count);
        
        return articles.Count;
    }
}