# Anvil

> Test Harness for Aura

## Overview

Anvil is a **test harness for validating Aura agent quality** through story execution. It runs stories through Aura, validates the generated code, and tracks regressions over time.

## Key Features

- **Story-driven** — Tests defined as human-readable specifications
- **Multi-modal** — Test REST API, VS Code Extension, and CLI paths
- **Multi-language** — Stories can target C#, Python, TypeScript
- **Regression tracking** — Compare runs, detect quality degradation
- **Rich CLI UX** — Designed for testers, not just CI

## Technology

- .NET 10 / C# 14
- System.CommandLine (CLI)
- SQLite (regression tracking)
- Spectre.Console (rich output)

## Quick Start

```bash
# Build
dotnet build

# Run a single story
anvil run stories/greenfield/cli-hello-world.md

# Run all stories
anvil run stories/

# Run with specific execution mode
anvil run stories/ --mode aura-api
anvil run stories/ --mode vscode
anvil run stories/ --mode copilot-cli

# Compare runs for regressions
anvil compare run-123 run-456
```

## Prerequisites

### Git Safe Directory (Windows)

When running Anvil with fixture repositories, git may block access due to ownership mismatches. Add the fixtures path as a safe directory:

```bash
git config --global --add safe.directory "c:/work/aura-anvil/anvil/fixtures/repos/csharp-console"
```

This is needed because Anvil executes git operations in fixture repos from a different context than the workspace root.

## Development

This project uses **Spec-Driven Development (SDD)**. Read:

1. [.sdd/philosophy.md](.sdd/philosophy.md) — Understand the methodology
2. [.project/architecture/project.md](.project/architecture/project.md) — Project structure
3. [.project/coding-guidelines/csharp.md](.project/coding-guidelines/csharp.md) — C# conventions

### Architecture Decision Records

| ADR | Decision |
|-----|----------|
| [ADR-001](./sdd/ADR/ADR-001-testing.md) | xUnit + FluentAssertions + NSubstitute |
| [ADR-002](./sdd/ADR/ADR-002-dependency-injection.md) | Microsoft.Extensions.DependencyInjection |
| [ADR-003](./sdd/ADR/ADR-003-logging.md) | Serilog with structured logging |
| [ADR-004](./sdd/ADR/ADR-004-cross-cutting-concerns.md) | Result<T,TError> with LINQ extensions |
| [ADR-005](./sdd/ADR/ADR-005-database-strategy.md) | SQLite for regression tracking |
| [ADR-006](./sdd/ADR/ADR-006-environment-configuration.md) | CLI > Env Vars > appsettings.json |
| [ADR-007](./sdd/ADR/ADR-007-vscode-extension-testing.md) | @vscode/test-electron with test runner |
| [ADR-008](./sdd/ADR/ADR-008-story-source-strategy.md) | File-based stories, GitHub Issues (future) |
| [ADR-009](./sdd/ADR/ADR-009-authentication-handling.md) | Local API (none), GitHub (PAT) |

### SDD Workflow

```
Research → [clear] → Plan → [clear] → Implement → [clear] → Validate
```

🔴 **Always clear context between phases.**

Never skip phases. See [.sdd/prompts/](.sdd/prompts/) for workflow prompts.

## Configuration

See [ADR-006](./sdd/ADR/ADR-006-environment-configuration.md) for configuration options.

```bash
# Environment variables (ANVIL__ prefix)
export ANVIL__AuraBaseUrl="http://localhost:5300"
export ANVIL__Timeout="00:02:00"

# Or appsettings.json
{
  "Anvil": {
    "AuraBaseUrl": "http://localhost:5300",
    "Timeout": "00:01:00",
    "StoriesPath": "stories",
    "DatabasePath": "anvil.db"
  }
}
```

## License

MIT
