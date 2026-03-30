---
description: |
  Analyzes merged pull requests for documentation needs. When a PR is merged,
  this workflow reviews the changes and determines if documentation updates are
  needed on dotnet/docs-maui. If updates are needed, it opens a draft PR directly
  on docs-maui. If not, it comments on the source PR explaining why.

on:
  pull_request:
    types: [closed]
    branches:
      - main
      - release/*
    paths-ignore:
      - "eng/**"
      - ".github/**"
      - ".azure-pipelines/**"
      - ".config/**"
      - ".editorconfig"
      - ".gitignore"
      - ".gitattributes"
      - "**/*.md"
      - "**/LICENSE*"
      - "**/THIRD-PARTY*"
      - "Directory.Build.*"
      - "NuGet.config"
      - "global.json"
      - "*.sln"
      - "*.slnf"
  workflow_dispatch:
    inputs:
      pr_number:
        description: "PR number to analyze"
        required: true
        type: string

if: >-
  (github.event.pull_request.merged == true || github.event_name == 'workflow_dispatch')
  && github.repository_owner == 'dotnet'

permissions:
  contents: read
  pull-requests: read
  issues: read

network:
  allowed:
    - defaults
    - github

tools:
  github:
    toolsets: [repos, issues, pull_requests]
    github-token: ${{ secrets.MAUI_BOT_TOKEN }}
  web-fetch:

safe-outputs:
  github-token: ${{ secrets.MAUI_BOT_TOKEN }}
  create-pull-request:
    title-prefix: "[maui-labs] "
    labels: [docs-from-code]
    draft: true
    target-repo: "dotnet/docs-maui"
    expires: 30
  add-comment:
    hide-older-comments: true

timeout-minutes: 15
---

# PR Documentation Check

Analyze a merged pull request in `dotnet/maui-labs` and determine whether
documentation updates are needed on the `dotnet/docs-maui` documentation site.

## Context

- **Repository**: `${{ github.repository }}`
- **PR Number**: `${{ github.event.pull_request.number || github.event.inputs.pr_number }}`
- **PR Title**: `${{ github.event.pull_request.title }}`

## Step 1: Gather PR Information

Use the GitHub tools to read the full pull request details for the PR number above,
including the title, description, author, base branch, and the full diff of changes.

If this was triggered via `workflow_dispatch`, use the `pr_number` input to look up
the PR details. If the PR number is invalid or the PR cannot be found, stop and
comment on the workflow run that the input was invalid. If the PR was not merged
(still open or closed without merging), comment that no documentation updates are
needed because the PR was not merged, then **stop**.

## Step 2: Quick Filter — Skip Obviously Non-Doc PRs

Before doing a deep analysis, check if this PR can be **skipped entirely**:

**Skip if:**
- PR author is a bot (`dotnet-maestro[bot]`, `dependabot[bot]`, `github-actions[bot]`)
- PR title contains "backport" (case-insensitive)
- ALL changed files are tests only (paths containing `/tests/`, `/TestUtils/`,
  files with `.Tests.` or `.UnitTests.` in name)
- ALL changed files are CI/build infrastructure only (`eng/`, `.github/`,
  `*.yml`, `*.yaml`, `*.props`, `*.targets`)

If the PR is skipped, comment on the PR with a brief one-line message confirming
no documentation updates are needed and why, then **stop**.

**Always proceed with analysis if:**
- Any file in `src/Cli/Microsoft.Maui.Cli/DevFlow/` was changed (CLI DevFlow code)
- Any file in `src/DevFlow/**/Mcp/Tools/` was changed (MCP tool changes)
- Any `.csproj` file with `IsPackable=true` was changed (package changes)

## Step 3: Analyze Changes for Documentation Needs

Review the PR diff for user-facing changes that affect documentation:

- **New or changed MCP tools** — new tools, renamed tools, changed parameters.
  These directly affect the MauiDevFlow docs page.
- **New public APIs** — new methods, classes, or properties in public-facing
  packages (AgentClient, DevFlow CLI, etc.)
- **New features or capabilities** — new platform support, new debugging
  capabilities, new protocol features
- **Breaking changes** — removed or renamed APIs, behavioral changes
- **New CLI commands or flags** — changes to the `maui` CLI or `maui devflow`
  subcommands that users interact with
- **Package changes** — new packages, removed packages, changed package contents

Changes that do NOT need documentation:
- Internal refactoring with no public API surface changes
- Test-only changes
- Bug fixes that don't change documented behavior
- Dependency version bumps
- Code style or formatting changes

## Step 4: If Documentation IS Needed

### 4a: Check Existing Documentation

Use the GitHub tools to read the current documentation in `dotnet/docs-maui`:

- Read `docs/ai-development/dev-flow.md` — this is the main MauiDevFlow docs page
- Check the TOC at `docs/TOC.yml` for the AI-assisted development section structure
- Identify which sections of `dev-flow.md` need updating

Before making changes, check if there are any open draft PRs on `dotnet/docs-maui`
with the `docs-from-code` label from recent maui-labs merges. If so, note any
potential conflicts in your PR description.

The documentation is written in Microsoft Learn Markdown format:
- Use `> [!NOTE]`, `> [!WARNING]`, `> [!TIP]` for callout boxes
- Use `ms.date: MM/DD/YYYY` format in frontmatter
- Code blocks use triple backticks with language identifier
- Tables use pipe syntax

### 4b: Open a Draft PR on docs-maui

Make the changes to the documentation files and open a draft pull request on
`dotnet/docs-maui`. The PR should:

- Update `docs/ai-development/dev-flow.md` with the new information
- Update the `ms.date` field in the frontmatter to today's date
- Include a clear PR title describing the documentation update
- In the PR body, link to the source PR in `dotnet/maui-labs` that triggered the change
- Keep changes minimal — only update what's needed for this specific PR

### 4c: Comment on the Source PR

Comment on the original PR in `dotnet/maui-labs` with:
- A summary of the documentation changes made
- A link to the draft PR on `dotnet/docs-maui`

## Step 5: If Documentation is NOT Needed

Comment on the PR in `dotnet/maui-labs` with a brief one-line message confirming
no documentation updates are required and a short reason (e.g., "internal
refactoring only", "test changes only", "bug fix with no behavioral change").

Keep the comment concise — one or two sentences maximum.
