using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using PositiveNews.Agents.Agents;
using PositiveNews.Core.Data;
using PositiveNews.Core.Entities;

namespace PositiveNews.Agents;

public sealed record CuratedStory(
    string Headline,
    string Body,
    string? ImageUrl,
    string SourceUrl,
    string Source,
    DateTimeOffset? PublishedAt,
    int Score,
    string Locale);

/// <summary>
/// Ties the pipeline together: <see cref="SearchAgent"/> finds candidates,
/// <see cref="PositivityScorerAgent"/> judges each one (fan-out, bounded concurrency,
/// per-candidate failures isolated rather than aborting the batch), survivors above
/// <see cref="MinScore"/> are kept (top <c>topN</c> by score), and
/// <see cref="SummarizerAgent"/> turns each survivor into final copy — the concrete
/// "multi-agents working together" artifact for this project.
///
/// Every stage persists incrementally via <see cref="PipelineCandidate"/>/<see cref="NewsStory"/>
/// rows (Phase 5), which is what makes a run idempotent (a day with a
/// <see cref="PipelineRunStatus.Completed"/> row is a no-op) and resumable at the
/// individual-candidate level (a rerun skips candidates already searched/scored/summarized
/// rather than redoing the whole run) rather than just idempotent at the whole-run level.
/// </summary>
public sealed class Orchestrator
{
    private const int MinScore = 6;
    private const int MaxConcurrency = 4;
    private const int MaxAttempts = 3;
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromMilliseconds(500);

    private readonly SearchAgent _searchAgent;
    private readonly PositivityScorerAgent _scorerAgent;
    private readonly SummarizerAgent _summarizerAgent;
    private readonly IDbContextFactory<PositiveNewsDbContext> _dbContextFactory;

    public Orchestrator(
        SearchAgent searchAgent,
        PositivityScorerAgent scorerAgent,
        SummarizerAgent summarizerAgent,
        IDbContextFactory<PositiveNewsDbContext> dbContextFactory)
    {
        _searchAgent = searchAgent;
        _scorerAgent = scorerAgent;
        _summarizerAgent = summarizerAgent;
        _dbContextFactory = dbContextFactory;
    }

    public async Task<IReadOnlyList<CuratedStory>> RunAsync(
        string topicHint,
        int topN,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var runDate = DateOnly.FromDateTime(DateTime.UtcNow);

        await using (var db = await _dbContextFactory.CreateDbContextAsync(ct))
        {
            var existing = await db.PipelineRuns
                .Include(r => r.Stories)
                .FirstOrDefaultAsync(r => r.RunDate == runDate, ct);

            if (existing is { Status: PipelineRunStatus.Completed })
            {
                progress?.Report(
                    $"PipelineRun for {runDate} already completed — returning " +
                    $"{existing.Stories.Count} existing stor{(existing.Stories.Count == 1 ? "y" : "ies")} (idempotent no-op).");
                return ToCuratedStories(existing.Stories);
            }
        }

        var runId = await GetOrCreateRunAsync(runDate, topicHint, progress, ct);

        try
        {
            var candidates = await GetOrSearchCandidatesAsync(runId, topicHint, progress, ct);
            await ScoreCandidatesAsync(runId, candidates, progress, ct);

            var survivors = await SelectSurvivorsAsync(runId, topN, ct);
            progress?.Report($"{survivors.Count} candidate(s) selected as top-{topN} survivors (score >= {MinScore}).");

            await SummarizeSurvivorsAsync(runId, survivors, progress, ct);

            return await CompleteRunAsync(runId, ct);
        }
        catch (Exception ex)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            var run = await db.PipelineRuns.FirstAsync(r => r.Id == runId, ct);
            run.Status = PipelineRunStatus.Failed;
            await db.SaveChangesAsync(ct);
            progress?.Report($"Pipeline run failed: {ex.Message}");
            throw;
        }
    }

    private async Task<int> GetOrCreateRunAsync(
        DateOnly runDate, string topicHint, IProgress<string>? progress, CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        var run = await db.PipelineRuns.FirstOrDefaultAsync(r => r.RunDate == runDate, ct);
        if (run is not null)
        {
            progress?.Report($"Resuming PipelineRun #{run.Id} for {runDate} (previous status: {run.Status}).");
            if (run.Status != PipelineRunStatus.InProgress)
            {
                run.Status = PipelineRunStatus.InProgress;
                await db.SaveChangesAsync(ct);
            }
            return run.Id;
        }

        var newRun = new PipelineRun
        {
            RunDate = runDate,
            Status = PipelineRunStatus.InProgress,
            StartedAt = DateTimeOffset.UtcNow,
            TopicHint = string.IsNullOrWhiteSpace(topicHint) ? null : topicHint,
        };
        db.PipelineRuns.Add(newRun);
        await db.SaveChangesAsync(ct);
        progress?.Report($"Created PipelineRun #{newRun.Id} for {runDate}.");
        return newRun.Id;
    }

    private async Task<List<PipelineCandidate>> GetOrSearchCandidatesAsync(
        int runId, string topicHint, IProgress<string>? progress, CancellationToken ct)
    {
        await using (var db = await _dbContextFactory.CreateDbContextAsync(ct))
        {
            var existing = await db.PipelineCandidates.Where(c => c.PipelineRunId == runId).ToListAsync(ct);
            if (existing.Count > 0)
            {
                progress?.Report($"Reusing {existing.Count} already-persisted candidate(s) — skipping search.");
                return existing;
            }
        }

        progress?.Report("Searching for candidates...");

        IReadOnlyList<NewsCandidate> found;
        int retries;
        try
        {
            (found, retries) = await WithRetryAsync(() => _searchAgent.RunAsync(topicHint, ct), ct);
            await RecordStepAsync(runId, "SearchAgent", null, PipelineStepStatus.Completed, retries, null, ct);
        }
        catch (Exception ex)
        {
            await RecordStepAsync(runId, "SearchAgent", null, PipelineStepStatus.Failed, MaxAttempts - 1, ex.Message, ct);
            throw;
        }

        progress?.Report($"Found {found.Count} candidate article(s).");

        var rows = found.Select(c => new PipelineCandidate
        {
            PipelineRunId = runId,
            Title = c.Title,
            Url = c.Url,
            Source = c.Source,
            PublishedAt = c.PublishedAt,
            Snippet = c.Snippet,
            ImageUrl = c.ImageUrl,
            Locale = c.Locale,
        }).ToList();

        await using var writeDb = await _dbContextFactory.CreateDbContextAsync(ct);
        writeDb.PipelineCandidates.AddRange(rows);
        await writeDb.SaveChangesAsync(ct);
        return rows;
    }

    private async Task ScoreCandidatesAsync(
        int runId, List<PipelineCandidate> candidates, IProgress<string>? progress, CancellationToken ct)
    {
        var unscored = candidates.Where(c => c.Score is null).ToList();
        if (unscored.Count == 0)
        {
            progress?.Report("All candidates already scored — skipping.");
            return;
        }

        progress?.Report($"Scoring {unscored.Count} of {candidates.Count} candidate(s)...");

        await MapBoundedAsync<PipelineCandidate, bool>(
            unscored,
            MaxConcurrency,
            async (candidate, token) =>
            {
                var (score, retries) = await WithRetryAsync(
                    () => _scorerAgent.RunAsync(ToNewsCandidate(candidate), token), token);

                await using var db = await _dbContextFactory.CreateDbContextAsync(token);
                var row = await db.PipelineCandidates.FirstAsync(c => c.Id == candidate.Id, token);
                row.Score = score.Score;
                row.ScoreReasoning = score.Reasoning;
                row.IsPositive = score.IsPositive;
                await db.SaveChangesAsync(token);

                await RecordStepAsync(runId, "PositivityScorerAgent", candidate.Title, PipelineStepStatus.Completed, retries, null, token);
                return true;
            },
            async (candidate, ex, token) =>
            {
                progress?.Report($"  scoring failed for \"{candidate.Title}\": {ex.Message}");
                await RecordStepAsync(runId, "PositivityScorerAgent", candidate.Title, PipelineStepStatus.Failed, MaxAttempts - 1, ex.Message, token);
            },
            ct);
    }

    private async Task<List<PipelineCandidate>> SelectSurvivorsAsync(int runId, int topN, CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        return await db.PipelineCandidates
            .Where(c => c.PipelineRunId == runId && c.IsPositive == true && c.Score >= MinScore)
            .OrderByDescending(c => c.Score)
            .Take(topN)
            .ToListAsync(ct);
    }

    private async Task SummarizeSurvivorsAsync(
        int runId, List<PipelineCandidate> survivors, IProgress<string>? progress, CancellationToken ct)
    {
        var pending = survivors.Where(c => !c.Summarized).ToList();
        if (pending.Count == 0)
        {
            progress?.Report("All selected survivors already summarized — skipping.");
            return;
        }

        progress?.Report($"Summarizing {pending.Count} of {survivors.Count} selected survivor(s)...");

        await MapBoundedAsync<PipelineCandidate, bool>(
            pending,
            MaxConcurrency,
            async (candidate, token) =>
            {
                var (summary, retries) = await WithRetryAsync(
                    () => _summarizerAgent.RunAsync(ToNewsCandidate(candidate), token), token);

                await using var db = await _dbContextFactory.CreateDbContextAsync(token);
                db.NewsStories.Add(new NewsStory
                {
                    PipelineRunId = runId,
                    Headline = summary.Headline,
                    Body = summary.Body,
                    ImageUrl = candidate.ImageUrl,
                    SourceUrl = candidate.Url,
                    Source = candidate.Source,
                    PublishedAt = candidate.PublishedAt,
                    Score = candidate.Score!.Value,
                    CreatedAt = DateTimeOffset.UtcNow,
                    Locale = candidate.Locale,
                });
                var row = await db.PipelineCandidates.FirstAsync(c => c.Id == candidate.Id, token);
                row.Summarized = true;
                await db.SaveChangesAsync(token);

                await RecordStepAsync(runId, "SummarizerAgent", candidate.Title, PipelineStepStatus.Completed, retries, null, token);
                return true;
            },
            async (candidate, ex, token) =>
            {
                progress?.Report($"  summarizing failed for \"{candidate.Title}\": {ex.Message}");
                await RecordStepAsync(runId, "SummarizerAgent", candidate.Title, PipelineStepStatus.Failed, MaxAttempts - 1, ex.Message, token);
            },
            ct);
    }

    private async Task<List<CuratedStory>> CompleteRunAsync(int runId, CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var run = await db.PipelineRuns.FirstAsync(r => r.Id == runId, ct);
        run.Status = PipelineRunStatus.Completed;
        run.CompletedAt = DateTimeOffset.UtcNow;

        var stories = await db.NewsStories.Where(s => s.PipelineRunId == runId).ToListAsync(ct);
        await db.SaveChangesAsync(ct);

        return ToCuratedStories(stories);
    }

    private static List<CuratedStory> ToCuratedStories(IEnumerable<NewsStory> stories) =>
        stories
            .Select(s => new CuratedStory(s.Headline, s.Body, s.ImageUrl, s.SourceUrl, s.Source, s.PublishedAt, s.Score, s.Locale))
            .OrderByDescending(s => s.Score)
            .ToList();

    private static NewsCandidate ToNewsCandidate(PipelineCandidate c) =>
        new(c.Title, c.Url, c.Source, c.PublishedAt, c.Snippet, c.ImageUrl, c.Locale);

    private async Task RecordStepAsync(
        int runId, string agentName, string? itemLabel, PipelineStepStatus status,
        int retryCount, string? errorMessage, CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        db.PipelineSteps.Add(new PipelineStep
        {
            PipelineRunId = runId,
            AgentName = agentName,
            ItemLabel = itemLabel,
            Status = status,
            RetryCount = retryCount,
            Timestamp = DateTimeOffset.UtcNow,
            ErrorMessage = errorMessage,
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Bounded retry with exponential backoff. Returns the attempt count actually
    /// used (0 = succeeded first try) so callers can record it on the step trace.</summary>
    private static async Task<(T Result, int RetryCount)> WithRetryAsync<T>(
        Func<Task<T>> action, CancellationToken ct)
    {
        var delay = InitialRetryDelay;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                return (await action(), attempt - 1);
            }
            catch when (attempt < MaxAttempts)
            {
                await Task.Delay(delay, ct);
                delay *= 2;
            }
        }

        throw new UnreachableException();
    }

    /// <summary>
    /// Runs <paramref name="work"/> over <paramref name="items"/> with at most
    /// <paramref name="maxConcurrency"/> in flight at once, isolating per-item failures via
    /// <paramref name="onError"/> instead of letting one bad item abort the whole batch —
    /// the fan-out/error-isolation shape both pipeline stages need, factored out once since
    /// it's used twice with different item/result types.
    /// </summary>
    private static async Task<List<TOut>> MapBoundedAsync<TIn, TOut>(
        IReadOnlyList<TIn> items,
        int maxConcurrency,
        Func<TIn, CancellationToken, Task<TOut>> work,
        Func<TIn, Exception, CancellationToken, Task> onError,
        CancellationToken ct)
    {
        var results = new ConcurrentBag<TOut>();
        using var gate = new SemaphoreSlim(maxConcurrency);

        var tasks = items.Select(async item =>
        {
            await gate.WaitAsync(ct);
            try
            {
                results.Add(await work(item, ct));
            }
            catch (Exception ex)
            {
                await onError(item, ex, ct);
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);
        return [.. results];
    }
}
