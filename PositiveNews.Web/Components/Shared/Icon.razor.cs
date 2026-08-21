using Microsoft.AspNetCore.Components;

namespace PositiveNews.Web.Components.Shared;

public partial class Icon
{
    [Parameter, EditorRequired]
    public IconKind Kind { get; set; }
}
