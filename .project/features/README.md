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
| [Unified Capability Model](completed/unified-capability-model.md) | Unified software-development-{language} capabilities | 2026-01-02 |
| [Unified Indexing Backend](completed/unified-indexing-backend.md) | Single pipeline for RAG + code graph | 2026-01-02 |
| [Tool Execution for Agents](completed/tool-execution-for-agents.md) | Agent tool invocation via LLM function calling | 2026-01-03 |
| [Language Specialist Agents](completed/generic-language-agent.md) | YAML-configurable language coding agents | 2026-01-03 |
| [Index Health Dashboard](completed/index-health-dashboard.md) | Workspace indexing visibility and health | 2026-01-03 |
| [Structured Output](completed/structured-output.md) | Schema-enforced JSON output for LLM responses | 2026-01-13 |
| [Streaming Responses](completed/streaming-responses.md) | Token-by-token streaming for chat | 2026-01-13 |
| [Unified Database](completed/unified-database.md) | Standardized PostgreSQL config across dev/prod | 2026-01-13 |
| [Workflow Step Debugging UX](completed/workflow-step-debugging-ux.md) | Enhanced step debugging and review UI | 2026-01-13 |
| [Workspace Onboarding](completed/workspace-onboarding.md) | Explicit workspace setup flow with consent | 2026-01-13 |
| [Chat Context Modes](completed/chat-context-modes.md) | Context mode selector for RAG/Graph chat | 2026-01-13 |
| [Indexing UX](completed/indexing-ux.md) | Progress UI, prompts, query commands | 2026-01-13 |
| [API Program Refactor](completed/api-program-refactor.md) | Extract Program.cs to organized endpoint files | 2026-01-13 |
| [MCP Server](completed/mcp-server.md) | Expose RAG/Code Graph to Copilot via MCP | 2026-01-15 |
| [MCP Tools Enhancement](completed/mcp-tools-enhancement.md) | 8 meta-tools: search, navigate, refactor, generate, etc. | 2026-01-15 |
| [Copilot CLI Parity](completed/copilot-cli-parity.md) | Autonomous mode, build-fix loop, visualization, GitHub Actions | 2026-01-15 |
| [Path Normalization](completed/path-normalization-review.md) | Consistent path handling across Windows/macOS | 2026-01-15 |
| [Story Model](completed/story-model.md) | GitHub Issue integration, workflow modes | 2026-01-15 |
| [Story Chat](completed/story-chat.md) | Chat-first workflow interaction | 2026-01-15 |
| [Operational Patterns](completed/operational-patterns.md) | Multi-step playbooks for complex operations | 2026-01-17 |
| [Worktree-Aware Indexing](completed/worktree-aware-indexing.md) | Isolated workspaces per worktree, cache invalidation | 2026-01-17 |
| [Pattern-Driven Stories](completed/pattern-driven-stories.md) | Patterns generate user-modifiable stories with discrete items | 2026-01-19 |
| [Pattern-Driven UX Gaps](completed/pattern-driven-ux-gaps.md) | 35 UX fixes for pattern-driven workflows (test gen, validation, etc.) | 2025-06-25 |
| [Agent Reflection](completed/agent-reflection.md) | Self-critique step for agent responses | 2026-01-19 |
| [Workflow Verification](completed/workflow-verification-stage.md) | Run build/lint/format checks before workflow completion | 2026-01-19 |
| [macOS Local Development](completed/macos-local-development.md) | TreeSitter 1.2.0, install script, local dev support | 2026-01-19 |
| [Modern C# Support](completed/aura-generate-modern-csharp.md) | Modern C# features for aura_generate | 2026-01-23 |
| [Agentic Execution v2](completed/agentic-execution-v2.md) | Sub-agents, retry loops, token budget awareness | 2026-01-23 |

## Upcoming Features

### High Priority

| Feature | Description |
|---------|-------------|
| [Pattern Catalog](upcoming/pattern-catalog.md) | 15+ patterns: refactoring, code generation, quality, architecture |

### Medium Priority (User Experience)

| Feature | Description |
|---------|-------------|
| [Multi-Language Refactoring](upcoming/multi-language-refactoring.md) | TypeScript (ts-morph), Go/Rust/Java (LSP) support |
| [Multi-Workspace](upcoming/multi-workspace.md) | Multiple workspace support |
| [macOS CI & Distribution](upcoming/macos-ci-and-distribution.md) | CI builds, Homebrew cask, menu bar app (needs self-hosted runner) |
| [API Harmonization](upcoming/api-review-harmonization.md) | Consistent REST API design |

### Lower Priority (Advanced)

| Feature | Description |
|---------|-------------|
| [Condensed Export](upcoming/condensed-export.md) | Export indexed context |
| [Internationalization](upcoming/internationalization.md) | Multi-language UI support |

### Unplanned (Backlog)

| Feature | Description |
|---------|-------------|
| [Quick Actions Bar](unplanned/quick-actions-bar.md) | Build/Test/Commit/PR buttons for workflow chat |
| [Indexing Epic](unplanned/indexing-epic.md) | Future indexing enhancements (smart content, boolean queries, etc.) |

## See Also

- [Roadmap](roadmap.md) - Prioritized sequencing of upcoming work
- [ADRs](../adr/README.md) - Architecture decisions
- [Progress](../progress/README.md) - Date-stamped status snapshots
