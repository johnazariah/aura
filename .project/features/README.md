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

## Upcoming Features

### High Priority (MVP Polish)

| Feature | Description |
|---------|-------------|
| [Smart Content](upcoming/smart-content.md) | LLM-driven content extraction |
| [Content File Indexing](upcoming/content-file-indexing.md) | Markdown/JSON/YAML indexing |
| [Cross-File Enrichment](upcoming/cross-file-enrichment.md) | Import resolution and call graphs |

### Medium Priority (User Experience)

| Feature | Description |
|---------|-------------|
| [Indexing Progress UI](upcoming/indexing-progress-ui.md) | Real-time indexing status |
| [MCP Server](upcoming/mcp-server.md) | Model Context Protocol integration |
| [Multi-Workspace](upcoming/multi-workspace.md) | Multiple workspace support |
| [File-Aware RAG](upcoming/file-aware-rag-queries.md) | Context-aware retrieval |

### Lower Priority (Advanced)

| Feature | Description |
|---------|-------------|
| [Condensed Export](upcoming/condensed-export.md) | Export indexed context |
| [Boolean Queries](upcoming/boolean-queries.md) | Advanced search operators |
| [Specialist Agents](upcoming/specialist-coding-agents.md) | Language-specific coding agents |
| [Tool Execution](upcoming/tool-execution-for-agents.md) | Agent tool invocation |
| [Internationalization](upcoming/internationalization.md) | Multi-language UI support |

### Implementation Plans

| Document | Description |
|----------|-------------|
| [Dependency Graph Edges](upcoming/dependency-graph-edges.md) | Implementation plan for import-based graph |
| [Unified Capability Model](upcoming/unified-capability-model.md) | Consolidate language capabilities |
| [Unified Indexing Pipeline](upcoming/unified-indexing-pipeline.md) | Single indexing architecture |
| [Indexing Roadmap](upcoming/indexing-roadmap.md) | Overall indexing strategy |
| [Graph Enhancements](upcoming/graph-and-indexing-enhancements.md) | Graph improvements |

## See Also

- [Roadmap](roadmap.md) - Prioritized sequencing of upcoming work
- [ADRs](../adr/README.md) - Architecture decisions
- [Progress](../progress/README.md) - Date-stamped status snapshots
