using System.Diagnostics;
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
    "run-pipeline" => await RunPipelineAsync(config, rest),
    "score-test" => await RunScoreTestAsync(config),
    "headline" => await RunHeadlineAsync(config, rest),
    _ => await RunHeadlineAsync(config, args), // backward-compatible default: whole args = topic
};

static string McpServerProjectPath() =>
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "PositiveNews.McpServer");

/// <summary>Binds AnthropicOptions and prints a standard error if the API key is missing.
/// Returns null on failure so callers can just <c>if (options is null) return 1;</c>.</summary>
static AnthropicOptions? TryGetAnthropicOptions(IConfiguration config)
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
        return null;
    }

    return options;
}

static async Task<int> RunHeadlineAsync(IConfiguration config, string[] topicArgs)
{
    if (TryGetAnthropicOptions(config) is not { } options)
    {
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
    if (TryGetAnthropicOptions(config) is not { } options)
    {
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

/// <summary>
/// Runs the full SearchAgent -> PositivityScorerAgent (fan-out) -> SummarizerAgent
/// pipeline via Orchestrator. The fast iteration loop for the rest of the project.
/// </summary>
static async Task<int> RunPipelineAsync(IConfiguration config, string[] topicArgs)
{
    if (TryGetAnthropicOptions(config) is not { } options)
    {
        return 1;
    }

    var topicHint = string.Join(' ', topicArgs);

    using var http = new HttpClient();
    var client = new AnthropicClient(http, options);

    await using var mcp = await NewsSearchMcpClient.ConnectAsync(McpServerProjectPath());

    var orchestrator = new Orchestrator(
        new SearchAgent(client, mcp),
        new PositivityScorerAgent(client),
        new SummarizerAgent(client));

    var progress = new Progress<string>(Console.WriteLine);

    Console.WriteLine(string.IsNullOrWhiteSpace(topicHint)
        ? "[run-pipeline] Running with no topic hint (broad mix)...\n"
        : $"[run-pipeline] Running with topic hint: {topicHint}\n");

    var stopwatch = Stopwatch.StartNew();
    var stories = await orchestrator.RunAsync(topicHint, topN: 5, progress);
    stopwatch.Stop();

    Console.WriteLine($"\n{stories.Count} curated stor{(stories.Count == 1 ? "y" : "ies")} in {stopwatch.Elapsed.TotalSeconds:F1}s:\n");
    foreach (var story in stories)
    {
        Console.WriteLine($"[{story.Score}/10] {story.Headline}");
        Console.WriteLine($"  {story.Body}");
        Console.WriteLine($"  {story.Source} | {story.SourceUrl}");
        if (story.ImageUrl is not null)
        {
            Console.WriteLine($"  Image: {story.ImageUrl}");
        }
        Console.WriteLine();
    }

    return 0;
}

/// <summary>
/// Runs PositivityScorerAgent alone against a deliberately positive and a deliberately
/// negative hardcoded headline, to demonstrate (and let you eyeball, on demand) that the
/// scorer actually discriminates rather than rubber-stamping everything.
/// </summary>
static async Task<int> RunScoreTestAsync(IConfiguration config)
{
    if (TryGetAnthropicOptions(config) is not { } options)
    {
        return 1;
    }

    using var http = new HttpClient();
    var client = new AnthropicClient(http, options);
    var scorer = new PositivityScorerAgent(client);

    var positive = new NewsCandidate(
        "Neighbors Rebuild a Burned-Down Playground in a Single Weekend",
        "https://example.com/positive",
        "Example News",
        DateTimeOffset.UtcNow,
        "Volunteers turned a scorched lot back into a working playground in under 48 hours, no city budget involved.",
        null);

    var negative = new NewsCandidate(
        "Factory Fire Kills Three, Dozens Injured in Industrial Accident",
        "https://example.com/negative",
        "Example News",
        DateTimeOffset.UtcNow,
        "Investigators are probing the cause of a blaze that tore through a chemical plant overnight, killing three workers.",
        null);

    Console.WriteLine("Scoring a deliberately positive headline vs. a deliberately negative one:\n");

    var positiveScore = await scorer.RunAsync(positive);
    Console.WriteLine($"POSITIVE: \"{positive.Title}\"");
    Console.WriteLine($"  Score: {positiveScore.Score}/10, IsPositive: {positiveScore.IsPositive}");
    Console.WriteLine($"  Reasoning: {positiveScore.Reasoning}\n");

    var negativeScore = await scorer.RunAsync(negative);
    Console.WriteLine($"NEGATIVE: \"{negative.Title}\"");
    Console.WriteLine($"  Score: {negativeScore.Score}/10, IsPositive: {negativeScore.IsPositive}");
    Console.WriteLine($"  Reasoning: {negativeScore.Reasoning}\n");

    if (positiveScore.Score > negativeScore.Score && positiveScore.IsPositive && !negativeScore.IsPositive)
    {
        Console.WriteLine("PASS: scorer discriminates correctly.");
        return 0;
    }

    Console.WriteLine("WARNING: scorer did not clearly discriminate — review PositivityScorerAgent's prompt.");
    return 1;
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
