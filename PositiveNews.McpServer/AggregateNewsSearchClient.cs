using Microsoft.Extensions.Logging;

namespace PositiveNews.McpServer;

/// <summary>
/// Fans a search out across every configured <see cref="INewsSearchClient"/> source
/// (NewsAPI.org + one client per RSS feed), merges the results (deduped by URL, newest
/// first), and caps to <c>max</c> — <see cref="NewsSearchTools.SearchNews"/> and everything
/// upstream of it (SearchAgent, Orchestrator) stays unaware there's more than one provider
/// behind <see cref="INewsSearchClient"/>.
///
/// One source failing doesn't sink the whole search (same fan-out-isolate-failures posture
/// as Orchestrator's per-candidate scoring/summarizing) — only if *every* source fails does
/// this return a Failure envelope.
///
/// <see cref="INewsSearchClient.IsQueryInvariant"/> sources get at most a quarter of
/// <c>max</c> reserved for them, the rest goes to query-driven sources. Without this, a
/// live test run (once Hindi sources with <c>matchQuery: false</c> were added) found total
/// distinct candidates per pipeline run drop from 20 to 9: SearchAgent asks several
/// differently-worded queries per run, a query-invariant source returns the exact same
/// "most recent" items to every one of them, and since those are typically the freshest
/// timestamps in the merge they crowded out query-driven sources' results — which vary per
/// query and would otherwise have contributed new, non-duplicate candidates each time.
/// </summary>
public sealed class AggregateNewsSearchClient : INewsSearchClient
{
    private readonly IReadOnlyList<INewsSearchClient> _sources;
    private readonly ILogger<AggregateNewsSearchClient> _logger;

    public AggregateNewsSearchClient(IReadOnlyList<INewsSearchClient> sources, ILogger<AggregateNewsSearchClient> logger)
    {
        _sources = sources;
        _logger = logger;
    }

    public async Task<NewsSearchResponse> SearchAsync(string query, int max, CancellationToken ct)
    {
        var results = await Task.WhenAll(_sources.Select(s => SearchOneAsync(s, query, max, ct)));

        var succeeded = new List<(INewsSearchClient Source, NewsSearchResponse Response)>();
        for (var i = 0; i < _sources.Count; i++)
        {
            if (results[i].Error is null)
            {
                succeeded.Add((_sources[i], results[i]));
            }
        }

        if (succeeded.Count == 0)
        {
            var messages = string.Join(" | ", results.Select(r => r.Error!.Message));
            return NewsSearchResponse.Failure(
                NewsSearchErrorCategory.UpstreamUnavailable, retryable: true,
                $"All {_sources.Count} news source(s) failed: {messages}");
        }

        var invariantArticles = DedupedNewestFirst(succeeded.Where(s => s.Source.IsQueryInvariant).SelectMany(s => s.Response.Articles));
        var drivenArticles = DedupedNewestFirst(succeeded.Where(s => !s.Source.IsQueryInvariant).SelectMany(s => s.Response.Articles));

        var invariantQuota = Math.Max(1, max / 4);
        var takenInvariant = invariantArticles.Take(invariantQuota).ToList();
        // Query-driven sources get whatever's left of the budget — including the
        // invariant quota's unused remainder, if that group came up short.
        var takenDriven = drivenArticles.Take(max - takenInvariant.Count).ToList();

        var merged = DedupedNewestFirst(takenInvariant.Concat(takenDriven)).Take(max).ToList();
        var totalCount = succeeded.Sum(s => s.Response.TotalCount);

        return new NewsSearchResponse(merged, totalCount, Error: null);
    }

    private static List<NewsArticle> DedupedNewestFirst(IEnumerable<NewsArticle> articles) =>
        articles
            .GroupBy(a => a.Url, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderByDescending(a => a.PublishedAt)
            .ToList();

    private async Task<NewsSearchResponse> SearchOneAsync(INewsSearchClient source, string query, int max, CancellationToken ct)
    {
        try
        {
            var result = await source.SearchAsync(query, max, ct);
            if (result.Error is { } error)
            {
                _logger.LogWarning(
                    "News source {Source} failed ({Category}, retryable={Retryable}): {Message}",
                    source.GetType().Name, error.Category, error.Retryable, error.Message);
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "News source {Source} threw unexpectedly", source.GetType().Name);
            return NewsSearchResponse.Failure(NewsSearchErrorCategory.Unknown, retryable: true, ex.Message);
        }
    }
}
