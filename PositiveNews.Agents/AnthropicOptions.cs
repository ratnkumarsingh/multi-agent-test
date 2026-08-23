namespace PositiveNews.Agents;

/// <summary>
/// Bound from the "Anthropic" configuration section. Mirrors the same shape used in the
/// sibling Aviral_Maths project's ClaudeOptions, so the same mental model (and the same
/// gateway, if you use one) carries over between projects.
/// </summary>
public sealed class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    /// <summary>API key. Set via user-secrets / environment, never committed to a config file.</summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Override the API endpoint root. Leave empty for Anthropic's own API
    /// (https://api.anthropic.com). Set to an Anthropic-compatible reseller/proxy root
    /// (e.g. an OpenRouter-style gateway) to route calls there instead. Do not include
    /// a trailing "/v1" — the client appends "v1/messages" itself.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Send the key as "Authorization: Bearer" instead of "x-api-key". Anthropic's own
    /// API uses x-api-key (false); OpenRouter-style gateways typically use Bearer (true).
    /// </summary>
    public bool UseBearerAuth { get; set; }

    /// <summary>
    /// Model id. Defaults to Anthropic's own naming; a gateway may need a namespaced id
    /// instead (e.g. "anthropic/claude-sonnet-5") — override here if so.
    /// </summary>
    public string Model { get; set; } = "claude-sonnet-5";

    /// <summary>
    /// Cheaper/faster model for low-stakes calls where <see cref="Model"/> would be
    /// overkill (currently just <c>TranslationAgent</c>). Same bare-id-vs-gateway-namespaced
    /// override rules as <see cref="Model"/> apply — override via
    /// <c>Anthropic:TranslationModel</c> if the configured gateway needs a namespaced id.
    /// </summary>
    public string TranslationModel { get; set; } = "claude-haiku-4-5-20251001";
}
