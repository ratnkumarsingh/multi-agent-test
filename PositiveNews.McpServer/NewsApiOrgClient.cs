using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PositiveNews.McpServer;

/// <summary>
/// <see cref="INewsSearchClient"/> backed by NewsAPI.org's "everything" endpoint. Isolated
/// behind the interface so swapping providers (e.g. GNews.io) is a one-file change.
/// </summary>
public sealed class NewsApiOrgClient : INewsSearchClient
{
    /// <summary>Hard server-side cap, independent of what a caller asks for — never trust
    /// the caller (or a prompt instruction) to self-limit page size.</summary>
    private const int MaxAllowed = 25;

    private readonly HttpClient _http;
    private readonly NewsApiOptions _options;

    public NewsApiOrgClient(HttpClient http, NewsApiOptions options)
    {
        _http = http;
        _http.BaseAddress ??= new Uri("https://newsapi.org/");
        // NewsAPI rejects anonymous requests (no User-Agent) with a 400 userAgentMissing.
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("PositiveNews.McpServer/1.0");
        }
        _options = options;
    }

    public async Task<NewsSearchResponse> SearchAsync(string query, int max, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return NewsSearchResponse.Failure(
                NewsSearchErrorCategory.InvalidRequest,
                retryable: false,
                "NewsApi:ApiKey is not configured (set via user-secrets or the NEWSAPI_KEY env var).");
        }

        var pageSize = Math.Clamp(max, 1, MaxAllowed);
        var url = $"v2/everything?q={Uri.EscapeDataString(query)}&pageSize={pageSize}&sortBy=publishedAt&language=en";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Api-Key", _options.ApiKey);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            return NewsSearchResponse.Failure(
                NewsSearchErrorCategory.UpstreamUnavailable, retryable: true, $"Network error calling NewsAPI: {ex.Message}");
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta;
                var suffix = retryAfter is { } d ? $" Retry after {d.TotalSeconds:F0}s." : string.Empty;
                return NewsSearchResponse.Failure(
                    NewsSearchErrorCategory.RateLimited, retryable: true, $"NewsAPI rate limit hit.{suffix}");
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
            {
                var body = await SafeReadErrorAsync(response, ct);
                return NewsSearchResponse.Failure(
                    NewsSearchErrorCategory.InvalidRequest, retryable: false, $"NewsAPI rejected the request: {body}");
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await SafeReadErrorAsync(response, ct);
                return NewsSearchResponse.Failure(
                    NewsSearchErrorCategory.UpstreamUnavailable, retryable: true,
                    $"NewsAPI returned {(int)response.StatusCode} {response.StatusCode}: {body}");
            }

            NewsApiEverythingResponse? payload;
            try
            {
                payload = await response.Content.ReadFromJsonAsync<NewsApiEverythingResponse>(JsonOptions, ct);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                // A 200 with a non-JSON body (HTML error page, truncated response, etc.) —
                // still a structured failure, never an unhandled exception surfaced as a
                // bare-string tool error.
                return NewsSearchResponse.Failure(
                    NewsSearchErrorCategory.UpstreamUnavailable, retryable: true,
                    $"NewsAPI returned a 200 with an unparseable body: {ex.Message}");
            }

            if (payload?.Articles is null)
            {
                return NewsSearchResponse.Failure(
                    NewsSearchErrorCategory.UpstreamUnavailable, retryable: true, "NewsAPI returned an unparseable response.");
            }

            var articles = payload.Articles
                .Where(a => a.Title is not null && a.Url is not null)
                .Select(a => new NewsArticle(
                    a.Title!,
                    a.Url!,
                    a.Source?.Name ?? "Unknown",
                    a.PublishedAt,
                    a.Description,
                    a.UrlToImage,
                    Locale: "en")) // this client always queries with &language=en
                .ToList();

            return new NewsSearchResponse(articles, payload.TotalResults, Error: null);
        }
    }

    private static async Task<string> SafeReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            return body.Length > 300 ? body[..300] : body;
        }
        catch
        {
            return "(no body)";
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record NewsApiEverythingResponse(
        [property: JsonPropertyName("totalResults")] int TotalResults,
        [property: JsonPropertyName("articles")] IReadOnlyList<NewsApiArticle>? Articles);

    private sealed record NewsApiArticle(
        [property: JsonPropertyName("source")] NewsApiSource? Source,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("publishedAt")] DateTimeOffset? PublishedAt,
        [property: JsonPropertyName("urlToImage")] string? UrlToImage);

    private sealed record NewsApiSource([property: JsonPropertyName("name")] string? Name);
}
