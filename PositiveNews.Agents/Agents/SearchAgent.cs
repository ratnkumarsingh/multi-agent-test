using System.Text.Json;
using PositiveNews.Agents.Mcp;

namespace PositiveNews.Agents.Agents;

public sealed record NewsCandidate(
    string Title,
    string Url,
    string Source,
    DateTimeOffset? PublishedAt,
    string? Snippet,
    string? ImageUrl);

/// <summary>
/// Decides which search queries to run for today's positive-news pass (forced tool_choice),
/// then runs each query against MCP <c>SearchNews</c> and dedupes the results by URL. Unlike
/// most agents in this project, this one does real I/O beyond its own forced-tool call — the
/// query-planning step is Claude-driven and structured, but fetching results is plain MCP
/// plumbing, the same round trip <c>PositiveNews.Cli</c>'s <c>mcp-direct</c> mode exercises.
/// </summary>
public sealed class SearchAgent : IAgent<string, IReadOnlyList<NewsCandidate>>
{
    private const int MaxCandidates = 20;
    private const int MaxResultsPerQuery = 8;

    private const string SystemPrompt = """
        You plan search queries for a daily positive-news pipeline. Given an optional topic
        hint, propose 3 to 5 distinct search queries likely to surface genuinely uplifting,
        hopeful, or heartwarming real news — not vague platitudes. Cover different angles
        (e.g. community, science/health breakthroughs, environment, everyday kindness) rather
        than near-duplicate phrasings of the same idea.
        """;

    private static readonly JsonElement Schema = JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            queries = new
            {
                type = "array",
                items = new { type = "string" },
                minItems = 3,
                maxItems = 5
            }
        },
        required = new[] { "queries" },
        additionalProperties = false
    });

    private static readonly AnthropicToolSpec Tool = new()
    {
        Name = "return_search_queries",
        Description = "Return 3-5 distinct search queries for today's positive-news search pass.",
        InputSchema = Schema
    };

    private readonly AnthropicClient _client;
    private readonly NewsSearchMcpClient _mcp;

    public SearchAgent(AnthropicClient client, NewsSearchMcpClient mcp)
    {
        _client = client;
        _mcp = mcp;
    }

    public async Task<IReadOnlyList<NewsCandidate>> RunAsync(string topicHint, CancellationToken ct = default)
    {
        // Dynamic per-call content at the end of the message, per convention.
        var userMessage = string.IsNullOrWhiteSpace(topicHint)
            ? "No specific topic hint — cover a broad, varied mix."
            : $"Topic hint: {topicHint}";

        var input = await _client.CallToolAsync(SystemPrompt, userMessage, Tool, forceTool: true, ct);

        var queries = new List<string>();
        foreach (var q in input.GetProperty("queries").EnumerateArray())
        {
            if (q.GetString() is { Length: > 0 } value)
            {
                queries.Add(value);
            }
        }

        var candidates = new List<NewsCandidate>();
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var query in queries)
        {
            if (candidates.Count >= MaxCandidates)
            {
                break;
            }

            NewsSearchResult result;
            try
            {
                result = await _mcp.SearchNewsAsync(query, MaxResultsPerQuery, ct);
            }
            catch (Exception)
            {
                // One bad query shouldn't sink the whole search phase — skip and continue.
                continue;
            }

            if (result.Error is not null)
            {
                continue;
            }

            foreach (var article in result.Articles)
            {
                if (candidates.Count >= MaxCandidates)
                {
                    break;
                }

                if (!seenUrls.Add(article.Url))
                {
                    continue;
                }

                candidates.Add(new NewsCandidate(
                    article.Title,
                    article.Url,
                    article.Source,
                    article.PublishedAt,
                    article.Snippet,
                    article.ImageUrl));
            }
        }

        return candidates;
    }
}
