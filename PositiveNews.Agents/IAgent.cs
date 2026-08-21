namespace PositiveNews.Agents;

/// <summary>
/// The convention every agent in this project follows: a typed input, a typed output,
/// and internally one JSON Schema constant + one forced tool_choice call to
/// <see cref="AnthropicClient.CallToolAsync"/>. See CLAUDE.md for the full convention.
/// </summary>
public interface IAgent<TIn, TOut>
{
    Task<TOut> RunAsync(TIn input, CancellationToken ct = default);
}
