# Aura Feature-Parity Specification

> **Purpose:** Comprehensive specification for rewriting Aura to feature-parity.
> **Created:** 2026-01-28
> **Aura Version:** 1.3.1

This directory contains a complete specification of Aura's functionality, suitable for an agent or team to rewrite the application from scratch while maintaining feature parity.

## Document Structure

| Document | Purpose |
|----------|---------|
| [00-overview.md](00-overview.md) | Executive summary, architecture, and principles |
| [01-foundation.md](01-foundation.md) | Core services: LLM, RAG, Tools, Agents, Git |
| [02-developer-module.md](02-developer-module.md) | Story management, Roslyn tools, refactoring |
| [03-api-layer.md](03-api-layer.md) | REST endpoints, MCP server, SSE streaming |
| [04-extension.md](04-extension.md) | VS Code extension architecture and UI |
| [05-data-model.md](05-data-model.md) | PostgreSQL schema, entities, migrations |
| [06-agents-prompts.md](06-agents-prompts.md) | Agent definitions, prompts, patterns |
| [07-configuration.md](07-configuration.md) | Configuration options, deployment modes |
| [08-code-review.md](08-code-review.md) | Technical debt, simplification opportunities |

## Key Metrics

| Metric | Value |
|--------|-------|
| **C# Projects** | 6 (Foundation, Developer Module, API, AppHost, ServiceDefaults, Tray) |
| **Test Count** | 754 passing tests |
| **API Endpoints** | ~50 REST endpoints |
| **MCP Tools** | 11 meta-tools (consolidated from 28) |
| **Agent Definitions** | 10 markdown + 13 YAML language configs |
| **Prompt Templates** | 13 Handlebars templates |
| **VS Code Views** | 4 tree views + 2 webview panels |
| **Database Tables** | ~15 tables (Foundation + Developer) |

## How to Use This Specification

### For a Full Rewrite

1. Start with [00-overview.md](00-overview.md) to understand the vision and architecture
2. Read [01-foundation.md](01-foundation.md) for core infrastructure requirements
3. Implement the data model from [05-data-model.md](05-data-model.md)
4. Build the API layer per [03-api-layer.md](03-api-layer.md)
5. Add the Developer module from [02-developer-module.md](02-developer-module.md)
6. Build the VS Code extension from [04-extension.md](04-extension.md)
7. Review [08-code-review.md](08-code-review.md) for simplification opportunities

### For Incremental Improvement

1. Review [08-code-review.md](08-code-review.md) for technical debt items
2. Prioritize by impact and complexity
3. Use the deep-dive specs as reference implementations

## Technology Stack

| Layer | Technology |
|-------|------------|
| **Backend Runtime** | .NET 8 / C# 12 |
| **Orchestration** | .NET Aspire |
| **Database** | PostgreSQL 15+ with pgvector |
| **Local LLM** | Ollama |
| **Cloud LLM** | Azure OpenAI, OpenAI (optional) |
| **Code Analysis** | Roslyn (C#), Tree-sitter (multi-language), rope (Python) |
| **Extension** | TypeScript / VS Code API |
| **Container Runtime** | Podman (Windows), OrbStack (macOS) |

## Maintaining This Specification

Use the prompt at `.github/prompts/maintain-feature-parity-spec.prompt.md` to keep this specification up-to-date after significant changes.
