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
- `PositiveNews.Web` — Blazor Web App (Interactive Server), scaffolded as a shell in
  Phase 3 with hardcoded placeholder data; wired to real data in Phase 6.
- `PositiveNews.Core` — added once persistence lands (Phase 5).
- `.claude/agents/multi-agent-reviewer.md`, `.claude/agents/schema-reviewer.md` — starter
  subagents for reviewing C# changes and JSON Schemas/MCP tool descriptions.
- `.claude/skills/blazor-skill/` — Blazor component/coding conventions for
  `PositiveNews.Web` (code-behind split, `EventCallback`, `@key`, CSS isolation).
- `.claude/settings.json` — empty settings, ready to wire up hooks.
- `hooks/` — empty; reserved for future hook executables.
- `.mcp.json` — registers `PositiveNews.McpServer` so it's directly pokeable from a
  Claude Code session during development.

## Status

Phase 3 complete: `PositiveNews.Web` (Blazor Web App, Interactive Server) shell —
two-column blog layout (article list + sidebar), hand-built reusable components
(`Icon`, `ArticleCard`, `SearchBox`, `Sidebar`, etc.) under `Components/Shared`,
hardcoded placeholder story data. The sidebar search box live-filters the list
via the Interactive Server circuit — no page reload — confirming the render
mode is genuinely active. Next: Phase 4 (multi-agent orchestration pipeline).
See the phase-wise plan
(`C:\Users\Ratnesh\.claude\plans\can-you-create-a-structured-rabin.md`) for the
full roadmap.

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

# Blazor Web App shell (placeholder data, no backend wiring yet)
dotnet run --project PositiveNews.Web
```
