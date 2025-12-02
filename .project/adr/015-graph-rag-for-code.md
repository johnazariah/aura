# ADR-015: Graph RAG for Code Understanding

## Status

Implemented

## Date

2025-12-02

## Context

Effective code generation requires understanding:
- What classes exist and what they do
- How classes relate to each other (inheritance, composition, calls)
- Where to find patterns to follow
- What dependencies are available

Simple vector RAG (chunk text → embed → similarity search) loses structural information. Asking "what implements IWorkflowService" requires graph traversal, not vector similarity.

## Decision

**Implement Entity-Relationship Graph RAG for code understanding.**

### Graph Model

**Nodes:**
| Node Type | Represents | Key Properties |
|-----------|------------|----------------|
| `Solution` | .sln file | Path, Name |
| `Project` | .csproj file | Path, Name, Framework, OutputType |
| `Namespace` | C# namespace | Name, FullName |
| `Type` | Class/Interface/Record/Struct/Enum | Name, FullName, Kind, Modifiers |
| `Member` | Method/Property/Field/Event | Name, Signature, ReturnType, Modifiers |
| `File` | Source file | Path, Language |

**Edges:**
| Edge Type | From | To | Meaning |
|-----------|------|-----|---------|
| `contains` | Solution | Project | Solution contains project |
| `contains` | Project | File | Project contains file |
| `contains` | File | Type | File contains type |
| `contains` | Type | Member | Type contains member |
| `declares` | Namespace | Type | Namespace declares type |
| `inherits` | Type | Type | Class inheritance |
| `implements` | Type | Type | Interface implementation |
| `references` | Project | Project | Project reference |
| `calls` | Member | Member | Method calls method |
| `uses` | Member | Type | Method uses type |

### Storage

PostgreSQL with the graph stored relationally:

```sql
CREATE TABLE code_nodes (
    id UUID PRIMARY KEY,
    node_type VARCHAR(50) NOT NULL,
    name VARCHAR(500) NOT NULL,
    full_name VARCHAR(2000),
    file_path VARCHAR(1000),
    line_number INT,
    properties JSONB,  -- Type-specific data
    workspace_path VARCHAR(1000),  -- For multi-worktree isolation
    indexed_at TIMESTAMPTZ
);

CREATE TABLE code_edges (
    id UUID PRIMARY KEY,
    edge_type VARCHAR(50) NOT NULL,
    source_id UUID REFERENCES code_nodes(id),
    target_id UUID REFERENCES code_nodes(id),
    properties JSONB
);

-- Indexes for common queries
CREATE INDEX idx_nodes_type ON code_nodes(node_type);
CREATE INDEX idx_nodes_workspace ON code_nodes(workspace_path);
CREATE INDEX idx_edges_source ON code_edges(source_id);
CREATE INDEX idx_edges_target ON code_edges(target_id);
CREATE INDEX idx_edges_type ON code_edges(edge_type);
```

### Query Patterns

**"What implements IWorkflowService?"**
```sql
SELECT t.* FROM code_nodes t
JOIN code_edges e ON e.source_id = t.id
JOIN code_nodes i ON e.target_id = i.id
WHERE e.edge_type = 'implements'
  AND i.name = 'IWorkflowService';
```

**"What methods call ExecuteAsync?"**
```sql
SELECT caller.* FROM code_nodes caller
JOIN code_edges e ON e.source_id = caller.id
JOIN code_nodes callee ON e.target_id = callee.id
WHERE e.edge_type = 'calls'
  AND callee.name = 'ExecuteAsync';
```

**"What does WorkflowService depend on?"**
```sql
WITH RECURSIVE deps AS (
    SELECT target_id, 1 as depth
    FROM code_edges
    WHERE source_id = (SELECT id FROM code_nodes WHERE name = 'WorkflowService')
      AND edge_type IN ('uses', 'calls', 'inherits', 'implements')
    
    UNION
    
    SELECT e.target_id, d.depth + 1
    FROM code_edges e
    JOIN deps d ON e.source_id = d.target_id
    WHERE d.depth < 3  -- Limit depth
)
SELECT DISTINCT n.* FROM code_nodes n
JOIN deps d ON n.id = d.target_id;
```

### Hybrid with Vector RAG

Graph RAG handles structural queries. Vector RAG handles semantic queries:

- **Graph**: "What implements X?" / "What calls Y?" / "What's in namespace Z?"
- **Vector**: "Find code similar to authentication" / "How do we handle errors?"

Both systems complement each other.

### Location

- **Graph schema**: `Aura.Foundation` (tables are general-purpose)
- **C# graph builder**: `Aura.Module.Developer` (Roslyn-based parsing)
- **Query APIs**: `Aura.Foundation.Rag` (graph queries alongside vector queries)

## Consequences

### Positive

- Structural queries are fast and precise
- Relationships are explicit, not inferred
- Scales to large codebases with proper indexing
- Complements vector RAG for different query types

### Negative

- More complex than pure vector RAG
- Requires keeping graph in sync with code changes
- Schema design affects query capabilities

### Mitigations

- Re-index on workflow creation (worktree is point-in-time)
- Start with core node/edge types, extend as needed
- Port proven patterns from hve-hack graph implementation

## References

- ADR-014: Developer Module Roslyn Tools (ingestion uses same Roslyn infrastructure)
- hve-hack Graph RAG implementation (to be ported)
