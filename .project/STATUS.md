# Aura Project Status

> **Last Updated**: 2026-02-06
> **Current Release**: v1.3.1
> **Branch**: main
> **Overall Status**: ✅ Production Ready

## Quick Summary

Aura is a **local-first, privacy-safe AI foundation** for knowledge work. The Developer Module is production-ready with MCP integration for GitHub Copilot, Roslyn refactoring tools, pattern-driven workflows, and multi-language support. The Researcher Module provides academic paper management and research workflows.

## Recent Changes

- **2026-02-06**: Remove Internal Agent Architecture (7,093 lines deleted)
  - Deleted RoslynCodingAgent, InternalAgentExecutor, ConversationService
  - Removed built-in chat UI (chatPanelProvider, chatWindowProvider, agentTreeProvider)
  - Removed 10 deprecated endpoints and 4 agent execution endpoints
  - Copilot Chat + MCP is now the only execution path
  - All 849 tests pass

- **2026-02-02**: Researcher Module - Academic paper and research management
  - Entities: Source, Excerpt, Concept, ConceptLink, Synthesis
  - Fetchers: ArxivFetcher, SemanticScholarFetcher, WebPageFetcher
  - Services: LibraryService, PdfExtractor, PdfToMarkdownService
  - API endpoints for source CRUD, import, excerpts, concepts, search
  - VS Code extension: Research Library view with import/search commands
  - Agents: research-agent.md, reading-assistant-agent.md
  - 29 unit tests passing

- **2026-01-29**: Unified Wave Orchestration - Simplified story execution model
  - Removed `StoryTask` abstraction (use `StoryStep` directly)
  - Removed `InternalAgentsDispatcher` (use Copilot CLI only for wave execution)
  - Removed `DispatchTarget` enum (no longer needed)
  - Extension now shows wave progress ("Wave 2/4") and groups steps by wave
  - Steps are updated in-place during dispatch (no conversion overhead)

## Component Status

| Component | Status | Key Files |
|-----------|--------|-----------|
| **Aura.Foundation** | ✅ Complete | `src/Aura.Foundation/` |
| **Aura.Module.Developer** | ✅ Complete | `src/Aura.Module.Developer/` |
| **Aura.Module.Researcher** | ✅ Complete | `src/Aura.Module.Researcher/` |
| **Aura.Api** | ✅ Complete | `src/Aura.Api/Program.cs` |
| **Aura.AppHost** | ✅ Complete | `src/Aura.AppHost/` |
| **VS Code Extension** | ✅ Complete | `extension/src/` |
| **Tests** | ✅ 849 passing | `tests/` |

## Feature Inventory

### Core Infrastructure

| Feature | Status | Spec/ADR |
|---------|--------|----------|
| Agent Registry & Loading | ✅ | [spec/01-agents.md](spec/01-agents.md) |
| Capability-based Routing | ✅ | [adr/011-two-tier-capability-model.md](adr/011-two-tier-capability-model.md) |
| LLM Providers (Ollama, Azure, OpenAI) | ✅ | [spec/24-llm-providers.md](spec/24-llm-providers.md) |
| RAG Pipeline | ✅ | [adr/008-local-rag-foundation.md](adr/008-local-rag-foundation.md) |
| Tool Framework (ReAct) | ✅ | [adr/012-tool-using-agents.md](adr/012-tool-using-agents.md) |
| Tool Execution (Function Calling) | ✅ | [features/completed/tool-execution-for-agents.md](features/completed/tool-execution-for-agents.md) |
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
| Language Specialist Agents | ✅ | [features/completed/generic-language-agent.md](features/completed/generic-language-agent.md) |

### VS Code Extension

| Feature | Status | Notes |
|---------|--------|-------|
| Workflow Tree View | ✅ | Grouped by status, filtered by workspace |
| Workflow Panel | ✅ | Full create/analyze/plan/execute UI |
| Status Tree View | ✅ | Health, Ollama models, RAG stats |
| Research Library View | ✅ | Source import, search, excerpts |

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

# Workspaces (indexing + code graph)
POST   /api/workspaces                 # Onboard (registers + indexes)
GET    /api/workspaces                 # List all
GET    /api/workspaces/{id}            # Get details + stats
POST   /api/workspaces/{id}/reindex    # Reindex existing
DELETE /api/workspaces/{id}            # Remove + clean data
GET    /api/workspaces/lookup?path=... # Look up by path

# RAG
POST   /api/rag/search

# Code Graph Queries
GET    /api/graph/find/{name}
GET    /api/graph/implementations/{interface}
GET    /api/graph/callers/{method}
GET    /api/graph/members/{type}
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

Aura runs as a Windows Service. For development:

```powershell
# Test API (Aura service must be running)
curl http://localhost:5300/health

# Build extension
.\scripts\Build-Extension.ps1

# Run tests
.\scripts\Run-UnitTests.ps1

# Update local install after code changes (run as Administrator)
.\scripts\Update-LocalInstall.ps1
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

## Release v1.3.0 (Jan 19, 2026)

Major release with 121 commits since v1.2.0.

### Highlights

- **MCP Tools Consolidation** - 28 tools consolidated into 8 meta-tools (`aura_search`, `aura_navigate`, `aura_inspect`, `aura_validate`, `aura_refactor`, `aura_generate`, `aura_pattern`, `aura_workflow`)
- **Roslyn Refactoring** - Full semantic refactoring: rename, extract method/variable/interface, move type to file, change signature, safe delete
- **Python Refactoring** - Cross-language refactoring support via rope
- **Blast Radius Protocol** - Analyze mode shows affected files before executing refactorings
- **Agent Reflection** - Agents self-critique responses before returning
- **Workflow Verification** - Verification stage ensures work is complete before finishing
- **Pattern System** - Tiered patterns with language overlays for complex multi-step operations
- **Guardian System** - Background guardians for CI, test coverage, and documentation
- **Worktree Support** - Path translation and cache invalidation for git worktrees
- **macOS Development** - Local development support for macOS
- **Test Generation** - Improved quality with compilation validation and proper namespace handling
- **GitHub Integration** - MCP server for Copilot, GitHub Actions tools

### Breaking Changes

None - all changes are additive.

## Release v1.3.1 (Jan 23, 2026)

### Agentic Execution v2 (Shipped)

Enhanced ReAct execution system with multi-agent orchestration, intelligent retry loops, and token budget awareness.

| Feature | Description |
|---------|-------------|
| Sub-Agent Spawning | `spawn_subagent` tool for hierarchical task delegation |
| Token Budget Tracking | `TokenTracker` class with configurable budgets |
| Context Budget Awareness | `check_token_budget` tool for agents to monitor capacity |
| Budget Warnings | Automatic warnings injected at 70%/80%/90% thresholds |
| Intelligent Retry | Retry loops for tool failures with error context injection |
| Retry Templates | `react-retry.prompt` Handlebars template |

### New Tools

- `spawn_subagent` - Delegate complex subtasks to a new agent with fresh context
- `check_token_budget` - Check remaining context window capacity

### Modern C# Support (Shipped)

Extended `aura_generate` MCP tool with modern C# 9-13 features:
- `required` properties, `init` setters
- Method modifiers (`virtual`/`override`/`abstract`/`sealed`/`new`)
- Primary constructors and positional records
- Generic type parameters with constraints
- Attributes on properties and methods
- Extension methods and XML documentation

### Other Improvements

- Fixed `Run-UnitTests.ps1` to exclude integration tests
- Added `aura_edit` tool for surgical text editing

See spec: [features/completed/agentic-execution-v2.md](features/completed/agentic-execution-v2.md)

## Recent Changes (Jan 13, 2026)

1. **Structured Output Mode (Complete)**
   - Added `JsonSchema` and `ChatOptions` for declarative response schemas
   - Implemented schema support in Azure OpenAI and OpenAI providers
   - Added Ollama fallback with JSON mode + schema injection
   - Created `WellKnownSchemas` (ReActResponse, WorkflowPlan, CodeModification)
   - Integrated structured output in ReActExecutor with `UseStructuredOutput` option
   - Integrated structured output in WorkflowService.PlanAsync for reliable step parsing

2. **Streaming Responses (Complete)**
   - Added `StreamChatAsync` to all LLM providers
   - Implemented NDJSON streaming for Ollama
   - Implemented SDK streaming for Azure OpenAI and OpenAI
   - Added `/api/agents/{agentId}/chat/stream` SSE endpoint
   - Updated extension chat panel to consume SSE stream
   - Added streaming to workflow step execution

## Recent Changes (Jan 3, 2026)

1. **Tool Execution for Agents (Complete)**
   - Added `ChatWithFunctionsAsync` to all LLM providers (Azure OpenAI, OpenAI, Ollama)
   - Added `FunctionDefinition`, `FunctionCall`, `LlmFunctionResponse` records for function calling
   - Updated `ConfigurableAgent` with tool execution loop supporting native LLM function calling
   - Added `IToolConfirmationService` for human-in-the-loop tool approval
   - Added `ToolConfirmationOptions` for configuring auto-approve/require-approval tools
   - Agents can now execute tools declared in their `## Tools Available` section
   - Tools requiring confirmation will be rejected in auto-approve mode

2. **Language Specialist Agents (Complete)**
   - Implemented `LanguageSpecialistAgent` that loads behavior from YAML config files
   - Added `LanguageConfigLoader` to parse `agents/languages/*.yaml` files
   - Added `LanguageToolFactory` to create CLI tools from YAML definitions
   - Added `RegisterLanguageAgentsTask` startup task to auto-load language agents
   - Python, TypeScript, Go, Rust, F#, etc. now have proper specialist agents
   - C# continues to use hardcoded `RoslynCodingAgent` (needs Roslyn APIs)

## Previous Changes (Jan 2, 2026)

1. **Unified Capability Model**
   - RoslynCodingAgent now uses `software-development-csharp` capability (replaces fragmented `csharp-coding`, `testing-csharp`, etc.)
   - Added capability aliases in AgentRegistry for backward compatibility
   - Old capability names automatically resolve to new unified names

2. **Removed Duplicate Language Agents**
   - Deleted `PythonCodingAgent.cs`, `GoCodingAgent.cs`, `TypeScriptCodingAgent.cs`, `FSharpCodingAgent.cs`
   - These duplicated functionality defined in `agents/languages/*.yaml`
   - Languages now fall back to generic `coding-agent.md` until LanguageSpecialistAgent is implemented

3. **Unified Indexing Backend (Complete)**
   - Created `RoslynCodeIngestor` that produces both RAG chunks AND code graph nodes in single parse
   - Added `RegisterCodeIngestorsTask` startup task to register ingestors at module load
   - `/api/semantic/index` endpoint now delegates to BackgroundIndexer with unified pipeline
   - Deleted `DeveloperSemanticIndexer` - no longer needed with unified approach
   - C# files indexed via background indexer now get both RAG embeddings and graph nodes

## Previous Changes (Dec 6-11, 2025)

1. **Agent Test-Writing Improvements**
   - Agents read existing tests to match framework and patterns
   - Explicit test file path instructions
   - Agents run tests after writing to verify correctness

2. **LLM Provider Refinements**
   - Default model fallback when agent doesn't specify a model
   - Unified LLM configuration across providers

3. **Git Tools for Agents** - Git status, diff, log, branch operations available as tools

4. **Bug Fixes**
   - Step status stuck in Running when HTTP request cancelled
   - WorkingDirectory for Roslyn validation
   - Background RAG indexing for faster workflow creation

5. **Documentation** - Added API Cheat Sheet for quick reference

## Earlier Changes (Dec 2-5, 2025)

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
- [x] Remove duplicate language agents (PythonCodingAgent, GoCodingAgent, TypeScriptCodingAgent, FSharpCodingAgent) ✅
- [x] Create MVP release tag ✅ (v1.0.0-mvp)
- [x] Release v1.3.0 ✅ (Jan 19, 2026)
- [ ] User testing with real workflows

## Principles

1. **Local-First, Privacy-Safe** - No cloud uploads, works offline
2. **Human-in-the-Loop** - Users control workflow execution
3. **Composable Modules** - Mix-and-match capabilities
4. **Hot-Reloadable Agents** - Drop markdown files to add agents

See [spec/00-overview.md](spec/00-overview.md) for full design principles.
