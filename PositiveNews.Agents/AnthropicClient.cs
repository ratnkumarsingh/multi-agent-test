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
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new ArgumentException("AnthropicOptions.ApiKey is required.", nameof(options));
        }

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
    /// </summary>
    public async Task<JsonElement> CallToolAsync(
        string systemPrompt,
        string userMessage,
        AnthropicToolSpec tool,
        bool forceTool = true,
        CancellationToken ct = default)
    {
        object toolChoice = forceTool
            ? new { type = "tool", name = tool.Name }
            : new { type = "auto" };

        var requestBody = new
        {
            model = _model,
            max_tokens = 4096,
            system = systemPrompt,
            messages = new object[]
            {
                new { role = "user", content = userMessage }
            },
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
        var content = doc.RootElement.GetProperty("content");

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
            $"Claude did not return a '{tool.Name}' tool_use block. Raw response: {raw}");
    }
}
