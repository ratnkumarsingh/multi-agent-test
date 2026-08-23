using System.Net;
using System.ServiceModel.Syndication;
using System.Xml;

namespace PositiveNews.McpServer;

/// <summary>
/// <see cref="INewsSearchClient"/> backed by a single public RSS/Atom feed (e.g. Phys.org,
/// The Conversation, BBC News, or one of several Hindi outlets). Unlike
/// <see cref="NewsApiOrgClient"/>'s "everything" search endpoint, an RSS feed isn't itself
/// queryable — it's just "whatever the source most recently published."
/// <see cref="SearchAsync"/> approximates a search by fetching the feed and keyword-matching
/// <paramref name="query"/> against each item's title/summary client-side; a blank query
/// just returns the most recent items.
/// </summary>
public sealed class RssNewsSearchClient : INewsSearchClient
{
    private const int MaxAllowed = 25;

    private readonly HttpClient _http;
    private readonly string _feedUrl;
    private readonly string _sourceName;
    private readonly string _locale;
    private readonly bool _matchQuery;

    /// <param name="matchQuery">
    /// When false, the query is ignored entirely and the most recent items are always
    /// returned — used for the Hindi sources, since SearchAgent only ever plans
    /// English-language queries and literal substring-matching those against Devanagari
    /// text would essentially never hit. English-language feeds keep real keyword matching.
    /// </param>
    public RssNewsSearchClient(HttpClient http, string feedUrl, string sourceName, string locale, bool matchQuery = true)
    {
        _http = http;
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            // A handful of these outlets (TV9, Jansatta) return 403 to a bare custom UA but
            // serve normally to something that looks like a real browser.
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120 Safari/537.36 PositiveNews.McpServer/1.0");
        }
        _feedUrl = feedUrl;
        _sourceName = sourceName;
        _locale = locale;
        _matchQuery = matchQuery;
    }

    public bool IsQueryInvariant => !_matchQuery;

    public async Task<NewsSearchResponse> SearchAsync(string query, int max, CancellationToken ct)
    {
        var take = Math.Clamp(max, 1, MaxAllowed);

        SyndicationFeed feed;
        try
        {
            using var stream = await _http.GetStreamAsync(_feedUrl, ct);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
            feed = SyndicationFeed.Load(reader)
                ?? throw new InvalidOperationException("Feed parsed to null.");
        }
        catch (HttpRequestException ex)
        {
            return NewsSearchResponse.Failure(
                NewsSearchErrorCategory.UpstreamUnavailable, retryable: true,
                $"Network error fetching {_sourceName} RSS feed: {ex.Message}");
        }
        catch (WebException ex)
        {
            return NewsSearchResponse.Failure(
                NewsSearchErrorCategory.UpstreamUnavailable, retryable: true,
                $"Network error fetching {_sourceName} RSS feed: {ex.Message}");
        }
        catch (XmlException ex)
        {
            return NewsSearchResponse.Failure(
                NewsSearchErrorCategory.UpstreamUnavailable, retryable: true,
                $"{_sourceName} RSS feed returned unparseable XML: {ex.Message}");
        }

        var keywords = _matchQuery
            ? query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
            : [];

        var matches = feed.Items
            .Where(item => keywords.Count == 0 || MatchesAnyKeyword(item, keywords))
            .Select(ToArticle)
            .ToList();

        return new NewsSearchResponse(matches.Take(take).ToList(), matches.Count, Error: null);
    }

    private static bool MatchesAnyKeyword(SyndicationItem item, List<string> keywords)
    {
        // Atom feeds commonly carry the body in <content> rather than <summary> (e.g. The
        // Conversation) — check both, or matching silently degrades to title-only.
        var haystack = $"{item.Title?.Text} {item.Summary?.Text} {(item.Content as TextSyndicationContent)?.Text}";
        return keywords.Any(k => haystack.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private NewsArticle ToArticle(SyndicationItem item)
    {
        var link = item.Links.FirstOrDefault(l => l.RelationshipType is null or "alternate")?.Uri.ToString()
            ?? item.Links.FirstOrDefault()?.Uri.ToString()
            ?? item.Id;

        var imageUrl = item.Links
            .FirstOrDefault(l => l.RelationshipType == "enclosure" && l.MediaType?.StartsWith("image/") == true)
            ?.Uri.ToString();

        return new NewsArticle(
            item.Title?.Text ?? "(untitled)",
            link,
            _sourceName,
            item.PublishDate == default ? null : item.PublishDate,
            item.Summary?.Text ?? Snippetize((item.Content as TextSyndicationContent)?.Text),
            imageUrl,
            _locale);
    }

    // <content> on a feed like The Conversation's is the full HTML article body — strip tags
    // and cap it to roughly what NewsAPI's own "description" field already gives the rest of
    // the pipeline, rather than dumping a full article's HTML into SearchAgent/ScorerAgent.
    private static string? Snippetize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
        return text.Length > 300 ? text[..300] + "…" : text;
    }
}
