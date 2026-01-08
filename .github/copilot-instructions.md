# Aura Project - Copilot Instructions

> **Read First**: `.project/STATUS.md` for current project state and feature inventory.

## Primary Goal

**Build Aura as a production-ready product**, not just fix local development issues. Every change should consider:

- Will this work for all users on a clean install?
- Is the UX clear without requiring technical knowledge?
- Are edge cases handled gracefully?
- Is this documented for future maintainers?

When encountering issues, fix them properly in the product—don't apply quick workarounds that only help the current developer.

## Core Principles

1. **NEVER implement without a spec** - All changes require documented requirements and context
2. **Design before coding** - Seek approval before implementing; prefer planning over spontaneous coding
3. **User controls the server** - Never start/stop the API server; user runs `Start-Api` manually
4. **Document all decisions** - Update `.project/STATUS.md` after significant changes
5. **Complete features properly** - Follow the ceremony in `.github/prompts/aura.complete-feature.prompt.md`
6. **Product-first mindset** - Fix issues for all users, not just the current environment

## Quick Context

Aura is a **local-first, privacy-safe AI foundation** for knowledge work. The Developer Module MVP is **complete** with full workflow UI, multi-language code indexing, and cloud LLM support.

## Key Documents (Source of Truth)

| Document | Purpose |
|----------|---------|
| `.project/STATUS.md` | **Start here** - Current state, feature inventory, open items |
| `.project/features/README.md` | Feature index with completion dates |
| `.project/reference/` | API cheat sheet, architecture, coding standards |
| `.project/adr/*.md` | Architecture decisions |
| `.github/prompts/aura.complete-feature.prompt.md` | **Ceremony for completing features** |

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
| New feature | `.project/features/upcoming/` | Create spec first |
| Complete feature | `.project/features/completed/` | **Follow ceremony** (see below) |

## Feature Completion Ceremony

When a feature is complete, you MUST follow the ceremony in `.github/prompts/aura.complete-feature.prompt.md`:

1. Move file from `features/upcoming/` → `features/completed/`
2. Add header with `**Status:** ✅ Complete` and `**Completed:** YYYY-MM-DD`
3. Update `features/README.md` index with link and date
4. Commit with `docs(features): complete {feature-name}`

**Validation**: Run `.\scripts\Validate-Features.ps1` to check conventions.
This script can be installed as a pre-commit hook: `.\scripts\Validate-Features.ps1 -Install`

## Container Runtime

- **Windows**: Podman
- **macOS**: OrbStack

Both are Docker-compatible for Aspire.