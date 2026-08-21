using Microsoft.AspNetCore.Components;

namespace PositiveNews.Web.Components.Shared;

public partial class StoryAvatar
{
    [Parameter, EditorRequired]
    public string Gradient { get; set; } = string.Empty;

    [Parameter]
    public StoryAvatarSize Size { get; set; } = StoryAvatarSize.Large;
}
