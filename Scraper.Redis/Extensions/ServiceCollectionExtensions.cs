using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scraper.Core.Interfaces;
using Scraper.Redis.Cache;
using StackExchange.Redis;

namespace Scraper.Redis.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRedisUrlCache(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Redis")
                               ?? throw new InvalidOperationException(
                                   "Redis connection string 'Redis' is not configured");
        
        services.AddSingleton<IConnectionMultiplexer>(
            _ => ConnectionMultiplexer.Connect(connectionString));

        services.AddScoped<IUrlCache, RedisUrlCache>();

        return services;
    }
}