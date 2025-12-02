# ADR-014: Developer Module Roslyn Tools

## Status

Accepted

## Date

2025-12-02

## Context

The Developer Module needs to perform code-aware operations:
- Generate new C# code that compiles
- Modify existing code safely
- Understand project structure and dependencies
- Run tests and report results

These operations require deep integration with the .NET toolchain, specifically Roslyn (the C# compiler platform) and MSBuild.

## Decision

**Implement Roslyn-based tools in the Developer Module that plug into the Foundation tool framework.**

### Tool Set (MVP)

| Tool | Purpose | Roslyn/MSBuild |
|------|---------|----------------|
| `list_projects` | Find projects in solution | MSBuild workspace |
| `list_classes` | Find classes in a project | Roslyn syntax trees |
| `get_class_info` | Get class details | Roslyn semantic model |
| `read_file` | Read file contents | File I/O (no Roslyn) |
| `write_file` | Create/overwrite file | File I/O (no Roslyn) |
| `modify_file` | Edit existing file | Roslyn syntax rewriting |
| `find_usages` | Find symbol references | Roslyn FindReferences |
| `get_project_references` | Get dependencies | MSBuild evaluation |
| `validate_compilation` | Check if compiles | Roslyn compilation |
| `run_tests` | Execute tests | `dotnet test` CLI |

### Tool Contracts

Each tool has strongly-typed input and output:

```csharp
// Tool: list_classes
public record ListClassesInput
{
    public required string ProjectPath { get; init; }
    public string? Namespace { get; init; }
    public bool IncludePrivate { get; init; } = false;
}

public record ListClassesOutput
{
    public required IReadOnlyList<ClassSummary> Classes { get; init; }
}

public record ClassSummary
{
    public required string Name { get; init; }
    public required string FullName { get; init; }
    public required string FilePath { get; init; }
    public required int LineNumber { get; init; }
    public required ClassKind Kind { get; init; }  // Class, Record, Struct, Interface
    public IReadOnlyList<string> BaseTypes { get; init; } = [];
}
```

### Location

- **Tool interfaces**: `Aura.Foundation` (ITool, IToolRegistry)
- **Roslyn tool implementations**: `Aura.Module.Developer`
- **Roslyn SDK dependency**: Only in Developer Module

This keeps Foundation lightweight while allowing rich tooling in the Developer Module.

### C# Ingestion

The Developer Module also owns C# code ingestion for RAG:
- Parse C# files into semantic chunks (classes, methods)
- Extract relationships (inheritance, calls, references)
- Store in Graph RAG for intelligent retrieval

Ingestion uses the same Roslyn infrastructure as the tools.

## Consequences

### Positive

- Rich code understanding via Roslyn semantic model
- Compile-time validation before changes are committed
- Consistent with .NET ecosystem tooling
- Reusable infrastructure for ingestion and tools

### Negative

- Roslyn SDK is a heavy dependency (~50MB+ of assemblies)
- MSBuild workspace loading can be slow for large solutions
- Complexity in handling incremental compilation

### Mitigations

- Roslyn dependency isolated to Developer Module
- Lazy workspace loading - only parse what's needed
- Cache compilation results within a workflow session
- Fall back to text-based tools for non-C# files

## References

- [Roslyn Overview](https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/)
- [MSBuild Workspace](https://docs.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.msbuild.msbuildworkspace)
- ADR-012: Tool-Using Agents
- ADR-013: Strongly-Typed Agent Contracts
