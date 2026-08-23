using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace PositiveNews.Agents.Mcp;

public sealed record NewsArticle(string Title, string Url, string Source, DateTimeOffset? PublishedAt, string? Snippet, string? ImageUrl, string Locale);

/// <summary>Mirrors <c>PositiveNews.McpServer.NewsSearchErrorCategory</c> — kept as an enum,
/// not a bare string, so callers get compile-time exhaustiveness/typo-checking when
/// branching on it, per CLAUDE.md's "stays structured" rule for values code compares on.</summary>
public enum NewsSearchErrorCategory
{
    InvalidRequest,
    RateLimited,
    UpstreamUnavailable,
    Unknown,
}

public sealed record NewsSearchFailure(NewsSearchErrorCategory Category, bool Retryable, string Message);

public sealed record NewsSearchResult(IReadOnlyList<NewsArticle> Articles, int TotalCount, NewsSearchFailure? Error);

/// <summary>
/// Spawns <c>PositiveNews.McpServer</c> over stdio and exposes its <c>SearchNews</c> tool
/// as a typed C# call. This is the client side of the MCP round trip — it deliberately
/// mirrors the server's DTOs rather than referencing the server project directly, the same
/// decoupling any other MCP client (including Claude Code itself) has to live with.
/// </summary>
public sealed class NewsSearchMcpClient : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        // JsonStringEnumConverter reads are case-insensitive regardless of naming policy,
        // so this tolerates either PascalCase or camelCase on the wire for the error category.
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private readonly McpClient _client;

    private NewsSearchMcpClient(McpClient client) => _client = client;

    /// <summary>
    /// Launches <c>dotnet run --project {mcpServerProjectPath}</c> as a child process and
    /// completes the MCP handshake over its stdio.
    /// </summary>
    public static async Task<NewsSearchMcpClient> ConnectAsync(
        string mcpServerProjectPath, CancellationToken ct = default)
    {
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "positivenews-mcp",
            Command = "dotnet",
            Arguments = ["run", "--project", mcpServerProjectPath],
        });

        var client = await McpClient.CreateAsync(transport, cancellationToken: ct);
        return new NewsSearchMcpClient(client);
    }

    public async Task<IReadOnlyList<McpClientTool>> ListToolsAsync(CancellationToken ct = default) =>
        (await _client.ListToolsAsync(cancellationToken: ct)).ToList();

    public async Task<NewsSearchResult> SearchNewsAsync(string query, int max = 10, CancellationToken ct = default)
    {
        var result = await _client.CallToolAsync(
            "SearchNews",
            new Dictionary<string, object?> { ["query"] = query, ["max"] = max },
            cancellationToken: ct);

        if (result.IsError == true)
        {
            var text = ExtractText(result);
            throw new InvalidOperationException($"SearchNews tool call failed: {text}");
        }

        var dto = DeserializeResponse(result);
        var articles = dto.Articles
            .Select(a => new NewsArticle(a.Title, a.Url, a.Source, a.PublishedAt, a.Snippet, a.ImageUrl, a.Locale))
            .ToList();

        var error = dto.Error is { } e ? new NewsSearchFailure(e.Category, e.Retryable, e.Message) : null;
        return new NewsSearchResult(articles, dto.TotalCount, error);
    }

    private static NewsSearchResponseDto DeserializeResponse(CallToolResult result)
    {
        // Prefer structured content (typed JSON per the MCP result schema); fall back to
        // parsing the first text block as JSON for servers/SDK versions that only emit that.
        if (result.StructuredContent is { } structured)
        {
            return structured.Deserialize<NewsSearchResponseDto>(JsonOptions)
                ?? throw new InvalidOperationException("SearchNews returned empty structured content.");
        }

        var text = ExtractText(result);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("SearchNews returned neither structured content nor text.");
        }

        return JsonSerializer.Deserialize<NewsSearchResponseDto>(text, JsonOptions)
            ?? throw new InvalidOperationException("SearchNews's text content was not valid JSON.");
    }

    private static string ExtractText(CallToolResult result) =>
        string.Join(" ", result.Content.OfType<TextContentBlock>().Select(c => c.Text));

    public ValueTask DisposeAsync() => _client.DisposeAsync();

    private sealed record NewsSearchResponseDto(
        IReadOnlyList<NewsArticleDto> Articles,
        int TotalCount,
        NewsSearchErrorDto? Error);

    private sealed record NewsArticleDto(
        string Title,
        string Url,
        string Source,
        DateTimeOffset? PublishedAt,
        string? Snippet,
        string? ImageUrl,
        string Locale);

    private sealed record NewsSearchErrorDto(NewsSearchErrorCategory Category, bool Retryable, string Message);
}
