namespace Scraper.Scraping.Services;

public sealed class RawScrapedItem
{
    public string? Url { get; set; }
    public string? Title { get; set; }
    public string? Summary { get; set; }
    public string? Source { get; set; }
    public string? PublishedAt { get; set; }
}