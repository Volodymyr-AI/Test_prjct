using System.Text.Json.Serialization;

namespace Scraper.Scraping.Models;

internal sealed class YahooCanonicalUrl
{
    [JsonPropertyName("url")]
    public string? Url { get; init; }
}