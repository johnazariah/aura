# Indexing Epic - Future Enhancements

**Status:** Unplanned  
**Priority:** Backlog  
**Created:** 2025-12-12

## Overview

This epic captures future indexing enhancements that are valuable but not immediate priorities. These will be promoted to individual stories when capacity allows.

## Related Completed Stories

- ✅ [TreeSitter Ingesters](../completed/treesitter-ingesters.md) - Multi-language AST parsing
- ✅ [Semantic Indexing](../completed/semantic-indexing.md) - Basic semantic chunking
- ✅ [Ingester Agents](../completed/ingester-agents.md) - Agent-based ingestion
- ✅ [File-Aware RAG Queries](../completed/file-aware-rag-queries.md) - Scoped queries
- ✅ [Code Graph Status Panel](../completed/code-graph-status-panel.md) - Sidebar status

## Related Active Stories

- 🔲 [Unified Indexing Backend](../upcoming/unified-indexing-backend.md) - Single pipeline for RAG + Code Graph
- 🔲 [Indexing UX](../upcoming/indexing-ux.md) - Progress UI, prompts, query commands

---

## Deferred Features

### 1. Dependency Graph Edges

**Priority:** High  
**Effort:** 3-4 hours  
**Source:** indexing-roadmap.md Phase 3

**Problem:** Current graph captures class/method nodes but not import/dependency relationships.

**Solution:** Use extracted imports to create graph edges.

**Tasks:**

1. Create `Imports` edge type in CodeGraph
2. Parse `using` statements in C# (Roslyn already does this)
3. Parse `import` statements in Python/TypeScript (TreeSitter)
4. Store edges: `File → Imports → Type/Module`

**Value:** Enables "what files depend on this?" and "what does this file depend on?" queries.

---

### 2. Smart Content (LLM Summaries)

**Priority:** Medium  
**Effort:** 4-6 hours  
**Source:** graph-and-indexing-enhancements.md Gap 1

**Problem:** Code chunks have raw content only. Users must read code to understand intent.

**Solution:** Generate LLM summaries for indexed elements.

```csharp
public class SemanticChunk
{
    // Existing fields...
    public string? SmartSummary { get; init; }
    public Vector? SmartSummaryEmbedding { get; init; }
}
```

**Tasks:**

1. Queue chunks for summary generation after chunking
2. Use Ollama to generate 1-2 sentence summaries
3. Embed summaries separately from raw content
4. Support dual-vector search: code similarity OR intent similarity

**Value:** Search by intent: "function that validates user input" finds relevant code without keyword match.

---

### 3. Content File Indexing

**Priority:** Low  
**Effort:** 3-4 hours  
**Source:** graph-and-indexing-enhancements.md Gap 2

**Problem:** Only code files are indexed. Markdown docs, config files, scripts are ignored.

**Solution:** Treat content files as first-class citizens in the graph.

```csharp
public enum CodeNodeType
{
    // Existing...
    Content,   // A content file (markdown, config, etc.)
    Section,   // A section within a document
}
```

**Tasks:**

1. Add ingester agents for `*.md`, `*.yaml`, `*.json`
2. Parse markdown into sections (headers become child nodes)
3. Create edges when content references code symbols
4. Enable queries like "find docs mentioning WorkflowService"

**Value:** Documentation included in semantic search, code-doc relationships visible.

---

### 4. Cross-File Enrichment

**Priority:** Low  
**Effort:** 6-8 hours  
**Source:** graph-and-indexing-enhancements.md Gap 3

**Problem:** Current graph captures basic relationships. Missing: transitive call chains, type usage across files, test-to-code mappings.

**Solution:** Add enrichment phase using enhanced Roslyn analysis.

**New Edge Types:**

- `TransitiveCalls` - A → B → C call chains
- `UsesType` - Method uses type from another file
- `TestsCode` - Test class tests production class

**Tasks:**

1. Post-indexing enrichment pass
2. Cross-reference method calls to resolve types
3. Match test files to production files by naming convention
4. Store derived edges

**Value:** Richer queries, better impact analysis.

---

### 5. Boolean Queries for RAG

**Priority:** Low  
**Effort:** 2-3 hours  
**Source:** boolean-queries.md

**Problem:** Current RAG search is pure vector similarity. Can't do "search for X but not in test files".

**Solution:** Add boolean query support.

**Query Syntax:**

```text
"WorkflowService" AND path:src/* NOT path:tests/*
```

**Tasks:**

1. Parse query into AST
2. Combine vector search with SQL WHERE clauses
3. Support operators: AND, OR, NOT, path:, type:

**Value:** Precise search control, exclude noise.

---

### 6. Tray Application Progress Display

**Priority:** Low  
**Effort:** 2-3 hours  
**Source:** indexing-progress-ui.md

**Problem:** Users on Windows/Mac can't see indexing progress without opening VS Code.

**Solution:** Show progress in system tray.

**Tasks:**

1. Add `IndexingMonitor` background service to tray app
2. Poll `/api/index/status` every 2 seconds
3. Update tooltip: "Aura - Indexing: 45% (123/456 files)"
4. Show notification on completion

**Value:** Awareness without opening VS Code.

---

## Superseded Documents

The following documents have been consolidated into this epic and the two active stories:

| Document | Content Merged Into |
|----------|---------------------|
| `unified-indexing-pipeline.md` | [unified-indexing-backend.md](../upcoming/unified-indexing-backend.md) |
| `unified-indexing-implementation.md` | [unified-indexing-backend.md](../upcoming/unified-indexing-backend.md) |
| `indexing-ux-improvements.md` | [indexing-ux.md](../upcoming/indexing-ux.md) |
| `indexing-progress-ui.md` | [indexing-ux.md](../upcoming/indexing-ux.md), this epic (#6) |
| `graph-and-indexing-enhancements.md` | This epic (#2, #3, #4) |
| `indexing-roadmap.md` | This epic (#1), historical reference |
| `dependency-graph-edges.md` | This epic (#1) |
| `smart-content.md` | This epic (#2) |
| `content-file-indexing.md` | This epic (#3) |
| `cross-file-enrichment.md` | This epic (#4) |
| `boolean-queries.md` | This epic (#5) |

These superseded documents should be archived to `.project/reference/archive/` or deleted.
