using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Scraper.Core.Interfaces;
using Scraper.Core.Models;
using Scraper.Scraping.Models;
using Scraper.Scraping.Options;

namespace Scraper.Scraping.Services;

/// <summary>
/// Traffic reduction strategy:
///   1. Abort non-essential resource types (images, fonts, css, media)
///   2. Abort known ad/analytics domains
///   3. Intercept XHR/Fetch responses from Yahoo internal stream API
///      → parse structured JSON directly, no DOM parsing overhead
///   4. DOM extraction as fallback / supplement 
/// </summary>
public sealed partial class PlaywrightScraperService : IScraperService
{
    private readonly ScraperOptions _options;
    private readonly ILogger<PlaywrightScraperService> _logger;
    
    private readonly List<ScrapedItem> _interceptedItems = [];
    private readonly Lock _lock = new();
    
    private IPlaywright? _playwright;
    private IBrowser?    _browser;
    
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };
    
    // Matches Yahoo Finance internal stream/pagination API endpoints
    [GeneratedRegex(
        @"(query\d+\.finance\.yahoo\.com|finance\.yahoo\.com).*(stream|pagination|news)",
        RegexOptions.IgnoreCase)]
    private static partial Regex YahooApiPattern();

    public PlaywrightScraperService(
        IOptions<ScraperOptions> options,
        ILogger<PlaywrightScraperService> logger)
    {
        _options = options.Value;
        _logger  = logger;
    }
    
    public async Task<IReadOnlyList<ScrapedItem>> ScrapeAsync(CancellationToken ct = default)
    {
        _interceptedItems.Clear();

        _playwright = await Playwright.CreateAsync();

        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args =
            [
                "--no-sandbox",
                "--disable-setuid-sandbox",
                "--disable-dev-shm-usage",
                "--disable-gpu",
                "--disable-extensions",
                "--disable-background-networking",
                "--disable-default-apps",
                "--no-first-run",
                "--disable-sync"
            ]
        });

        await using var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                        "AppleWebKit/537.36 (KHTML, like Gecko) " +
                        "Chrome/124.0.0.0 Safari/537.36",
            Locale = "en-US",
            JavaScriptEnabled = true
        });

        var page = await context.NewPageAsync();

        await RegisterRouteHandlerAsync(page);

        // === Navigate ===
        _logger.LogInformation("Navigating to {Url}", _options.TargetUrl);

        await page.GotoAsync(_options.TargetUrl, new PageGotoOptions
        {
            // DOMContentLoaded — don't wait for blocked resources to settle
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout   = 30_000
        });

        await DismissCookieConsentAsync(page);

        // === Scroll ===
        var domItems = await ScrollAndExtractAsync(page, ct);

        // === Merge: API items override DOM items (more accurate data) ===
        return MergeResults(domItems);
    }

    // === Route handler ===

    private async Task RegisterRouteHandlerAsync(IPage page)
    {
        await page.RouteAsync("**/*", async route =>
        {
            var request      = route.Request;
            var resourceType = request.ResourceType;
            var url          = request.Url;

            // 1. Block by resource type
            if (_options.BlockedResourceTypes.Contains(
                    resourceType, StringComparer.OrdinalIgnoreCase))
            {
                await route.AbortAsync();
                return;
            }

            // 2. Block ad/analytics domains
            if (_options.BlockedDomains.Any(
                    d => url.Contains(d, StringComparison.OrdinalIgnoreCase)))
            {
                await route.AbortAsync();
                return;
            }

            // 3. Intercept Yahoo API XHR/Fetch responses
            if (resourceType is "xhr" or "fetch" && YahooApiPattern().IsMatch(url))
            {
                var response = await route.FetchAsync();
                var body     = await response.TextAsync();

                _ = Task.Run(() => TryParseApiResponse(url, body));

                await route.FulfillAsync(new RouteFulfillOptions
                {
                    Response = response
                });
                return;
            }

            await route.ContinueAsync();
        });
    }

    // === Scroll + DOM extraction === 

    private async Task<List<ScrapedItem>> ScrollAndExtractAsync(
        IPage page,
        CancellationToken ct)
    {
        var stableCount   = 0;
        var previousCount = 0;

        for (var i = 0; i < _options.MaxScrollIterations && !ct.IsCancellationRequested; i++)
        {
            await page.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight)");
            await page.WaitForTimeoutAsync(_options.ScrollDelayMs);

            var currentCount = await page.EvaluateAsync<int>(
                "document.querySelectorAll(" +
                "'li[class*=\"stream-item\"]," +
                "div[class*=\"stream-item\"]," +
                "[data-testid=\"stream-item\"]'" +
                ").length");

            _logger.LogDebug(
                "Scroll {Iter}/{Max} — {Count} articles in DOM",
                i + 1, _options.MaxScrollIterations, currentCount);

            if (currentCount == previousCount)
            {
                stableCount++;

                if (stableCount >= _options.StableScrollThreshold)
                {
                    _logger.LogInformation(
                        "Scroll complete after {Iter} iterations " +
                        "({Stable} consecutive stable ticks)",
                        i + 1, stableCount);
                    break;
                }
            }
            else
            {
                stableCount = 0;
            }

            previousCount = currentCount;
        }

        return await ExtractFromDomAsync(page);
    }

    private static async Task<List<ScrapedItem>> ExtractFromDomAsync(IPage page)
    {
        const string script = """
            (() => {
                const items = [];
                const seen  = new Set();
                const selectors = [
                    'li[class*="stream-item"]',
                    'div[class*="stream-item"]',
                    '[data-testid="stream-item"]',
                    'article'
                ];

                for (const sel of selectors) {
                    document.querySelectorAll(sel).forEach(el => {
                        const anchor  = el.querySelector('a[href*="/news/"], a[href^="https"]');
                        const title   = el.querySelector('h3, h2, [class*="title"]');
                        const summary = el.querySelector('p, [class*="summary"]');
                        const source  = el.querySelector('[class*="provider"], cite');
                        const time    = el.querySelector('time');

                        if (!anchor || !title) return;

                        const url = anchor.href;
                        if (!url || seen.has(url)) return;
                        seen.add(url);

                        items.push({
                            url,
                            title:       title.innerText?.trim()   ?? '',
                            summary:     summary?.innerText?.trim() ?? null,
                            source:      source?.innerText?.trim()  ?? null,
                            publishedAt: time?.getAttribute('datetime') ?? null
                        });
                    });
                }
                return items;
            })()
            """;

        var raw = await page.EvaluateAsync<List<Dictionary<string, object?>>>(script);

        return raw
            .Where(r => !string.IsNullOrWhiteSpace(r["url"]?.ToString())
                     && !string.IsNullOrWhiteSpace(r["title"]?.ToString()))
            .Select(r =>
            {
                DateTimeOffset? publishedAt = null;

                if (DateTimeOffset.TryParse(r["publishedAt"]?.ToString(), out var dt))
                    publishedAt = dt;

                return new ScrapedItem(
                    Url:         r["url"]!.ToString()!,
                    Title:       r["title"]!.ToString()!,
                    Summary:     r["summary"]?.ToString(),
                    Source:      r["source"]?.ToString(),
                    PublishedAt: publishedAt);
            })
            .ToList();
    }

    // === XHR interception ===

    private void TryParseApiResponse(string url, string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return;

        try
        {
            var response = JsonSerializer.Deserialize<YahooStreamResponse>(body, JsonOpts);
            var stream   = response?.Data?.Main?.Stream;

            if (stream is null || stream.Count == 0) return;

            var parsed = stream
                .Where(i => i.Content is not null
                         && !string.IsNullOrWhiteSpace(i.Content.Title)
                         && !string.IsNullOrWhiteSpace(i.Content.CanonicalUrl?.Url))
                .Select(i =>
                {
                    DateTimeOffset? publishedAt = null;

                    if (DateTimeOffset.TryParse(i.Content!.PubDate, out var dt))
                        publishedAt = dt;

                    return new ScrapedItem(
                        Url:         i.Content.CanonicalUrl!.Url!,
                        Title:       i.Content.Title!,
                        Summary:     i.Content.Summary,
                        Source:      i.Content.Provider?.DisplayName,
                        PublishedAt: publishedAt);
                })
                .ToList();

            if (parsed.Count == 0) return;

            lock (_lock)
            {
                _interceptedItems.AddRange(parsed);
            }

            _logger.LogDebug(
                "Intercepted {Count} articles from API response: {Url}",
                parsed.Count, url);
        }
        catch (JsonException) { /* not a matching endpoint — ignore */ }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse intercepted response from {Url}", url);
        }
    }
    
    private IReadOnlyList<ScrapedItem> MergeResults(List<ScrapedItem> domItems)
    {
        // API items take priority — structured data with accurate timestamps
        var merged = new Dictionary<string, ScrapedItem>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in domItems)
            merged[item.Url] = item;

        lock (_lock)
        {
            foreach (var item in _interceptedItems)
                merged[item.Url] = item;  // API overrides DOM
        }

        _logger.LogInformation(
            "Merge complete — {Total} unique articles " +
            "({Api} from XHR interception, {Dom} from DOM)",
            merged.Count, _interceptedItems.Count, domItems.Count);

        return [.. merged.Values];
    }
    
    private static async Task DismissCookieConsentAsync(IPage page)
    {
        try
        {
            foreach (var selector in new[]
            {
                "button[name='agree']",
                "button:has-text('Accept all')",
                "button:has-text('Accept')"
            })
            {
                var btn = page.Locator(selector);
                if (await btn.CountAsync() > 0)
                {
                    await btn.First.ClickAsync();
                    await page.WaitForTimeoutAsync(800);
                    break;
                }
            }
        }
        catch { /* best-effort */ }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
            await _browser.CloseAsync();

        _playwright?.Dispose();
    }
}