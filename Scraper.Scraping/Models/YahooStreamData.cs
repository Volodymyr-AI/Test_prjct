using System.Text.Json.Serialization;

namespace Scraper.Scraping.Models;

internal sealed class YahooStreamData
{
    [JsonPropertyName("main")]
    public YahooStreamMain? Main { get; init; }
}