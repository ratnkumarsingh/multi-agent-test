using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using PositiveNews.Core.Data;
using PositiveNews.Core.Entities;

namespace PositiveNews.Web.Components.Pages;

public partial class Story
{
    [Parameter]
    public int Id { get; set; }

    [Inject]
    private IDbContextFactory<PositiveNewsDbContext> DbContextFactory { get; set; } = default!;

    private NewsStory? CurrentStory { get; set; }

    private string PublishedLabel =>
        CurrentStory?.PublishedAt is { } p ? p.ToString("MMM d, yyyy") : "Date unknown";

    protected override async Task OnParametersSetAsync()
    {
        await using var db = await DbContextFactory.CreateDbContextAsync();
        CurrentStory = await db.NewsStories.AsNoTracking().FirstOrDefaultAsync(s => s.Id == Id);
    }
}
