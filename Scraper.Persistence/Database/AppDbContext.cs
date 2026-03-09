using Microsoft.EntityFrameworkCore;
using Scraper.Core.CoreEntitities;

namespace Scraper.Persistence.Database;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext
{
    public DbSet<Article> NewsArticles => Set<Article>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

}