---
name: blazor-skill
description: Blazor component/coding conventions for PositiveNews.Web (this project's Blazor Web App). Use whenever writing, reviewing, or reasoning about any .razor component, layout, or page in PositiveNews.Web.
---

# blazor-skill

Reference conventions for `PositiveNews.Web`, scoped to what this specific project actually
needs — not a general Blazor tutorial. Pasted by Ratnesh as a "Blazor UI Specialist" brief
during Phase 3 (the Blazor shell); trimmed here to the parts that apply to this codebase.
See root `CLAUDE.md` for the project's non-Blazor conventions (agent pattern, MCP, secrets).

## Component structure

- **Code-behind split**: `ComponentName.razor` (markup only) + `ComponentName.razor.cs`
  (partial class with any non-trivial `@code`). Trivial components (a couple of parameters,
  no logic) may keep a small `@code` block inline in the `.razor` file — don't force a
  `.razor.cs` file that would just contain three property declarations.
- **CSS**: one `wwwroot/app.css` for global resets, typography, and CSS custom-property
  design tokens (`--color-*`, `--font-*`, `--layout-*`, defined in Phase 3). Every component
  that needs its own layout/spacing gets a sibling `ComponentName.razor.css` (Blazor CSS
  isolation) — reference the shared tokens via `var(--token-name)`, don't redefine colors
  locally.
- **Single responsibility, reusable**: if a piece of markup would be duplicated across two
  components, it's a new component, not a copy-paste. Parameters for inputs, `EventCallback`
  for outputs — never a component reaching into a sibling's state directly.
- **`@key`** on every `@foreach` that renders a list of components, keyed on a stable id
  (not the loop index unless the list truly has no other stable identity).
- **Dispose**: any component that owns an `IDisposable`/`IAsyncDisposable` resource (a
  timer, an event subscription, a JS module reference) implements `IDisposable` /
  `IAsyncDisposable` itself and disposes it in `Dispose`/`DisposeAsync`.
- **JS interop**: avoid it. Everything in this project's UI so far (Phase 3 onward) is
  achievable with native Blazor event binding and CSS — reach for interop only when there's
  genuinely no other way, and isolate it behind a small wrapper service rather than scattering
  `IJSRuntime` calls through components.
- **Cascading parameters**: only for genuinely ambient/shared data (e.g. a theme or current
  user, if those are ever added) — not as a shortcut around passing a couple of parameters
  down two levels.

## What this project deliberately does NOT use

Per root `CLAUDE.md`'s stance on hand-rolling over reaching for frameworks that would hide
the mechanisms this project exists to teach:

- No MudBlazor/FluentUI or other component libraries — components here are hand-built.
- No Fluxor/global state library — component state and DI-registered services are enough at
  this project's scale so far. Revisit only if a later phase's actual scope needs it.
- No auth yet — nothing in the current phase plan requires it. If a future phase adds it,
  extend this skill then rather than speculatively now.
- Render mode is fixed to Interactive Server project-wide (a fixed constraint from the
  phase plan, not a per-component choice) — don't introduce WebAssembly or `Auto` render
  modes without checking against the plan first.

## Reference shapes in this codebase

Once Phase 3 lands, `PositiveNews.Web/Components/Shared/` has worked examples of this
skill's conventions in practice (`Icon`, `ArticleCard`, `SearchBox`, etc.) — prefer copying
the shape of an existing component in that folder over inventing a new pattern.
