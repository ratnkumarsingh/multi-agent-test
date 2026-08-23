using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using PositiveNews.Agents.Agents;
using PositiveNews.Core.Data;
using PositiveNews.Core.Entities;

namespace PositiveNews.Web.Components.Pages;

public partial class Story
{
    // Broad national + international coverage per Ratnesh's request, excluding Arabic,
    // Urdu, and Bengali. Translation is on-demand/cached (not a pipeline-run fan-out), same
    // design as the original Hindi-only button — this list only drives what the switcher
    // offers; a language is only ever actually translated if a viewer picks it.
    private static readonly (string Locale, string Name)[] SupportedLocales =
    [
        ("hi", "Hindi"),
        ("en", "English"),
        ("es", "Spanish"),
        ("fr", "French"),
        ("de", "German"),
        ("pt", "Portuguese"),
        ("ru", "Russian"),
        ("zh", "Chinese"),
        ("ja", "Japanese"),
        ("ko", "Korean"),
        ("it", "Italian"),
        ("id", "Indonesian"),
        ("vi", "Vietnamese"),
        ("th", "Thai"),
        ("tr", "Turkish"),
        ("ta", "Tamil"),
        ("te", "Telugu"),
        ("mr", "Marathi"),
        ("gu", "Gujarati"),
        ("kn", "Kannada"),
        ("ml", "Malayalam"),
        ("pa", "Punjabi"),
        ("or", "Odia"),
        ("as", "Assamese"),
    ];

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
    private Dictionary<string, StoryTranslation> _translations = new();
    private string _selectedLocale = "";
    private bool _showingTranslation;
    private bool _isTranslating;
    private string? _translationError;

    // A story can't be translated into the language it's already natively written in.
    private (string Locale, string Name)[] AvailableLocales =>
        SupportedLocales.Where(l => l.Locale != CurrentStory?.Locale).ToArray();

    private string SelectedLanguageName =>
        SupportedLocales.FirstOrDefault(l => l.Locale == _selectedLocale).Name ?? _selectedLocale;

    private string PublishedLabel =>
        CurrentStory?.PublishedAt is { } p ? p.ToString("MMM d, yyyy") : "Date unknown";

    private string DisplayHeadline =>
        _showingTranslation && _translations.TryGetValue(_selectedLocale, out var t) ? t.Headline : CurrentStory?.Headline ?? "";

    private string DisplayBody =>
        _showingTranslation && _translations.TryGetValue(_selectedLocale, out var t) ? t.Body : CurrentStory?.Body ?? "";

    protected override async Task OnParametersSetAsync()
    {
        await using var db = await DbContextFactory.CreateDbContextAsync();
        CurrentStory = await db.NewsStories.AsNoTracking().FirstOrDefaultAsync(s => s.Id == Id);

        if (CurrentStory is not null)
        {
            var rows = await db.StoryTranslations
                .AsNoTracking()
                .Where(t => t.NewsStoryId == CurrentStory.Id)
                .ToListAsync();
            _translations = rows.ToDictionary(t => t.Locale);

            if (_selectedLocale == "" && AvailableLocales.Length > 0)
            {
                _selectedLocale = AvailableLocales[0].Locale;
            }
        }
    }

    private void OnLocaleChanged(ChangeEventArgs e)
    {
        _selectedLocale = e.Value?.ToString() ?? _selectedLocale;
        // Switching languages mid-view shouldn't keep showing the previous language's text
        // under the newly-selected label — go back to original until "Translate" is clicked.
        _showingTranslation = false;
        _translationError = null;
    }

    private async Task ToggleTranslationAsync()
    {
        if (_showingTranslation)
        {
            _showingTranslation = false;
            return;
        }

        if (_translations.ContainsKey(_selectedLocale))
        {
            _showingTranslation = true;
            return;
        }

        _isTranslating = true;
        _translationError = null;

        try
        {
            var languageName = SupportedLocales.First(l => l.Locale == _selectedLocale).Name;
            var translationAgent = Services.GetRequiredService<TranslationAgent>();
            var request = new TranslationRequest(CurrentStory!.Headline, CurrentStory.Body, languageName);
            var result = await translationAgent.RunAsync(request);

            var row = new StoryTranslation
            {
                NewsStoryId = CurrentStory.Id,
                Locale = _selectedLocale,
                Headline = result.Headline,
                Body = result.Body,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            await using var db = await DbContextFactory.CreateDbContextAsync();
            db.StoryTranslations.Add(row);
            await db.SaveChangesAsync();

            _translations[_selectedLocale] = row;
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
