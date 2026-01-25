# Tech Debt: Eliminate Stringly-Typed Code Patterns

**Status:** 📋 Ready for Development  
**Priority:** Medium  
**Type:** Tech Debt / Code Quality  
**Estimated Effort:** 4-6 hours (parallelizable with sub-agents)

## Problem Statement

The codebase has 200+ violations of stringly-typed patterns that reduce type safety, refactorability, and IDE support. These patterns make the code fragile and harder to maintain.

## Violation Categories

### 1. Missing `nameof()` for Member Names (~150 occurrences)

String literals used instead of `nameof()` for property/method names:

```csharp
// ❌ Current
options.RequireProperty("ConnectionStrings");
builder.AddNpgsql("aura-db");
context.Items["path"]

// ✅ Should be
options.RequireProperty(nameof(ConnectionStrings));
// For Aspire resource names - use constants
builder.AddNpgsql(ResourceNames.AuraDb);
// For context items - use typed accessor or constants
context.GetPath() // or context.Items[ContextKeys.Path]
```

**Key files:**
- `src/Aura.Foundation/Configuration/ConfigurationExtensions.cs` - `RequireProperty` calls
- `src/Aura.AppHost/Program.cs` - Aspire resource names
- `src/Aura.Foundation/Data/AuraDbContext.cs` - EF configuration
- `src/Aura.Module.Developer/Services/*.cs` - Various string keys

### 2. Hand-Written JSON Schemas (~10 occurrences)

JSON schemas written as raw strings that can drift from actual DTOs:

```csharp
// ❌ Current (WellKnownSchemas.cs)
public static readonly string WorkflowPlan = """
{
  "type": "object",
  "properties": {
    "pattern": { "type": "string" },
    ...
  }
}
""";

// ✅ Should be
public static string WorkflowPlan => JsonSchemaGenerator.Generate<WorkflowPlanDto>();
```

**Key file:** `src/Aura.Foundation/Llm/WellKnownSchemas.cs`

### 3. Untyped Dictionary Access (~20 occurrences)

Using `Dictionary<string, object>` for known schemas:

```csharp
// ❌ Current
var result = context.Items["result"] as string;

// ✅ Should be
record ExecutionContext(string Result, ...);
var result = context.Result;
```

### 4. Magic String Constants (~30 occurrences)

Repeated string literals that should be constants:

```csharp
// ❌ Current
if (status == "Completed") ...
options.Provider = "openai";

// ✅ Should be
if (status == StoryStatus.Completed) ...
options.Provider = LlmProviders.OpenAI;
```

## Specific Violations Found

| File | Line | Pattern | Fix |
|------|------|---------|-----|
| `ConfigurationExtensions.cs` | 15 | `"ConnectionStrings"` | `nameof(ConnectionStrings)` |
| `ConfigurationExtensions.cs` | 25 | `"Llm"` | `nameof(LlmOptions)` |
| `WellKnownSchemas.cs` | * | Hand-written JSON | Generate from DTOs |
| `Program.cs` (AppHost) | * | `"aura-db"`, `"aura-api"` | `ResourceNames.X` |
| `AuraDbContext.cs` | * | `"stories"`, `"steps"` | Table name constants |
| `StoryService.cs` | * | Status strings | `StoryStatus.X` enum |
| `ReactExecutor.cs` | * | Tool names as strings | `ToolIds.X` constants |

## Proposed Solution

### Phase 1: Create Constants Classes

```csharp
// src/Aura.Foundation/Constants/ResourceNames.cs
public static class ResourceNames
{
    public const string AuraDb = "aura-db";
    public const string AuraApi = "aura-api";
    public const string AuraCache = "aura-cache";
}

// src/Aura.Foundation/Constants/ContextKeys.cs  
public static class ContextKeys
{
    public const string Path = "path";
    public const string Result = "result";
    // ...
}
```

### Phase 2: Generate JSON Schemas from DTOs

```csharp
// Use System.Text.Json or NJsonSchema to generate
public static class WellKnownSchemas
{
    private static readonly JsonSchemaGenerator _generator = new();
    
    public static string WorkflowPlan => _generator.Generate<WorkflowPlanDto>();
    public static string StructuredSpec => _generator.Generate<StructuredSpecDto>();
}
```

### Phase 3: Replace All String Literals

Use Roslyn refactoring (`aura_refactor`) or find-replace to update all occurrences.

## Acceptance Criteria

- [ ] All `RequireProperty` calls use `nameof()`
- [ ] Aspire resource names use `ResourceNames` constants
- [ ] JSON schemas are generated from DTOs (or validated against them)
- [ ] No magic strings for status values (use enums)
- [ ] Context/dictionary keys use typed accessors or constants
- [ ] `dotnet build` succeeds with no warnings
- [ ] All existing tests pass

## Implementation Notes

1. **Parallelizable**: Each category can be fixed independently by sub-agents
2. **Low risk**: These are mechanical refactors with no behavior change
3. **Testable**: Compilation success proves correctness for most changes
4. **Roslyn tools available**: Use `aura_refactor` for systematic replacement

## Future Prevention

After fixing, add analyzers:
- Enable `CA1507` (Use nameof in place of string)
- Custom analyzer for dictionary key access patterns
- Schema validation in CI (compare generated vs hand-written)

## References

- Coding standards: `.project/standards/coding-standards.md`
- ADR on type safety: Consider creating `.project/adr/adr-XXX-type-safe-patterns.md`
