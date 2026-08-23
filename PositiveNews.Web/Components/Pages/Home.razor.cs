using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using PositiveNews.Core.Data;
using PositiveNews.Core.Entities;

namespace PositiveNews.Web.Components.Pages;

public partial class Home : IAsyncDisposable
{
    private const int TopRatedCount = 3;
    private const int PageSize = 10;

    [Inject]
    private IDbContextFactory<PositiveNewsDbContext> DbContextFactory { get; set; } = default!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    private string _searchQuery = string.Empty;
    private bool _hasMore = true;
    private bool _isLoadingMore;
    private ElementReference _sentinel;
    private IJSObjectReference? _module;
    private IJSObjectReference? _observer;
    private DotNetObjectReference<Home>? _dotNetRef;

    private string SearchQuery
    {
        get => _searchQuery;
        set => _searchQuery = value;
    }

    private List<NewsStory> Stories { get; set; } = [];

    private List<NewsStory> TopRatedStories { get; set; } = [];

    private List<NewsStory> FilteredStories =>
        Stories
            .Where(s => string.IsNullOrWhiteSpace(SearchQuery) ||
                        s.Headline.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
            .ToList();

    protected override async Task OnInitializedAsync()
    {
        Stories = await LoadStoriesPageAsync(skip: 0, take: PageSize);
        _hasMore = Stories.Count == PageSize;

        // Top Rated stays scoped to just the latest run (not the full scrolling history
        // below), so it reflects "today's best" rather than drifting toward old stories
        // that happen to have a high score.
        await using var db = await DbContextFactory.CreateDbContextAsync();
        var latestCompletedRun = await db.PipelineRuns
            .AsNoTracking()
            .Where(r => r.Status == PipelineRunStatus.Completed)
            .OrderByDescending(r => r.RunDate)
            .FirstOrDefaultAsync();

        if (latestCompletedRun is not null)
        {
            TopRatedStories = await db.NewsStories
                .AsNoTracking()
                .Where(s => s.PipelineRunId == latestCompletedRun.Id)
                .OrderByDescending(s => s.Score)
                .Take(TopRatedCount)
                .ToListAsync();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && _hasMore)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            _module = await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/infiniteScroll.js");
            _observer = await _module.InvokeAsync<IJSObjectReference>(
                "observe", _dotNetRef, nameof(LoadMoreAsync), _sentinel);
        }
    }

    [JSInvokable]
    public async Task LoadMoreAsync()
    {
        if (_isLoadingMore || !_hasMore)
        {
            return;
        }

        _isLoadingMore = true;
        StateHasChanged();

        var nextPage = await LoadStoriesPageAsync(skip: Stories.Count, take: PageSize);
        Stories.AddRange(nextPage);
        _hasMore = nextPage.Count == PageSize;
        _isLoadingMore = false;
        StateHasChanged();
    }

    // Pages across every Completed run's stories, most recent run first (via PipelineRunId,
    // which is monotonically increasing with run creation — avoids ordering on a
    // DateTimeOffset column, which SQLite/EF Core can't translate server-side), then by
    // score within a run.
    private async Task<List<NewsStory>> LoadStoriesPageAsync(int skip, int take)
    {
        await using var db = await DbContextFactory.CreateDbContextAsync();

        return await db.NewsStories
            .AsNoTracking()
            .Where(s => db.PipelineRuns.Any(r => r.Id == s.PipelineRunId && r.Status == PipelineRunStatus.Completed))
            .OrderByDescending(s => s.PipelineRunId)
            .ThenByDescending(s => s.Score)
            .ThenByDescending(s => s.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_observer is not null)
            {
                await _observer.InvokeVoidAsync("disconnect");
                await _observer.DisposeAsync();
            }

            if (_module is not null)
            {
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
            // Circuit already gone (browser closed / navigated away) — nothing to clean up.
        }

        _dotNetRef?.Dispose();
    }
}
