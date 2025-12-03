# Indexing & Semantic Extraction Roadmap

## Status Summary

| Task | Status | Priority |
|------|--------|----------|
| TreeSitter Basic Extraction | ✅ Complete | - |
| TreeSitter Semantic Enrichment | 🔄 In Progress | High |
| Smart Content (LLM Summaries) | 🔲 Not Started | Medium |
| Content File Indexing | 🔲 Not Started | Low |
| Cross-File Enrichment | 🔲 Not Started | Low |

## Current Architecture

```
File → Ingester Selection (by priority) → SemanticChunk → RagChunk (embedded)
                                              ↓
                                         CodeNode/CodeEdge (graph)
```

### Ingester Priority Stack

| Priority | Ingester | Status | Languages |
|----------|----------|--------|-----------|
| 10 | CSharpIngesterAgent | ✅ | C# (Roslyn - full semantic) |
| 20 | TreeSitterIngesterAgent | ✅ | 30+ languages (AST only) |
| 40 | generic-code-ingester | ✅ | F#, any unsupported (LLM) |
| 70 | TextIngesterAgent | ✅ | Plain text |
| 99 | FallbackIngesterAgent | ✅ | Everything else |

## The Gap: Shallow vs Deep Extraction

### What We Extract Now (TreeSitter)

```python
def calculate_total(items: List[Item], tax_rate: float = 0.08) -> Decimal:
    """Calculate total including tax."""
    subtotal = sum(item.price for item in items)
    return subtotal * (1 + tax_rate)
```

**Current extraction:**

- ✅ Full function text as chunk
- ✅ Symbol name: `calculate_total`
- ✅ Chunk type: `function`
- ✅ Line numbers: 1-5
- ✅ Parent symbol (if nested in class)

### What Competition Extracts

- ✅ Everything above, plus:
- ⬜ **Signature only**: `def calculate_total(items: List[Item], tax_rate: float = 0.08) -> Decimal`
- ⬜ **Docstring**: `"Calculate total including tax."`
- ⬜ **Parameters**: `[{name: "items", type: "List[Item]"}, {name: "tax_rate", type: "float", default: "0.08"}]`
- ⬜ **Return type**: `Decimal`
- ⬜ **Type references**: `List`, `Item`, `Decimal`
- ⬜ **Decorators**: (none in this example)

This enables:

- "Find functions that take a List parameter"
- "Find functions that return Decimal"
- "Find all uses of the Item type"
- Semantic search by docstring content

## Implementation Phases

### Phase 1: TreeSitter Basic ✅ DONE

- Unified `TreeSitterIngesterAgent` for 30+ languages
- Basic chunk extraction (text, symbol, type, lines)
- Delete redundant regex ingesters

### Phase 2: TreeSitter Semantic Enrichment 🎯 NEXT

**Goal**: Extract signatures, docstrings, parameters, types

Tasks:

1. Extend `SemanticChunk` with new fields
2. Create language-specific extractors for:
   - Python (docstrings, type hints, decorators)
   - TypeScript/JavaScript (JSDoc, type annotations)
   - Go (doc comments, struct tags)
   - Rust (/// comments, lifetime annotations)
   - Java (Javadoc, annotations)
3. Extract imports for dependency graph

**Files to modify:**

- `src/Aura.Foundation/Rag/SemanticChunk.cs` - Add fields
- `src/Aura.Module.Developer/Agents/TreeSitterIngesterAgent.cs` - Add extractors

### Phase 3: Smart Content (LLM Summaries)

**Goal**: LLM-generated summaries for undocumented code

- Only process chunks without extracted docstrings
- Background job, not blocking indexing
- Dual-vector search (code embedding + summary embedding)

**Dependency**: Phase 2 (so we know which chunks need LLM summaries)

### Phase 4: Content File Indexing

**Goal**: Index markdown, YAML, scripts as first-class citizens

- New node types: `Content`, `Section`, `ConfigBlock`
- Extract markdown headers as sections
- Link docs to code via symbol references

### Phase 5: Cross-File Enrichment

**Goal**: Build relationship graph across files

- `UsesType` edges from `TypeReferences` field
- `Tests` edges from test file detection
- Transitive call chains

**Dependency**: Phase 2 (needs TypeReferences)

## Success Metrics

| Metric | Current | Target |
|--------|---------|--------|
| Searchable by signature | ❌ | ✅ |
| Searchable by docstring | ❌ | ✅ |
| Type relationship graph | ❌ | ✅ |
| Test coverage mapping | ❌ | ✅ |
| Files/second (TreeSitter) | ~1000 | ~1000 |
| Files/second (LLM) | ~1 | ~1 (background only) |

## Related Files

- `treesitter-ingesters.md` - TreeSitter implementation details
- `gap-01-smart-content.md` - LLM summary generation
- `gap-02-content-files.md` - Non-code file indexing
- `gap-03-enrichment.md` - Cross-file relationships

## Competition Analysis

### Cursor

- SCIP-based indexing for type-aware search
- Semantic search by intent
- "Find functions that return X"

### Cody (Sourcegraph)

- Full repository graph
- Cross-repo references
- Enterprise-grade indexing

### Continue

- TreeSitter + embeddings hybrid
- Fast local parsing
- LLM-enhanced retrieval

### Our Approach

- TreeSitter for fast AST parsing (✅ done)
- Rich semantic extraction from AST (🎯 next)
- LLM for summaries only when needed (cheaper)
- Local-first, no cloud dependency
