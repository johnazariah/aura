# Aura Project Status

> **Last Updated**: 2026-03-26
> **Current Branch**: trim/personal-knowledge-mcp
> **Overall Status**: 🔄 Major Pivot In Progress

## Quick Summary

Aura is a **personal knowledge indexing platform** with an MCP interface. It indexes code, documents, PDFs, and structured data locally (Roslyn, TreeSitter, pgvector RAG) and exposes tools via MCP for GitHub Copilot. Embedding generation uses Ollama (local) or OpenAI (hosted) — configurable. The system runs as a Windows Service with a cross-platform tray app for monitoring.

**This is a major architectural pivot from the previous agent-orchestration platform.** See [ADR-025](adr/025-personal-knowledge-mcp-pivot.md) for rationale.

## Recent Changes

- **2026-03-26**: Workspace Model Unified
  - Replaced dual workspace storage (DB + JSON registry) with a single DB-backed model
  - Added `Alias`, `Tags`, and `IsDefault` to the `Workspace` entity
  - Updated MCP `aura_workspace` and REST `/api/workspaces` to share one model
  - Removed stale workflow/extension/chat docs and obsolete installer helper scripts

- **2026-03-03**: Personal Knowledge MCP Pivot
  - Removed agent orchestration, VS Code extension, prompt templates, story lifecycle, GitHub integration, Aspire host (~60,000 lines deleted)
  - Added PdfIngestor, TreeSitterCodeIngestor, StructuredDataIngestor
  - Added `aura_index` MCP tool for on-demand indexing
  - Added OpenAI embedding provider with configurable batching
  - Restored cross-platform system tray app (Avalonia)
  - Updated CI/release pipelines for Windows + macOS
  - 388 tests passing across 4 projects

## Component Status

| Component | Status | Notes |
|-----------|--------|-------|
| **Aura.Foundation** | ✅ Active | RAG pipeline, embeddings, code graph, git, shell |
| **Aura.Module.Developer** | ✅ Active | Roslyn, TreeSitter, Python/TS refactoring |
| **Aura.Module.Researcher** | ✅ Active | PDF extraction, library management |
| **Aura.Api** | ✅ Active | MCP server + REST endpoints |
| **Aura.Tray** | ✅ Active | Cross-platform system tray monitor |
| **Aura.ServiceDefaults** | ✅ Active | Shared service configuration |
| ~~Aura.AppHost~~ | ❌ Removed | Aspire host no longer needed |
| ~~VS Code Extension~~ | ❌ Removed | Copilot Chat is the UI |
| **Tests** | ✅ 388 passing | Foundation, Developer, Researcher, Api |

## Architecture

```
Copilot CLI / Copilot Chat
  └─ MCP connection
      └─ Aura MCP Server (port 5300, Windows Service)
          ├─ aura_search     → semantic search across all indexed content
          ├─ aura_navigate   → code relationships (Roslyn/TreeSitter graph)
          ├─ aura_inspect    → type structure
          ├─ aura_refactor   → code transforms (Roslyn/rope/ts-morph)
          ├─ aura_generate   → code generation (Roslyn)
          ├─ aura_validate   → build/test
          ├─ aura_index      → trigger indexing of files/directories
          ├─ aura_architect  → architecture analysis (planned)
          ├─ aura_tree       → hierarchical code exploration
          └─ aura_workspace  → manage indexed collections
              │
              ├─ Ingestor Pipeline (priority order)
              │   ├─ RoslynCodeIngestor (.cs) — full AST + code graph
              │   ├─ TreeSitterCodeIngestor (.py/.ts/.js/.go/.rs/.java/.c++)
              │   ├─ StructuredDataIngestor (.json/.yaml/.xml/.toml)
              │   ├─ PdfIngestor (.pdf) — via pdftotext
              │   ├─ MarkdownIngestor (.md)
              │   ├─ CodeIngestor (regex fallback)
              │   └─ PlainTextIngestor (everything else)
              │
              ├─ Embedding Layer
              │   ├─ OllamaProvider (local, nomic-embed-text, 768d)
              │   └─ OpenAiEmbeddingProvider (hosted, dimensions configurable to match local index)
              │
              └─ Storage (PostgreSQL + pgvector)
```

## MCP Tools (10)

| Tool | Purpose | Language Support |
|------|---------|-----------------|
| `aura_search` | Semantic search across indexed content | All |
| `aura_navigate` | Find callers, implementations, usages, references | C# (Roslyn), Python (rope), TypeScript (ts-morph) |
| `aura_inspect` | Examine type members, list types | C# (Roslyn) |
| `aura_refactor` | Rename, extract, move, change signature | C# (Roslyn), Python (rope), TypeScript (ts-morph) |
| `aura_generate` | Create types, implement interfaces, generate tests | C# (Roslyn) |
| `aura_validate` | Compilation check, run tests | C# (dotnet), TypeScript (tsc) |
| `aura_index` | Index files/directories, check job status, get stats | All |
| `aura_workspace` | Manage workspaces: add, remove, list, status | All |
| `aura_tree` | Hierarchical code exploration | All |
| `aura_architect` | Architecture analysis (planned) | C# |

## Configuration

### Embedding Provider

```json
{
  "Aura": {
    "Embedding": {
      "Provider": "ollama"
    }
  }
}
```

Set `Provider` to `"openai"` for hosted embeddings. See [ADR-027](adr/027-configurable-embedding-providers.md).

### Running the System

```powershell
# Build
dotnet build

# Run tests
dotnet test --filter "FullyQualifiedName!~IntegrationTests"

# Test API (service must be running)
curl http://localhost:5300/health

# Lint
dotnet format --verify-no-changes
```

## Project Structure

```
src/
├── Aura.Foundation/          # Core: RAG, embeddings, git, shell, data
├── Aura.Module.Developer/    # Roslyn, TreeSitter, Python/TS services
├── Aura.Module.Researcher/   # PDF extraction, library management
├── Aura.Api/                 # MCP server + REST endpoints
├── Aura.Tray/                # Cross-platform system tray (Avalonia)
└── Aura.ServiceDefaults/     # Shared service config

tests/
├── Aura.Foundation.Tests/    # 216 tests
├── Aura.Module.Developer.Tests/  # 27 tests
├── Aura.Module.Researcher.Tests/ # 61 tests
└── Aura.Api.Tests/           # 84 tests

patterns/                     # Operational patterns for complex tasks
scripts/                      # Build, test, publish scripts
installers/windows/           # Inno Setup installer
```

## Key ADRs

| ADR | Decision |
|-----|----------|
| [ADR-025](adr/025-personal-knowledge-mcp-pivot.md) | Pivot to personal knowledge MCP server |
| [ADR-026](adr/026-multi-language-ast-indexing.md) | Multi-language AST indexing via TreeSitter |
| [ADR-027](adr/027-configurable-embedding-providers.md) | Configurable embedding providers (Ollama + OpenAI) |
| [ADR-008](adr/008-local-rag-foundation.md) | Local RAG as foundation component |
| [ADR-015](adr/015-graph-rag-for-code.md) | Graph RAG for code understanding |
| [ADR-001](adr/001-local-first-architecture.md) | Local-first, privacy-safe architecture |

## Superseded ADRs

These decisions were valid in the agent-orchestration era but are no longer applicable:

| ADR | Was | Now |
|-----|-----|-----|
| ADR-004 | Markdown Agent Definitions | Agents deleted — Copilot CLI is the agent |
| ADR-005 | Aspire Orchestration | AppHost deleted — single-process deployment |
| ADR-011 | Two-Tier Capability Model | Capability routing deleted |
| ADR-012 | Tool-Using Agents (ReAct) | Agent execution deleted |
| ADR-018 | Prompt Templates | Prompts deleted |
| ADR-022 | Multi-Agent Orchestration | Orchestration deleted |
| ADR-024 | Hybrid Architecture | Superseded by ADR-025 |
