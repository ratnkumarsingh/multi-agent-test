namespace PositiveNews.McpServer;

/// <summary>
/// Bound from the "NewsApi" configuration section. Mirrors the secrets pattern used by
/// <c>PositiveNews.Agents.AnthropicOptions</c>: user-secrets first, environment variable
/// fallback, never hardcoded or committed.
/// </summary>
public sealed class NewsApiOptions
{
    public const string SectionName = "NewsApi";

    /// <summary>NewsAPI.org API key. Set via user-secrets or the NEWSAPI_KEY env var.</summary>
    public string? ApiKey { get; set; }
}
