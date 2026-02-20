# Implementation Roadmap: Post-MVP Enhancements

**Created**: 2025-12-12  
**Status**: Planning  
**Last Updated**: 2025-12-12

## Overview

This document provides a prioritized sequence for implementing remaining planned features after the MVP is complete. Each task references its detailed implementation plan.

## Priority Tiers

| Tier | Timeline | Focus |
|------|----------|-------|
| **P0** | Next Sprint | Core functionality improvements |
| **P1** | Near Term | Enhanced search & capabilities |
| **P2** | Medium Term | Developer experience |
| **P3** | Long Term | Advanced features |

---

## P0: High Priority (Next Sprint)

### 1. Dependency Graph Edges
**Effort**: 4-6 hours  
**Plan**: [`plan/implementation/12-dependency-graph-edges.md`](implementation/12-dependency-graph-edges.md)  
**Task**: [`tasks/treesitter-ingesters.md`](../tasks/treesitter-ingesters.md) Phase 3

**Goal**: Use TreeSitter-extracted imports to create graph edges for "find all files that import X" queries.

**Why P0**: TreeSitter already extracts imports - this is low-hanging fruit that enables powerful queries.

**Steps**:
1. Add `Imports` edge type
2. Create `ImportEdgeBuilder` service
3. Wire into indexing pipeline
4. Add API endpoints
5. Add tests

---

### 2. Unified Capability Model
**Effort**: 6-8 hours  
**Plan**: [`plan/implementation/13-unified-capability-model.md`](implementation/13-unified-capability-model.md)  
**Spec**: [`spec/unified-software-development-capability.md`](../spec/unified-software-development-capability.md)

**Goal**: Consolidate `*-coding` capabilities to single `software-development-{language}` per language.

**Why P0**: Simplifies agent discovery, reduces confusion, cleaner architecture.

**Steps**:
1. Update RoslynCodingAgent capabilities
2. Add capability aliases for backward compatibility
3. Create config-based agent framework
4. Add language configs (TypeScript, Python, Go)
5. Update workflow planner prompt

---

## P1: Near Term

### 3. Smart Content (LLM Summaries)
**Effort**: 8-12 hours  
**Task**: [`tasks/gap-01-smart-content.md`](../tasks/gap-01-smart-content.md)

**Goal**: Generate LLM summaries for undocumented code, enable intent-based search.

**Why P1**: TreeSitter now extracts docstrings - Smart Content fills gaps for undocumented code.

**Steps**:
1. Add `SmartSummary` fields to CodeNode, SemanticChunk, RagChunk
2. Create `ISmartContentService` for background generation
3. Add dual-vector search (code OR intent)
4. Create background job with rate limiting
5. Add API endpoints

**Dependencies**: None (can run in parallel with P0)

---

### 4. Content File Indexing
**Effort**: 6-8 hours  
**Task**: [`tasks/gap-02-content-files.md`](../tasks/gap-02-content-files.md)

**Goal**: Index markdown, config, scripts as first-class graph nodes.

**Why P1**: Enables "find docs that mention WorkflowService" queries.

**Steps**:
1. Add `Content`, `Section` node types
2. Create markdown ingester
3. Create YAML/JSON ingesters
4. Create edges when docs reference code symbols
5. Add tests

**Dependencies**: None

---

### 5. Cross-File Enrichment
**Effort**: 8-12 hours  
**Task**: [`tasks/gap-03-enrichment.md`](../tasks/gap-03-enrichment.md)

**Goal**: Add transitive relationships: type usage, test-to-code mappings.

**Why P1**: Depends on TypeReferences from TreeSitter Phase 2 (✅ complete).

**Steps**:
1. Add `UsesType`, `Tests`, `Documents` edge types
2. Create enrichment pass after indexing
3. Walk call graphs for transitive relationships
4. Match test patterns to code
5. Add query methods

**Dependencies**: Dependency Graph Edges (P0 #1)

---

## P2: Medium Term

### 6. Boolean Query Parser
**Effort**: 4-6 hours  
**Task**: [`tasks/gap-07-boolean-queries.md`](../tasks/gap-07-boolean-queries.md)

**Goal**: Support `AND`, `OR`, `NOT` and type prefixes in search.

**Why P2**: Nice-to-have, current search is sufficient for MVP.

**Steps**:
1. Create query parser
2. Add type prefix support (`class:`, `method:`, `file:`)
3. Combine with vector similarity
4. Add tests

**Dependencies**: None

---

### 7. Indexing Progress UI
**Effort**: 4-6 hours  
**Task**: [`tasks/indexing-progress-ui.md`](../tasks/indexing-progress-ui.md)

**Goal**: Show indexing progress in VS Code extension.

**Why P2**: Improves UX but not blocking.

**Steps**:
1. Add progress tracking to indexer
2. Create SSE/WebSocket endpoint for progress
3. Add UI in extension status view
4. Show file count, estimated time

**Dependencies**: None

---

### 8. Tool Execution Part 2
**Effort**: 8-12 hours  
**Spec**: [`spec/tool-execution-for-agents.md`](../spec/tool-execution-for-agents.md) Part 2

**Goal**: Full tool binding and human-in-the-loop confirmation.

**Why P2**: Part 1 (frontmatter) is done. Part 2 adds confirmation flow.

**Steps**:
1. Add `ChatWithFunctionsAsync` to providers
2. Create `IToolConfirmationService`
3. Add WebSocket for approval requests
4. Update extension UI for tool approval
5. Add configuration for auto-approve vs confirm

**Dependencies**: None

---

## P3: Long Term / Future

### 9. MCP Server
**Effort**: 12-16 hours  
**Task**: [`tasks/gap-04-mcp-server.md`](../tasks/gap-04-mcp-server.md)

**Goal**: Expose code graph as MCP server for AI assistants.

**Why P3**: Enables integration with external AI tools but not core functionality.

**Steps**:
1. Create `Aura.Mcp` project
2. Implement MCP tools (search, find implementations, etc.)
3. Add stdio transport
4. Test with Claude, GitHub Copilot

**Dependencies**: Core graph queries working (P0-P1)

---

### 10. Multi-Workspace Registry
**Effort**: 12-16 hours  
**Task**: [`tasks/gap-05-multi-workspace.md`](../tasks/gap-05-multi-workspace.md)

**Goal**: Index multiple repos, cross-workspace queries.

**Why P3**: Most users work on one project at a time.

**Steps**:
1. Add `WorkspaceRegistry` entity
2. Create CLI commands
3. Enable cross-workspace queries
4. Add workspace management UI

**Dependencies**: None

---

### 11. Condensed Export
**Effort**: 6-8 hours  
**Task**: [`tasks/gap-06-condensed-export.md`](../tasks/gap-06-condensed-export.md)

**Goal**: Export graph to portable JSONL for sharing/backup.

**Why P3**: Nice-to-have for enterprise/team scenarios.

**Steps**:
1. Create export format
2. Implement export service
3. Implement import service
4. Add CLI commands

**Dependencies**: None

---

## Future Verticals

### Research Module
**Effort**: 40+ hours  
**Spec**: [`spec/00-overview.md`](../spec/00-overview.md)

Paper management, synthesis, citations. Post-v1 scope.

---

### Personal Module
**Effort**: 40+ hours  
**Spec**: [`spec/00-overview.md`](../spec/00-overview.md)

Receipts, budgets, general assistant. Post-v1 scope.

---

## Effort Summary

| Priority | Tasks | Total Effort |
|----------|-------|--------------|
| P0 | 2 tasks | 10-14 hours |
| P1 | 3 tasks | 22-32 hours |
| P2 | 3 tasks | 16-24 hours |
| P3 | 3 tasks | 30-40 hours |
| **Total** | **11 tasks** | **78-110 hours** |

---

## Recommended Sprint Plan

### Sprint 1 (Week 1)
- [ ] Dependency Graph Edges (P0)
- [ ] Unified Capability Model (P0)

### Sprint 2 (Week 2)
- [ ] Smart Content (P1)
- [ ] Content File Indexing (P1)

### Sprint 3 (Week 3)
- [ ] Cross-File Enrichment (P1)
- [ ] Boolean Query Parser (P2)

### Sprint 4 (Week 4)
- [ ] Indexing Progress UI (P2)
- [ ] Tool Execution Part 2 (P2)

### Future Sprints
- MCP Server, Multi-Workspace, Export (as needed)

---

## Document Index

| Plan | Description |
|------|-------------|
| [`12-dependency-graph-edges.md`](implementation/12-dependency-graph-edges.md) | Import edges from TreeSitter data |
| [`13-unified-capability-model.md`](implementation/13-unified-capability-model.md) | Consolidate coding capabilities |
| [`gap-01-smart-content.md`](../tasks/gap-01-smart-content.md) | LLM summaries for code |
| [`gap-02-content-files.md`](../tasks/gap-02-content-files.md) | Index markdown, config |
| [`gap-03-enrichment.md`](../tasks/gap-03-enrichment.md) | Cross-file relationships |
| [`gap-04-mcp-server.md`](../tasks/gap-04-mcp-server.md) | MCP protocol support |
| [`gap-05-multi-workspace.md`](../tasks/gap-05-multi-workspace.md) | Multi-repo indexing |
| [`gap-06-condensed-export.md`](../tasks/gap-06-condensed-export.md) | Graph export/import |
| [`gap-07-boolean-queries.md`](../tasks/gap-07-boolean-queries.md) | Advanced query syntax |
