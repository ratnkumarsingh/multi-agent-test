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
var rssSources = new (string Url, string SourceName)[]
{
    ("https://phys.org/rss-feed/", "Phys.org"),
    ("https://theconversation.com/us/articles.atom", "The Conversation"),
    ("https://feeds.bbci.co.uk/news/rss.xml", "BBC News"),
};

builder.Services.AddSingleton<INewsSearchClient>(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var logger = sp.GetRequiredService<ILogger<AggregateNewsSearchClient>>();

    var sources = new List<INewsSearchClient> { sp.GetRequiredService<NewsApiOrgClient>() };
    sources.AddRange(rssSources.Select(s => new RssNewsSearchClient(httpClientFactory.CreateClient(), s.Url, s.SourceName)));

    return new AggregateNewsSearchClient(sources, logger);
});

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<NewsSearchTools>();

await builder.Build().RunAsync();
