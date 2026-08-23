using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using PositiveNews.Core.Data;
using PositiveNews.Core.Entities;

namespace PositiveNews.Web.Components.Pages;

public partial class Home
{
    private const int TopRatedCount = 3;

    [Inject]
    private IDbContextFactory<PositiveNewsDbContext> DbContextFactory { get; set; } = default!;

    private string _searchQuery = string.Empty;

    private string SearchQuery
    {
        get => _searchQuery;
        set => _searchQuery = value;
    }

    private List<NewsStory> Stories { get; set; } = [];

    private List<NewsStory> FilteredStories =>
        Stories
            .Where(s => string.IsNullOrWhiteSpace(SearchQuery) ||
                        s.Headline.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
            .ToList();

    private List<NewsStory> TopRatedStories =>
        Stories.OrderByDescending(s => s.Score).Take(TopRatedCount).ToList();

    protected override async Task OnInitializedAsync()
    {
        await using var db = await DbContextFactory.CreateDbContextAsync();

        // The most recently completed run, regardless of date — there's no scheduler yet
        // (Phase 7), so "today's stories" would be blank most of the time otherwise.
        var latestCompletedRun = await db.PipelineRuns
            .AsNoTracking()
            .Where(r => r.Status == PipelineRunStatus.Completed)
            .OrderByDescending(r => r.RunDate)
            .FirstOrDefaultAsync();

        if (latestCompletedRun is null)
        {
            return;
        }

        Stories = await db.NewsStories
            .AsNoTracking()
            .Where(s => s.PipelineRunId == latestCompletedRun.Id)
            .OrderByDescending(s => s.Score)
            .ToListAsync();
    }
}
