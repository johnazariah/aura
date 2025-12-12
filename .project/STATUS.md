# Aura Project Status

> **Last Updated**: 2025-12-11
> **Branch**: main (up to date with origin)
> **Overall Status**: ✅ MVP Complete

## Quick Summary

Aura is a **local-first, privacy-safe AI foundation** for knowledge work. The Developer Module MVP is complete with full workflow UI, multi-language code indexing, and cloud LLM support.

## Component Status

| Component | Status | Key Files |
|-----------|--------|-----------|
| **Aura.Foundation** | ✅ Complete | `src/Aura.Foundation/` |
| **Aura.Module.Developer** | ✅ Complete | `src/Aura.Module.Developer/` |
| **Aura.Api** | ✅ Complete | `src/Aura.Api/Program.cs` |
| **Aura.AppHost** | ✅ Complete | `src/Aura.AppHost/` |
| **VS Code Extension** | ✅ Complete | `extension/src/` |
| **Tests** | ✅ 205+ passing | `tests/` |

## Feature Inventory

### Core Infrastructure

| Feature | Status | Spec/ADR |
|---------|--------|----------|
| Agent Registry & Loading | ✅ | [spec/01-agents.md](spec/01-agents.md) |
| Capability-based Routing | ✅ | [adr/011-two-tier-capability-model.md](adr/011-two-tier-capability-model.md) |
| LLM Providers (Ollama, Azure, OpenAI) | ✅ | [spec/24-llm-providers.md](spec/24-llm-providers.md) |
| RAG Pipeline | ✅ | [adr/008-local-rag-foundation.md](adr/008-local-rag-foundation.md) |
| Tool Framework (ReAct) | ✅ | [adr/012-tool-using-agents.md](adr/012-tool-using-agents.md) |
| Prompt Templates | ✅ | [adr/018-prompt-template-architecture.md](adr/018-prompt-template-architecture.md) |

### Developer Module

| Feature | Status | Spec/ADR |
|---------|--------|----------|
| Workflow Lifecycle | ✅ | [spec/12-developer-module.md](spec/12-developer-module.md) |
| Git Worktree Integration | ✅ | [spec/05-git-worktrees.md](spec/05-git-worktrees.md) |
| Assisted Workflow UI | ✅ | [spec/assisted-workflow-ui.md](spec/assisted-workflow-ui.md) |
| Roslyn Tools (6 tools) | ✅ | [adr/014-developer-module-roslyn-tools.md](adr/014-developer-module-roslyn-tools.md) |
| Graph RAG for Code | ✅ | [adr/015-graph-rag-for-code.md](adr/015-graph-rag-for-code.md) |
| TreeSitter Ingesters | ✅ | [spec/22-ingester-agents.md](spec/22-ingester-agents.md) |
| Semantic Enrichment | ✅ | [tasks/treesitter-ingesters.md](tasks/treesitter-ingesters.md) |

### VS Code Extension

| Feature | Status | Notes |
|---------|--------|-------|
| Workflow Tree View | ✅ | Grouped by status, filtered by workspace |
| Workflow Panel | ✅ | Full create/analyze/plan/execute UI |
| Step Management | ✅ | Approve/reject/skip/chat/reassign |
| Agent Tree View | ✅ | Hierarchical by capability |
| Status Tree View | ✅ | Health, Ollama models, RAG stats |
| Chat Window | ✅ | Per-agent chat panels |

## API Reference

All endpoints in `src/Aura.Api/Program.cs`. Quick reference: [ARCHITECTURE-QUICK-REFERENCE.md](ARCHITECTURE-QUICK-REFERENCE.md)

### Key Endpoints

```
# Workflows
POST   /api/developer/workflows              # Create
GET    /api/developer/workflows/{id}         # Get with steps
POST   /api/developer/workflows/{id}/analyze # Enrich with RAG
POST   /api/developer/workflows/{id}/plan    # Generate steps
POST   /api/developer/workflows/{id}/steps/{stepId}/execute

# Step Management (Assisted UI)
POST   /api/developer/workflows/{id}/steps/{stepId}/approve
POST   /api/developer/workflows/{id}/steps/{stepId}/reject
POST   /api/developer/workflows/{id}/steps/{stepId}/skip
POST   /api/developer/workflows/{id}/steps/{stepId}/chat
POST   /api/developer/workflows/{id}/steps/{stepId}/reassign

# RAG
POST   /api/rag/index/directory
POST   /api/rag/search
GET    /api/rag/stats/directory?path=...

# Code Graph
POST   /api/semantic/index    # Preferred: graph + embeddings
POST   /api/graph/index       # Roslyn only
```

## Configuration

### LLM Providers

```json
{
  "LlmProviders": {
    "default": {
      "Provider": "ollama",
      "Endpoint": "http://localhost:11434",
      "Model": "llama3:8b"
    }
  }
}
```

See [spec/24-llm-providers.md](spec/24-llm-providers.md) for Azure/OpenAI config.

### Running the System

```powershell
# Start API (includes PostgreSQL + Ollama via Aspire)
.\scripts\Start-Api.ps1

# Build extension
.\scripts\Build-Extension.ps1

# Run tests
.\scripts\Run-UnitTests.ps1
```

## Project Structure

```
src/
├── Aura.Foundation/          # Core: agents, LLM, RAG, data, tools
├── Aura.Module.Developer/    # Developer vertical: workflows, git, Roslyn
├── Aura.Api/                 # API host (all endpoints in Program.cs)
└── Aura.AppHost/             # Aspire orchestration

extension/
└── src/
    ├── providers/            # Tree views and panels
    └── services/             # API client

agents/                       # Markdown agent definitions
prompts/                      # Handlebars prompt templates
```

## Key Documents

| Document | Purpose |
|----------|---------|
| [ARCHITECTURE-QUICK-REFERENCE.md](ARCHITECTURE-QUICK-REFERENCE.md) | API endpoints, file locations, debugging |
| [plan/implementation/00-overview.md](plan/implementation/00-overview.md) | Implementation status and architecture |
| [spec/12-developer-module.md](spec/12-developer-module.md) | Developer workflow design |
| [spec/assisted-workflow-ui.md](spec/assisted-workflow-ui.md) | UI collaboration model |
| [progress/2025-12-11.md](progress/2025-12-11.md) | Latest weekly progress |

## Recent Changes (Dec 6-11, 2025)

1. **Agent Test-Writing Improvements**
   - Agents read existing tests to match framework and patterns
   - Explicit test file path instructions
   - Agents run tests after writing to verify correctness
   - `roslyn-coding` agent gains `testing-csharp` and `testing` capabilities

2. **LLM Provider Refinements**
   - Default model fallback when agent doesn't specify a model
   - Unified LLM configuration across providers

3. **Git Tools for Agents** - Git status, diff, log, branch operations available as tools

4. **Bug Fixes**
   - Step status stuck in Running when HTTP request cancelled
   - WorkingDirectory for Roslyn validation
   - Background RAG indexing for faster workflow creation

5. **Documentation** - Added API Cheat Sheet for quick reference

## Previous Changes (Dec 2-5, 2025)

1. **Assisted Workflow UI** - 5-phase implementation complete
   - Step cards with collapsible output/chat
   - Execute gating based on predecessor completion
   - Approve/reject step outputs
   - Step-level chat with agents
   - Reassign agents, edit descriptions, skip steps

2. **Cloud LLM Providers** - Azure OpenAI and OpenAI support

3. **TreeSitter Semantic Enrichment** - Docstrings, imports, types extracted

4. **RAG Improvements** - Case-insensitive paths, multi-query approach

## Open Items

### Technical Debt

| Item | Impact | Notes |
|------|--------|-------|
| Duplicate language coding agents | Low | `PythonCodingAgent.cs`, `GoCodingAgent.cs`, `TypeScriptCodingAgent.cs`, `FSharpCodingAgent.cs` duplicate YAML configs in `agents/languages/*.yaml`. Remove hardcoded agents after implementing `LanguageSpecialistAgent` that loads YAML configs. |
| ReActExecutor inline prompt | Low | Core ReAct prompt is inline in `ReActExecutor.cs` - tightly coupled to parsing logic, consider externalizing with care. |

### Not Yet Implemented

| Item | Priority | Notes |
|------|----------|-------|
| Dependency Graph edges | Low | Import relationships in code graph |
| Azure AD for LLM | Future | Currently API key only |
| Cost tracking | Future | For cloud LLM usage |
| Parallel step execution | Future | Currently sequential |

### Pending Actions

- [x] Push commits to origin ✅
- [x] Update progress documentation ✅
- [ ] Create MVP release tag
- [ ] User testing with real workflows
- [ ] Review and commit/discard 11 local uncommitted changes

## Principles

1. **Local-First, Privacy-Safe** - No cloud uploads, works offline
2. **Human-in-the-Loop** - Users control workflow execution
3. **Composable Modules** - Mix-and-match capabilities
4. **Hot-Reloadable Agents** - Drop markdown files to add agents

See [spec/00-overview.md](spec/00-overview.md) for full design principles.
