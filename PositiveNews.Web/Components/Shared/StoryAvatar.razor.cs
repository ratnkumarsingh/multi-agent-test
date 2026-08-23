using Microsoft.AspNetCore.Components;

namespace PositiveNews.Web.Components.Shared;

public partial class StoryAvatar
{
    /// <summary>Real image URL, when the story has one. Not every source article does
    /// (NewsAPI doesn't always return one) — falls back to <see cref="FallbackGradient"/>
    /// when null/empty rather than requiring the caller to pre-supply a color.</summary>
    [Parameter]
    public string? ImageUrl { get; set; }

    /// <summary>Any stable per-story string (e.g. the headline) used to deterministically
    /// pick a fallback gradient — same story always gets the same color for as long as the
    /// app process stays up. Only required when <see cref="ImageUrl"/> is absent, but always
    /// passing it keeps callers simple.</summary>
    [Parameter, EditorRequired]
    public string Seed { get; set; } = string.Empty;

    [Parameter]
    public StoryAvatarSize Size { get; set; } = StoryAvatarSize.Large;

    private static readonly string[] GradientPalette =
    [
        "linear-gradient(135deg, #f97316, #fb923c)",
        "linear-gradient(135deg, #0ea5e9, #22d3ee)",
        "linear-gradient(135deg, #a855f7, #ec4899)",
        "linear-gradient(135deg, #22c55e, #4ade80)",
        "linear-gradient(135deg, #eab308, #facc15)",
    ];

    private string FallbackGradient =>
        GradientPalette[(uint)Seed.GetHashCode() % GradientPalette.Length];
}
