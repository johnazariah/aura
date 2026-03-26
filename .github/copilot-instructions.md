# Aura - Copilot Instructions

Aura is a local indexing platform plus MCP server. It indexes codebases and documents locally (Roslyn, TreeSitter, pgvector RAG, PDF ingestion) and exposes tools via MCP for GitHub Copilot. Embeddings come from configurable providers such as OpenAI and Ollama.

> Read `.project/STATUS.md` for current project state and feature inventory.
> Read `.project/reference/coding-standards.md` for full coding standards.

## Build, Test, and Lint

```powershell
# Build
dotnet build

# Run all unit tests (excludes integration tests)
.\scripts\Run-UnitTests.ps1
# or directly:
dotnet test --filter "FullyQualifiedName!~IntegrationTests"

# Run a single test class
dotnet test --filter "FullyQualifiedName~TokenTrackerTests"

# Run a single test method
dotnet test --filter "FullyQualifiedName~TokenTrackerTests.TrackUsage_AddsTokens"

# Lint / format check
dotnet format --verify-no-changes

# Test the running API
curl http://localhost:5300/health
```

**CI** (`.github/workflows/ci.yml`): Runs on push/PR to main. Builds and tests on Ubuntu and Windows. Integration tests require PostgreSQL (pgvector) and only run on Ubuntu.

**Pre-push hook**: `.githooks/pre-push.ps1` runs build, unit tests, and `dotnet format` before push. Install with `.\scripts\Install-GitHooks.ps1`.

## Architecture

```
src/
├── Aura.Foundation/          # Core: embeddings, RAG, data, shell, git
├── Aura.Module.Developer/    # Roslyn + multi-language code intelligence
├── Aura.Module.Researcher/   # PDF and document ingestion
├── Aura.Api/                 # HTTP + MCP host (endpoints + MCP handlers)
├── Aura.ServiceDefaults/     # Shared service configuration
└── Aura.Tray/                # System tray app

patterns/                     # Operational patterns for complex tasks
```

### Key architectural patterns

- **API endpoints**: All defined via `Map*Endpoints()` extension methods called from `src/Aura.Api/Program.cs`. Each endpoint group is in its own file under `src/Aura.Api/Endpoints/`.
- **MCP handlers**: `src/Aura.Api/Mcp/McpHandler.cs` is a partial class split by domain (`.Generate.cs`, `.Index.cs`, `.Inspect.cs`, `.Navigate.cs`, `.Refactor.cs`, `.Search.cs`, `.Tree.cs`, `.Validate.cs`, `.Workspaces.cs`).
- **DI registration**: Each project exposes an `Add{Module}()` extension method on `IServiceCollection` (e.g., `AddAuraFoundation()`, `AddDeveloperModule()`). These chain sub-registrations internally.
- **Module pattern**: `Aura.Module.Developer` and `Aura.Module.Researcher` are vertical slices that register their own services and ingestors independently.
- **Data layer**: EF Core with `AuraDbContext` in `Aura.Foundation/Data/`. Migrations are code-first.

### Service deployment

Aura runs as a **Windows Service** deployed to `C:\Program Files\Aura`. Use `scripts\Deploy-Dev.ps1` for local redeploys when running elevated.

Logs are at `C:\ProgramData\Aura\logs\aura-YYYYMMDD.log`.

## Conventions

### C# (.NET 10, C# latest)

- **Nullable reference types enabled** globally (`Directory.Build.props`)
- **Warnings as errors** — all warnings must be resolved
- **Records for DTOs** — immutable by default, use `required` properties over long constructors
- **Primary constructors** for DI injection (C# 12 style)
- **`nameof()`** — never string literals for member/parameter names
- **Strongly-typed** — no `Dictionary<string, object>` for known schemas; use typed records
- **Enums over string constants** for closed sets of values
- **Result pattern** for expected errors, not exceptions
- **One type per file**, namespace matches folder path

### Testing

- **Framework**: xUnit with `[Fact]` and `[Theory]`/`[InlineData]`
- **Assertions**: FluentAssertions (`result.Should().BeTrue()`)
- **Mocking**: NSubstitute (`Substitute.For<IService>()`)
- **Loggers in tests**: Use `NullLogger<T>.Instance`, not `Substitute.For<ILogger>()`
- **Naming**: `{ClassName}Tests` for class, `{Method}_{Scenario}_{Expected}` for methods
- **Test projects mirror source**: `tests/Aura.Foundation.Tests/` → `src/Aura.Foundation/`

### File formatting

- **LF line endings** everywhere (`.editorconfig`). The pre-commit hook rejects CRLF.
- **UTF-8** without BOM, final newline required
- **4 spaces** for C#; **2 spaces** for JSON, YAML, csproj

### Feature lifecycle

Features are tracked in `.project/features/`. When completing a feature, follow the ceremony in `.github/prompts/aura.complete-feature.prompt.md`: move from `upcoming/` to `completed/`, add completion header, update the index, and commit with `docs(features): complete {name}`.

### Container runtime

- **Windows**: Podman
- **macOS**: OrbStack

Both are Docker-compatible for Aspire orchestration.