---
agent: agent
description: Update technical documentation (architecture, concepts, agent development, configuration) to reflect current Aura internals.
---

# Update Technical Documentation

You are updating Aura's technical documentation for developers and contributors who need to understand the system internals.

## Architecture Context

Aura is an AI-powered development assistant with a hybrid architecture:
- **Local code intelligence**: Roslyn (C# semantic analysis), TreeSitter (polyglot parsing), PostgreSQL + pgvector (RAG)
- **Cloud LLM inference**: Azure OpenAI (default), Ollama (optional local alternative)
- **MCP server**: Exposes 13 tools to GitHub Copilot via Model Context Protocol
- **Windows Service**: Runs on port 5300, deployed via `Update-LocalInstall.ps1`

See `.project/adr/024-hybrid-architecture.md` for the authoritative architecture decision.

## Current Technical Stack

| Component | Technology |
|-----------|------------|
| Runtime | .NET 9, C# 13 |
| Orchestration | .NET Aspire |
| Database | PostgreSQL 17 + pgvector |
| Code analysis | Roslyn (C#), TreeSitter (9 languages) |
| LLM (default) | Azure OpenAI |
| LLM (local) | Ollama (optional) |
| Git | LibGit2Sharp |
| GitHub | Octokit |
| Container | Podman (Windows), OrbStack (macOS) |
| Extension | VS Code, TypeScript |

## Key Source Paths (verify against these)

| What | Path |
|------|------|
| API host | `src/Aura.Api/` |
| MCP handler | `src/Aura.Api/Mcp/McpHandler.cs` (+ 11 partial files) |
| Endpoints | `src/Aura.Api/Endpoints/*.cs` |
| Foundation | `src/Aura.Foundation/` |
| Developer module | `src/Aura.Module.Developer/` |
| Researcher module | `src/Aura.Module.Researcher/` |
| Agent definitions | `agents/*.md` |
| Prompt templates | `prompts/*.prompt` |
| Patterns | `patterns/*.md` |
| Extension | `extension/src/` |
| Tests | `tests/Aura.Foundation.Tests/`, `tests/Aura.Module.Developer.Tests/` |

## MCP Tools (13 consolidated meta-tools)

| Tool | Purpose |
|------|---------|
| `aura_architect` | Architecture analysis |
| `aura_docs` | Documentation access |
| `aura_edit` | File editing |
| `aura_generate` | Code generation (create_type, method, property, constructor, tests, implement_interface) |
| `aura_inspect` | Type/member inspection (type_members, list_types) |
| `aura_navigate` | Code navigation (callers, implementations, derived_types, usages, references, definition, by_attribute) |
| `aura_pattern` | Load operational patterns (list, get) |
| `aura_refactor` | Code transformations (rename, extract_method, extract_variable, extract_interface, change_signature, safe_delete, move_type_to_file) |
| `aura_search` | Semantic code search |
| `aura_tree` | File tree listing |
| `aura_validate` | Compilation and test validation |
| `aura_workflow` | Story management (list, get, get_by_path, create, enrich, update_step) |
| `aura_workspace` | Workspace registration |

## What Has Been Removed (do NOT reference)

- ❌ `IAgentExecutor` interface — internal agent architecture removed 2026-02-06
- ❌ Code-based agents via C# classes — replaced by markdown agents + Copilot CLI
- ❌ Plugin system (`IAgentPlugin`, hot-reload plugins) — never shipped
- ❌ `AgentOrchestrator.*` namespace — renamed to `Aura.*`
- ❌ `DynamicAgentRegistry` for code-based agents — only markdown agents remain
- ❌ Agent Hub, Task Monitor, Insights views in extension — removed
- ❌ Port 5258 — now 5300
- ❌ `.NET 9` — project uses .NET 10

## Documentation to Update

### Architecture & Concepts (`docs/configuration/`)
- `docs/configuration/llm-providers.md` — embedding provider setup and fallback behavior
- `docs/configuration/settings.md` — appsettings.json structure

### RAG & Knowledge
- `src/Aura.Foundation/Rag/RagService.cs` — vector search
- `src/Aura.Foundation/Rag/ICodeGraphService.cs` — Roslyn code graph
- Prompt templates define `ragQueries` in frontmatter for automatic context retrieval

## Writing Guidelines

1. **Verify everything against source code** — read actual files, don't propagate stale info
2. **Use correct namespace** — `Aura.*`, never `AgentOrchestrator.*`
3. **Describe what exists** — not what was planned or removed
4. **Include code examples** from actual source, not hypothetical
5. **Link to ADRs** for design decisions rather than re-explaining
6. **Technical but accessible** — explain terminology, use diagrams where helpful

## Quality Criteria

- ✅ All source paths point to files that actually exist
- ✅ No references to removed features (IAgentExecutor, plugins, code-based agents)
- ✅ Correct namespace (Aura.*, not AgentOrchestrator.*)
- ✅ Accurate MCP tool names and operations
- ✅ Azure OpenAI documented as default provider
- ✅ .NET 9 (not .NET 10)
- ✅ Port 5300 throughout
