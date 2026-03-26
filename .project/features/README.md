# Features

This directory contains both the current Aura feature set and historical feature records from the earlier extension/agent/workflow era.

The current product direction is the local indexing + MCP platform described in [ADR-025](../adr/025-personal-knowledge-mcp-pivot.md).

## Current Focus

- local indexing
- MCP tools
- Roslyn and Tree-sitter code intelligence
- PDF and document ingestion
- embedding providers
- tray/service packaging
- workspace management

## Directory Layout

```text
features/
├── completed/     # Shipped capabilities and historical feature records
├── in-progress/   # Current active work
├── proposed/      # Backlog ideas
├── spikes/        # Time-boxed research
└── templates/     # Feature and spike templates
```

## Completed

- [agent-discovery.md](completed/agent-discovery.md)
- [agent-reflection.md](completed/agent-reflection.md)
- [agentic-execution-v2.md](completed/agentic-execution-v2.md)
- [agents.md](completed/agents.md)
- [api-endpoints.md](completed/api-endpoints.md)
- [api-program-refactor.md](completed/api-program-refactor.md)
- [api-review-harmonization.md](completed/api-review-harmonization.md)
- [aspire-architecture.md](completed/aspire-architecture.md)
- [assisted-workflow-ui.md](completed/assisted-workflow-ui.md)
- [aura-docs-bundled-documentation.md](completed/aura-docs-bundled-documentation.md)
- [aura-generate-modern-csharp.md](completed/aura-generate-modern-csharp.md)
- [aura-tree-hierarchical-exploration.md](completed/aura-tree-hierarchical-exploration.md)
- [bundled-extension.md](completed/bundled-extension.md)
- [chat-context-modes.md](completed/chat-context-modes.md)
- [cloud-llm-providers.md](completed/cloud-llm-providers.md)
- [code-aware-chat.md](completed/code-aware-chat.md)
- [code-graph-status-panel.md](completed/code-graph-status-panel.md)
- [coding-agent-v2-mcp-validation.md](completed/coding-agent-v2-mcp-validation.md)
- [composable-modules.md](completed/composable-modules.md)
- [copilot-cli-parity.md](completed/copilot-cli-parity.md)
- [data-model.md](completed/data-model.md)
- [database-schema-cleanup.md](completed/database-schema-cleanup.md)
- [developer-module.md](completed/developer-module.md)
- [end-user-documentation.md](completed/end-user-documentation.md)
- [extension.md](completed/extension.md)
- [file-aware-rag-queries.md](completed/file-aware-rag-queries.md)
- [foundation.md](completed/foundation.md)
- [generic-language-agent.md](completed/generic-language-agent.md)
- [git-worktrees.md](completed/git-worktrees.md)
- [github-release-automation.md](completed/github-release-automation.md)
- [hardcoded-agents.md](completed/hardcoded-agents.md)
- [index-health-dashboard.md](completed/index-health-dashboard.md)
- [indexing-ux.md](completed/indexing-ux.md)
- [ingester-agents.md](completed/ingester-agents.md)
- [llm-providers.md](completed/llm-providers.md)
- [macos-local-development.md](completed/macos-local-development.md)
- [mcp-server.md](completed/mcp-server.md)
- [mcp-tools-enhancement.md](completed/mcp-tools-enhancement.md)
- [multi-registry-workspaces.md](completed/multi-registry-workspaces.md)
- [operational-patterns.md](completed/operational-patterns.md)
- [orchestrator-parallel-dispatch.md](completed/orchestrator-parallel-dispatch.md)
- [overview.md](completed/overview.md)
- [path-normalization-review.md](completed/path-normalization-review.md)
- [pattern-driven-stories.md](completed/pattern-driven-stories.md)
- [pattern-driven-ux-gaps.md](completed/pattern-driven-ux-gaps.md)
- [polyglot-mcp-tools.md](completed/polyglot-mcp-tools.md)
- [postgresql-setup.md](completed/postgresql-setup.md)
- [react-post-code-validation.md](completed/react-post-code-validation.md)
- [remove-internal-agent-architecture.md](completed/remove-internal-agent-architecture.md)
- [researcher-module.md](completed/researcher-module.md)
- [rfc7807-problem-details.md](completed/rfc7807-problem-details.md)
- [sdd-artifact-export.md](completed/sdd-artifact-export.md)
- [semantic-indexing.md](completed/semantic-indexing.md)
- [service-account-for-aura.md](completed/service-account-for-aura.md)
- [story-chat.md](completed/story-chat.md)
- [story-model.md](completed/story-model.md)
- [streaming-responses.md](completed/streaming-responses.md)
- [structured-output.md](completed/structured-output.md)
- [tech-debt-stringly-typed-code.md](completed/tech-debt-stringly-typed-code.md)
- [technical-debt-cleanup.md](completed/technical-debt-cleanup.md)
- [test-generation.md](completed/test-generation.md)
- [testing.md](completed/testing.md)
- [tool-execution-for-agents.md](completed/tool-execution-for-agents.md)
- [treesitter-ingesters.md](completed/treesitter-ingesters.md)
- [typescript-refactoring.md](completed/typescript-refactoring.md)
- [unified-capability-model.md](completed/unified-capability-model.md)
- [unified-database.md](completed/unified-database.md)
- [unified-indexing-backend.md](completed/unified-indexing-backend.md)
- [unified-wave-orchestration.md](completed/unified-wave-orchestration.md)
- [workflow-pr-creation.md](completed/workflow-pr-creation.md)
- [workflow-step-debugging-ux.md](completed/workflow-step-debugging-ux.md)
- [workflow-verification-stage.md](completed/workflow-verification-stage.md)
- [workspace-onboarding.md](completed/workspace-onboarding.md)
- [worktree-aware-indexing.md](completed/worktree-aware-indexing.md)
- [worktree-index-detection.md](completed/worktree-index-detection.md)

## In Progress

- [condensed-export.md](in-progress/condensed-export.md)
- [internationalization.md](in-progress/internationalization.md)
- [layered-fleet-architecture.md](in-progress/layered-fleet-architecture.md)
- [macos-ci-and-distribution.md](in-progress/macos-ci-and-distribution.md)
- [python-inspect-validate.md](in-progress/python-inspect-validate.md)

## Proposed

- [agent-capability-comparison.md](proposed/agent-capability-comparison.md)
- [azure-devops-jira-integration.md](proposed/azure-devops-jira-integration.md)
- [document-ingestion.md](proposed/document-ingestion.md)
- [indexing-epic.md](proposed/indexing-epic.md)
- [lsp-refactoring-framework.md](proposed/lsp-refactoring-framework.md)
- [orchestrator-ghcp-integration.md](proposed/orchestrator-ghcp-integration.md)
- [pattern-catalog.md](proposed/pattern-catalog.md)
- [quick-actions-bar.md](proposed/quick-actions-bar.md)
- [research-workflows.md](proposed/research-workflows.md)
- [web-ui.md](proposed/web-ui.md)

## Spikes

- [emic-pdf-approach.md](spikes/emic-pdf-approach.md)

## See Also

- [../STATUS.md](../STATUS.md)
- [../adr](../adr)
- [../reference](../reference)
