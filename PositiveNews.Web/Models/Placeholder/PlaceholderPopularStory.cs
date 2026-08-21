namespace PositiveNews.Web.Models.Placeholder;

/// <summary>PLACEHOLDER — see <see cref="PlaceholderStory"/>.</summary>
public sealed record PlaceholderPopularStory(
    string Title,
    DateOnly PublishedOn,
    string Author,
    int ViewCount,
    string AccentGradient);
