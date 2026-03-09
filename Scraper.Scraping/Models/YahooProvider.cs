using System.Text.Json.Serialization;

namespace Scraper.Scraping.Models;

internal sealed class YahooProvider
{
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }
}