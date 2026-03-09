namespace Scraper.Worker.Options;

public sealed class WorkerOptions
{
    public const string SectionName = "Worker";

    /// <summary>How often to run the scraper.</summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromHours(1);
}