# Aura Rewrite Specification

**Version:** 2.1  
**Status:** ✅ Complete (Core Features)  
**Last Updated:** 2025-12-12

## Executive Summary

Aura is a **local-first, privacy-safe AI foundation** for personal knowledge work. Think of it as **"Windows Recall, but local and safe"** - your data never leaves your machine.

At its core, Aura provides:
- **Local LLM** - Ollama running on your GPU/CPU
- **Local RAG** - Index and query your files, code, documents, receipts
- **Local Database** - PostgreSQL storing everything locally
- **Local Agents** - Hot-reloadable capabilities for any domain

The **first vertical application** is a **developer automation system**. But the same foundation can power research assistants, financial tracking, personal knowledge management - anything you want to index and query locally.

## The Local-First Promise

```
┌─────────────────────────────────────────────────────────────┐
│                     YOUR MACHINE                             │
│                                                              │
│  ┌─────────────────────────────────────────────────────┐    │
│  │                    Aura                              │    │
│  │  ┌─────────┐  ┌─────────┐  ┌─────────┐              │    │
│  │  │ Ollama  │  │Postgres │  │   RAG   │              │    │
│  │  │ (GPU)   │  │  (Data) │  │ (Index) │              │    │
│  │  └─────────┘  └─────────┘  └─────────┘              │    │
│  │                                                      │    │
│  │  Your code, your documents, your receipts,          │    │
│  │  your papers, your notes - indexed and queryable    │    │
│  │  WITHOUT ever leaving your machine.                 │    │
│  └─────────────────────────────────────────────────────┘    │
│                                                              │
│  ❌ No cloud uploads    ❌ No telemetry    ❌ No API keys   │
│  ✅ Works offline       ✅ You own it      ✅ Private       │
└─────────────────────────────────────────────────────────────┘
```

## Architecture Layers

```text
┌─────────────────────────────────────────────────────────────┐
│                    Vertical Applications                     │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐       │
│  │ Dev Workflow │  │   Research   │  │   Personal   │  ...  │
│  │    Agents    │  │   Assistant  │  │   Finance    │       │
│  └──────────────┘  └──────────────┘  └──────────────┘       │
├─────────────────────────────────────────────────────────────┤
│                   Aura Foundation (LOCAL)                    │
│  ┌────────────┐  ┌────────────┐  ┌────────────┐             │
│  │   Agent    │  │  Ollama    │  │    RAG     │             │
│  │  Registry  │  │  (Local)   │  │  (Local)   │             │
│  └────────────┘  └────────────┘  └────────────┘             │
│  ┌────────────┐  ┌────────────┐  ┌────────────┐             │
│  │ PostgreSQL │  │   Tools    │  │    Git     │             │
│  │  (Local)   │  │  Registry  │  │  Worktrees │             │
│  └────────────┘  └────────────┘  └────────────┘             │
├─────────────────────────────────────────────────────────────┤
│                     .NET Runtime                             │
│            Windows • macOS • Linux                           │
└─────────────────────────────────────────────────────────────┘
```

## Core Principles

### 1. Local-First, Privacy-Safe

**Your data NEVER leaves your machine.**

| Component | Local Implementation |
|-----------|---------------------|
| **LLM** | Ollama on your GPU/CPU |
| **Database** | PostgreSQL in Docker or native |
| **RAG Index** | Local embeddings, local vector store |
| **File Access** | Direct filesystem, no cloud sync |
| **Git** | Local repos, local worktrees |

Optional cloud integration (user opt-in only):
- Remote LLM providers (Azure OpenAI, Anthropic) for users who want it
- GitHub/Azure DevOps sync for issue tracking
- Never required, always optional

### 2. Cross-Platform

- Runs wherever .NET runs: **Windows, macOS, Linux**
- No platform-specific dependencies in core
- Platform-specific features (tray icon, service) are optional extensions
- Single codebase, multiple deployment targets

### 3. Foundation + Verticals

The system is layered:

- **Foundation**: RAG, LLM, Database, Agent Registry, Tools
- **Verticals**: Domain-specific agent collections

The foundation is general-purpose:

- Index any content (code, documents, receipts, papers)
- Query with natural language
- Execute agent-driven workflows
- Store and retrieve structured data

Verticals are pluggable agent sets:

- **Developer**: Coding, testing, documentation, Git, PR management
- **Research**: Paper indexing, synthesis, citation management
- **Financial**: Receipt parsing, expense tracking, budget analysis
- **Custom**: User-defined agents for any domain

### 4. Everything is an Agent

Every capability in the system is implemented as an agent:

- **Coding agents** - Generate and modify code (Roslyn, Python, TypeScript, etc.)
- **Analysis agents** - Business analysis, requirements breakdown
- **Infrastructure agents** - Git operations, build monitoring, PR management
- **Ingestion agents** - RAG pipeline, codebase indexing, documentation parsing
- **Integration agents** - GitHub sync, Azure DevOps sync (optional)
- **Domain agents** - Research assistant, financial tracker, etc.

### 5. Hot-Reloadable

- Add/remove agents without restart
- File-based agent definitions (Markdown)
- API-based agent registration
- Folder watching for automatic reload
- Drop new agent files → immediately available

### 6. Human-in-the-Loop (HITL)

- Users control the workflow
- Review and approve each step
- Override agent selection
- Chat-augmented interaction
- Autonomous mode optional per-agent

### 7. Composable Modules

**No fixed SKUs. Mix and match what you need.**

- Foundation is always loaded (LLM, RAG, DB, agents, tools)
- Modules are independent and optional
- Enable modules via config, not code changes
- Zero coupling between modules
- Research module doesn't drag in coding agents

### 8. Simplicity

- Single library DLL for core functionality
- Single host binary (runs as console, service, or Aspire-managed)
- Minimal abstraction layers
- Direct, explicit API contracts
- No over-engineering

## System Components

| Component | Description |
|-----------|-------------|
| **Aura.Foundation** | Core library - agents, LLM, RAG, database, tools (always loaded) |
| **Aura.Module.Developer** | Developer module - workflows, git, coding agents (optional) |
| **Aura.Module.Research** | Research module - papers, synthesis, citations (optional) |
| **Aura.Module.Personal** | Personal module - receipts, budget, general assistant (optional) |
| **Aura.Api** | API service - loads enabled modules |
| **Aura.AppHost** | Aspire host - orchestrates local infrastructure |
| **VS Code Extension** | Developer UI - workflow management |

## Module Configuration

Users enable modules they need:

```json
{
  "Aura": {
    "Modules": {
      "Enabled": ["developer", "research"]
    }
  }
}
```

Or via command line:

```bash
dotnet run -- --Aura:Modules:Enabled:0=research --Aura:Modules:Enabled:1=personal
```

## Deployment Targets

| Platform | Console | Service | Tray App |
|----------|---------|---------|----------|
| **Windows** | ✅ | ✅ Windows Service | ✅ WinForms/WPF |
| **macOS** | ✅ | ✅ launchd | ✅ (future) |
| **Linux** | ✅ | ✅ systemd | ❌ |

## Related Specifications

- [01-agents.md](01-agents.md) - Agent architecture and contracts
- [02-llm-providers.md](02-llm-providers.md) - LLM provider abstraction
- [03-data-model.md](03-data-model.md) - Database schema and entities
- [04-api-endpoints.md](04-api-endpoints.md) - REST API contract
- [05-git-worktrees.md](05-git-worktrees.md) - Git worktree management
- [06-extension.md](06-extension.md) - VS Code extension design
- [07-testing.md](07-testing.md) - Testing strategy
- [08-foundation.md](08-foundation.md) - Foundation layer architecture
- [09-aspire-architecture.md](09-aspire-architecture.md) - Aspire infrastructure orchestration
- [10-composable-modules.md](10-composable-modules.md) - Module system and composition

## Non-Goals (Explicit Exclusions)

- Multi-tenant SaaS deployment
- Centralized agent marketplace
- Real-time collaboration between users
- Mobile or web clients (v1)
- Platform-specific core dependencies
- Fixed SKUs (modules are mix-and-match)

## Implementation Status

All core components are complete and functional:

| Component | Status | Notes |
|-----------|--------|-------|
| **Aura.Foundation** | ✅ Complete | Agents, LLM, RAG, Data, Tools, Git, Shell, Prompts, Conversations |
| **Aura.Module.Developer** | ✅ Complete | Workflows, Roslyn tools, Graph RAG, ReAct executor, TreeSitter ingesters |
| **Aura.Api** | ✅ Complete | All endpoints for Foundation + Developer + Assisted Workflow |
| **Aura.AppHost** | ✅ Complete | Aspire orchestration with PostgreSQL + Ollama |
| **VS Code Extension** | ✅ Complete | Full workflow UI with assisted collaboration |
| **Tests** | ✅ 205+ passing | Unit + integration (Testcontainers) |

### Developer Module Features

| Feature | Status |
|---------|--------|
| Workflow lifecycle (Create → Analyze → Plan → Execute → Complete) | ✅ |
| WorkflowService with all operations | ✅ |
| Git worktree integration | ✅ |
| RAG auto-indexing of worktrees | ✅ |
| Chat-based plan modification | ✅ |
| Prompt externalization (Handlebars) | ✅ |
| Extension UI for workflows | ✅ |
| Assisted Workflow UI | ✅ |
| Step Management (approve/reject/skip/chat) | ✅ |
| Iterative Refinement with Cascade Rework | ✅ |

### Project Structure (Final)

```text
src/
├── Aura.Foundation/              # Core services (always loaded)
│   ├── Agents/                   # Agent registry, loading, execution
│   ├── Llm/                      # LLM abstraction (Ollama + cloud providers)
│   ├── Rag/                      # Local RAG pipeline, TreeSitter ingesters
│   ├── Data/                     # EF Core, foundation entities
│   ├── Tools/                    # Tool registry
│   └── Modules/                  # Module loader, IAuraModule
│
├── Aura.Module.Developer/        # Developer module
│   ├── DeveloperModule.cs        # IAuraModule implementation
│   ├── Services/                 # Workflow, git worktree
│   ├── Tools/                    # Roslyn, git tools
│   └── Data/                     # Developer entities
│
├── Aura.Api/                     # API host (all endpoints in Program.cs)
├── Aura.AppHost/                 # Aspire orchestration
└── Aura.ServiceDefaults/         # Shared Aspire configuration
```
