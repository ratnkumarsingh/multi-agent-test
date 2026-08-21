// AgentSchemaGuard — a Claude Code PreToolUse hook written in .NET.
//
// Deterministic enforcement (a hook) beats an advisory CLAUDE.md note for something that
// should never be skipped silently — see root CLAUDE.md's "Agent convention": every agent
// owns a JSON Schema constant and calls AnthropicClient.CallToolAsync with forceTool: true.
// This hook warns ("ask", never "deny" — it's a nudge, not a hard block; forceTool: false
// is legitimate for the rare Claude-decides case) when a new/edited file under
// PositiveNews.Agents/Agents/ looks like an agent definition but is missing either marker.
//
// Matched on Write, Edit, AND Bash (a hook scoped only to Write/Edit is bypassable via a
// shell redirect into the same path — see CLAUDE.md's Claude Code conventions section).
//
// Hook contract (PreToolUse), the parts we use:
//   INPUT  (stdin):  { "hook_event_name": "PreToolUse",
//                      "tool_name": "Write" | "Edit" | "Bash",
//                      "tool_input": { "file_path": "...", "content"/"new_string": "...", "command": "..." } }
//   OUTPUT (stdout): { "hookSpecificOutput": {
//                        "hookEventName": "PreToolUse",
//                        "permissionDecision": "allow" | "deny" | "ask",
//                        "permissionDecisionReason": "..." } }
//   Exit code 0 = handled normally.

using System.Text.Json;
using System.Text.Json.Nodes;

string raw = await Console.In.ReadToEndAsync();

string decision = "allow";
string reason = "No agent-convention concerns detected.";

try
{
    JsonNode? root = JsonNode.Parse(raw);
    string? toolName = root?["tool_name"]?.GetValue<string>();
    JsonNode? toolInput = root?["tool_input"];

    if (toolName is "Write" or "Edit")
    {
        string? filePath = toolInput?["file_path"]?.GetValue<string>();

        if (IsAgentFile(filePath))
        {
            string content = toolName == "Write"
                ? GetString(toolInput, "content")
                : GetString(toolInput, "new_string");

            // Only judge content that looks like a full/near-full agent class definition —
            // a small tweak to an already-compliant file shouldn't spuriously warn just
            // because the edited fragment alone doesn't contain every marker.
            if (LooksLikeAgentDefinition(content))
            {
                bool hasSchema = content.Contains("JsonElement") &&
                    (content.Contains("Schema") || content.Contains("InputSchema"));
                bool hasForcedTool = content.Contains("CallToolAsync") &&
                    (content.Contains("forceTool: true") || content.Contains("forceTool:true") ||
                     !content.Contains("forceTool"));  // forceTool defaults to true if omitted

                if (!hasSchema || !hasForcedTool)
                {
                    decision = "ask";
                    string missing = string.Join(" and ", new[]
                    {
                        !hasSchema ? "a JSON Schema constant" : null,
                        !hasForcedTool ? "a forced tool_choice CallToolAsync call" : null,
                    }.Where(s => s is not null));

                    reason = $"AgentSchemaGuard: this looks like an IAgent<TIn,TOut> implementation " +
                        $"under PositiveNews.Agents/Agents/ but {missing} wasn't found. Every agent " +
                        "should own a Schema + forced tool_choice call (CLAUDE.md's Agent convention). " +
                        "Proceed if this is intentional — e.g. a deliberately non-forced, Claude-decides " +
                        "agent (rare; see CLAUDE.md's note on when forceTool: false is appropriate).";
                }
            }
        }
    }
    else if (toolName == "Bash")
    {
        string command = GetString(toolInput, "command");
        if (TargetsAgentFileViaShell(command))
        {
            decision = "ask";
            reason = "AgentSchemaGuard: this shell command appears to write into " +
                "PositiveNews.Agents/Agents/ directly, bypassing Write/Edit — this hook can't verify " +
                "the JSON Schema/forced tool_choice convention for shell-based writes. Proceed if " +
                "you've checked the result manually.";
        }
    }
}
catch (JsonException)
{
    // Malformed payload: don't block the user, just allow and move on.
}

var output = new JsonObject
{
    ["hookSpecificOutput"] = new JsonObject
    {
        ["hookEventName"] = "PreToolUse",
        ["permissionDecision"] = decision,
        ["permissionDecisionReason"] = reason,
    },
};

Console.WriteLine(output.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
return 0;

static string GetString(JsonNode? toolInput, string property) =>
    toolInput?[property]?.GetValue<string>() ?? string.Empty;

static bool IsAgentFile(string? filePath)
{
    if (string.IsNullOrEmpty(filePath))
    {
        return false;
    }

    string normalized = filePath.Replace('\\', '/');
    return normalized.Contains("PositiveNews.Agents/Agents/") && normalized.EndsWith(".cs");
}

static bool LooksLikeAgentDefinition(string content) =>
    content.Contains(": IAgent<") || (content.Contains("class ") && content.Contains("RunAsync"));

static bool TargetsAgentFileViaShell(string command)
{
    if (string.IsNullOrEmpty(command))
    {
        return false;
    }

    bool targetsAgentsDir = command.Replace('\\', '/').Contains("PositiveNews.Agents/Agents/");
    if (!targetsAgentsDir)
    {
        return false;
    }

    string[] writeIndicators = [">", ">>", "New-Item", "Set-Content", "Add-Content", "tee "];
    return writeIndicators.Any(indicator => command.Contains(indicator));
}
