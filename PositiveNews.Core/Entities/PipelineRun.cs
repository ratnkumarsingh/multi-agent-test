namespace PositiveNews.Core.Entities;

public enum PipelineRunStatus
{
    InProgress,
    Completed,
    Failed,
}

/// <summary>
/// One row per calendar day — the idempotency anchor. A day with a
/// <see cref="PipelineRunStatus.Completed"/> row is a no-op on rerun; a day with an
/// <see cref="PipelineRunStatus.InProgress"/> or <see cref="PipelineRunStatus.Failed"/> row
/// is resumed rather than started over, using <see cref="PipelineCandidate"/> rows already
/// persisted for it.
/// </summary>
public sealed class PipelineRun
{
    public int Id { get; set; }

    /// <summary>UTC calendar date this run covers. Unique — see
    /// <c>PositiveNewsDbContext.OnModelCreating</c>.</summary>
    public DateOnly RunDate { get; set; }

    public PipelineRunStatus Status { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? TopicHint { get; set; }

    public List<PipelineStep> Steps { get; set; } = [];
    public List<PipelineCandidate> Candidates { get; set; } = [];
    public List<NewsStory> Stories { get; set; } = [];
}
