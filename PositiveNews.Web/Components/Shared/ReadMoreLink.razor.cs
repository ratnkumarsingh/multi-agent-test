using Microsoft.AspNetCore.Components;

namespace PositiveNews.Web.Components.Shared;

public partial class ReadMoreLink
{
    [Parameter]
    public string Href { get; set; } = "#";
}
