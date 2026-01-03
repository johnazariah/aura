# Features

Unified documentation for all Aura features - combining specification, design, and implementation details in one place. Each feature document contains:

- **Specification** - What the feature does and why
- **Design** - Architecture and key decisions
- **Implementation Status** - Current state and what's been built

## Structure

```text
features/
├── completed/     # Implemented and shipped (each doc has Implementation Status section)
├── upcoming/      # Planned but not yet implemented
└── roadmap.md     # Prioritized sequencing of upcoming work
```

## Completed Features

| Feature | Description | Completed |
|---------|-------------|-----------|

| [Overview](completed/overview.md) | Project vision and architecture | 2025-12-01 |
| [Agents](completed/agents.md) | Agent framework and lifecycle | 2025-11-27 |
| [LLM Providers](completed/llm-providers.md) | Multi-provider LLM integration | 2025-11-27 |
| [Data Model](completed/data-model.md) | PostgreSQL schema and repositories | 2025-11-27 |
| [API Endpoints](completed/api-endpoints.md) | REST API design | 2025-11-28 |
| [Git Worktrees](completed/git-worktrees.md) | Multi-worktree support | 2025-11-28 |
| [Extension](completed/extension.md) | VS Code extension architecture | 2025-11-29 |
| [Testing](completed/testing.md) | Test strategy and organization | 2025-11-27 |
| [Foundation](completed/foundation.md) | Core infrastructure services | 2025-11-27 |
| [Aspire Architecture](completed/aspire-architecture.md) | .NET Aspire integration | 2025-11-28 |
| [Composable Modules](completed/composable-modules.md) | Module registration system | 2025-11-28 |
| [Agent Discovery](completed/agent-discovery.md) | Hot-reload agent discovery | 2025-11-29 |
| [Developer Module](completed/developer-module.md) | Developer-focused workflows | 2025-12-01 |
| [Semantic Indexing](completed/semantic-indexing.md) | Code semantic analysis (superseded) | 2025-12-03 |
| [Ingester Agents](completed/ingester-agents.md) | Language-specific ingesters | 2025-12-05 |
| [Hardcoded Agents](completed/hardcoded-agents.md) | Built-in agent definitions | 2025-12-05 |
| [Cloud LLM Providers](completed/cloud-llm-providers.md) | Azure OpenAI, OpenAI support | 2025-12-05 |
| [Assisted Workflow UI](completed/assisted-workflow-ui.md) | Workflow step UI in extension | 2025-12-08 |
| [TreeSitter Ingesters](completed/treesitter-ingesters.md) | Multi-language TreeSitter parsing | 2025-12-10 |
| [Workflow PR Creation](completed/workflow-pr-creation.md) | Finalize workflows with commit/push/PR | 2025-12-12 |
| [Code Graph Status Panel](completed/code-graph-status-panel.md) | Code Graph stats in System Status | 2025-12-12 |
| [File-Aware RAG Queries](completed/file-aware-rag-queries.md) | Context-aware retrieval with file scoping | 2025-12-12 |
| [Code-Aware Chat](completed/code-aware-chat.md) | Chat with RAG + Code Graph context | 2025-12-13 |
| [Bundled Extension](completed/bundled-extension.md) | VS Code extension bundled with installer | 2025-12-24 |
| [PostgreSQL Setup](completed/postgresql-setup.md) | Embedded PostgreSQL with pgvector | 2025-12-25 |
| [GitHub Release Automation](completed/github-release-automation.md) | Tag-triggered releases via GitHub Actions | 2025-12-25 |
| [End-User Documentation](completed/end-user-documentation.md) | User-facing docs for install, config, usage | 2025-12-25 |

## Upcoming Features

### High Priority (MVP Polish)

| Feature | Description |
|---------|-------------|
| [Chat Context Modes](upcoming/chat-context-modes.md) | Context mode selector for RAG/Graph chat |
| [Index Health Dashboard](upcoming/index-health-dashboard.md) | Index coverage, freshness, and actions |
| [Unified Indexing Backend](upcoming/unified-indexing-backend.md) | Single pipeline for RAG + Code Graph |
| [Indexing UX](upcoming/indexing-ux.md) | Progress UI, prompts, query commands |

### Medium Priority (User Experience)

| Feature | Description |
|---------|-------------|

| [MCP Server](upcoming/mcp-server.md) | Model Context Protocol integration |
| [Multi-Workspace](upcoming/multi-workspace.md) | Multiple workspace support |
| [macOS Support](upcoming/macos-support.md) | Native macOS build, installer, and CI |

### Lower Priority (Advanced)

| Feature | Description |
|---------|-------------|

| [Condensed Export](upcoming/condensed-export.md) | Export indexed context |
| [Specialist Agents](upcoming/specialist-coding-agents.md) | Language-specific coding agents |
| [Tool Execution](upcoming/tool-execution-for-agents.md) | Agent tool invocation |
| [Internationalization](upcoming/internationalization.md) | Multi-language UI support |
| [Generic Language Agent](upcoming/generic-language-agent.md) | Language-agnostic coding agent |
| [Unified Capability Model](upcoming/unified-capability-model.md) | Consolidate language capabilities |
| [Unified Software Development](upcoming/unified-software-development-capability.md) | Universal dev capability |

### Unplanned (Backlog)

| Feature | Description |
|---------|-------------|

| [Indexing Epic](unplanned/indexing-epic.md) | Future indexing enhancements (smart content, boolean queries, etc.) |

## See Also

- [Roadmap](roadmap.md) - Prioritized sequencing of upcoming work
- [ADRs](../adr/README.md) - Architecture decisions
- [Progress](../progress/README.md) - Date-stamped status snapshots
