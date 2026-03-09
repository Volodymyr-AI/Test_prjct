namespace Scraper.Core.Models;

public sealed record ScrapedItem(
    string Url,
    string Title,
    string? Summary,
    string? Source,
    DateTimeOffset? PublishedAt);