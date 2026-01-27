# Aura Overview Specification

**Status:** Reference Documentation
**Created:** 2026-01-28

## Executive Summary

Aura is a **local-first, privacy-safe AI foundation** for knowledge work. The core promise is that user data never leaves their machine unless explicitly configured for cloud LLM providers.

### Primary Use Case

Developer automation: a VS Code extension that helps developers implement features by:
1. Creating isolated git worktrees for each task
2. Indexing the codebase for semantic search and code navigation
3. Using AI agents to analyze, plan, and execute development steps
4. Providing human-in-the-loop review before code changes

### System Components

```
┌─────────────────────────────────────────────────────────────────┐
│                      VS Code Extension                           │
│   ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐          │
│   │ Workflow │ │  Agent   │ │  Status  │ │ Welcome  │          │
│   │   Tree   │ │   Tree   │ │   Tree   │ │   View   │          │
│   └──────────┘ └──────────┘ └──────────┘ └──────────┘          │
│   ┌───────────────────────┐ ┌───────────────────────┐          │
│   │    Workflow Panel     │ │     Chat Window       │          │
│   │   (Step management)   │ │   (Agent chat)        │          │
│   └───────────────────────┘ └───────────────────────┘          │
├─────────────────────────────────────────────────────────────────┤
│                         Aura API                                 │
│   ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐          │
│   │  REST    │ │   MCP    │ │   SSE    │ │  Health  │          │
│   │ Endpoints│ │  Server  │ │ Streaming│ │  Checks  │          │
│   └──────────┘ └──────────┘ └──────────┘ └──────────┘          │
├─────────────────────────────────────────────────────────────────┤
│                    Developer Module                              │
│   ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐          │
│   │  Story   │ │  Roslyn  │ │   Code   │ │ Guardians│          │
│   │ Service  │ │  Tools   │ │  Graph   │ │          │          │
│   └──────────┘ └──────────┘ └──────────┘ └──────────┘          │
├─────────────────────────────────────────────────────────────────┤
│                    Aura Foundation                               │
│   ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐          │
│   │  Agents  │ │   LLM    │ │   RAG    │ │  Tools   │          │
│   │ Registry │ │ Providers│ │  Index   │ │ Registry │          │
│   └──────────┘ └──────────┘ └──────────┘ └──────────┘          │
│   ┌──────────┐ ┌──────────┐ ┌──────────┐                       │
│   │  Git     │ │ Prompts  │ │Convers-  │                       │
│   │ Service  │ │ Registry │ │ations    │                       │
│   └──────────┘ └──────────┘ └──────────┘                       │
├─────────────────────────────────────────────────────────────────┤
│                        Data Layer                                │
│   ┌────────────────────────────────────────────────────────┐    │
│   │     PostgreSQL 15+ with pgvector extension             │    │
│   │  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐       │    │
│   │  │ Foundation  │ │  Developer  │ │   Vector    │       │    │
│   │  │   Tables    │ │   Tables    │ │   Indexes   │       │    │
│   │  └─────────────┘ └─────────────┘ └─────────────┘       │    │
│   └────────────────────────────────────────────────────────┘    │
├─────────────────────────────────────────────────────────────────┤
│                       LLM Providers                              │
│   ┌──────────┐ ┌──────────────┐ ┌──────────┐ ┌──────────┐      │
│   │  Ollama  │ │ Azure OpenAI │ │  OpenAI  │ │   Stub   │      │
│   │ (Local)  │ │  (Optional)  │ │(Optional)│ │ (Testing)│      │
│   └──────────┘ └──────────────┘ └──────────┘ └──────────┘      │
└─────────────────────────────────────────────────────────────────┘
```

## Core Principles

### 1. Local-First, Privacy-Safe

**Data never leaves the machine by default:**
- Ollama runs LLMs locally on GPU/CPU
- PostgreSQL runs locally via container or native install
- RAG embeddings are generated and stored locally
- No telemetry, no cloud uploads without explicit opt-in

**Optional cloud integrations (user-configured):**
- Azure OpenAI or OpenAI for cloud LLM
- GitHub API for issue integration

### 2. Hot-Reloadable Agents

Agents are defined as markdown files in the `agents/` directory:
- Drop a new `.md` file → agent is immediately available
- Edit an agent file → changes take effect on next request
- No server restart required

### 3. Human-in-the-Loop (HITL)

The "Assisted" mode (default) requires user approval at each step:
- User reviews proposed changes
- Can approve, reject, skip, or modify
- Can chat with the agent about the step
- Can reassign to a different agent

Autonomous mode available for trusted operations.

### 4. Composable Modules

The system is modular:
- **Foundation** - Core services, always loaded
- **Developer Module** - Story management, Roslyn tools
- Future modules can be added without modifying core

### 5. MCP Integration

Aura exposes its capabilities to GitHub Copilot via the Model Context Protocol:
- Semantic code search
- Code graph navigation
- Refactoring operations
- Test generation

## Technology Choices

### .NET Aspire

Orchestrates the distributed components:
- PostgreSQL container management
- Service discovery
- Health checks
- Logging/telemetry aggregation

### Roslyn

Microsoft's C# compiler APIs provide:
- Semantic code analysis
- Symbol resolution
- Safe refactoring operations
- Compilation validation

### Tree-sitter

Multi-language parsing for:
- Code chunking for RAG
- Symbol extraction for code graph
- Languages: Python, TypeScript, Rust, Go, F#, etc.

### pgvector

PostgreSQL extension for vector similarity search:
- 1536-dimensional embeddings (OpenAI-compatible)
- HNSW indexes for fast similarity search
- Cosine distance for semantic matching

## Deployment Modes

### Development Mode

```powershell
.\scripts\Start-Api.ps1
```

- Aspire orchestrates PostgreSQL and Ollama containers
- Hot-reload enabled for agents
- Verbose logging to console

### Production Install (Windows)

```powershell
.\scripts\Update-LocalInstall.ps1  # Requires elevation
```

- Installs to `C:\Program Files\Aura`
- Runs as Windows Service
- Uses embedded PostgreSQL
- Logs to `C:\ProgramData\Aura\logs`

### macOS Development

```bash
./setup/install-mac.sh
./scripts/Start-Dev.ps1
```

- OrbStack for containers
- Native PostgreSQL option available

## Key Workflows

### 1. Workspace Onboarding

```
User opens VS Code in a codebase
  → Extension detects workspace
  → Shows "Onboard" button in Welcome view
  → User clicks "Onboard"
  → API creates Workspace entity
  → Background indexing starts (RAG + Code Graph)
  → Progress shown in Status view
  → Workspace is now searchable
```

### 2. Story Creation from GitHub Issue

```
User runs "Aura: Start Story from Issue"
  → Enters GitHub issue URL
  → API fetches issue details
  → Creates Story entity with issue metadata
  → Creates git worktree and branch
  → Opens new VS Code window in worktree
  → Extension shows Story panel
```

### 3. Step Execution (Assisted Mode)

```
Story has steps from planning phase
  → User clicks "Execute" on step
  → Agent runs with ReAct loop
  → Tools invoked as needed (file.write, roslyn.*, etc.)
  → Step output shown in panel
  → Step marked "Needs Review"
  → User approves or rejects
  → If rejected, agent revises with feedback
```

### 4. MCP Tool Invocation

```
Copilot needs to understand codebase
  → Calls aura_search with query
  → Aura searches RAG + Code Graph
  → Returns relevant code snippets
  → Copilot uses context for generation
```

## File Organization

```
aura/
├── src/
│   ├── Aura.Foundation/           # Core services
│   │   ├── Agents/                # Agent framework
│   │   ├── Llm/                   # LLM providers
│   │   ├── Rag/                   # RAG pipeline + Code Graph
│   │   ├── Tools/                 # Tool framework
│   │   ├── Git/                   # Git operations
│   │   ├── Data/                  # EF Core entities
│   │   └── Prompts/               # Handlebars templates
│   ├── Aura.Module.Developer/     # Developer vertical
│   │   ├── Services/              # Story, Roslyn, Refactoring
│   │   ├── Tools/                 # Developer-specific tools
│   │   ├── Data/                  # Developer entities
│   │   └── Guardians/             # Background monitors
│   ├── Aura.Api/                  # HTTP API host
│   │   ├── Endpoints/             # REST endpoint handlers
│   │   ├── Mcp/                   # MCP JSON-RPC handler
│   │   └── Program.cs             # App startup
│   ├── Aura.AppHost/              # Aspire orchestration
│   └── Aura.Tray/                 # System tray app (optional)
├── extension/                      # VS Code extension
│   └── src/
│       ├── providers/             # Tree/panel providers
│       └── services/              # API client
├── agents/                         # Agent markdown definitions
│   └── languages/                 # YAML language configs
├── prompts/                        # Handlebars prompt templates
├── patterns/                       # Multi-step operation patterns
├── guardians/                      # YAML guardian definitions
└── tests/                          # Unit and integration tests
```

## Success Criteria for Rewrite

A feature-parity rewrite must support:

1. **Local LLM execution** via Ollama
2. **Local RAG** with pgvector embeddings
3. **Code Graph** for structural navigation
4. **Story lifecycle** with step-by-step execution
5. **Git worktree isolation** per story
6. **MCP server** for Copilot integration
7. **VS Code extension** with workflow UI
8. **Hot-reloadable agents** from markdown
9. **Handlebars prompt templates**
10. **Multi-language support** (C# via Roslyn, others via Tree-sitter)
