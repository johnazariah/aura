# Implementation Plan Overview

**Version:** 2.1
**Status:** Complete (Core Features)
**Last Updated:** 2025-12-05

## Summary

This document outlines the implementation plan for the Aura rewrite. The goal is to replace the current 17-project, 38k+ line codebase with a clean **layered architecture**:

- **Foundation**: Cross-platform AI infrastructure (RAG, LLM, DB, agents)
- **Verticals**: Domain-specific agent collections (Developer is first vertical)

## Current Status

| Component | Status | Notes |
|-----------|--------|-------|
| **Aura.Foundation** | ✅ Complete | Agents, LLM, RAG, Data, Tools, Git, Shell, Prompts, Conversations |
| **Aura.Module.Developer** | ✅ Complete | Workflows, Roslyn tools, Graph RAG, ReAct executor, TreeSitter ingesters |
| **Aura.Api** | ✅ Complete | All endpoints for Foundation + Developer + Assisted Workflow |
| **Aura.AppHost** | ✅ Complete | Aspire orchestration with PostgreSQL + Ollama |
| **VS Code Extension** | ✅ Complete | Full workflow UI with assisted collaboration |
| **Tests** | ✅ 205+ passing | Unit + integration (Testcontainers) |

### Developer Module Silver Thread

| Feature | Status |
|---------|--------|
| Workflow lifecycle (Create → Analyze → Plan → Execute → Complete) | ✅ |
| WorkflowService with all operations | ✅ |
| Git worktree integration | ✅ |
| RAG auto-indexing of worktrees | ✅ |
| Chat-based plan modification | ✅ |
| Prompt externalization (Handlebars) | ✅ |
| **Extension UI for workflows** | ✅ Complete |
| **Assisted Workflow UI** | ✅ Complete |
| **Step Management (approve/reject/skip/chat)** | ✅ Complete |
| **Iterative Refinement with Cascade Rework** | ✅ Complete |

### Additional Features Implemented

| Feature | ADR | Status |
|---------|-----|--------|
| Capability-based Agent Routing | ADR-011 | ✅ |
| Tool-Using Agents (ReAct) | ADR-012 | ✅ |
| Strongly-typed Agent Contracts | ADR-013 | ✅ |
| Roslyn Tools (6 tools) | ADR-014 | ✅ |
| Graph RAG for Code | ADR-015 | ✅ |
| Configurable RAG Queries | ADR-016 | ✅ |
| Case-Insensitive Paths | ADR-017 | ✅ |
| Prompt Template Architecture | ADR-018 | ✅ |
| TreeSitter Multi-Language Ingesters | Spec-22 | ✅ |
| Semantic Enrichment (docstrings, imports, types) | Task | ✅ |
| Cloud LLM Providers (Azure OpenAI, OpenAI) | - | ✅ (needs spec) |

## Architecture Vision

```text
┌─────────────────────────────────────────────────────────────┐
│                    Vertical Applications                     │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐       │
│  │ Dev Workflow │  │   Research   │  │   Financial  │  ...  │
│  │    Agents    │  │   Assistant  │  │   Tracker    │       │
│  └──────────────┘  └──────────────┘  └──────────────┘       │
├─────────────────────────────────────────────────────────────┤
│                      Aura Foundation                         │
│  Agent Registry │ LLM Providers │ RAG │ Database │ Tools    │
├─────────────────────────────────────────────────────────────┤
│              .NET 9 (Windows • macOS • Linux)                │
└─────────────────────────────────────────────────────────────┘
```

## Project Structure

```text
src/
├── Aura.Foundation/              # Core services (always loaded)
│   ├── Agents/                   # Agent registry, loading, execution
│   ├── Llm/                      # LLM abstraction (local Ollama)
│   ├── Rag/                      # Local RAG pipeline
│   ├── Data/                     # EF Core, foundation entities
│   ├── Tools/                    # Tool registry
│   └── Modules/                  # Module loader, IAuraModule
│
├── Aura.Module.Developer/        # Developer module (optional)
│   ├── DeveloperModule.cs        # IAuraModule implementation
│   ├── Services/                 # Workflow, git worktree
│   ├── Tools/                    # Roslyn, git tools
│   ├── Agents/                   # Coded agents
│   └── Endpoints/                # /api/dev/*
│
├── Aura.Module.Research/         # Research module (optional, future)
├── Aura.Module.Personal/         # Personal module (optional, future)
│
├── Aura.Api/                     # API host
│   └── Program.cs                # Module loader
│
└── Aura.AppHost/                 # Aspire AppHost
    └── Program.cs                # Local infrastructure
```

See [10-composable-modules.md](../../spec/10-composable-modules.md) for module system details.

## Cross-Platform Requirement

**Aura must run wherever .NET runs: Windows, macOS, Linux.**

Implementation rules:

1. **No P/Invoke** in Foundation or Module libraries
2. **No platform-specific paths** - use `Path.Combine()` everywhere
3. **Abstract file system** - use `System.IO.Abstractions` for testability
4. **Shell detection** - auto-detect bash vs cmd vs pwsh
5. **Service hosting** - use generic host pattern (works as console/service/daemon)

## Implementation Phases

| Phase | Focus | Duration | Dependency |
|-------|-------|----------|------------|
| [Phase 1](01-core-infrastructure.md) | Foundation: Agent registry + module loader | 2-3 hours | None |
| [Phase 2](02-llm-providers.md) | Foundation: LLM abstraction (Ollama) | 1-2 hours | Phase 1 |
| [Phase 3](03-data-layer.md) | Foundation: Database | 1-2 hours | Phase 1 |
| [Phase 4](04-api-endpoints.md) | API + AppHost | 2-3 hours | Phases 1-3 |
| [Phase 5](05-git-worktrees.md) | Module.Developer: Git, workflows | 1-2 hours | Phase 4 |
| [Phase 6](06-extension.md) | VS Code extension | 3-4 hours | Phase 4 |
| [Phase 7](07-migration.md) | Migration and cleanup | 2-3 hours | All |

**Total estimated time:** 12-19 hours

## Module System

**No fixed SKUs. Modules are mix-and-match.**

Enable modules via config:

```json
{
  "Aura": {
    "Modules": {
      "Enabled": ["developer"]
    }
  }
}
```

Or command line:

```bash
# Just research, no developer
dotnet run -- --Aura:Modules:Enabled:0=research

# Research + personal
dotnet run -- --Aura:Modules:Enabled:0=research --Aura:Modules:Enabled:1=personal
```

Each module is completely independent - Research doesn't drag in Developer.

## Migration Strategy

We're doing a **parallel implementation**, not an in-place refactor:

1. Create new `Aura.Foundation/`, `Aura.Vertical.Developer/`, `Aura.Api/`, `Aura.AppHost/` projects
2. Implement core functionality fresh
3. Port battle-tested code (git worktrees) with simplification
4. Update extension to use new API
5. Delete old projects when new system works

## What We Keep

From the current codebase, we port:

| Component | Source | Action |
|-----------|--------|--------|
| Git worktree logic | `AgentOrchestrator.Git` | Port and simplify |
| Markdown agent parser | `AgentOrchestrator.Agents` | Port with cleanup |
| Ollama client | `AgentOrchestrator.AI` | Port, add interface |
| Roslyn agent | `AgentOrchestrator.Roslyn` | Port as coded agent |
| Agent definitions | `agents/*.md` | Keep as-is |

## What We Delete

| Component | Reason |
|-----------|--------|
| `AgentOrchestrator.Orchestration` | Over-engineered, replaced by simple execution |
| `IExecutionPlanner`, `IWorkflowOrchestrator` | Not needed for HITL model |
| `AgentOrchestrator.AgentService` | gRPC service not used |
| `AgentOrchestrator.Tray` | Separate concern, later |
| `AgentOrchestrator.Installer` | Separate concern, later |
| Complex validation loop | Simplified to step retry |

## Definition of Done

Each phase is complete when:

1. ✅ Code compiles on all platforms (or CI validates)
2. ✅ Unit tests pass
3. ✅ Integration tests pass (where applicable)
4. ✅ Manual smoke test works
5. ✅ No platform-specific code in core library

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Breaking existing workflows | Parallel implementation, don't touch old code until new works |
| Missing edge cases | Port tests alongside code |
| Scope creep | Strict phase boundaries |
| Integration issues | Test extension against new API early (Phase 4) |
| Platform-specific bugs | CI matrix: Windows + macOS + Linux |

## Success Criteria

The rewrite is successful when:

1. **Fewer projects:** 2 instead of 17
2. **Less code:** < 5,000 lines instead of 38,000
3. **All tests pass:** Unit, integration, extension
4. **Extension works:** 3-phase workflow functional
5. **Agents work:** Hot-reload, capability matching, execution
6. **Git works:** Worktree creation, commits, cleanup
7. **Cross-platform:** Runs on Windows, macOS, Linux

## Future Verticals (Post-v1)

Once the Developer vertical is stable, the foundation enables:

| Vertical | Description | Key Agents |
|----------|-------------|------------|
| **Research** | Academic paper management | paper-indexer, synthesis, citations |
| **Financial** | Personal finance tracking | receipt-parser, expense-tracker, budget |
| **Notes** | Knowledge management | note-indexer, linker, summarizer |
| **Custom** | User-defined | drop markdown files in folder |

## Next Steps

1. Review specs with stakeholders
2. Create `Aura/` and `Aura.Host/` project scaffolds
3. Begin Phase 1: Core Infrastructure (cross-platform from day 1)
