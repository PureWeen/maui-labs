---
description: >
  Weekly audit of AI configuration files (AGENTS.md, copilot-instructions.md,
  instruction files) to keep them in sync with the actual codebase.
on:
  schedule: weekly
  workflow_dispatch:
permissions:
  contents: read
  pull-requests: read
  issues: read
tools:
  edit:
  bash:
    - grep
    - find
    - cat
    - ls
    - wc
    - sort
    - diff
    - head
    - tail
  github:
network:
  allowed:
    - defaults
safe-outputs:
  create-pull-request:
    title-prefix: "[ai-docs] "
    labels: [ai-docs-audit]
    draft: true
  create-issue:
    title-prefix: "[ai-docs] "
    labels: [ai-docs-audit]
    close-older-issues: true
    max: 1
---

# Weekly AI Docs Audit

You are auditing the AI configuration files in this repository to ensure they
accurately reflect the current state of the codebase. These files are read by
AI coding agents (GitHub Copilot, Claude, etc.) and stale information causes
them to generate incorrect code.

## Step 1: Gather what changed

Check which PRs were merged in the past 7 days. Review their titles, descriptions,
and changed files to understand what evolved in the codebase.

## Step 2: Audit each AI config file

For each file below, verify its contents against the actual source code.

### AGENTS.md

- **MCP tools table**: Scan every `[McpServerTool]` attribute in
  `src/DevFlow/**/Mcp/Tools/*.cs`. The table in AGENTS.md must list every tool
  with its correct name, source file, and description. Regenerate the entire
  table from source rather than patching individual rows.
- **Package list**: Check all `.csproj` files for `IsPackable=true` and
  cross-reference with the documented package list.
- **Build commands**: Verify solution filter paths (`ls src/DevFlow/*.slnf
  src/Client/*.slnf 2>/dev/null`) and ensure build commands are correct.
- **NuGet feeds**: Compare `NuGet.config` with the documented feed list.
- **SDK version**: Check `global.json` against documented version.

### .github/copilot-instructions.md

- **Platform patterns**: Verify that documented code patterns (Core/Platform,
  `#if` guards, handler registration) still match actual source conventions.
- **MCP tool conventions**: Check that naming conventions (`maui_` prefix,
  snake_case, parameter ordering) match actual tools.
- **Arcade gotchas**: Verify documented build system quirks are still relevant.

### .github/instructions/devflow-architecture.instructions.md

- **Architecture diagram**: Verify project references in `.csproj` files match
  the dependency graph. Check for new or removed projects.
- **Communication protocols**: Verify WebSocket/HTTP patterns are still accurate.
- **Package dependency graph**: Verify dependencies between shipping packages.

### .github/instructions/mcp-tools.instructions.md

- **Tool creation guide**: Verify the step-by-step guide produces valid tools
  by cross-referencing with recently added tools.
- **Naming conventions**: Check that Tool/Tools suffix guidance matches actual
  class names in the codebase.
- **Tools reference table**: Must match the AGENTS.md table exactly.

### .github/instructions/testing.instructions.md

- **Test projects**: Verify documented test projects exist.
- **Test patterns**: Check that xUnit patterns match actual test files.
- **CI matrix**: Verify documented CI platforms match pipeline YAML files.

### .github/instructions/packaging.instructions.md

- **Package metadata**: Verify Arcade SDK version, signing config, CPM setup.
- **Package list**: Must match AGENTS.md.
- **Feed configuration**: Must match NuGet.config.

## Step 3: Decide and act

- If ANY file needs updating, make all corrections and open a single pull request.
  In the PR description, list each change with a reference to the merged PR that
  caused the drift.
- If NOTHING needs updating, create an issue noting that all AI config files are
  current, listing the PRs you reviewed.

## Rules

- Only modify files listed in the `allowed-files` safe output config.
- Never modify source code, tests, or build pipelines.
- When updating the MCP tools table, regenerate the entire table from source.
- Preserve existing formatting and structure of each file.
- Be precise — wrong information in these files is worse than missing information.
