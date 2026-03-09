using Microsoft.Extensions.Logging;
using Scraper.Core.Interfaces;
using StackExchange.Redis;

namespace Scraper.Redis.Cache;

public sealed class RedisUrlCache : IUrlCache
{
    private const string Key = "scraper:known_urls";
    
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisUrlCache> _logger;

    public RedisUrlCache(IConnectionMultiplexer redis, ILogger<RedisUrlCache> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> FilterNewUrlsAsync(
        IEnumerable<string> urls,
        CancellationToken ct = default)
    {
        var urlList = urls
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Distinct()
            .ToList();

        if (urlList.Count == 0)
            return [];

        var db = _redis.GetDatabase();
        
        var redisValues = urlList
            .Select(u => (RedisValue)u)
            .ToArray();
        
        var existsFlags = await db.SetContainsAsync(Key, redisValues);
        
        var newUrls = urlList
            .Zip(existsFlags, (url, exists) => (url, exists))
            .Where(x => !x.exists)
            .Select(x => x.url)
            .ToList();
        
        _logger.LogDebug(
            "URL filter: {Total} total, {New} new, {Skipped} already cached",
            urlList.Count, newUrls.Count, urlList.Count - newUrls.Count);
        
        return newUrls;
    }

    public async Task MarkAsStoredAsync(
        IEnumerable<string> urls,
        CancellationToken ct = default)
    {
        var values = urls
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => (RedisValue)u)
            .ToArray();

        if (values.Length == 0)
            return;
        
        var db = _redis.GetDatabase();
        
        await db.SetAddAsync(Key, values);
        
        _logger.LogDebug("Marked {Count} URLs as stored in Redis", values.Length);
    }
}