using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PositiveNews.McpServer;
using PositiveNews.McpServer.Tools;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .AddUserSecrets(Assembly.GetExecutingAssembly())
    .AddEnvironmentVariables();

// stdout is reserved for MCP protocol messages — all logs go to stderr.
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

var newsApiOptions = new NewsApiOptions();
builder.Configuration.GetSection(NewsApiOptions.SectionName).Bind(newsApiOptions);
newsApiOptions.ApiKey ??= builder.Configuration["NEWSAPI_KEY"];
builder.Services.AddSingleton(newsApiOptions);

builder.Services.AddHttpClient<NewsApiOrgClient>();
builder.Services.AddHttpClient(); // enables IHttpClientFactory.CreateClient() for the RSS sources below

// Public RSS/Atom feeds with no API key required, in addition to NewsAPI.org — see
// RssNewsSearchClient's doc comment for how "search" is approximated over a feed. AP News
// was considered too (per a source-quality list Ratnesh provided) but has no working free
// public RSS feed left to point at, so it's left out rather than wired to a guessed URL.
var englishRssSources = new (string Url, string SourceName)[]
{
    ("https://phys.org/rss-feed/", "Phys.org"),
    ("https://theconversation.com/us/articles.atom", "The Conversation"),
    ("https://feeds.bbci.co.uk/news/rss.xml", "BBC News"),
};

// Hindi outlets, per a source-quality list Ratnesh provided (two rounds of it). Kept
// native — SummarizerAgent writes these stories' final copy in Hindi rather than
// translating to English (Ratnesh's explicit call), so the Home feed is genuinely
// mixed-language, not just more diverse English candidates. `matchQuery: false` because
// SearchAgent only ever plans English-language queries. Every URL below was individually
// verified with real content before being hardcoded — several plausible guesses for other
// major Hindi outlets (Hindustan, Dainik Jagran, Navbharat Times, ABP News, Business
// Standard Hindi, Zee News, The Quint) turned up nothing real (404s, dead subdomains) or
// were bot-blocked (Dainik Bhaskar's bhaskarhindi.com mirror, The Better India's Hindi
// site both 403'd regardless of User-Agent) and were left out rather than guessed.
var hindiRssSources = new (string Url, string SourceName)[]
{
    ("https://feeds.bbci.co.uk/hindi/rss.xml", "BBC Hindi"),
    ("https://www.amarujala.com/rss/breaking-news.xml", "Amar Ujala"),
    ("https://www.bhaskar.com/rss-v1--category-1061.xml", "Dainik Bhaskar"),
    ("https://feeds.feedburner.com/ndtvkhabar-latest", "NDTV Khabar"),
    ("https://hindi.news18.com/rss/khabar/nation/nation.xml", "News18 Hindi"),
    ("https://www.vishvasnews.com/feed/", "Vishvas News"),
    ("https://hindi.oneindia.com/rss/hindi-news-fb.xml", "Oneindia Hindi"),
    ("https://tv9hindi.com/feed", "TV9 Bharatvarsh"),
    ("https://www.jansatta.com/feed/", "Jansatta"),
    ("https://www.aajtak.in/rssfeeds/?id=home", "Aaj Tak"),
};

builder.Services.AddSingleton<INewsSearchClient>(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var logger = sp.GetRequiredService<ILogger<AggregateNewsSearchClient>>();

    var sources = new List<INewsSearchClient> { sp.GetRequiredService<NewsApiOrgClient>() };
    sources.AddRange(englishRssSources.Select(s =>
        new RssNewsSearchClient(httpClientFactory.CreateClient(), s.Url, s.SourceName, locale: "en")));
    sources.AddRange(hindiRssSources.Select(s =>
        new RssNewsSearchClient(httpClientFactory.CreateClient(), s.Url, s.SourceName, locale: "hi", matchQuery: false)));

    return new AggregateNewsSearchClient(sources, logger);
});

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<NewsSearchTools>();

await builder.Build().RunAsync();
