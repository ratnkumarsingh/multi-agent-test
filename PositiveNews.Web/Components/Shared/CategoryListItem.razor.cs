using Microsoft.AspNetCore.Components;
using PositiveNews.Web.Models.Placeholder;

namespace PositiveNews.Web.Components.Shared;

public partial class CategoryListItem
{
    [Parameter, EditorRequired]
    public PlaceholderCategory Category { get; set; } = null!;

    [Parameter]
    public string Href { get; set; } = "#";
}
