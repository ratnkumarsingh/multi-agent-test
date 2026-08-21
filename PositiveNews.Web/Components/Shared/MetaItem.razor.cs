using Microsoft.AspNetCore.Components;

namespace PositiveNews.Web.Components.Shared;

public partial class MetaItem
{
    [Parameter, EditorRequired]
    public IconKind Kind { get; set; }

    [Parameter, EditorRequired]
    public string Text { get; set; } = string.Empty;
}
