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

builder.Services.AddHttpClient<INewsSearchClient, NewsApiOrgClient>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<NewsSearchTools>();

await builder.Build().RunAsync();
