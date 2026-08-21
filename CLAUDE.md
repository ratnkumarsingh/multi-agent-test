# PositiveNews

A daily "positive/happy news" website, built as a deliberate vehicle to learn: agentic
architecture & orchestration, Claude Code configuration & workflows, prompt engineering &
structured output, tool design & MCP integration, context management & reliability, and
multi-agent coordination. See `.claude/plans/` history (or ask) for the full phase-wise plan.

Sibling to `../DotNetTest`, which demonstrates the four Claude Code extension types
(skill, hook, subagent, MCP server) in .NET as reference material. This repo builds real
runtime logic on top of those patterns.

## Two agent notions — kept distinct

- **Dev-time Claude Code tooling** (`.claude/agents/`, `.claude/skills/`, `hooks/`,
  this file, `.mcp.json`): used *while building* this repo with Claude Code.
- **Runtime app agents** (`PositiveNews.Agents/Agents/*.cs`): plain C# classes that run
  inside the pipeline to produce news content. They have nothing to do with Claude
  Code's own subagent feature — don't conflate the two.

## Project layout

| Project | Purpose |
|---|---|
| `PositiveNews.Cli` | Fast console harness for iterating on agents/orchestrator without the web UI. |
| `PositiveNews.Agents` | Anthropic HTTP client, `IAgent<TIn,TOut>` pattern, concrete agents, orchestrator, MCP client wiring. |
| `PositiveNews.McpServer` | Custom MCP server exposing news/video search tools. |
| `PositiveNews.Core` | Domain entities + EF Core `DbContext` + migrations. |
| `PositiveNews.Web` | Blazor Web App (Interactive Server) — UI, daily scheduler, live progress. |

## Agent convention

Every agent implements `IAgent<TIn, TOut>` (`PositiveNews.Agents/IAgent.cs`) and internally:

1. Owns one JSON Schema constant (`additionalProperties: false`, every field `required`
   unless it's genuinely optional — see nullable-field note below).
2. Owns one system prompt describing its narrow job.
3. Calls `AnthropicClient.CallToolAsync(...)` with `forceTool: true`, so Claude always
   answers via a single `tool_use` block instead of prose. Non-forced (`forceTool: false`,
   i.e. `tool_choice: auto`) is used only where Claude should genuinely *decide* whether
   to call a tool at all (e.g. an MCP search tool) — don't default to it.

See `PositiveNews.Agents/Agents/HeadlineIdeaAgent.cs` for the reference shape.

**Why forced tool use, not prose + regex/markdown-stripping/few-shot:** it's the only
mechanism that structurally guarantees schema-valid output regardless of temperature or
wording. Prompt tricks reduce malformed output; they never eliminate it.

**Schema conventions:**
- Fields that can legitimately be absent should be typed nullable (e.g.
  `["integer","null"]`) with the model told to emit real `null` — never coerce to `0`
  or `"N/A"` in the prompt.
- Any per-call dynamic content (ids, timestamps, the specific topic/article being
  processed) belongs at the *end* of the user message, not the start — keeps a static
  system-prompt prefix cache-friendly once prompt caching is added.
- Numeric/structured values that downstream code does exact arithmetic or comparison on
  must stay structured fields — never summarized into prose.

## MCP tool conventions (from Phase 2 onward)

- Cap payload size structurally at the server (max results, field selection, row caps) —
  never rely on a prompt instruction to self-limit.
- Errors are structured (`isError`/category/retryability), never a bare string sharing
  the success content channel.
- Tool names must accurately reflect replace-vs-append semantics.
- The MCP server is also registered in `.mcp.json` so it's directly pokeable from a
  Claude Code session during development, independent of the app's own agents.

## Secrets

`AnthropicClient` is configured from `AnthropicOptions` (bound from the `"Anthropic"`
config section), read via `Microsoft.Extensions.Configuration` from user-secrets:

```
dotnet user-secrets set "Anthropic:ApiKey" <key> --project PositiveNews.Cli
```

with a bare `ANTHROPIC_API_KEY` environment variable as a fallback. Never hardcoded,
never committed to any `appsettings*.json`.

`AnthropicOptions` also supports `BaseUrl` + `UseBearerAuth`, mirroring the pattern used
in the sibling `Aviral_Maths` project (`Aviral_Maths.AI/ClaudeOptions.cs`) — set those two
(via the same `dotnet user-secrets set "Anthropic:BaseUrl" ...` form) to route through an
Anthropic-compatible gateway instead of `api.anthropic.com` directly, if you want to. Leave
them unset to talk to Anthropic directly (the default).

## Reliability posture

Each pipeline stage (search, score, summarize, translate, ...) is its own agent call
rather than one long-lived conversation thread — this sidesteps context dilution (a
long session's early instructions losing effective salience even below the token limit)
by construction, not by periodic resets. Once persistence lands (Phase 4), pipeline runs
are idempotent per calendar day and each step is traced for inspection/resumability.

## Verification loop

`PositiveNews.Cli` is the fast iteration path — prefer `dotnet run --project
PositiveNews.Cli -- <args>` over the Blazor UI while developing agent logic.
