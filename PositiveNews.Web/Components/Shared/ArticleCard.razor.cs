using Microsoft.AspNetCore.Components;
using PositiveNews.Web.Models.Placeholder;

namespace PositiveNews.Web.Components.Shared;

public partial class ArticleCard
{
    [Parameter, EditorRequired]
    public PlaceholderStory Story { get; set; } = null!;

    private string CommentLabel =>
        Story.CommentCount == 1 ? "1 Comment" : $"{Story.CommentCount} Comments";
}
