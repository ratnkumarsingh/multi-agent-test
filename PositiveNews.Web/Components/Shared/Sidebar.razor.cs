using Microsoft.AspNetCore.Components;
using PositiveNews.Core.Entities;

namespace PositiveNews.Web.Components.Shared;

public partial class Sidebar
{
    [Parameter, EditorRequired]
    public IReadOnlyList<NewsStory> TopRatedStories { get; set; } = [];

    [Parameter]
    public string SearchQuery { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<string> SearchQueryChanged { get; set; }

    private Task HandleQueryChanged(string value) => SearchQueryChanged.InvokeAsync(value);
}
