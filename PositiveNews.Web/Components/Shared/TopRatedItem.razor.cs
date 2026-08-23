using Microsoft.AspNetCore.Components;
using PositiveNews.Core.Entities;

namespace PositiveNews.Web.Components.Shared;

public partial class TopRatedItem
{
    [Parameter, EditorRequired]
    public NewsStory Story { get; set; } = null!;

    private string StoryHref => $"/story/{Story.Id}";

    private string PublishedLabel =>
        Story.PublishedAt is { } p ? p.ToString("MMM d, yyyy") : "Date unknown";
}
