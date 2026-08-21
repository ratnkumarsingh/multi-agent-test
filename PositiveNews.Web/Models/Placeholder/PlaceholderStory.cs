namespace PositiveNews.Web.Models.Placeholder;

/// <summary>
/// PLACEHOLDER — hardcoded sample data for the Phase 3 UI shell. Replaced by a real query
/// against <c>PositiveNews.Core</c>'s <c>NewsStory</c> entity in Phase 6, once persistence
/// (Phase 5) exists.
/// </summary>
public sealed record PlaceholderStory(
    string Title,
    string Category,
    DateOnly PublishedOn,
    int CommentCount,
    string Excerpt,
    string AccentGradient);
