namespace PositiveNews.Core.Entities;

/// <summary>The final curated, summarized output — one row per published story.</summary>
public sealed class NewsStory
{
    public int Id { get; set; }

    public int PipelineRunId { get; set; }
    public PipelineRun? Run { get; set; }

    public string Headline { get; set; } = "";
    public string Body { get; set; } = "";
    public string? ImageUrl { get; set; }
    public string SourceUrl { get; set; } = "";
    public string Source { get; set; } = "";
    public DateTimeOffset? PublishedAt { get; set; }
    public int Score { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
