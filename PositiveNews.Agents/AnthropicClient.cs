using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PositiveNews.Agents;

/// <summary>A single tool Claude is offered, described as a JSON Schema.</summary>
public sealed class AnthropicToolSpec
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required JsonElement InputSchema { get; init; }
}

public sealed class AnthropicApiException(HttpStatusCode statusCode, string body)
    : Exception($"Anthropic API returned {(int)statusCode} {statusCode}: {body}")
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}

/// <summary>
/// Thin wrapper over the Anthropic Messages API. Every agent in this project drives
/// Claude through <see cref="CallToolAsync"/> with a single tool and a JSON Schema —
/// this is the "structured output via forced tool use" pattern, used deliberately
/// instead of parsing free-text/markdown-wrapped JSON.
/// </summary>
public sealed class AnthropicClient
{
    private const string ApiVersion = "2023-06-01";

    private readonly HttpClient _http;
    private readonly string _model;

    /// <summary>
    /// Configures the client from <see cref="AnthropicOptions"/>. Defaults to Anthropic's
    /// own API (x-api-key auth); set <see cref="AnthropicOptions.BaseUrl"/> +
    /// <see cref="AnthropicOptions.UseBearerAuth"/> to route through an
    /// Anthropic-compatible gateway instead — see AnthropicOptions' doc comments.
    /// </summary>
    public AnthropicClient(HttpClient http, AnthropicOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ApiKey, nameof(options.ApiKey));

        _http = http;
        _model = options.Model;

        var root = string.IsNullOrWhiteSpace(options.BaseUrl)
            ? "https://api.anthropic.com/"
            : options.BaseUrl.TrimEnd('/') + "/";
        _http.BaseAddress ??= new Uri(root);

        if (options.UseBearerAuth)
        {
            _http.DefaultRequestHeaders.Remove("Authorization");
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {options.ApiKey}");
        }
        else
        {
            _http.DefaultRequestHeaders.Remove("x-api-key");
            _http.DefaultRequestHeaders.Add("x-api-key", options.ApiKey);
        }

        _http.DefaultRequestHeaders.Remove("anthropic-version");
        _http.DefaultRequestHeaders.Add("anthropic-version", ApiVersion);
    }

    /// <summary>
    /// Sends a single-turn request offering exactly one tool, and returns that tool's
    /// parsed <c>input</c> once Claude calls it. When <paramref name="forceTool"/> is
    /// true (the default, and the shape almost every agent in this project uses),
    /// <c>tool_choice</c> is pinned to the tool so Claude always answers via a single
    /// structured tool_use block rather than free text. Set it false only when Claude
    /// should genuinely decide whether to call the tool at all (e.g. an MCP search tool).
    /// <paramref name="model"/> overrides the client's configured default model for this
    /// call only — for agents that deliberately run on a different (typically cheaper)
    /// model than the rest of the pipeline, e.g. <c>TranslationAgent</c>.
    /// </summary>
    public async Task<JsonElement> CallToolAsync(
        string systemPrompt,
        string userMessage,
        AnthropicToolSpec tool,
        bool forceTool = true,
        string? model = null,
        CancellationToken ct = default)
    {
        var content = await SendMessagesAsync(
            systemPrompt,
            [new { role = "user", content = userMessage }],
            tool,
            forceTool,
            model,
            ct);

        foreach (var block in content.EnumerateArray())
        {
            if (block.GetProperty("type").GetString() == "tool_use" &&
                block.GetProperty("name").GetString() == tool.Name)
            {
                // Clone so the value survives past the JsonDocument's lifetime.
                return block.GetProperty("input").Clone();
            }
        }

        throw new InvalidOperationException(
            $"Claude did not return a '{tool.Name}' tool_use block. Raw content: {content}");
    }

    /// <summary>
    /// Offers <paramref name="tool"/> with <c>tool_choice: auto</c> so Claude genuinely
    /// decides whether to call it — the contrasting mode to <see cref="CallToolAsync"/>'s
    /// forced call, used where Claude should weigh whether the tool is even needed (e.g.
    /// an MCP search tool). Each time Claude calls the tool, <paramref name="executeTool"/>
    /// runs it and the result is fed back for another turn — tool_choice stays "auto"
    /// throughout, so Claude may call the tool more than once (e.g. to refine a query)
    /// before answering in text, which is why this loops rather than doing a single
    /// request/response/request round trip.
    /// </summary>
    public async Task<string> RunAutoToolConversationAsync(
        string systemPrompt,
        string userMessage,
        AnthropicToolSpec tool,
        Func<JsonElement, CancellationToken, Task<JsonElement>> executeTool,
        CancellationToken ct = default)
    {
        const int MaxToolCalls = 5;

        List<object> messages = [new { role = "user", content = userMessage }];

        for (var i = 0; i <= MaxToolCalls; i++)
        {
            var content = await SendMessagesAsync(systemPrompt, messages, tool, forceTool: false, model: null, ct);

            JsonElement? toolUse = null;
            foreach (var block in content.EnumerateArray())
            {
                if (block.GetProperty("type").GetString() == "tool_use")
                {
                    toolUse = block;
                    break;
                }
            }

            if (toolUse is not { } toolUseBlock)
            {
                return ExtractText(content);
            }

            if (i == MaxToolCalls)
            {
                return ExtractText(content) is { Length: > 0 } text
                    ? text
                    : $"(Claude kept calling {tool.Name} without answering after {MaxToolCalls} calls.)";
            }

            var toolResult = await executeTool(toolUseBlock.GetProperty("input"), ct);

            messages.Add(new { role = "assistant", content });
            messages.Add(new
            {
                role = "user",
                content = new object[]
                {
                    new
                    {
                        type = "tool_result",
                        tool_use_id = toolUseBlock.GetProperty("id").GetString(),
                        content = toolResult.GetRawText()
                    }
                }
            });
        }

        throw new UnreachableException();
    }

    private async Task<JsonElement> SendMessagesAsync(
        string systemPrompt,
        IReadOnlyList<object> messages,
        AnthropicToolSpec tool,
        bool forceTool,
        string? model,
        CancellationToken ct)
    {
        object toolChoice = forceTool
            ? new { type = "tool", name = tool.Name }
            : new { type = "auto" };

        var requestBody = new
        {
            model = model ?? _model,
            max_tokens = 4096,
            system = systemPrompt,
            messages,
            tools = new object[]
            {
                new { name = tool.Name, description = tool.Description, input_schema = tool.InputSchema }
            },
            tool_choice = toolChoice
        };

        using var response = await _http.PostAsJsonAsync("v1/messages", requestBody, ct);
        var raw = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new AnthropicApiException(response.StatusCode, raw);
        }

        using var doc = JsonDocument.Parse(raw);
        // Clone so the value survives past the JsonDocument's lifetime.
        return doc.RootElement.GetProperty("content").Clone();
    }

    private static string ExtractText(JsonElement content)
    {
        var parts = new List<string>();
        foreach (var block in content.EnumerateArray())
        {
            if (block.GetProperty("type").GetString() == "text")
            {
                parts.Add(block.GetProperty("text").GetString() ?? string.Empty);
            }
        }
        return string.Join("\n", parts);
    }
}
