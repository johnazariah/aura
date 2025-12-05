# Aura Project - Copilot Instructions

> **Read First**: `.project/STATUS.md` for current project state and feature inventory.

## Core Principles

1. **NEVER implement without a spec** - All changes require documented requirements and context
2. **Design before coding** - Seek approval before implementing; prefer planning over spontaneous coding
3. **User controls the server** - Never start/stop the API server; user runs `Start-Api` manually
4. **Document all decisions** - Update `.project/STATUS.md` after significant changes

## Quick Context

Aura is a **local-first, privacy-safe AI foundation** for knowledge work. The Developer Module MVP is **complete** with full workflow UI, multi-language code indexing, and cloud LLM support.

## Key Documents (Source of Truth)

| Document | Purpose |
|----------|---------|
| `.project/STATUS.md` | **Start here** - Current state, feature inventory, open items |
| `.project/ARCHITECTURE-QUICK-REFERENCE.md` | API endpoints, file locations, debugging |
| `.project/spec/*.md` | Feature specifications (read before implementing) |
| `.project/adr/*.md` | Architecture decisions |
| `.project/standards/coding-standards.md` | Code style and patterns |

## Development Protocol

### What You CAN Do

```powershell
# Test API (server must be running)
curl http://localhost:5300/health
curl http://localhost:5300/api/developer/workflows

# Build extension after changes
.\scripts\Build-Extension.ps1

# Run tests
.\scripts\Run-UnitTests.ps1
dotnet test

# Build solution
dotnet build
```

### What You Must NOT Do

```powershell
# NEVER run these - user controls the server
.\scripts\Start-Api.ps1
dotnet run --project src/Aura.AppHost
```

### Workflow

1. **Understand** - Read relevant spec/ADR before making changes
2. **Propose** - Describe planned changes and get approval
3. **Implement** - Make code changes
4. **Build** - If extension changed → `Build-Extension.ps1`
5. **Ask for restart** - If server code changed → ask user to run `Start-Api`
6. **Test** - Use curl to verify
7. **Document** - Update STATUS.md if needed

## Project Structure

```
src/
├── Aura.Foundation/          # Core: agents, LLM, RAG, data, tools
├── Aura.Module.Developer/    # Developer vertical: workflows, git, Roslyn
├── Aura.Api/                 # API host (all endpoints in Program.cs)
└── Aura.AppHost/             # Aspire orchestration

extension/src/                # VS Code extension
agents/                       # Markdown agent definitions
prompts/                      # Handlebars prompt templates
tests/                        # Unit and integration tests
.project/                     # Specs, ADRs, status, standards
```

## Coding Standards

See `.project/standards/coding-standards.md`. Key rules:

- **Strongly-typed** - No `Dictionary<string, object>` for known schemas
- **Use `nameof()`** - Never string literals for member names
- **Records for DTOs** - Immutable by default
- **Nullable reference types** - Handle nulls explicitly

## When Making Changes

| Change Type | Location | Notes |
|-------------|----------|-------|
| API endpoints | `src/Aura.Api/Program.cs` | Single file for all endpoints |
| Agent behavior | `agents/*.md` or `prompts/*.prompt` | Hot-reloadable |
| Extension UI | `extension/src/providers/` | Run Build-Extension after |
| Documentation | `.project/STATUS.md` | Keep current |

## Container Runtime

- **Windows**: Podman
- **macOS**: OrbStack

Both are Docker-compatible for Aspire.