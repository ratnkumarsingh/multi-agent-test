namespace PositiveNews.Core.Entities;

/// <summary>
/// A cached on-demand translation of one <see cref="NewsStory"/> into one locale — a
/// story is translated once (the first "Translate" click), not on every view. See the
/// unique index on <c>(NewsStoryId, Locale)</c> in <c>PositiveNewsDbContext</c>.
/// </summary>
public sealed class StoryTranslation
{
    public int Id { get; set; }

    public int NewsStoryId { get; set; }
    public NewsStory? Story { get; set; }

    /// <summary>e.g. "hi" for Hindi.</summary>
    public string Locale { get; set; } = "";

    public string Headline { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
