using Microsoft.AspNetCore.Components;
using PositiveNews.Web.Models.Placeholder;

namespace PositiveNews.Web.Components.Shared;

public partial class Sidebar
{
    [Parameter, EditorRequired]
    public IReadOnlyList<PlaceholderCategory> Categories { get; set; } = [];

    [Parameter, EditorRequired]
    public IReadOnlyList<PlaceholderPopularStory> PopularStories { get; set; } = [];

    [Parameter, EditorRequired]
    public IReadOnlyList<string> Tags { get; set; } = [];

    [Parameter]
    public string SearchQuery { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<string> SearchQueryChanged { get; set; }

    private Task HandleQueryChanged(string value) => SearchQueryChanged.InvokeAsync(value);
}
