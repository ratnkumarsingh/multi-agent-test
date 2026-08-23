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

        var succeeded = results.Where(r => r.Error is null).ToList();
        if (succeeded.Count == 0)
        {
            var messages = string.Join(" | ", results.Select(r => r.Error!.Message));
            return NewsSearchResponse.Failure(
                NewsSearchErrorCategory.UpstreamUnavailable, retryable: true,
                $"All {_sources.Count} news source(s) failed: {messages}");
        }

        var merged = succeeded
            .SelectMany(r => r.Articles)
            .GroupBy(a => a.Url, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderByDescending(a => a.PublishedAt)
            .Take(max)
            .ToList();

        var totalCount = succeeded.Sum(r => r.TotalCount);

        return new NewsSearchResponse(merged, totalCount, Error: null);
    }

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
