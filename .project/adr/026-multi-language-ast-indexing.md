# ADR-026: Multi-Language AST Indexing via TreeSitter

## Status
Accepted

## Date
2026-03-03

## Context

Aura's code indexing relied on two separate mechanisms:
- **RoslynCodeIngestor** for C# — full AST parsing with code graph nodes/edges, but was never registered in the DI container
- **CodeIngestor** for everything else — regex-based pattern matching that missed nested structures, decorators, arrow functions, and complex signatures

The `TreeSitter.DotNet` package (v1.2.0) was already a dependency but completely unused. Tree-sitter provides fast, incremental parsing with grammars for 28+ languages.

## Decision

1. **Register RoslynCodeIngestor** as the primary handler for `.cs` files, replacing regex-based CodeIngestor for C#
2. **Create TreeSitterCodeIngestor** using TreeSitter.DotNet for Python, TypeScript, JavaScript, Go, Rust, Java, C/C++, Ruby, Swift, Kotlin
3. **Create StructuredDataIngestor** for JSON, YAML, XML, TOML with structure-aware chunking
4. **Enrich Roslyn code graph** with `Inherits` and `Implements` edges from base type lists

### Ingestor Priority Chain
```
RoslynCodeIngestor (.cs) →
TreeSitterCodeIngestor (py/ts/js/go/rs/java/c++) →
StructuredDataIngestor (json/yaml/xml/toml) →
PdfIngestor (.pdf) →
MarkdownIngestor (.md) →
CodeIngestor (regex fallback) →
PlainTextIngestor (everything else)
```

Both RoslynCodeIngestor and TreeSitterCodeIngestor implement `ICodeIngestor`, producing:
- **RAG chunks** (text + metadata for semantic search)
- **CodeNode entries** (types, functions, classes in the code graph)
- **CodeEdge entries** (containment, inheritance, implementation relationships)

## Consequences

### Positive
- All indexed languages now get AST-quality chunking instead of regex guesses
- Code graph populated for Python/TS/JS/Go/Rust (was C#-only before)
- `aura_navigate` can find structural relationships across languages
- JSON/YAML configs indexed with key-level granularity

### Negative
- TreeSitter native binaries add platform-specific deployment complexity
- Tree-sitter grammars may not cover all language edge cases (new syntax, etc.)

## Relates To
- ADR-015 (Graph RAG for Code) — extended to multi-language
- ADR-025 (Personal Knowledge MCP Pivot) — part of the indexing improvements
