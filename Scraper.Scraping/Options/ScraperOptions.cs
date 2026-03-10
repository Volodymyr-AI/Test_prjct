namespace Scraper.Scraping.Options;

public sealed class ScraperOptions
{
    public const string SectionName = "Scraper";
    
    public string TargetUrl { get; init; } =
        "https://finance.yahoo.com/topic/latest-news/";
    
    /// <summary>Max scroll attempts before giving up.</summary>
    public int MaxScrollIterations { get; init; } = 60;
    
    /// <summary>Milliseconds to wait after each scroll for content to load.</summary>
    public int ScrollDelayMs { get; init; } = 1500;
    
    /// <summary>
    /// Consecutive iterations with no new articles before scroll is considered complete.
    /// </summary>
    public int StableScrollThreshold { get; init; } = 3;
    
    /// <summary>
    /// Resource types to abort before download.
    /// Primary traffic reduction mechanism — saves ~65% bandwidth.
    /// </summary>
    public string[] BlockedResourceTypes { get; init; } =
        ["image", "media", "font", "stylesheet", "ping", "other"];
    
    /// <summary>Ad/analytics domains to block entirely.</summary>
    public string[] BlockedDomains { get; init; } =
    [
        "google-analytics.com",
        "googletagmanager.com",
        "doubleclick.net",
        "googlesyndication.com",
        "scorecardresearch.com",
        "omtrdc.net",
        "adsymptotic.com",
        "ads.yahoo.com",
        "analytics.yahoo.com",
        "beacon.yahoo.com",
        "finance.yahoo.com/xhr/beacon"
    ];
}