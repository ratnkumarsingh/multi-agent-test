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
- `PositiveNews.Web` — Blazor Web App (Interactive Server), scaffolded as a shell in
  Phase 3 with hardcoded placeholder data; wired to real data in Phase 6.
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

Phase 5 complete: persistence & reliability. `Orchestrator` now persists every stage to
SQLite (`PositiveNews.Core`) incrementally rather than only returning an in-memory
result — a `PipelineRun` row anchors idempotency per calendar day, `PipelineCandidate`
rows make the run resumable at the individual-candidate level (a rerun skips
already-searched/scored/summarized work), and `PipelineStep` rows trace every agent
invocation. Each agent call is wrapped in bounded retry-with-backoff. All three verify
scenarios confirmed live: same-day rerun after completion is a 0.6s no-op; killing the
process mid-scoring and rerunning resumed from exactly where it left off (skipped search
entirely, only scored the remaining unscored candidates); a forced network failure
retried, then failed cleanly with the run marked `Failed` rather than hanging, and a
subsequent real rerun resumed correctly from that `Failed` state. `PositiveNews.Cli`
gained `history` (recent runs, or a full step trace for one run). Next: Phase 6 (wire
the Blazor shell to this real data). See the phase-wise plan
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

# Full orchestration pipeline: search -> score -> summarize -> curated stories
dotnet run --project PositiveNews.Cli -- run-pipeline "optional topic hint"

# Sanity-check PositivityScorerAgent alone against a hardcoded positive/negative pair
dotnet run --project PositiveNews.Cli -- score-test

# List recent pipeline runs (or `history <id>` for one run's full step trace)
dotnet run --project PositiveNews.Cli -- history
```
