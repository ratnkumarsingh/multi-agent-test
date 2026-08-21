namespace PositiveNews.Core.Entities;

public enum PipelineStepStatus
{
    Completed,
    Failed,
}

/// <summary>
/// One row per agent invocation (written once it resolves — success or final failure after
/// retries are exhausted), the inspectable trace history for a <see cref="PipelineRun"/>.
/// </summary>
public sealed class PipelineStep
{
    public int Id { get; set; }

    public int PipelineRunId { get; set; }
    public PipelineRun? Run { get; set; }

    public string AgentName { get; set; } = "";

    /// <summary>What this invocation was about — a candidate title for per-candidate steps,
    /// null for whole-run steps like SearchAgent's single query-planning call.</summary>
    public string? ItemLabel { get; set; }

    public PipelineStepStatus Status { get; set; }
    public int RetryCount { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? ErrorMessage { get; set; }
}
