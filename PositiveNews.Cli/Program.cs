using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using PositiveNews.Agents;
using PositiveNews.Agents.Agents;
using PositiveNews.Agents.Mcp;

var config = new ConfigurationBuilder()
    .AddUserSecrets(Assembly.GetExecutingAssembly())
    .AddEnvironmentVariables()
    .Build();

var mode = args.Length > 0 ? args[0] : "headline";
var rest = args.Length > 1 ? args[1..] : [];

return mode switch
{
    "mcp-direct" => await RunMcpDirectAsync(rest),
    "mcp-auto" => await RunMcpAutoAsync(config, rest),
    "headline" => await RunHeadlineAsync(config, rest),
    _ => await RunHeadlineAsync(config, args), // backward-compatible default: whole args = topic
};

static string McpServerProjectPath() =>
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "PositiveNews.McpServer");

static async Task<int> RunHeadlineAsync(IConfiguration config, string[] topicArgs)
{
    var options = new AnthropicOptions();
    config.GetSection(AnthropicOptions.SectionName).Bind(options);
    options.ApiKey ??= config["ANTHROPIC_API_KEY"];

    if (string.IsNullOrWhiteSpace(options.ApiKey))
    {
        Console.Error.WriteLine(
            "Missing Anthropic API key. Set it via:\n" +
            "  dotnet user-secrets set \"Anthropic:ApiKey\" <key> --project PositiveNews.Cli\n" +
            "(add \"Anthropic:BaseUrl\"/\"Anthropic:UseBearerAuth\" the same way to route through a\n" +
            "gateway instead of Anthropic directly) or set ANTHROPIC_API_KEY as an environment variable.");
        return 1;
    }

    using var http = new HttpClient();
    var client = new AnthropicClient(http, options);
    var agent = new HeadlineIdeaAgent(client);

    var topic = topicArgs.Length > 0 ? string.Join(' ', topicArgs) : "renewable energy breakthroughs";
    Console.WriteLine($"Asking HeadlineIdeaAgent about: {topic}\n");

    var ideas = await agent.RunAsync(topic);
    foreach (var idea in ideas)
    {
        Console.WriteLine($"- {idea.Title}");
        Console.WriteLine($"  {idea.OneLineSummary}\n");
    }

    return 0;
}

/// <summary>
/// Calls the MCP server's SearchNews tool directly over stdio, bypassing Claude entirely.
/// Proves the MCP server/client plumbing works in isolation from any LLM call.
/// </summary>
static async Task<int> RunMcpDirectAsync(string[] queryArgs)
{
    var query = queryArgs.Length > 0 ? string.Join(' ', queryArgs) : "positive news";
    Console.WriteLine($"[mcp-direct] Connecting to PositiveNews.McpServer and searching: {query}\n");

    await using var mcp = await NewsSearchMcpClient.ConnectAsync(McpServerProjectPath());

    var tools = await mcp.ListToolsAsync();
    Console.WriteLine($"Tools available: {string.Join(", ", tools.Select(t => t.Name))}\n");

    var result = await mcp.SearchNewsAsync(query, max: 5);
    PrintResult(result);
    return 0;
}

/// <summary>
/// Lets Claude decide (tool_choice: auto) whether to call SearchNews at all. If it does,
/// we execute the call over MCP and feed the result back for a grounded final answer.
/// </summary>
static async Task<int> RunMcpAutoAsync(IConfiguration config, string[] queryArgs)
{
    var options = new AnthropicOptions();
    config.GetSection(AnthropicOptions.SectionName).Bind(options);
    options.ApiKey ??= config["ANTHROPIC_API_KEY"];

    if (string.IsNullOrWhiteSpace(options.ApiKey))
    {
        Console.Error.WriteLine(
            "Missing Anthropic API key. Set it via:\n" +
            "  dotnet user-secrets set \"Anthropic:ApiKey\" <key> --project PositiveNews.Cli\n" +
            "or set ANTHROPIC_API_KEY as an environment variable.");
        return 1;
    }

    var request = queryArgs.Length > 0
        ? string.Join(' ', queryArgs)
        : "Find one genuinely uplifting recent news story and summarize it in two sentences.";

    Console.WriteLine($"[mcp-auto] Asking Claude (tool_choice: auto): {request}\n");

    using var http = new HttpClient();
    var client = new AnthropicClient(http, options);

    await using var mcp = await NewsSearchMcpClient.ConnectAsync(McpServerProjectPath());

    const string systemPrompt = """
        You help find positive news. You have access to a SearchNews tool that searches
        recent news articles by free-text query. Only call it if searching for real,
        current articles would actually help answer the user's request — for requests
        that don't need current news, just answer directly.
        """;

    var tool = new AnthropicToolSpec
    {
        Name = "SearchNews",
        Description = "Search recent news articles matching a free-text query. Returns up to `max` articles.",
        InputSchema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                query = new { type = "string", description = "Free-text search query." },
                max = new { type = "integer", description = "Max articles to return (server-capped at 25)." }
            },
            required = new[] { "query" },
            additionalProperties = false
        })
    };

    var answer = await client.RunAutoToolConversationAsync(
        systemPrompt,
        request,
        tool,
        async (input, ct) =>
        {
            var q = input.GetProperty("query").GetString()!;
            var max = input.TryGetProperty("max", out var m) ? m.GetInt32() : 10;
            Console.WriteLine($"  -> Claude called SearchNews(query: \"{q}\", max: {max})\n");

            var result = await mcp.SearchNewsAsync(q, max, ct);
            return JsonSerializer.SerializeToElement(result);
        });

    Console.WriteLine("Claude's final answer:\n");
    Console.WriteLine(answer);
    return 0;
}

static void PrintResult(NewsSearchResult result)
{
    if (result.Error is { } error)
    {
        Console.WriteLine($"Error ({error.Category}, retryable: {error.Retryable}): {error.Message}");
        return;
    }

    Console.WriteLine($"Total matches: {result.TotalCount} (showing {result.Articles.Count})\n");
    foreach (var article in result.Articles)
    {
        Console.WriteLine($"- {article.Title}");
        Console.WriteLine($"  {article.Source} | {article.PublishedAt:u}");
        Console.WriteLine($"  {article.Url}\n");
    }
}
