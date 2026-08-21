using System.Reflection;
using Microsoft.Extensions.Configuration;
using PositiveNews.Agents;
using PositiveNews.Agents.Agents;

var config = new ConfigurationBuilder()
    .AddUserSecrets(Assembly.GetExecutingAssembly())
    .AddEnvironmentVariables()
    .Build();

var options = new AnthropicOptions();
config.GetSection(AnthropicOptions.SectionName).Bind(options);
// Fall back to a bare ANTHROPIC_API_KEY env var (direct Anthropic API) if
// "Anthropic:ApiKey" wasn't set via user-secrets/config.
options.ApiKey ??= config["ANTHROPIC_API_KEY"];

if (string.IsNullOrWhiteSpace(options.ApiKey))
{
    Console.Error.WriteLine(
        "Missing Anthropic API key. Set it via:\n" +
        "  dotnet user-secrets set \"Anthropic:ApiKey\" <key> --project PositiveNews.Cli\n" +
        "(add \"Anthropic:BaseUrl\"/\"Anthropic:UseBearerAuth\" the same way to route through a\n" +
        "gateway instead of Anthropic directly) or set ANTHROPIC_API_KEY as an environment variable.");
    return 1;
}

using var http = new HttpClient();
var client = new AnthropicClient(http, options);
var agent = new HeadlineIdeaAgent(client);

var topic = args.Length > 0 ? string.Join(' ', args) : "renewable energy breakthroughs";
Console.WriteLine($"Asking HeadlineIdeaAgent about: {topic}\n");

var ideas = await agent.RunAsync(topic);
foreach (var idea in ideas)
{
    Console.WriteLine($"- {idea.Title}");
    Console.WriteLine($"  {idea.OneLineSummary}\n");
}

return 0;
