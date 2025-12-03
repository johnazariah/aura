# Aura Graph & Indexing Enhancements Spec

## Overview

This spec identifies gaps in Aura's current RAG and code graph implementation compared to state-of-the-art code intelligence systems, and proposes enhancements to close those gaps.

## Current State

Aura has:
- PostgreSQL-backed code graph (`CodeNode`, `CodeEdge` entities)
- **Agent-based ingestion** via `IAgent` with `ingest:{extension}` capabilities (see [Spec 22](./22-ingester-agents.md))
- Roslyn-based C# parsing via `CSharpIngesterAgent` (see [Spec 23](./23-hardcoded-agents.md))
- Vector search via pgvector
- `ICodeGraphService` for relationship queries (implementations, callers, derived types)
- Hardcoded agents for performance-critical parsing (Roslyn, TreeSitter)
- Markdown agents for LLM-based parsing (hot-reloadable, extensible)

> **Note**: The previous `IFileIndexingStrategy` pattern has been superseded by agent-based ingestion.
> See [Spec 21](./21-semantic-indexing.md) for historical context.

## Identified Gaps

### Gap 1: Smart Content (LLM-Generated Summaries)

**Problem**: Code chunks are stored with raw content only. Users must read code to understand what it does.

**Solution**: Generate concise LLM summaries for every indexed element (file, class, method). Store both the summary and its embedding for semantic search by *intent*.

**Data Model Changes**:
```csharp
public class SemanticChunk
{
    // Existing fields...
    
    /// <summary>
    /// LLM-generated summary explaining what this code does.
    /// Generated asynchronously during indexing.
    /// </summary>
    public string? SmartSummary { get; init; }
    
    /// <summary>
    /// Embedding vector for the smart summary (for intent-based search).
    /// </summary>
    public Vector? SmartSummaryEmbedding { get; init; }
}
```

**Implementation**:
1. After chunking, queue chunks for smart summary generation
2. Use Ollama (local) to generate 1-2 sentence summaries
3. Embed the summary separately from the raw content
4. Support dual-vector search: code similarity OR intent similarity

**Example**:
```
Code: public async Task<Result<Workflow>> ExecuteAsync(...)
Smart Summary: "Executes a workflow step-by-step, with human-in-the-loop approval between steps."
```

---

### Gap 2: Content File Indexing

**Problem**: Only code files are indexed. Markdown docs, config files, scripts, and other text files are ignored.

**Solution**: Treat non-code files as first-class citizens in the graph with their own node type and embeddings.

**Data Model Changes**:
```csharp
public enum CodeNodeType
{
    // Existing...
    
    /// <summary>A content file (markdown, config, script, etc.)</summary>
    Content,
    
    /// <summary>A section within a document.</summary>
    Section,
}
```

**Implementation**:

1. Add ingester agents for content files (e.g., `markdown-ingester.md`, `yaml-ingester.md`)
2. Parse markdown into sections (headers become child nodes)
3. Create edges when content files reference code symbols (e.g., `ClassName` mentions)
4. Enable queries like "find all docs that mention WorkflowService"

---

### Gap 3: Cross-File Relationship Enrichment

**Problem**: Current graph captures basic relationships. Missing: transitive call chains, type usage across files, and test-to-code mappings.

**Solution**: Add enrichment phase using SCIP (Source Code Intelligence Protocol) or enhanced Roslyn analysis.

**New Edge Types**:
```csharp
public enum CodeEdgeType
{
    // Existing...
    Contains,
    Inherits,
    Implements,
    Calls,
    References,
    
    // New
    /// <summary>Method uses this type (parameter, return, local variable).</summary>
    UsesType,
    
    /// <summary>Test method transitively tests this code.</summary>
    Tests,
    
    /// <summary>Document references this code element.</summary>
    Documents,
}
```

**Implementation**:
1. Post-indexing enrichment pass
2. Walk call graphs to compute transitive relationships
3. Match test file patterns to discover test coverage edges
4. Link content files to code they mention

---

### Gap 4: MCP Server for Agent Access

**Problem**: Agents can only access the graph via REST API. No native MCP (Model Context Protocol) support.

**Solution**: Expose `ICodeGraphService` as an MCP server so AI assistants can directly query the graph.

**MCP Tools to Expose**:
| Tool | Description |
|------|-------------|
| `search_nodes` | Text/regex/semantic search across all nodes |
| `find_implementations` | Find all types implementing an interface |
| `find_callers` | Find all methods calling a given method |
| `find_dependencies` | Find what a method depends on |
| `get_node_content` | Get full content + smart summary for a node |
| `get_related_tests` | Find tests that exercise a given code element |
| `get_documentation` | Find docs that reference a code element |

**Implementation**:
1. Create `Aura.Mcp` project
2. Use FastMCP or similar .NET MCP library
3. Wire to existing `ICodeGraphService`
4. Expose via stdio for VS Code extension integration

---

### Gap 5: Multi-Workspace Registry

**Problem**: Aura indexes one workspace at a time. No way to query across multiple repositories.

**Solution**: Add workspace registry for managing multiple indexed repositories.

**Data Model**:
```csharp
public class WorkspaceRegistry
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Path { get; init; }
    public string? RemoteUrl { get; init; }
    public WorkspaceStatus Status { get; init; }
    public DateTimeOffset LastIndexed { get; init; }
}

public enum WorkspaceStatus
{
    Pending,
    Indexing,
    Ready,
    Error,
    Archived,
}
```

**Implementation**:
1. Add `WorkspaceRegistry` entity and `IWorkspaceRegistryService`
2. Support `aura workspace add <path>` CLI command
3. Enable cross-workspace queries: "find all usages of ILogger across all repos"
4. Allow exporting condensed graph as shareable JSON

---

### Gap 6: Condensed Export Format

**Problem**: Graph data is locked in PostgreSQL. Cannot share or use offline.

**Solution**: Export graph to portable JSONL format for sharing, backup, or offline use.

**Format**:
```jsonl
{"type":"meta","version":"1.0","workspace":"aura","indexed_at":"2025-12-03T..."}
{"type":"node","id":"class:Aura.Foundation/WorkflowService","name":"WorkflowService","nodeType":"Class","smartSummary":"...","embedding":[...]}
{"type":"node","id":"method:Aura.Foundation/WorkflowService.Execute","name":"Execute","nodeType":"Method",...}
{"type":"edge","source":"method:...","target":"method:...","edgeType":"Calls"}
```

**Implementation**:
1. Add `IGraphExporter` service
2. Export command: `aura export --output graph.jsonl`
3. Import command: `aura import graph.jsonl`
4. Enable loading external graphs for cross-repo queries

---

### Gap 7: Boolean & Advanced Query Syntax

**Problem**: Current queries are single-term. No support for boolean operators or complex patterns.

**Solution**: Add query parser supporting OR, AND, and pattern prefixes.

**Syntax**:
```
# Boolean OR (union of results)
Calculator OR Validator

# Type-prefixed search
class:Calculator
method:Execute
file:WorkflowService.cs

# Class-scoped method search  
method:Calculator.Add

# Regex (auto-detected or explicit)
class:.*Service$
-r "method:Get.*Async"

# Combined
(class:Calculator OR class:Validator) AND method:.*Async
```

**Implementation**:
1. Add `QueryParser` that tokenizes and builds expression tree
2. Execute sub-queries and merge/intersect results
3. Support in both REST API and MCP tools

---

## Implementation Phases

### Phase 1: Smart Content
- Add `SmartSummary` field to chunks
- Background job to generate summaries via Ollama
- Dual-vector search support

### Phase 2: Content Files

- Add content ingester agents for markdown/text
- New `Content` node type
- Document-to-code linking

### Phase 3: Enrichment
- Add `UsesType`, `Tests`, `Documents` edge types
- Post-indexing enrichment pass
- Transitive relationship computation

### Phase 4: MCP Server
- Create `Aura.Mcp` project
- Implement core MCP tools
- VS Code extension integration

### Phase 5: Multi-Workspace
- Workspace registry
- Cross-workspace queries
- Condensed export/import

### Phase 6: Advanced Queries
- Query parser
- Boolean operators
- Pattern prefixes

---

## Success Criteria

1. **Smart Content**: Every indexed method has a summary; intent-based search works
2. **Content Files**: README.md appears in graph; can find "docs mentioning WorkflowService"
3. **Enrichment**: Can query "what tests cover this method" transitively
4. **MCP**: Claude/Copilot agents can query graph via MCP tools
5. **Multi-Workspace**: Can index 3+ repos and query across all of them
6. **Queries**: `class:.*Service OR interface:I.*` returns expected results

---

## Dependencies

- Ollama (for smart summary generation)
- MCP library for .NET (FastMCP or equivalent)
- SCIP tooling (optional, for enhanced cross-file analysis)

## Risks

1. **Smart Summary Cost**: Generating summaries for large codebases takes time
   - Mitigation: Background processing, incremental updates, caching

2. **Graph Size**: Adding content files may bloat the graph
   - Mitigation: Configurable inclusion patterns, lazy loading

3. **MCP Stability**: MCP spec is evolving
   - Mitigation: Abstract behind interface, version pinning

---

## Implementation Status

> **Last Updated**: December 2025

### Completed (via Specs 21-23)

The ingestion architecture has been fully implemented:

| Component | Status | Location |
|-----------|--------|----------|
| Agent-based ingestion | ✅ Done | [Spec 22](./22-ingester-agents.md) |
| Hardcoded agent pattern | ✅ Done | [Spec 23](./23-hardcoded-agents.md) |
| `IHardcodedAgentProvider` | ✅ Done | `Aura.Foundation/Agents/` |
| `CSharpIngesterAgent` (Roslyn) | ✅ Done | `Aura.Module.Developer/Agents/` |
| `TextIngesterAgent` | ✅ Done | `Aura.Foundation/Agents/` |
| `FallbackIngesterAgent` | ✅ Done | `Aura.Foundation/Agents/` |
| Capability-based agent lookup | ✅ Done | `AgentRegistry.GetBestForCapability()` |
| Wildcard capabilities (`ingest:*`) | ✅ Done | `AgentRegistry` |

### In Progress

| Gap | Task | Status |
|-----|------|--------|
| Gap 1 | Smart Content | 📋 Planned |
| Gap 2 | Content File Indexing | 📋 Planned |
| Gap 3 | Cross-File Enrichment | 📋 Planned |
| Gap 4 | MCP Server | 📋 Planned |
| Gap 5 | Multi-Workspace | 📋 Planned |
| Gap 6 | Condensed Export | 📋 Planned |
| Gap 7 | Boolean Queries | 📋 Planned |

### Superseded

The following patterns from [Spec 21](./21-semantic-indexing.md) have been replaced:

- ❌ `IFileIndexingStrategy` → Use `IAgent` with `ingest:{ext}` capability
- ❌ Strategy pattern dispatcher → Use `AgentRegistry.GetBestForCapability()`
- ❌ Per-language strategy classes → Use ingester agents (hardcoded or markdown)

See task files in `.project/tasks/gap-*.md` for implementation details.
