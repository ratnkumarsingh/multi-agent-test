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

`PositiveNews.Web` shares `PositiveNews.Cli`'s user-secrets store on purpose (same
`<UserSecretsId>` in both `.csproj`s — it's just a shared file on disk keyed by that GUID)
so the Anthropic key only needs setting once for the whole repo, not per-project.

**Per-call model override**: `AnthropicClient.CallToolAsync` takes an optional `model`
parameter, defaulting to `AnthropicOptions.Model` when omitted — for an agent that
deliberately runs on a different model than the rest of the pipeline (currently just
`TranslationAgent`, on the cheaper `AnthropicOptions.TranslationModel`). Both `Model` and
`TranslationModel` follow the same bare-id-vs-gateway-namespaced-id rule — a gateway may
reject the bare id (`"claude-haiku-4-5-20251001 is not a valid model ID"` was the actual
error hit here), in which case override `Anthropic:TranslationModel` via user-secrets the
same way `Anthropic:Model` was already namespaced for this project's gateway.

## Persistence

One SQLite file at the repo root, `positivenews.db` (gitignored — local state, not shared),
resolved via `AppContext.BaseDirectory` rather than a relative path so every project that
touches it (`PositiveNews.Cli` and `PositiveNews.Web` both) lands on the same physical
file regardless of which process is running or its working directory. `DbSet`s
live on `PositiveNewsDbContext` (`PositiveNews.Core/Data/`); `IDbContextFactory<PositiveNewsDbContext>`
(not a single shared `DbContext`) is what callers inject, since `DbContext` isn't
thread-safe and `Orchestrator`'s fan-out stages need several short-lived contexts open
concurrently — each factory call opens its own connection.

New migration after an entity change:
```
dotnet ef migrations add <Name> --project PositiveNews.Core
```
(`PositiveNewsDbContextDesignTimeFactory` lets this run without needing `--startup-project`.)
Migrations apply automatically at startup in both `PositiveNews.Cli` (before a
DB-touching command runs) and `PositiveNews.Web` (`Program.cs`, before `app.Run()`) — not
manually.

**Don't fabricate UI data for a field the pipeline doesn't actually produce.** Phase 3's
placeholder shell had Categories, a Tag Cloud, and view-counted "Popular Articles" —
Phase 6 dropped Categories/Tags entirely and repurposed "Popular" into "Top Rated" backed
by the real `Score` field, rather than inventing a category taxonomy or fake view counts
just to keep the Phase 3 layout intact. If a later phase wants any of those back, the
right fix is a real upstream source for the field (an agent that assigns a category, an
actual view-tracking mechanism) — not a plausible-looking placeholder in the view layer.

## On-demand agent calls from PositiveNews.Web

`PositiveNews.Web` referencing `PositiveNews.Agents` was originally slated for Phase 7 (a
"run now" button calling `Orchestrator`) — the Hindi-translation button pulled that
boundary-crossing forward, since it needs `TranslationAgent` directly. When a component
needs an agent for a single user-triggered action (not a page-load-time need), inject
`IServiceProvider` and resolve the agent lazily inside the click handler's `try`/`catch`,
rather than `[Inject]`-ing the agent (or anything that transitively constructs
`AnthropicClient`) as a component property. `AnthropicClient`'s constructor validates the
API key eagerly and throws if it's missing — a direct property injection would run that
check (and fail the whole page load) every time the page renders, not just when the
action is actually used. See `Story.razor.cs`'s `ToggleTranslationAsync` for the pattern:
lazy resolve, call, catch, show an inline error — never let an agent-call failure crash
the page.

## Reliability posture

Each pipeline stage (search, score, summarize, translate, ...) is its own agent call
rather than one long-lived conversation thread — this sidesteps context dilution (a
long session's early instructions losing effective salience even below the token limit)
by construction, not by periodic resets. `Orchestrator`'s fan-out stages (scoring,
summarizing) isolate per-candidate failures rather than aborting the whole batch, each
agent call is wrapped in bounded retry-with-backoff, and every stage persists
incrementally to SQLite (`PositiveNews.Core`) — see `PositiveNews.Agents/Orchestrator.cs`.
This is what makes a `PipelineRun` both idempotent per calendar day (a `Completed` run is
a no-op on rerun) and resumable at the individual-candidate level (a rerun skips
candidates already searched/scored/summarized, not just skips the whole run), with every
agent invocation traced in `PipelineStep` for inspection via `PositiveNews.Cli history`.

**A real SQLite/EF Core gotcha hit here**: the SQLite provider can't translate
`ORDER BY` on a `DateTimeOffset` column into SQL (`NotSupportedException` at query time,
not at compile time) — order by an int/auto-increment `Id` or a `DateOnly` column instead
when the query needs to run server-side.

## Verification loop

`PositiveNews.Cli` is the fast iteration path — prefer `dotnet run --project
PositiveNews.Cli -- <args>` over the Blazor UI while developing agent logic.

## Claude Code tooling in this repo

- **`.claude/skills/agent-scaffold/`** — generates a new `IAgent<TIn,TOut>` stub matching
  the Agent convention above (`dotnet run --project ".claude/skills/agent-scaffold/tools/Scaffolder" -- --name <AgentName> ...`).
- **`hooks/AgentSchemaGuard/`** — a `PreToolUse` hook (matched on `Write`/`Edit`/`Bash`, so
  a shell redirect can't bypass it) that warns (`ask`, never `deny`) if a new/edited file
  under `PositiveNews.Agents/Agents/` looks like an agent but is missing a JSON Schema
  constant or a forced `tool_choice` call. **One-time setup after cloning** (the published
  binary isn't committed — a self-contained win-x64 build is ~70MB and not delta-friendly
  across rebuilds):
  ```
  dotnet publish hooks/AgentSchemaGuard -c Release -r win-x64 --self-contained -o hooks/AgentSchemaGuard/publish
  ```
  Re-run this after editing the hook's `Program.cs`. Registered in `.claude/settings.json`.
- **`.claude/skills/blazor-skill/`** — Blazor component/coding conventions for
  `PositiveNews.Web` (code-behind split, `EventCallback`, `@key`, CSS isolation).
- **`.claude/skills/hindi-translation/`** — dev-time reference for translation-quality
  principles (natural over literal, register matching, the "would a native speaker
  believe this was original" test). Note this skill alone changes nothing live — the
  fix for actual translation quality is `TranslationAgent.cs`'s `SystemPrompt` itself,
  per the "two agent notions" split above. Consult this skill when revising that prompt.
- **`.claude/agents/multi-agent-reviewer.md`, `.claude/agents/schema-reviewer.md`** —
  subagents for reviewing C# diffs and JSON Schemas/MCP tool descriptions, respectively.
