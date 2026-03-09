using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scraper.Core.Interfaces;
using Scraper.Scraping.Options;
using Scraper.Scraping.Services;

namespace Scraper.Scraping.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPlaywrightScraper(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ScraperOptions>(
            configuration.GetSection(ScraperOptions.SectionName));

        // New Playwright browser instance for each run
        services.AddTransient<IScraperService, PlaywrightScraperService>();

        return services;
    }
}