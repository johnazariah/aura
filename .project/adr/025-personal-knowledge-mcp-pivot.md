# ADR-025: Pivot to Personal Knowledge MCP Server

## Status
Accepted

## Date
2026-03-03

## Context

Aura was built as a full AI-powered development assistant with its own agent orchestration layer (ReAct loop, function calling, sub-agent spawning), VS Code extension, prompt templates, story lifecycle management, guardian system, and multi-LLM chat providers. Meanwhile, GitHub Copilot CLI matured rapidly with superior agent capabilities, native tool calling, and a fleet of specialized agents.

A strategic review identified that ~70% of Aura's codebase was redundant with Copilot CLI's capabilities. However, the local indexing infrastructure — pgvector RAG, Ollama embeddings, Roslyn code intelligence, content ingestors, and workspace management — had no equivalent in Copilot CLI. These components could serve as the **data layer** that Copilot queries via MCP.

Additionally, the user's needs extended beyond code: indexing personal documents (PDFs, receipts, research papers), photos, and other local files for semantic search — a personal knowledge management system.

## Decision

Transform Aura from a full agent orchestration platform into a **lean personal knowledge indexing MCP server**:

### Removed (~60,000 lines)
- Agent orchestration (ReAct loop, function calling, agent registry, sub-agents)
- VS Code extension (Copilot Chat replaces the UI)
- Prompt templates (Handlebars .prompt files)
- Story lifecycle management (create → plan → execute → verify)
- Guardian system (background CI/test/doc watchers)
- Built-in tools (file read/write, shell, grep — Copilot CLI has these)
- LLM chat providers (Azure OpenAI, OpenAI — keep only embedding providers)
- Aspire orchestration host (single-process deployment)
- GitHub integration (Copilot CLI has native GitHub tools)

### Retained
- **RAG pipeline**: pgvector storage, embedding generation, semantic search
- **Content ingestors**: Code (Roslyn + TreeSitter), Markdown, PDF, JSON/YAML/XML, PlainText
- **Code graph**: Roslyn-powered structural navigation (callers, implementations, usages)
- **MCP server**: 10 tools exposed to Copilot (search, navigate, inspect, refactor, generate, validate, index, workspace, tree, architect)
- **Workspace management**: Multi-workspace isolation, file watching, incremental indexing
- **Researcher module**: PDF extraction, academic paper management, source fetchers
- **System tray app**: Cross-platform status monitor (Avalonia)

### Added
- **TreeSitterCodeIngestor**: AST-aware indexing for Python, TypeScript, JavaScript, Go, Rust, Java, C/C++, Ruby, Swift, Kotlin
- **StructuredDataIngestor**: Structure-aware chunking for JSON, YAML, XML, TOML
- **PdfIngestor**: PDF text extraction via pdftotext
- **OpenAiEmbeddingProvider**: Hosted embedding API for faster indexing of large repositories
- **aura_index MCP tool**: On-demand indexing of files and directories
- **IncrementalIndexer routing through ingestor pipeline**: File watcher now uses proper ingestors instead of plain text fallback

## Consequences

### Positive
- **~60,000 lines of code removed** — dramatically simpler codebase
- **No agent maintenance burden** — Copilot CLI handles orchestration
- **Broader use cases** — personal documents, not just code
- **Faster indexing** — OpenAI embeddings provide ~10x throughput vs local Ollama
- **Cross-platform** — Windows + macOS support (tray app + service)
- **Clean MCP surface** — 10 focused tools instead of 28 overlapping ones

### Negative
- **No standalone agent execution** — requires Copilot CLI or another MCP client
- **Chat providers removed** — can't use Aura for LLM chat directly
- **VS Code extension gone** — no custom UI for status/workflow management (tray app partially replaces this)

### Risks
- MCP protocol may evolve — tool schemas may need updating
- TreeSitter.DotNet dependency may have platform-specific issues
- OpenAI embedding costs at scale (mitigated by configurable batch size and local Ollama fallback)

## Alternatives Considered

1. **Keep everything, add MCP as another interface** — Rejected: maintenance burden of two parallel systems
2. **Extract only Roslyn tools as MCP** — Rejected: misses the personal knowledge vision
3. **Rewrite from scratch** — Rejected: existing RAG/indexing infrastructure is battle-tested

## Supersedes
- ADR-004 (Markdown Agent Definitions) — agents deleted
- ADR-005 (Aspire Orchestration) — AppHost deleted
- ADR-011 (Two-Tier Capability Model) — capability routing deleted
- ADR-012 (Tool-Using Agents with ReAct Loop) — agent execution deleted
- ADR-018 (Prompt Template Architecture) — prompts deleted
- ADR-022 (Multi-Agent Orchestration) — orchestration deleted
