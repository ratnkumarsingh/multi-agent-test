using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using PositiveNews.Agents.Agents;
using PositiveNews.Core.Data;
using PositiveNews.Core.Entities;

namespace PositiveNews.Web.Components.Pages;

public partial class Story
{
    private const string TargetLocale = "hi";
    private const string TargetLanguageName = "Hindi";

    [Parameter]
    public int Id { get; set; }

    [Inject]
    private IDbContextFactory<PositiveNewsDbContext> DbContextFactory { get; set; } = default!;

    // Resolved lazily (not [Inject]-ed directly) so a missing/invalid Anthropic API key
    // only ever surfaces when "Translate" is actually clicked — not on every story page
    // load, which is what a direct TranslationAgent property injection would cause
    // (AnthropicClient's constructor validates the key eagerly).
    [Inject]
    private IServiceProvider Services { get; set; } = default!;

    private NewsStory? CurrentStory { get; set; }
    private StoryTranslation? _translation;
    private bool _showingTranslation;
    private bool _isTranslating;
    private string? _translationError;

    private string PublishedLabel =>
        CurrentStory?.PublishedAt is { } p ? p.ToString("MMM d, yyyy") : "Date unknown";

    private string DisplayHeadline =>
        _showingTranslation && _translation is not null ? _translation.Headline : CurrentStory?.Headline ?? "";

    private string DisplayBody =>
        _showingTranslation && _translation is not null ? _translation.Body : CurrentStory?.Body ?? "";

    protected override async Task OnParametersSetAsync()
    {
        await using var db = await DbContextFactory.CreateDbContextAsync();
        CurrentStory = await db.NewsStories.AsNoTracking().FirstOrDefaultAsync(s => s.Id == Id);

        if (CurrentStory is not null)
        {
            _translation = await db.StoryTranslations
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.NewsStoryId == CurrentStory.Id && t.Locale == TargetLocale);
        }
    }

    private async Task ToggleTranslationAsync()
    {
        if (_showingTranslation)
        {
            _showingTranslation = false;
            return;
        }

        if (_translation is not null)
        {
            _showingTranslation = true;
            return;
        }

        _isTranslating = true;
        _translationError = null;

        try
        {
            var translationAgent = Services.GetRequiredService<TranslationAgent>();
            var request = new TranslationRequest(CurrentStory!.Headline, CurrentStory.Body, TargetLanguageName);
            var result = await translationAgent.RunAsync(request);

            var row = new StoryTranslation
            {
                NewsStoryId = CurrentStory.Id,
                Locale = TargetLocale,
                Headline = result.Headline,
                Body = result.Body,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            await using var db = await DbContextFactory.CreateDbContextAsync();
            db.StoryTranslations.Add(row);
            await db.SaveChangesAsync();

            _translation = row;
            _showingTranslation = true;
        }
        catch (Exception ex)
        {
            _translationError = $"Translation failed — please try again. ({ex.Message})";
        }
        finally
        {
            _isTranslating = false;
        }
    }
}
