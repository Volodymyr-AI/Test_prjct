using System.Text.Json.Serialization;

namespace Scraper.Scraping.Models;

internal sealed class YahooContent
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("summary")]
    public string? Summary { get; init; }

    [JsonPropertyName("canonicalUrl")]
    public YahooCanonicalUrl? CanonicalUrl { get; init; }

    [JsonPropertyName("pubDate")]
    public string? PubDate { get; init; }

    [JsonPropertyName("provider")]
    public YahooProvider? Provider { get; init; }
}