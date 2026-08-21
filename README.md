# multi-agent-test

A scratch project for testing multi-agent patterns in a .NET / Claude Code
context. Sibling to `DotNetTest` in this workspace — see
`../DotNetTest/README.md` for how the `.claude/` pieces (skills, hooks,
subagents, MCP servers) fit together in general.

## Layout

- `MultiAgentTest.csproj`, `Program.cs` — a plain .NET console app (default
  template, not yet customized).
- `.claude/agents/multi-agent-reviewer.md` — a starter subagent for
  reviewing C# changes in this project.
- `.claude/skills/` — empty; add a skill here once there's a concrete
  scaffolding or automation task for this project.
- `.claude/settings.json` — empty settings, ready to wire up hooks.
- `hooks/` — empty; reserved for future hook executables.

## Status

Just scaffolded. No multi-agent logic yet.

## Run

```
dotnet run
```
