# Microsoft.Maui.Cli

A command-line tool for .NET MAUI development environment setup and device management.

> ⚠️ **Experimental** — APIs may change between releases. Not covered by the Microsoft Support Policy.

## Package

| Package | Description |
|---------|-------------|
| **Microsoft.Maui.Cli** | Global CLI tool (`maui`) for environment setup, device management, and diagnostics. |

## Quick Start

### 1. Install the CLI tool

```bash
dotnet tool install -g Microsoft.Maui.Cli
```

### 2. Check your environment

```bash
# Run diagnostics
maui doctor

# List connected devices
maui device list
```

### 3. Manage .NET MAUI versions

```bash
# Check installed MAUI version in your project
maui version check
maui version check --project ./src/MyApp/MyApp.csproj

# List available versions from NuGet
maui version list
maui version list --channel nightly
maui version list --prerelease --take 20

# Update to latest stable
maui version update --latest

# Switch to nightly builds
maui version update --latest-nightly --nuget-config

# Install a specific version (e.g. from a PR build)
maui version update --version 10.0.0-preview.5.25180.1
```

### 4. Set up Android development

```bash
# Full interactive Android setup (JDK + SDK + emulator)
maui android install

# Manage Android SDK packages
maui android sdk list
maui android sdk install "platforms;android-35"

# Manage JDK installations
maui android jdk install

# Create and manage emulators
maui android emulator create --name MyEmulator
maui android emulator start --name MyEmulator
```

## Commands

| Command | Description |
|---------|-------------|
| `maui doctor` | Run environment diagnostics and auto-fix issues |
| `maui device list` | List connected devices and emulators |
| `maui version` | Display CLI version information |
| `maui version check` | Check installed MAUI version in a project |
| `maui version list` | List available .NET MAUI versions from NuGet feeds |
| `maui version update` | Update MAUI package versions (stable, nightly, or specific) |
| `maui android install` | Full interactive Android environment setup |
| `maui android sdk list` | List available and installed Android SDK packages |
| `maui android sdk install` | Install Android SDK packages |
| `maui android jdk install` | Install and manage JDK versions |
| `maui android emulator create` | Create an Android emulator |
| `maui android emulator start` | Start an Android emulator |
| `maui android emulator stop` | Stop a running emulator |
| `maui android emulator delete` | Delete an emulator |
| `maui devflow` | MAUI app automation via Agent API and Blazor WebViews via CDP |
| `maui devflow MAUI tree` | Dump the visual tree of a running MAUI app |
| `maui devflow MAUI screenshot` | Take a screenshot of a running MAUI app |
| `maui devflow cdp` | Blazor WebView automation via Chrome DevTools Protocol |
| `maui devflow mcp` | Start MCP server for AI agent integration |
| `maui devflow broker` | Manage the DevFlow agent broker |

## Global Options

| Option | Description |
|--------|-------------|
| `--json` | Output in JSON format (for scripting and CI) |
| `-v`, `--verbose` | Enable verbose output |
| `--dry-run` | Show what would be done without making changes |
| `--ci` | CI mode — non-interactive, fail fast on errors |

## Output Formats

The CLI supports two output modes:

- **Interactive** (default) — Rich Spectre.Console output with colors, tables, and progress bars
- **JSON** (`--json`) — Machine-readable JSON for scripting and CI pipelines

```bash
# Human-friendly output
maui doctor

# JSON output for scripting
maui doctor --json | jq '.checks[] | select(.status == "failed")'
```

## Platform Support

| Platform | Status |
|----------|--------|
| macOS | ✅ |
| Windows | ✅ |
| Linux | ✅ |

## Development

```bash
# Open just the CLI in your IDE
open src/Cli/Cli.slnf

# Build
dotnet build src/Cli/Cli.slnf

# Run tests
dotnet test src/Cli/Microsoft.Maui.Cli.UnitTests/Microsoft.Maui.Cli.UnitTests.csproj

# Run locally without installing
dotnet run --project src/Cli/Microsoft.Maui.Cli/ -- doctor
```
