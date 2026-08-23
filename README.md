# PositiveNews (multi-agent-test)

A daily "positive/happy news" website, built as a deliberate vehicle to learn agentic
architecture, Claude Code workflows, prompt engineering & structured output, MCP tool
design, and multi-agent orchestration. Sibling to `DotNetTest` in this workspace — see
`../DotNetTest/README.md` for how the `.claude/` pieces (skills, hooks, subagents, MCP
servers) fit together in general. See `CLAUDE.md` for this project's own conventions.

## Layout

- `PositiveNews.Cli` — fast console harness for iterating on agents without the web UI.
- `PositiveNews.Agents` — Anthropic client, `IAgent<TIn,TOut>` agents (`HeadlineIdeaAgent`,
  `SearchAgent`, `PositivityScorerAgent`, `SummarizerAgent`, `TranslationAgent`),
  `Orchestrator` (fan-out scoring/summarizing with per-candidate failure isolation), MCP
  client wiring (`Mcp/NewsSearchMcpClient.cs`).
- `PositiveNews.McpServer` — custom MCP server exposing a `SearchNews` tool over stdio,
  backed by NewsAPI.org (`INewsSearchClient`, one-file provider swap).
- `PositiveNews.Web` — Blazor Web App (Interactive Server), reading real curated stories
  from `PositiveNews.Core` (`Home.razor` — story list + "Top Rated" sidebar by score;
  `Story.razor` — detail page, with an on-demand "Translate to Hindi" button). Still
  read-only otherwise — no scheduled/"run now" pipeline-triggering yet (Phase 7).
- `PositiveNews.Core` — `PositiveNewsDbContext` + entities (`PipelineRun`, `PipelineStep`,
  `PipelineCandidate`, `NewsStory`, `StoryTranslation`) over SQLite, one file at the repo
  root.
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

Phase 6 complete, plus an on-demand Hindi-translation feature (a scoped-down slice of
Phase 8) built ahead of Phase 7. `Story.razor` has a "Translate to Hindi" button:
`TranslationAgent` (forced-tool, same `{Headline, Body}` shape as `SummarizerAgent`) runs
on a cheaper model (`AnthropicOptions.TranslationModel`, via a new optional `model`
parameter on `AnthropicClient.CallToolAsync`) and caches the result in `StoryTranslation`
so a story is translated once, not on every view — verified live, including the cache
(reloading and clicking translate again is instant, no second API call). This is what
pulled `PositiveNews.Web`'s reference to `PositiveNews.Agents` forward from Phase 7.
Next: Phase 7 (daily scheduling + live run progress — the "run now" button and
background scheduler). See the phase-wise plan
(`C:\Users\Ratnesh\.claude\plans\can-you-create-a-structured-rabin.md`) for the
full roadmap.

## Run

```
dotnet user-secrets set "Anthropic:ApiKey" <key> --project PositiveNews.Cli
dotnet user-secrets set "NewsApi:ApiKey" <key> --project PositiveNews.McpServer
# PositiveNews.Web shares the Cli's user-secrets store (same UserSecretsId) — no separate
# key-setting step needed for it. If your gateway needs a namespaced Haiku model id for
# translation (it likely does, the same way Anthropic:Model needed one):
dotnet user-secrets set "Anthropic:TranslationModel" "anthropic/claude-haiku-4-5" --project PositiveNews.Cli

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
