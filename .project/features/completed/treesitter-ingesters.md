# TreeSitter Ingesters

**Status:** ✅ Complete  
**Completed:** 2025-12-10  
**Last Updated:** 2025-12-10

## Overview

TreeSitter-based ingester for 30+ languages is implemented using `TreeSitter.DotNet v1.1.1`. Phase 2a semantic enrichment is complete with signatures, docstrings, imports, parameters, types, and decorators.

## What's Implemented

### Phase 1 (Complete)
- **Package**: `TreeSitter.DotNet v1.1.1` (correct package - `TreeSitter.Bindings` doesn't work)
- **Agent**: `TreeSitterIngesterAgent.cs` in `Aura.Module.Developer`
- **Languages**: Python, TypeScript, JavaScript, Go, Rust, Java, C/C++, Ruby, PHP, Swift, Scala, Haskell, OCaml, Julia, Bash, HTML, CSS, JSON, TOML
- **Not Supported**: F# (no grammar available - falls back to LLM at priority 40)

### Phase 2a (Complete - Semantic Enrichment)
- **Extended SemanticChunk** with new fields:
  - `Signature` - function/method signature line
  - `Docstring` - full docstring/doc comment
  - `Summary` - first sentence of docstring
  - `ReturnType` - return type annotation
  - `Parameters` - list of `ParameterInfo` (name, type, default, description)
  - `Decorators` - decorators/attributes
  - `TypeReferences` - types used in signature
  - `Imports` - module imports from file root

- **Language-specific extractors implemented**:
  - ✅ Python (signatures, type hints, decorators, docstrings, imports)
  - ✅ TypeScript (signatures, type annotations, JSDoc, imports)
  - ✅ JavaScript (signatures, JSDoc, imports)
  - ✅ Go (signatures, doc comments, parameters, imports)
  - ✅ Rust (signatures, doc comments, attributes, imports)
  - ✅ Java (signatures, Javadoc, annotations, imports)

- **17 unit tests** covering all extraction features

## Current Priority Scheme

| Priority | Agent | Parser |
|----------|-------|--------|
| 10 | CSharpIngesterAgent | Roslyn (full semantic) |
| 20 | TreeSitterIngesterAgent | TreeSitter (AST) |
| 40 | generic-code-ingester | LLM |
| 70 | TextIngesterAgent | Line-based |
| 99 | FallbackIngesterAgent | None |

## Gap: Richer Semantic Extraction

The competition extracts more than just code chunks. We should extract:

### 1. **Signatures and Docstrings** (High Priority)
Extract function/method signatures separately from bodies:
```python
# Current: Just the whole function as one chunk
# Desired: Extract signature + docstring as searchable metadata

def calculate_total(items: List[Item], tax_rate: float = 0.08) -> Decimal:
    """Calculate total price including tax.
    
    Args:
        items: List of items to sum
        tax_rate: Tax rate as decimal (default 8%)
    
    Returns:
        Total price including tax
    """
    ...
```

**Extract**:
- Signature: `def calculate_total(items: List[Item], tax_rate: float = 0.08) -> Decimal`
- Parameters: `items: List[Item]`, `tax_rate: float = 0.08`
- Return type: `Decimal`
- Docstring: Full docstring text
- Summary: First line of docstring ("Calculate total price including tax.")

### 2. **Type References** (Medium Priority)
Extract types used in signatures for relationship edges:
```typescript
// Function uses these types:
function processOrder(order: Order, customer: Customer): Invoice
```
**Extract**: References to `Order`, `Customer`, `Invoice`

### 3. **Decorators/Attributes** (Medium Priority)
```python
@router.post("/api/orders")
@requires_auth
async def create_order(request: OrderRequest) -> OrderResponse:
```
**Extract**: `@router.post("/api/orders")`, `@requires_auth`

### 4. **Imports/Dependencies** (Medium Priority)
```typescript
import { useState, useEffect } from 'react';
import { OrderService } from './services/OrderService';
```
**Extract**: Module dependencies for dependency graph

### 5. **Comments Near Code** (Low Priority)
Comments immediately before a function/class are often important:
```python
# DEPRECATED: Use new_calculate_total instead
# TODO: Remove in v2.0
def calculate_total(items):
```

## Implementation Plan

### Phase 2a: Enrich Chunk Metadata

Update `SemanticChunk` to include:
```csharp
public record SemanticChunk
{
    // Existing
    public string Text { get; init; }
    public string? SymbolName { get; init; }
    public string? ChunkType { get; init; }
    ...
    
    // NEW: Richer extraction
    public string? Signature { get; init; }           // Just the signature line
    public string? Docstring { get; init; }           // Full docstring/JSDoc/XML doc
    public string? Summary { get; init; }             // First line/sentence of doc
    public string? ReturnType { get; init; }          // Return type if available
    public IReadOnlyList<ParameterInfo>? Parameters { get; init; }
    public IReadOnlyList<string>? Decorators { get; init; }
    public IReadOnlyList<string>? TypeReferences { get; init; }  // Types used
    public IReadOnlyList<string>? PrecedingComments { get; init; }
}

public record ParameterInfo(
    string Name,
    string? Type,
    string? DefaultValue,
    string? Description);
```

### Phase 2b: Language-Specific Extractors

Create extraction helpers for each language family:

```csharp
public interface ISemanticExtractor
{
    string Language { get; }
    ChunkEnrichment Extract(Node node, string sourceText);
}

public class PythonSemanticExtractor : ISemanticExtractor
{
    public string Language => "python";
    
    public ChunkEnrichment Extract(Node node, string sourceText)
    {
        // Extract Python-specific: docstrings, type hints, decorators
    }
}

public class TypeScriptSemanticExtractor : ISemanticExtractor
{
    // Extract: JSDoc, type annotations, decorators
}
```

### Phase 2c: Import/Dependency Extraction

Add separate pass for imports:
```csharp
public record ImportInfo
{
    public string ModulePath { get; init; }     // 'react', './services/OrderService'
    public bool IsRelative { get; init; }       // ./ or ../ prefix
    public IReadOnlyList<string> Symbols { get; init; }  // Named imports
    public string? DefaultImport { get; init; } // Default import name
}
```

## Updated Acceptance Criteria

### Phase 1: ✅ Complete
- [x] Evaluate TreeSitter .NET bindings → `TreeSitter.DotNet v1.1.1`
- [x] Create unified `TreeSitterIngesterAgent` for all languages
- [x] Register in `DeveloperAgentProvider`
- [x] Delete regex ingesters (Python, TS, Go, Rust, F#)
- [x] Update priority scheme

### Phase 2: ✅ Semantic Enrichment (Complete)
- [x] Add `Signature`, `Docstring`, `Summary` to `SemanticChunk`
- [x] Extract Python docstrings and type hints
- [x] Extract TypeScript/JavaScript JSDoc comments
- [x] Extract Go doc comments
- [x] Extract Rust doc comments (`///`)
- [x] Extract Java Javadoc
- [x] Extract decorators/attributes
- [x] Add `Parameters` with types and descriptions
- [x] Add `TypeReferences` for used types
- [x] Add `Imports` for dependency tracking

### Phase 3: Dependency Graph
- [x] Extract import statements for each language (done in Phase 2)
- [ ] Create `Imports` edges in code graph
- [ ] Enable "find all files that import X" queries

## Benchmarks

Target: Parse 1000 files/second (vs ~1 file/sec for LLM)

Current: Need to measure after Phase 2 additions

## Related Tasks

- `gap-01-smart-content.md` - LLM summaries (complementary to docstrings)
- `gap-03-enrichment.md` - Cross-file relationships (uses TypeReferences)

## Reference: Competition Features

### Cursor
- Semantic search by function signature
- "Find functions that return X type"
- Docstring-aware search

### Cody (Sourcegraph)
- SCIP-based indexing with full type information
- Cross-repository references
- "Find all callers of this function"

### Continue
- TreeSitter + LLM hybrid
- Contextual code retrieval

## Notes

The gap isn't TreeSitter vs Regex - it's shallow extraction vs deep semantic extraction. TreeSitter gives us the AST; we need to extract more meaning from it.