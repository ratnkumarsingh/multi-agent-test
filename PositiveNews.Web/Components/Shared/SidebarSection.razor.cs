using Microsoft.AspNetCore.Components;

namespace PositiveNews.Web.Components.Shared;

public partial class SidebarSection
{
    [Parameter, EditorRequired]
    public string Heading { get; set; } = string.Empty;

    [Parameter, EditorRequired]
    public RenderFragment? ChildContent { get; set; }
}
