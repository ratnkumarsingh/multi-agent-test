# PositiveNews (multi-agent-test)

A daily "positive/happy news" website, built as a deliberate vehicle to learn agentic
architecture, Claude Code workflows, prompt engineering & structured output, MCP tool
design, and multi-agent orchestration. Sibling to `DotNetTest` in this workspace — see
`../DotNetTest/README.md` for how the `.claude/` pieces (skills, hooks, subagents, MCP
servers) fit together in general. See `CLAUDE.md` for this project's own conventions.

## Layout

- `PositiveNews.Cli` — fast console harness for iterating on agents without the web UI.
- `PositiveNews.Agents` — Anthropic client, `IAgent<TIn,TOut>` agents, orchestrator (later phases).
- `PositiveNews.McpServer`, `PositiveNews.Core`, `PositiveNews.Web` — added in later phases.
- `.claude/agents/multi-agent-reviewer.md` — a starter subagent for
  reviewing C# changes in this project.
- `.claude/skills/` — empty; add a skill here once there's a concrete
  scaffolding or automation task for this project.
- `.claude/settings.json` — empty settings, ready to wire up hooks.
- `hooks/` — empty; reserved for future hook executables.

## Status

Phase 1 (foundation: Anthropic client + first structured-output agent). See the
phase-wise plan for what's next.

## Run

```
dotnet user-secrets set ANTHROPIC_API_KEY <key> --project PositiveNews.Cli
dotnet run --project PositiveNews.Cli -- "topic to brainstorm about"
```
