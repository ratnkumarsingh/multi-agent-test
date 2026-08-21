using Microsoft.AspNetCore.Components;

namespace PositiveNews.Web.Components.Shared;

public partial class TagChip
{
    [Parameter, EditorRequired]
    public string Text { get; set; } = string.Empty;

    [Parameter]
    public string Href { get; set; } = "#";
}
