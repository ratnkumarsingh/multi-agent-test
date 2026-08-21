namespace PositiveNews.McpServer;

/// <summary>Category for a failed search, so a caller can decide whether to retry.</summary>
public enum NewsSearchErrorCategory
{
    /// <summary>Bad query/config (e.g. missing or invalid API key) — retrying won't help.</summary>
    InvalidRequest,

    /// <summary>Upstream rate limit hit — safe to retry after a backoff.</summary>
    RateLimited,

    /// <summary>Upstream provider unreachable or errored — safe to retry.</summary>
    UpstreamUnavailable,

    /// <summary>Anything that doesn't fit the categories above.</summary>
    Unknown,
}

/// <summary>
/// A structured search failure. Returned as a field on <see cref="NewsSearchResponse"/>
/// rather than a bare string, and never sharing the success content channel — a caller
/// (an LLM or C# code) can branch on <see cref="Category"/>/<see cref="Retryable"/>
/// without parsing prose.
/// </summary>
public sealed record NewsSearchError(NewsSearchErrorCategory Category, bool Retryable, string Message);

public sealed record NewsArticle(
    string Title,
    string Url,
    string Source,
    DateTimeOffset? PublishedAt,
    string? Snippet);

/// <summary>
/// The one envelope <c>SearchNews</c> ever returns. <see cref="TotalCount"/> is the
/// upstream's true match count even when <see cref="Articles"/> is capped shorter, so a
/// caller doesn't need to page through everything just to know how many results exist.
/// </summary>
public sealed record NewsSearchResponse(
    IReadOnlyList<NewsArticle> Articles,
    int TotalCount,
    NewsSearchError? Error)
{
    public static NewsSearchResponse Failure(NewsSearchErrorCategory category, bool retryable, string message) =>
        new([], 0, new NewsSearchError(category, retryable, message));
}

public interface INewsSearchClient
{
    Task<NewsSearchResponse> SearchAsync(string query, int max, CancellationToken ct);
}
