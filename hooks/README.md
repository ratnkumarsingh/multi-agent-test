# hooks/

Reserved for future Claude Code hook executables (e.g. a `PreToolUse` or
`PostToolUse` command), the same pattern as `DotNetTest\hooks\FormatGuard\`
in the sibling `DotNetTest` project. A hook here would be a small .NET
console app that reads the event JSON on stdin, and gets wired up via
`.claude/settings.json`.

Empty for now — add a project folder here once a hook is needed.
