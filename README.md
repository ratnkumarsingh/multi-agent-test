# PositiveNews (multi-agent-test)

A daily "positive/happy news" website, built as a deliberate vehicle to learn agentic
architecture, Claude Code workflows, prompt engineering & structured output, MCP tool
design, and multi-agent orchestration. Sibling to `DotNetTest` in this workspace — see
`../DotNetTest/README.md` for how the `.claude/` pieces (skills, hooks, subagents, MCP
servers) fit together in general. See `CLAUDE.md` for this project's own conventions.

## Layout

- `PositiveNews.Cli` — fast console harness for iterating on agents without the web UI.
- `PositiveNews.Agents` — Anthropic client, `IAgent<TIn,TOut>` agents, orchestrator (later
  phases), MCP client wiring (`Mcp/NewsSearchMcpClient.cs`).
- `PositiveNews.McpServer` — custom MCP server exposing a `SearchNews` tool over stdio,
  backed by NewsAPI.org (`INewsSearchClient`, one-file provider swap).
- `PositiveNews.Core`, `PositiveNews.Web` — added in later phases.
- `.claude/agents/multi-agent-reviewer.md`, `.claude/agents/schema-reviewer.md` — starter
  subagents for reviewing C# changes and JSON Schemas/MCP tool descriptions.
- `.claude/skills/` — empty; add a skill here once there's a concrete
  scaffolding or automation task for this project.
- `.claude/settings.json` — empty settings, ready to wire up hooks.
- `hooks/` — empty; reserved for future hook executables.
- `.mcp.json` — registers `PositiveNews.McpServer` so it's directly pokeable from a
  Claude Code session during development.

## Status

Phase 2 complete and verified live (custom MCP server + client, both a direct
tool call and a Claude `tool_choice: auto` round trip against real NewsAPI.org
data). Next: Phase 3 (multi-agent orchestration pipeline). See the phase-wise
plan (`C:\Users\Ratnesh\.claude\plans\can-you-create-a-structured-rabin.md`)
for the full roadmap.

## Run

```
dotnet user-secrets set "Anthropic:ApiKey" <key> --project PositiveNews.Cli
dotnet user-secrets set "NewsApi:ApiKey" <key> --project PositiveNews.McpServer

# Headline ideas (forced tool_choice, no external I/O)
dotnet run --project PositiveNews.Cli -- headline "topic to brainstorm about"

# Direct MCP tool call, bypassing Claude entirely
dotnet run --project PositiveNews.Cli -- mcp-direct "search query"

# Claude decides (tool_choice: auto) whether to call SearchNews
dotnet run --project PositiveNews.Cli -- mcp-auto "find one uplifting recent news story"
```
