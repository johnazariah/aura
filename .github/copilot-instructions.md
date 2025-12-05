# Aura Project - Copilot Instructions

## Project Overview

Aura is a **local-first, privacy-safe AI foundation** for knowledge work. Think of it as "Windows Recall, but local and safe" - all data stays on the user's machine.

## Core Principles

1. **Local-First, Privacy-Safe** - No cloud uploads, no telemetry, works offline
2. **Cross-Platform** - Windows, macOS, Linux (wherever .NET runs)
3. **Composable Modules** - Mix-and-match capabilities, no fixed SKUs
4. **Hot-Reloadable Agents** - Add agents by dropping markdown files
5. **Human-in-the-Loop** - Users control workflow execution

## Architecture

```
Aura.Foundation (always loaded)
    ├── Agents/       - Agent registry, loading, execution
    ├── Llm/          - Local LLM via Ollama
    ├── Rag/          - Local RAG pipeline
    ├── Data/         - PostgreSQL via EF Core
    ├── Tools/        - Tool registry
    └── Modules/      - Module loader (IAuraModule)

Aura.Module.Developer (optional)
    ├── Workflows, git worktrees, coding agents

Aura.Module.Research (optional, future)
    ├── Paper indexing, synthesis, citations

Aura.Module.Personal (optional, future)
    ├── Receipts, budgets, general assistant

Aura.Api
    ├── REST API, loads enabled modules

Aura.AppHost
    ├── Aspire orchestration (PostgreSQL, Ollama, API)
```

## Implementation Status

### Completed
- [x] GitHub repo created (johnazariah/aura)
- [x] Solution scaffolded (Aura.sln)
- [x] Directory.Build.props configured
- [x] .editorconfig, .gitignore in place
- [x] Specs and plans copied to .project/
- [x] CI/CD pipeline (.github/workflows/ci.yml)
- [x] README and LICENSE

### In Progress
- [ ] **Phase 1: Core Infrastructure** (.project/plan/implementation/01-core-infrastructure.md)
  - [ ] Aura.Foundation project
  - [ ] Agent interfaces (IAgent, IAgentRegistry)
  - [ ] Agent loading (markdown parser)
  - [ ] Module system (IAuraModule, ModuleLoader)

### Not Started
- [ ] Phase 2: LLM Providers (Ollama)
- [ ] Phase 3: Data Layer (PostgreSQL, EF Core)
- [ ] Phase 4: API + AppHost
- [ ] Phase 5: Developer Module (git worktrees, workflows)
- [ ] Phase 6: VS Code Extension
- [ ] Phase 7: Migration from hve-hack

## Key Files

| File | Purpose |
|------|---------|
| `.project/spec/00-overview.md` | System overview and principles |
| `.project/spec/10-composable-modules.md` | Module system design |
| `.project/plan/implementation/01-core-infrastructure.md` | Current phase implementation guide |
| `src/Aura.Foundation/` | Core library (in progress) |

## Coding Standards

Follow the standards from hve-hack (see `c:\work\hve-hack\docs\CODING-STANDARDS.md`):

1. **Strongly-typed over stringly-typed** - No `Dictionary<string, object>` for known schemas
2. **Use `nameof()`** - Never string literals for member names
3. **Enums over string constants** - For closed value sets
4. **Result<T> for expected errors** - Not exceptions
5. **Immutability by default** - Records, init-only properties
6. **Nullable reference types** - Handle nulls explicitly

## Module Contract

```csharp
public interface IAuraModule
{
    string ModuleId { get; }
    string Name { get; }
    string Description { get; }
    IReadOnlyList<string> Dependencies => [];  // Should be empty!
    
    void ConfigureServices(IServiceCollection services, IConfiguration config);
    void RegisterAgents(IAgentRegistry registry, IConfiguration config);
}
```

**Critical: Modules must NOT depend on each other. Only on Foundation.**

## Configuration

```json
{
  "Aura": {
    "Modules": {
      "Enabled": ["developer"]
    }
  }
}
```

## Next Steps

1. Continue Phase 1: Create Aura.Foundation with:
   - `Agents/IAgent.cs` - Agent contract
   - `Agents/AgentMetadata.cs` - Agent definition
   - `Agents/IAgentRegistry.cs` - Registry interface
   - `Agents/AgentRegistry.cs` - Implementation
   - `Agents/MarkdownAgentLoader.cs` - Parse .md agent files
   - `Modules/IAuraModule.cs` - Module contract
   - `Modules/ModuleLoader.cs` - Module discovery

2. Reference the existing hve-hack code for patterns:
   - `c:\work\hve-hack\src\AgentOrchestrator.Agents\` - Agent parsing
   - `c:\work\hve-hack\src\AgentOrchestrator.Core\` - Core interfaces
   - `c:\work\hve-hack\agents\*.md` - Agent markdown format

3. After Phase 1 compiles, move to Phase 2 (LLM providers).

## Reference: Old Codebase

The old codebase is at `c:\work\hve-hack\` for reference:
- 17 projects, ~38k lines (over-engineered)
- Good code to port: Git worktree service, agent markdown parser, Ollama client
- Delete: Orchestration layer, execution planner, complex validation

## Commands

```bash
# Build
dotnet build

# Test
dotnet test

# Run (after AppHost is created)
dotnet run --project src/Aura.AppHost
```

## Development Environment

**Container Runtime** (README says "Docker" for discoverability, but we use):
- **Windows**: Podman
- **macOS**: OrbStack

Both are Docker-compatible, so Aspire works seamlessly with either.

## Quick Reference

**READ FIRST**: See `.project/ARCHITECTURE-QUICK-REFERENCE.md` for:
- API endpoint reference (all endpoints are in `src/Aura.Api/Program.cs`)
- Key file locations
- Common debugging commands
- Configuration locations
- RAG and prompt architecture
