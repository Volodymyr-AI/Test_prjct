namespace Scraper.Core.CoreEntitities;

public sealed class Article
{
    public int Id { get; private set; }
    
    /// <summary>Original URL of the article</summary>
    public string Url { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    /// <summary>Brief summary / description if available</summary>
    public string? Summary { get; private set; }

    /// <summary>Publisher / source name ("Reuters", "Bloomberg")</summary>
    public string? Source { get; private set; }

    /// <summary>Publication timestamp as reported by Yahoo Finance</summary>
    public DateTimeOffset? PublishedAt { get; private set; }

    /// <summary>UTC timestamp when the record was inserted into the local DB</summary>
    public DateTimeOffset ScrapedAt { get; private set; }
    
    private Article() {}

    public static Article Create(
        string url,
        string title,
        string? summary,
        string? source,
        DateTimeOffset? publishedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        return new Article
        {
            Url = url.Trim(),
            Title = title.Trim(),
            Summary = summary?.Trim(),
            Source = source?.Trim(),
            PublishedAt = publishedAt,
            ScrapedAt = DateTimeOffset.UtcNow
        };
    }

    public override string ToString() =>
        $"[{PublishedAt:yyyy-MM-dd HH:mm}] {Title} | {Source} | {Url}";
}