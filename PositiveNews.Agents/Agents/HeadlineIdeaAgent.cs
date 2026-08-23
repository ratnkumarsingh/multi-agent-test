using System.Text.Json;

namespace PositiveNews.Agents.Agents;

public sealed record HeadlineIdea(string Title, string OneLineSummary);

/// <summary>
/// Deliberately trivial first agent: no external I/O, just a topic in and a few
/// structured headline ideas out. The point of this agent is the mechanism
/// (forced tool_choice -> JSON Schema -> C# record), not the content.
/// </summary>
public sealed class HeadlineIdeaAgent : IAgent<string, IReadOnlyList<HeadlineIdea>>
{
    private const string SystemPrompt = """
        You brainstorm short, upbeat headline ideas for a positive-news website.
        Given a topic, propose 3 distinct headline ideas that are genuinely positive
        (uplifting, hopeful, or heartwarming) and plausible as real news, not vague
        platitudes. Each idea needs a punchy title and a one-sentence summary.
        """;

    private static readonly JsonElement Schema = JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            ideas = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        title = new { type = "string" },
                        oneLineSummary = new { type = "string" }
                    },
                    required = new[] { "title", "oneLineSummary" },
                    additionalProperties = false
                }
            }
        },
        required = new[] { "ideas" },
        additionalProperties = false
    });

    private static readonly AnthropicToolSpec Tool = new()
    {
        Name = "return_headline_ideas",
        Description = "Return a list of positive-news headline ideas for the given topic.",
        InputSchema = Schema
    };

    private readonly AnthropicClient _client;

    public HeadlineIdeaAgent(AnthropicClient client) => _client = client;

    public async Task<IReadOnlyList<HeadlineIdea>> RunAsync(string topic, CancellationToken ct = default)
    {
        // Dynamic per-call content (the topic) goes at the end of the message, not the
        // start, so a static prefix stays prompt-cache-friendly once caching is added.
        var userMessage = $"Topic: {topic}";

        var input = await _client.CallToolAsync(SystemPrompt, userMessage, Tool, forceTool: true, ct: ct);

        var result = new List<HeadlineIdea>();
        foreach (var idea in input.GetProperty("ideas").EnumerateArray())
        {
            result.Add(new HeadlineIdea(
                idea.GetProperty("title").GetString()!,
                idea.GetProperty("oneLineSummary").GetString()!));
        }
        return result;
    }
}
