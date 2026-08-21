using PositiveNews.Web.Models.Placeholder;

namespace PositiveNews.Web.Components.Pages;

// PLACEHOLDER — all data on this page is hardcoded sample content for the Phase 3 UI
// shell. Replaced by a real query against PositiveNews.Core in Phase 6, once persistence
// (Phase 5) and the orchestration pipeline (Phase 4) exist to produce real stories.
public partial class Home
{
    private string _searchQuery = string.Empty;

    private string SearchQuery
    {
        get => _searchQuery;
        set => _searchQuery = value;
    }

    private List<PlaceholderStory> FilteredStories =>
        Stories
            .Where(s => string.IsNullOrWhiteSpace(SearchQuery) ||
                        s.Title.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
            .ToList();

    private static readonly IReadOnlyList<PlaceholderStory> Stories =
    [
        new("Neighbors Rebuild a Burned-Down Playground in a Single Weekend",
            "Community", new DateOnly(2026, 8, 18), 12,
            "Word spread on a local group chat, and by Sunday afternoon a scorched lot had swings again — no city budget, no permits, just volunteers.",
            "linear-gradient(135deg, #f97316, #fb923c)"),

        new("Small Coral Reef Shows First Signs of Recovery in a Decade",
            "Environment", new DateOnly(2026, 8, 16), 8,
            "Marine biologists tracking a bleached reef off the coast recorded live coral growth for the first time since 2016 — a slow but real turnaround.",
            "linear-gradient(135deg, #0ea5e9, #22d3ee)"),

        new("Retired Teacher's Free Tutoring Sessions Hit 1,000 Students",
            "Education", new DateOnly(2026, 8, 14), 21,
            "What started as helping one neighbor's kid with algebra turned into a weekend fixture at the community library, entirely free of charge.",
            "linear-gradient(135deg, #a855f7, #ec4899)"),

        new("Lab-Grown Skin Graft Breakthrough Cuts Recovery Time in Half",
            "Health", new DateOnly(2026, 8, 12), 15,
            "A new grafting technique moved from trial to hospital use faster than expected, and early patients are already reporting shorter, less painful recoveries.",
            "linear-gradient(135deg, #22c55e, #4ade80)"),

        new("City's Bike-Share Program Cuts Commute Emissions by a Third",
            "Environment", new DateOnly(2026, 8, 9), 6,
            "A year-one report on the municipal bike-share scheme shows commuters are actually switching, not just supplementing car trips.",
            "linear-gradient(135deg, #eab308, #facc15)"),
    ];

    private static readonly IReadOnlyList<PlaceholderCategory> Categories =
    [
        new("Community", 14),
        new("Environment", 9),
        new("Health", 7),
        new("Education", 5),
        new("Innovation", 11),
    ];

    private static readonly IReadOnlyList<PlaceholderPopularStory> PopularStories =
    [
        new("Neighbors Rebuild a Burned-Down Playground in a Single Weekend",
            new DateOnly(2026, 8, 18), "Dana Lewis", 340,
            "linear-gradient(135deg, #f97316, #fb923c)"),

        new("Small Coral Reef Shows First Signs of Recovery in a Decade",
            new DateOnly(2026, 8, 16), "Dana Lewis", 512,
            "linear-gradient(135deg, #0ea5e9, #22d3ee)"),

        new("Lab-Grown Skin Graft Breakthrough Cuts Recovery Time in Half",
            new DateOnly(2026, 8, 12), "Marcus Diallo", 289,
            "linear-gradient(135deg, #22c55e, #4ade80)"),
    ];

    private static readonly IReadOnlyList<string> Tags =
    [
        "Kindness", "Science", "Nature", "Community", "Health", "Innovation",
    ];
}
