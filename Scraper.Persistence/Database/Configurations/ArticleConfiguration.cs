using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scraper.Core.CoreEntitities;

namespace Scraper.Persistence.Database.Configurations;

public sealed class ArticleConfiguration : IEntityTypeConfiguration<Article>
{
    public void Configure(EntityTypeBuilder<Article> a)
    {
        a.HasKey(e => e.Id);

        a.Property(e => e.Id)
            .ValueGeneratedOnAdd();

        a.HasIndex(e => e.Url)
            .IsUnique()
            .HasDatabaseName("NewsArticles_Url");

        a.Property(e => e.Url)
            .IsRequired()
            .HasMaxLength(2048);

        a.Property(e => e.Title)
            .IsRequired()
            .HasMaxLength(1024);

        a.Property(e => e.Summary)
            .HasMaxLength(4096);

        a.Property(e => e.Source)
            .HasMaxLength(256);

        a.Property(e => e.PublishedAt)
            .HasConversion(
                v => v.HasValue ? v.Value.ToUnixTimeSeconds() : (long?)null,
                v => v.HasValue ? DateTimeOffset.FromUnixTimeSeconds(v.Value) : null);

        a.Property(e => e.ScrapedAt)
            .HasConversion(
                v => v.ToUnixTimeSeconds(),
                v => DateTimeOffset.FromUnixTimeSeconds(v));
    }
}