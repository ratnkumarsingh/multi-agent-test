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

    /// <summary>The language this story's own Headline/Body is written in (e.g. "en", "hi")
    /// — carried over from the source candidate's <c>Locale</c>, not translated. A story
    /// with Locale "hi" is native Hindi copy, distinct from a <see cref="StoryTranslation"/>
    /// (an on-demand translation of an English story).</summary>
    public string Locale { get; set; } = "en";

    public List<StoryTranslation> Translations { get; set; } = [];
}
