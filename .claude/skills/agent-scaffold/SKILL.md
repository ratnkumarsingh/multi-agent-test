---
name: agent-scaffold
description: Scaffold a new IAgent<TIn,TOut> stub for PositiveNews.Agents, following this project's forced tool_choice/JSON Schema convention. Use when the user asks to create/scaffold/generate a new agent in this project.
---

# agent-scaffold

Generates a new `IAgent<TIn,TOut>` implementation using a bundled .NET console tool —
mirrors `DotNetTest`'s `dotnet-scaffold` skill (skill → .NET handoff: the skill is just
Markdown, the real work is a compiled .NET program). Built in Phase 4, when this project
went from one agent to several, since that's exactly when a consistent stub starts paying
for itself.

## How to use it

1. Determine from the user's request: the **agent name** (PascalCase, ending in `Agent` by
   convention — e.g. `TrendSpotterAgent`), the **input type** (`--in`, defaults to `string`),
   and the **output type** (`--out`) — if omitted, the tool generates a small
   `{AgentName}Result` record alongside the agent, matching how `HeadlineIdeaAgent` owns
   `HeadlineIdea` and `PositivityScorerAgent` owns `PositivityScore`.
2. Run the bundled scaffolder tool:

   ```bash
   dotnet run --project ".claude/skills/agent-scaffold/tools/Scaffolder" -- \
     --name <AgentName> --in <InputType> --out <OutputType> \
     --output PositiveNews.Agents/Agents
   ```

   The tool prints the path of the file it created and echoes the content.
3. Open the generated file and fill in the `TODO`s: the system prompt, the JSON Schema
   properties (one per output field, `additionalProperties: false`, nullable-union types
   for fields that can legitimately be absent), the tool description, and the
   `RunAsync` mapping from the parsed tool input to the output type.
4. If the agent will process externally-sourced text (article content, any scraped web
   content), keep the scaffolded system-prompt line that tells it to treat that content as
   data, never instructions — see `PositivityScorerAgent`/`SummarizerAgent` for the exact
   wording this project uses.
5. Register the new agent with whatever constructs it (`Orchestrator`, `PositiveNews.Cli`
   mode, etc.) — the scaffolder doesn't wire it in anywhere, it only creates the file.

## Notes

- The tool writes into `./generated/` by default; pass `--output <dir>` to change it (the
  example above points it straight at `PositiveNews.Agents/Agents`).
- If `--namespace` is omitted the tool uses `PositiveNews.Agents.Agents`.
- The tool is self-contained — no NuGet dependencies — so first run just incurs a `dotnet`
  build.
- The generated stub already has a `JsonElement Schema` constant and a forced
  `CallToolAsync(..., forceTool: true, ...)` call — it's deliberately impossible to
  scaffold a non-compliant agent by accident, which is also exactly what
  `hooks/AgentSchemaGuard` checks for on `Write`/`Edit`/`Bash` into
  `PositiveNews.Agents/Agents/`.
