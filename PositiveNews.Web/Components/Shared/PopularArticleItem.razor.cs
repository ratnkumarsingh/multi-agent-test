using Microsoft.AspNetCore.Components;
using PositiveNews.Web.Models.Placeholder;

namespace PositiveNews.Web.Components.Shared;

public partial class PopularArticleItem
{
    [Parameter, EditorRequired]
    public PlaceholderPopularStory Story { get; set; } = null!;
}
