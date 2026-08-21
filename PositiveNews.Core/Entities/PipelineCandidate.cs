namespace PositiveNews.Core.Entities;

/// <summary>
/// A search candidate persisted as soon as <c>SearchAgent</c> finds it, then updated in
/// place as scoring completes. This is what makes the pipeline resumable at the
/// individual-candidate level: a rerun re-queries candidates missing a score (rather than
/// re-searching) and re-summarizes selected survivors missing a <see cref="Summarized"/>
/// flag (rather than re-scoring/re-summarizing everything).
/// </summary>
public sealed class PipelineCandidate
{
    public int Id { get; set; }

    public int PipelineRunId { get; set; }
    public PipelineRun? Run { get; set; }

    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string Source { get; set; } = "";
    public DateTimeOffset? PublishedAt { get; set; }
    public string? Snippet { get; set; }
    public string? ImageUrl { get; set; }

    /// <summary>Null until <c>PositivityScorerAgent</c> has scored this candidate.</summary>
    public int? Score { get; set; }
    public string? ScoreReasoning { get; set; }
    public bool? IsPositive { get; set; }

    /// <summary>True once this candidate was selected as a top-N survivor and successfully
    /// summarized into a <see cref="NewsStory"/>.</summary>
    public bool Summarized { get; set; }
}
