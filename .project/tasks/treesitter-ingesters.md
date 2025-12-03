# TreeSitter Ingesters

## Status: Future Work

## Overview

Add TreeSitter-based ingester agents for Python and TypeScript to provide fast, deterministic parsing without LLM overhead.

## Motivation

Currently, Python and TypeScript files use the LLM-based `generic-code-ingester.md` (priority 20). This works but:
- Slow (~1 file/sec vs ~1000 files/sec for native parsing)
- Uses inference compute
- May have occasional parsing errors

TreeSitter provides:
- Fast native parsing
- Perfect AST accuracy
- No LLM cost
- Offline capability

## Scope

Add to `Aura.Module.Developer`:
- `PythonIngesterAgent` (priority 10)
- `TypeScriptIngesterAgent` (priority 10)

## Dependencies

Need to evaluate .NET TreeSitter bindings:
- [tree-sitter-dotnet](https://github.com/AzureMarker/tree-sitter-dotnet) - Unofficial bindings
- Native interop via P/Invoke
- WASM-based TreeSitter (browser-compatible)

## Implementation Approach

```csharp
public class PythonIngesterAgent : IAgent
{
    public string AgentId => "python-ingester";
    
    public AgentMetadata Metadata { get; } = new(
        Name: "Python TreeSitter Ingester",
        Capabilities: ["ingest:py", "ingest:pyw"],
        Priority: 10,
        Provider: "native",
        Model: "treesitter",
        // ...
    );

    public async Task<AgentOutput> ExecuteAsync(AgentContext context, CancellationToken ct)
    {
        // Parse with TreeSitter
        // Extract: classes, functions, methods, decorators
        // Return SemanticChunks
    }
}
```

## Chunk Extraction

### Python
- `class` definitions
- `def` functions/methods
- `async def` async functions
- Decorated functions (@decorator)
- Module-level assignments (constants)
- Type aliases

### TypeScript
- `class` definitions
- `interface` definitions
- `type` aliases
- `function` declarations
- `const`/`let` arrow functions
- `export` statements

## Priority

| Agent | Priority | Parser |
|-------|----------|--------|
| CSharpIngesterAgent | 10 | Roslyn |
| PythonIngesterAgent | 10 | TreeSitter |
| TypeScriptIngesterAgent | 10 | TreeSitter |
| generic-code-ingester | 20 | LLM |
| text-ingester | 50 | Rule-based |
| fallback-ingester | 99 | None |

## Acceptance Criteria

- [ ] Evaluate TreeSitter .NET bindings
- [ ] Create `PythonIngesterAgent`
- [ ] Create `TypeScriptIngesterAgent`
- [ ] Register in `DeveloperAgentProvider`
- [ ] Add unit tests
- [ ] Benchmark: files/sec comparison with LLM ingester

## Risks

- .NET TreeSitter bindings may be immature
- Native dependencies complicate cross-platform builds
- Grammar files need maintenance as languages evolve

## Alternative

If TreeSitter proves problematic, consider:
- Language Server Protocol (LSP) integration
- Dedicated language-specific parsers (e.g., Python's `ast` module via IronPython)
- Stick with LLM-based ingestion (acceptable for background indexing)

## Related

- [Spec 22: Ingester Agents](../spec/22-ingester-agents.md)
- [Spec 23: Hardcoded Agents](../spec/23-hardcoded-agents.md)
