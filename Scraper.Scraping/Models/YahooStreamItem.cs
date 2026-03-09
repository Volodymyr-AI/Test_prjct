using System.Text.Json.Serialization;

namespace Scraper.Scraping.Models;

internal sealed class YahooStreamItem
{
    [JsonPropertyName("content")]
    public YahooContent? Content { get; init; }
}