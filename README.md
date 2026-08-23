# PositiveNews (multi-agent-test)

A daily "positive/happy news" website, built as a deliberate vehicle to learn agentic
architecture, Claude Code workflows, prompt engineering & structured output, MCP tool
design, and multi-agent orchestration. Sibling to `DotNetTest` in this workspace — see
`../DotNetTest/README.md` for how the `.claude/` pieces (skills, hooks, subagents, MCP
servers) fit together in general. See `CLAUDE.md` for this project's own conventions.

## Layout

- `PositiveNews.Cli` — fast console harness for iterating on agents without the web UI.
- `PositiveNews.Agents` — Anthropic client, `IAgent<TIn,TOut>` agents (`HeadlineIdeaAgent`,
  `SearchAgent`, `PositivityScorerAgent`, `SummarizerAgent`), `Orchestrator` (fan-out
  scoring/summarizing with per-candidate failure isolation), MCP client wiring
  (`Mcp/NewsSearchMcpClient.cs`).
- `PositiveNews.McpServer` — custom MCP server exposing a `SearchNews` tool over stdio,
  backed by NewsAPI.org (`INewsSearchClient`, one-file provider swap).
- `PositiveNews.Web` — Blazor Web App (Interactive Server), reading real curated stories
  from `PositiveNews.Core` (`Home.razor` — story list + "Top Rated" sidebar by score;
  `Story.razor` — detail page). Read-only — no run-triggering yet (Phase 7).
- `PositiveNews.Core` — `PositiveNewsDbContext` + entities (`PipelineRun`, `PipelineStep`,
  `PipelineCandidate`, `NewsStory`) over SQLite, one file at the repo root.
- `.claude/agents/multi-agent-reviewer.md`, `.claude/agents/schema-reviewer.md` — starter
  subagents for reviewing C# changes and JSON Schemas/MCP tool descriptions.
- `.claude/skills/blazor-skill/` — Blazor component/coding conventions for
  `PositiveNews.Web` (code-behind split, `EventCallback`, `@key`, CSS isolation).
- `.claude/skills/agent-scaffold/` — generates a new `IAgent<TIn,TOut>` stub matching the
  Agent convention.
- `hooks/AgentSchemaGuard/` — `PreToolUse` hook warning if a new/edited agent file is
  missing a JSON Schema constant or forced `tool_choice` call; see `CLAUDE.md` for the
  one-time `dotnet publish` setup step.
- `.mcp.json` — registers `PositiveNews.McpServer` so it's directly pokeable from a
  Claude Code session during development.

## Status

Phase 6 complete: the Blazor shell now reads real data. `Home.razor` queries the latest
completed `PipelineRun`'s `NewsStory` rows instead of Phase 3's hardcoded placeholders;
`Story.razor` is a new detail page. The sidebar's Categories and Tag Cloud sections were
dropped (no category/tag data exists anywhere in the pipeline — nothing was faked to fill
them); "Popular Articles" was repurposed as "Top Rated", backed by the real `Score` field
instead of a fabricated view count. `StoryAvatar` now shows a story's real image when
NewsAPI provided one, falling back to a deterministic gradient (keyed off the headline)
when it didn't — verified live with a mix of both in the same run. Next: Phase 7 (daily
scheduling + live run progress — the "run now" button and background scheduler this
phase deliberately left out). See the phase-wise plan
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

# Blazor Web App — reads real curated stories from positivenews.db
dotnet run --project PositiveNews.Web

# Full orchestration pipeline: search -> score -> summarize -> curated stories
dotnet run --project PositiveNews.Cli -- run-pipeline "optional topic hint"

# Sanity-check PositivityScorerAgent alone against a hardcoded positive/negative pair
dotnet run --project PositiveNews.Cli -- score-test

# List recent pipeline runs (or `history <id>` for one run's full step trace)
dotnet run --project PositiveNews.Cli -- history
```
