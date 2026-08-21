using Microsoft.AspNetCore.Components;

namespace PositiveNews.Web.Components.Shared;

public partial class SearchBox
{
    [Parameter]
    public string Query { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<string> QueryChanged { get; set; }

    [Parameter]
    public string Placeholder { get; set; } = "Type a keyword and hit enter";

    private Task HandleInput(ChangeEventArgs e)
    {
        var value = e.Value?.ToString() ?? string.Empty;
        return QueryChanged.InvokeAsync(value);
    }
}
