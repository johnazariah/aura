# ADR-013: Strongly-Typed Agent Contracts

## Status

Accepted

## Date

2025-12-02

## Context

Agent execution produces outputs that need to be:
- Stored in the database
- Displayed in the UI
- Passed between workflow steps
- Validated for correctness

Previous attempts (hve-hack) used `Dictionary<string, object>` for flexibility, which led to:
- Runtime errors from typos in key names
- No IntelliSense or compiler checking
- Difficult debugging when shapes didn't match expectations
- Hours of frustration tracking down stringly-typed bugs

## Decision

**All agent inputs and outputs use strongly-typed contracts. No exceptions.**

### Contract Model

**One contract per capability:**

```csharp
// Capability: coding
public record CodingInput
{
    public required string TaskDescription { get; init; }
    public required string TargetPath { get; init; }
    public string? Language { get; init; }
    public IReadOnlyList<string>? RelevantFiles { get; init; }
}

public record CodingOutput
{
    public required IReadOnlyList<FileArtifact> Files { get; init; }
    public required bool Compiles { get; init; }
    public IReadOnlyList<CompileDiagnostic>? Diagnostics { get; init; }
}

// Capability: testing
public record TestingInput
{
    public required string TargetClass { get; init; }
    public required string TargetProject { get; init; }
    public string? TestFramework { get; init; }
}

public record TestingOutput
{
    public required IReadOnlyList<FileArtifact> TestFiles { get; init; }
    public required bool Compiles { get; init; }
    public TestRunResult? TestResults { get; init; }
}
```

### Agent Selection with Specialization

Agents declare capabilities. Selection prefers specific over general:

```
Request: capability="coding", language="csharp"

1. Search for "csharp-coding" capability → RoslynAgent (priority 30)
2. Fallback to "coding" capability → PolyglotCodingAgent (priority 80)

Both return CodingOutput - same contract.
```

### Optional Enrichments

Contracts have:
- **Required fields**: Core data every implementation must provide
- **Optional fields**: Enrichments that specialized agents may populate

```csharp
public record CodingOutput
{
    // Required - all coding agents must provide
    public required IReadOnlyList<FileArtifact> Files { get; init; }
    public required bool Compiles { get; init; }
    
    // Optional - specialized agents may provide
    public IReadOnlyList<CompileDiagnostic>? Diagnostics { get; init; }
    public CompilationMetrics? Metrics { get; init; }
}
```

### Forbidden Patterns

The following are **VERBOTEN**:

```csharp
// ❌ NO: Stringly-typed dictionaries
Dictionary<string, object> data;

// ❌ NO: Magic strings for property access
var value = data["someKey"];

// ❌ NO: Reflection to read properties
var prop = obj.GetType().GetProperty("Name");

// ❌ NO: JSON parsing to dynamic
dynamic result = JsonSerializer.Deserialize<dynamic>(json);

// ❌ NO: Untyped anonymous objects crossing boundaries
return new { files = files, success = true };
```

## Consequences

### Positive

- Compiler catches contract mismatches at build time
- IntelliSense shows available fields
- Refactoring tools work correctly
- Self-documenting - contracts ARE the documentation
- Serialization is predictable and testable

### Negative

- More types to define upfront
- Adding fields requires contract changes
- Less "flexible" (but that flexibility was a bug, not a feature)

### Mitigations

- Contracts live in `Aura.Foundation.Contracts` namespace
- Clear naming convention: `{Capability}Input`, `{Capability}Output`
- Optional fields provide extension points without breaking changes

## References

- [hve-hack CODING-STANDARDS.md](../../docs/CODING-STANDARDS.md) - Original standards
- ADR-012: Tool-Using Agents (tools also have typed contracts)
