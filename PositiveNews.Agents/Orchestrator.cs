using System.Collections.Concurrent;
using PositiveNews.Agents.Agents;

namespace PositiveNews.Agents;

public sealed record CuratedStory(
    string Headline,
    string Body,
    string? ImageUrl,
    string SourceUrl,
    string Source,
    DateTimeOffset? PublishedAt,
    int Score);

/// <summary>
/// Ties the pipeline together: <see cref="SearchAgent"/> finds candidates,
/// <see cref="PositivityScorerAgent"/> judges each one (fan-out, bounded concurrency,
/// per-candidate failures isolated rather than aborting the batch), survivors above
/// <see cref="MinScore"/> are kept (top <c>topN</c> by score), and
/// <see cref="SummarizerAgent"/> turns each survivor into final copy — the concrete
/// "multi-agents working together" artifact for this project.
/// </summary>
public sealed class Orchestrator
{
    private const int MinScore = 6;
    private const int MaxConcurrency = 4;

    private readonly SearchAgent _searchAgent;
    private readonly PositivityScorerAgent _scorerAgent;
    private readonly SummarizerAgent _summarizerAgent;

    public Orchestrator(SearchAgent searchAgent, PositivityScorerAgent scorerAgent, SummarizerAgent summarizerAgent)
    {
        _searchAgent = searchAgent;
        _scorerAgent = scorerAgent;
        _summarizerAgent = summarizerAgent;
    }

    public async Task<IReadOnlyList<CuratedStory>> RunAsync(
        string topicHint,
        int topN,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        progress?.Report("Searching for candidates...");
        var candidates = await _searchAgent.RunAsync(topicHint, ct);
        progress?.Report($"Found {candidates.Count} candidate article(s).");

        var scored = await MapBoundedAsync(
            candidates,
            MaxConcurrency,
            async (candidate, token) => (candidate, score: await _scorerAgent.RunAsync(candidate, token)),
            (candidate, ex) => progress?.Report($"  scoring failed for \"{candidate.Title}\": {ex.Message}"),
            ct);

        var survivors = scored
            .Where(x => x.score.IsPositive && x.score.Score >= MinScore)
            .OrderByDescending(x => x.score.Score)
            .Take(topN)
            .ToList();

        progress?.Report($"{survivors.Count} of {scored.Count} scored candidate(s) passed the positivity bar (score >= {MinScore}).");

        var stories = await MapBoundedAsync(
            survivors,
            MaxConcurrency,
            async (item, token) =>
            {
                var summary = await _summarizerAgent.RunAsync(item.candidate, token);
                return new CuratedStory(
                    summary.Headline,
                    summary.Body,
                    item.candidate.ImageUrl,
                    item.candidate.Url,
                    item.candidate.Source,
                    item.candidate.PublishedAt,
                    item.score.Score);
            },
            (item, ex) => progress?.Report($"  summarizing failed for \"{item.candidate.Title}\": {ex.Message}"),
            ct);

        return stories.OrderByDescending(s => s.Score).ToList();
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
        Action<TIn, Exception> onError,
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
                onError(item, ex);
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
