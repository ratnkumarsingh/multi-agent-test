using System.ComponentModel;
using ModelContextProtocol.Server;

namespace PositiveNews.McpServer.Tools;

/// <summary>
/// MCP tools for finding candidate news articles. <see cref="INewsSearchClient"/> is
/// injected per-call from DI (registered in Program.cs) rather than new'd here, so the
/// provider stays swappable.
/// </summary>
internal sealed class NewsSearchTools
{
    [McpServerTool(Name = "SearchNews")]
    [Description(
        "Searches English-language news articles from roughly the last month matching a " +
        "free-text query. Returns up to `max` matching articles plus the true total match " +
        "count (TotalCount) even when the returned list is capped shorter. An empty article " +
        "list can mean either no matches or a failed call — check Error (a structured " +
        "category/retryable/message, not a bare string) before treating it as no matches.")]
    public static Task<NewsSearchResponse> SearchNews(
        INewsSearchClient newsSearchClient,
        [Description("Free-text search query, e.g. \"renewable energy breakthrough\".")]
        string query,
        [Description("Maximum number of articles to return. Defaults to 10; server-capped at 25 regardless of what's requested.")]
        int max = 10,
        CancellationToken cancellationToken = default)
        => newsSearchClient.SearchAsync(query, max, cancellationToken);
}
