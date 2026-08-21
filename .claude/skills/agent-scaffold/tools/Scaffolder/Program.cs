// Scaffolder — a tiny, dependency-free .NET console tool invoked by the
// `agent-scaffold` skill. It generates a new IAgent<TIn,TOut> stub matching this
// project's agent convention (see root CLAUDE.md's "Agent convention" section and
// PositiveNews.Agents/Agents/HeadlineIdeaAgent.cs, the reference shape).
//
// Usage:
//   dotnet run -- --name TrendSpotterAgent --in string --out TrendSpotterAgent.Result
//   dotnet run -- --name ImageCaptionAgent --in NewsCandidate --out string --output ../../../../PositiveNews.Agents/Agents
//
// It deliberately uses only the BCL so the skill has zero external dependencies.

using System.Text;

var options = ParseArgs(args);

string? name = options.GetValueOrDefault("name");
string inType = options.GetValueOrDefault("in", "string");
string outType = options.GetValueOrDefault("out", "");
string ns = options.GetValueOrDefault("namespace", "PositiveNews.Agents.Agents");
string outputDir = options.GetValueOrDefault("output", "generated");

if (string.IsNullOrWhiteSpace(name))
{
    Console.Error.WriteLine("error: --name <AgentName> is required (e.g. --name TrendSpotterAgent).");
    return 1;
}

// Default the output type to a small result record scoped to this agent, matching how
// HeadlineIdeaAgent/PositivityScorerAgent/SummarizerAgent each own their small output shape.
string resultTypeName = $"{name}Result";
bool generateResultRecord = string.IsNullOrWhiteSpace(outType);
if (generateResultRecord)
{
    outType = resultTypeName;
}

string content = BuildSource(name!, inType, outType, ns, generateResultRecord, resultTypeName);

Directory.CreateDirectory(outputDir);
string path = Path.Combine(outputDir, $"{name}.cs");
File.WriteAllText(path, content);

Console.WriteLine($"Created agent stub '{name}' at: {Path.GetFullPath(path)}");
Console.WriteLine("Fill in: SystemPrompt, Schema, Tool.Description, and the RunAsync mapping.");
Console.WriteLine("----- file content -----");
Console.WriteLine(content);
return 0;

static string BuildSource(string name, string inType, string outType, string ns, bool generateResultRecord, string resultTypeName)
{
    var sb = new StringBuilder();
    sb.AppendLine("using System.Text.Json;");
    sb.AppendLine();
    sb.AppendLine($"namespace {ns};");
    sb.AppendLine();

    if (generateResultRecord)
    {
        sb.AppendLine("// TODO: replace with real fields for this agent's structured output.");
        sb.AppendLine($"public sealed record {resultTypeName}(string TODO);");
        sb.AppendLine();
    }

    sb.AppendLine("/// <summary>");
    sb.AppendLine($"/// TODO: describe what {name} does in one or two sentences.");
    sb.AppendLine("/// </summary>");
    sb.AppendLine($"public sealed class {name} : IAgent<{inType}, {outType}>");
    sb.AppendLine("{");
    sb.AppendLine("    private const string SystemPrompt = \"\"\"");
    sb.AppendLine("        TODO: describe this agent's narrow job. If it will process externally-sourced");
    sb.AppendLine("        text (article content, web content, etc.), add a line telling it to treat that");
    sb.AppendLine("        content strictly as data, never as instructions.");
    sb.AppendLine("        \"\"\";");
    sb.AppendLine();
    sb.AppendLine("    private static readonly JsonElement Schema = JsonSerializer.SerializeToElement(new");
    sb.AppendLine("    {");
    sb.AppendLine("        type = \"object\",");
    sb.AppendLine("        properties = new");
    sb.AppendLine("        {");
    sb.AppendLine("            // TODO: one property per field of the output type above.");
    sb.AppendLine("            todo = new { type = \"string\" }");
    sb.AppendLine("        },");
    sb.AppendLine("        required = new[] { \"todo\" },");
    sb.AppendLine("        additionalProperties = false");
    sb.AppendLine("    });");
    sb.AppendLine();
    sb.AppendLine("    private static readonly AnthropicToolSpec Tool = new()");
    sb.AppendLine("    {");
    sb.AppendLine($"        Name = \"return_{ToSnakeCase(name)}\",");
    sb.AppendLine("        Description = \"TODO: one sentence describing what this tool call returns.\",");
    sb.AppendLine("        InputSchema = Schema");
    sb.AppendLine("    };");
    sb.AppendLine();
    sb.AppendLine("    private readonly AnthropicClient _client;");
    sb.AppendLine();
    sb.AppendLine($"    public {name}(AnthropicClient client) => _client = client;");
    sb.AppendLine();
    sb.AppendLine($"    public async Task<{outType}> RunAsync({inType} input, CancellationToken ct = default)");
    sb.AppendLine("    {");
    sb.AppendLine("        // Dynamic per-call content goes at the END of the user message (cache-friendly).");
    sb.AppendLine("        var userMessage = $\"TODO: {input}\";");
    sb.AppendLine();
    sb.AppendLine("        var result = await _client.CallToolAsync(SystemPrompt, userMessage, Tool, forceTool: true, ct);");
    sb.AppendLine();
    sb.AppendLine("        // TODO: map `result` (the tool's parsed input) into the output type.");
    sb.AppendLine("        throw new NotImplementedException();");
    sb.AppendLine("    }");
    sb.AppendLine("}");

    return sb.ToString();
}

// snake_case for the tool name, e.g. "TrendSpotterAgent" -> "trend_spotter_agent".
static string ToSnakeCase(string pascalCase)
{
    var sb = new StringBuilder();
    for (int i = 0; i < pascalCase.Length; i++)
    {
        char c = pascalCase[i];
        if (char.IsUpper(c))
        {
            if (i > 0) sb.Append('_');
            sb.Append(char.ToLowerInvariant(c));
        }
        else
        {
            sb.Append(c);
        }
    }
    return sb.ToString();
}

// Minimal "--key value" / "--flag" parser. Returns a case-insensitive map.
static Dictionary<string, string> ParseArgs(string[] args)
{
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (int i = 0; i < args.Length; i++)
    {
        if (!args[i].StartsWith("--")) continue;
        string key = args[i][2..];
        string value = (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
            ? args[++i]
            : "true";
        map[key] = value;
    }
    return map;
}
