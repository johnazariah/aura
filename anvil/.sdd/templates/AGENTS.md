# AGENTS.md

> This file is the **entry point** for AI assistants working on Anvil. Copy it to the project root when scaffolding.

## Project Overview

**Anvil** is a test harness for validating Aura agent quality through story execution. It executes stories through Aura (REST API, VS Code Extension, shell commands), validates the generated code, and tracks regressions over time.

## SDD Methodology

This project uses **Spec-Driven Development (SDD)**. Before implementing any feature:

1. Read `.sdd/philosophy.md` for the methodology overview
2. Follow the workflow in `.project/architecture/project.md`
3. Use the prompts in `.sdd/prompts/` for each phase

## Architecture Decision Records

| ADR | Topic | Decision |
|-----|-------|----------|
| [ADR-001](./ADR/ADR-001-testing.md) | Testing | xUnit + FluentAssertions + NSubstitute |
| [ADR-002](./ADR/ADR-002-dependency-injection.md) | Dependency Injection | Microsoft.Extensions.DependencyInjection |
| [ADR-003](./ADR/ADR-003-logging.md) | Logging | Serilog with structured logging |
| [ADR-004](./ADR/ADR-004-cross-cutting-concerns.md) | Error Handling | Result<T,TError> with LINQ extensions |
| [ADR-005](./ADR/ADR-005-database-strategy.md) | Database | SQLite via EF Core |
| [ADR-006](./ADR/ADR-006-environment-configuration.md) | Configuration | CLI > Env Vars > appsettings.json |
| [ADR-007](./ADR/ADR-007-vscode-extension-testing.md) | VS Code Testing | @vscode/test-electron + test runner |
| [ADR-008](./ADR/ADR-008-story-source-strategy.md) | Story Sources | File-based (Phase 1), GitHub Issues (Phase 2) |
| [ADR-009](./ADR/ADR-009-authentication-handling.md) | Authentication | Local API (none), GitHub (PAT) |

## Key Documentation

| Document | Purpose |
|----------|---------|
| `.sdd/philosophy.md` | SDD methodology overview |
| `.project/architecture/project.md` | Full architecture, folder structure, execution modes |
| `.project/architecture/principles.md` | Clean Architecture, layer responsibilities |
| `.project/coding-guidelines/csharp.md` | C# 14/.NET 10 conventions, Result pattern |

## Quick Commands

```bash
# Build the solution
dotnet build Anvil.sln

# Run tests
dotnet test Anvil.sln

# Run a story
dotnet run --project src/Anvil.Cli -- run stories/greenfield/cli-hello-world.md

# Run with specific execution mode
dotnet run --project src/Anvil.Cli -- run stories/ --mode aura-api
dotnet run --project src/Anvil.Cli -- run stories/ --mode vscode
dotnet run --project src/Anvil.Cli -- run stories/ --mode copilot-cli

# List available stories
dotnet run --project src/Anvil.Cli -- list

# Compare runs for regressions
dotnet run --project src/Anvil.Cli -- compare run-123 run-456
```

## Coding Standards

### Critical Rules

- **Use Result<T, TError>** for operations that can fail expectedly
- **Use structured logging** with message templates (no interpolation)
- **Constructor injection** via primary constructors
- **Records** for immutable DTOs
- **CancellationToken** on all async methods

### File Structure

```
src/
├── Anvil.Core/            # Domain types (no dependencies)
├── Anvil.Application/     # Use cases, orchestration
├── Anvil.Infrastructure/  # External I/O (HTTP, files, DB)
└── Anvil.Cli/             # Entry point, DI wiring
```

### Layer Dependencies

```
Cli → Application → Core
        ↓
  Infrastructure
```

## Testing

| Test Type | Location | What It Tests |
|-----------|----------|---------------|
| Unit | `tests/Anvil.Core.Tests/` | Domain logic |
| Unit | `tests/Anvil.Application.Tests/` | Service orchestration |
| Integration | `tests/Anvil.Infrastructure.Tests/` | External I/O |

Use `NullLogger<T>.Instance` for logger dependencies in tests.

## Common Patterns

### Creating a Service

```csharp
public class StoryRunner(
    IStorySource source,
    IStoryExecutor executor,
    ILogger<StoryRunner> logger)
{
    public async Task<Result<StoryResult, AnvilError>> RunAsync(
        string path,
        CancellationToken ct = default)
    {
        logger.LogInformation("Running story {Path}", path);
        
        return
            from story in await source.LoadAsync(path)
            from output in await executor.ExecuteAsync(story, ct)
            select new StoryResult(story.Id, output.IsSuccess, output.Duration);
    }
}
```

### Error Handling

```csharp
public abstract record AnvilError(string Message)
{
    public record StoryNotFound(string Path) 
        : AnvilError($"Story not found: {Path}");
    
    public record ExecutionFailed(string StoryId, string Reason)
        : AnvilError($"Story {StoryId} execution failed: {Reason}");
}
```

### Registration

```csharp
services.AddSingleton<IStorySource, FileStorySource>();
services.AddSingleton<IStoryExecutor, AuraApiExecutor>();
services.AddSingleton<StoryRunner>();
```

## When Starting a New Feature

1. **Read** `.project/architecture/project.md` for context
2. **Check** relevant ADRs for design decisions
3. **Follow** the SDD workflow (Research → Plan → Implement → Validate)
4. **Use** prompts in `.sdd/prompts/` for each phase

## Using Templates

Templates for all SDD artifacts are in `.sdd/templates/`:

| Template | Copy To | When |
|----------|---------|------|
| `VISION.md` | `.project/VISION.md` | Project bootstrap |
| `STATUS.md` | `.project/STATUS.md` | Project bootstrap |
| `adr-template.md` | `.project/adr/ADR-NNN-title.md` | New architecture decision |
| `backlog-item.md` | `.project/backlog/item-name.md` | New work item |
| `research.md` | `.project/research/research-{item}-{date}.md` | Research phase |
| `plan.md` | `.project/plans/plan-{item}-{date}.md` | Plan phase |
| `changes.md` | `.project/changes/changes-{item}-{date}.md` | Implement phase |
| `review.md` | `.project/reviews/review-{item}-{date}.md` | Verify phase |
| `handoff.md` | `.project/handoffs/handoff-{context}-{date}.md` | Session handoff |
| `commit-message.txt` | Git config | Agent-attributed commits |

