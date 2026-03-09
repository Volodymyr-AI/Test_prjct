using System.Text.Json.Serialization;

namespace Scraper.Scraping.Models;

internal sealed class YahooStreamMain
{
    [JsonPropertyName("stream")]
    public List<YahooStreamItem>? Stream { get; init; }
}