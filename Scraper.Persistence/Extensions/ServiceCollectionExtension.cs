using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scraper.Core.Interfaces;
using Scraper.Persistence.Repositories;

namespace Scraper.Persistence.Extensions;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddClickHousePersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ClickHouse")
                               ?? throw new InvalidOperationException(
                                   "ClickHouse connection string 'ClickHouse' is not configured");

        services.AddScoped<IArticleRepository>(sp =>
            new ClickHouseArticleRepository(
                connectionString,
                sp.GetRequiredService<ILogger<ClickHouseArticleRepository>>()));

        return services;
    }
}