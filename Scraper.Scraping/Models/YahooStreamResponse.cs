using System.Text.Json.Serialization;

namespace Scraper.Scraping.Models;

internal sealed class YahooStreamResponse
{
    [JsonPropertyName("data")]
    public YahooStreamData? Data { get; init; }
}