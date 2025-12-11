# Feature Specifications

Design specifications for Aura features.

## Status Key

| Icon | Status |
|------|--------|
| ✅ | Implemented |
| 🔄 | In Progress |
| 🔲 | Not Started |
| 📋 | Planned |

## Spec Index

### Core Architecture

| # | Spec | Status | Description |
|---|------|--------|-------------|
| 00 | [00-overview.md](00-overview.md) | ✅ | System overview and principles |
| 01 | [01-agents.md](01-agents.md) | ✅ | Agent architecture |
| 02 | [02-llm-providers.md](02-llm-providers.md) | ✅ | LLM abstraction layer |
| 03 | [03-data-model.md](03-data-model.md) | ✅ | Database schema |
| 04 | [04-api-endpoints.md](04-api-endpoints.md) | ✅ | REST API contract |
| 08 | [08-foundation.md](08-foundation.md) | ✅ | Foundation layer |
| 09 | [09-aspire-architecture.md](09-aspire-architecture.md) | ✅ | Aspire orchestration |
| 10 | [10-composable-modules.md](10-composable-modules.md) | ✅ | Module system |
| 11 | [11-agent-discovery.md](11-agent-discovery.md) | ✅ | Agent discovery |

### Developer Module

| # | Spec | Status | Description |
|---|------|--------|-------------|
| 05 | [05-git-worktrees.md](05-git-worktrees.md) | ✅ | Git worktree integration |
| 06 | [06-extension.md](06-extension.md) | ✅ | VS Code extension |
| 07 | [07-testing.md](07-testing.md) | ✅ | Testing strategy |
| 12 | [12-developer-module.md](12-developer-module.md) | ✅ | Developer workflow |
| - | [assisted-workflow-ui.md](assisted-workflow-ui.md) | ✅ | Assisted workflow UI |
| - | [tool-execution-for-agents.md](tool-execution-for-agents.md) | 🔄 | Tool execution |

### Indexing & RAG

| # | Spec | Status | Description |
|---|------|--------|-------------|
| 15 | [15-graph-and-indexing-enhancements.md](15-graph-and-indexing-enhancements.md) | 🔄 | Graph enhancements |
| 21 | [21-semantic-indexing.md](21-semantic-indexing.md) | ✅ | Semantic indexing |
| 22 | [22-ingester-agents.md](22-ingester-agents.md) | ✅ | Ingester agents |
| 23 | [23-hardcoded-agents.md](23-hardcoded-agents.md) | ✅ | Roslyn, TreeSitter |

### LLM & Agents

| # | Spec | Status | Description |
|---|------|--------|-------------|
| 24 | [24-llm-providers.md](24-llm-providers.md) | ✅ | Cloud LLM support |
| 35 | [35-generic-language-agent.md](35-generic-language-agent.md) | 🔲 | Generic agents |
| - | [unified-software-development-capability.md](unified-software-development-capability.md) | 🔲 | Unified capabilities |

### Future

| # | Spec | Status | Description |
|---|------|--------|-------------|
| 20 | [20-internationalization.md](20-internationalization.md) | 📋 | i18n support |

## Adding a New Spec

Use the next available number. Template:

```markdown
# Spec [NN]: [Title]

## Status
Draft | In Progress | Implemented | Superseded

## Overview
[What this feature does]

## Requirements
[Functional requirements]

## Design
[Technical design]

## API Changes
[New endpoints, if any]

## Migration
[Database/config changes]
```
