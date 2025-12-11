# Tasks

Active work items and feature requests.

## Status Key

| Icon | Status |
|------|--------|
| ✅ | Complete |
| 🔄 | In Progress |
| 🔲 | Not Started |
| ⏸️ | On Hold |

## Task Index

### Indexing & RAG

| Task | Status | Priority | Description |
|------|--------|----------|-------------|
| [treesitter-ingesters.md](treesitter-ingesters.md) | ✅ Complete | - | TreeSitter multi-language indexing |
| [00-indexing-roadmap.md](00-indexing-roadmap.md) | 🔄 In Progress | High | Master indexing roadmap |
| [gap-01-smart-content.md](gap-01-smart-content.md) | 🔲 Not Started | Medium | LLM-generated summaries |
| [gap-02-content-files.md](gap-02-content-files.md) | 🔲 Not Started | Medium | Index markdown, config |
| [gap-03-enrichment.md](gap-03-enrichment.md) | 🔲 Not Started | Medium | Cross-file relationships |
| [gap-07-boolean-queries.md](gap-07-boolean-queries.md) | 🔲 Not Started | Low | Boolean query parser |

### Infrastructure

| Task | Status | Priority | Description |
|------|--------|----------|-------------|
| [gap-04-mcp-server.md](gap-04-mcp-server.md) | 🔲 Not Started | Low | MCP protocol support |
| [gap-05-multi-workspace.md](gap-05-multi-workspace.md) | 🔲 Not Started | Low | Multi-repo indexing |
| [gap-06-condensed-export.md](gap-06-condensed-export.md) | 🔲 Not Started | Low | Graph export/import |

### UI & UX

| Task | Status | Priority | Description |
|------|--------|----------|-------------|
| [indexing-progress-ui.md](indexing-progress-ui.md) | 🔲 Not Started | Medium | Show indexing progress |

### Agents

| Task | Status | Priority | Description |
|------|--------|----------|-------------|
| [add-specialist-coding-agent.md](add-specialist-coding-agent.md) | 🔲 Not Started | Low | Language specialist agents |
| [file-aware-rag-queries.md](file-aware-rag-queries.md) | 🔲 Not Started | Low | Context-aware RAG |

## Adding a New Task

Create a new file with this template:

```markdown
# Task: [Title]

## Status
Not Started | In Progress | Complete

## Priority
Low | Medium | High | Critical

## Overview
[What needs to be done]

## Goals
1. [Goal 1]
2. [Goal 2]

## Implementation
[Details]

## Acceptance Criteria
- [ ] Criterion 1
- [ ] Criterion 2
```
