---
title: "ADR-004: Cross-Cutting Concerns Management"
status: "Accepted"
date: "2026-01-30"
authors: "Anvil Team"
tags: ["architecture", "cross-cutting", "consistency", "result-types"]
supersedes: ""
superseded_by: ""
---

# ADR-004: Cross-Cutting Concerns Management

## Status

Accepted

## Context

Cross-cutting concerns are aspects that affect multiple parts of the codebase:

- Logging
- Error handling
- Configuration
- Retry logic
- Validation

Without clear guidance, engineers may:
- Reinvent patterns that already exist
- Implement concerns inconsistently
- Create coupling between unrelated modules

## Decision

We adopt a **documented discovery and reuse** approach for cross-cutting concerns, with **Result types** for error handling and **LINQ extensions** for monadic composition.

### Defined Cross-Cutting Concerns

| Concern | Pattern | Location | ADR |
|---------|---------|----------|-----|
| **Logging** | `ILogger<T>` + Serilog | Each service | ADR-003 |
| **Dependency Injection** | Microsoft.Extensions.DI | Program.cs | ADR-002 |
| **Testing** | Fakes, 5-field Test Doc | `tests/Anvil.Tests/` | ADR-001 |
| **Error Handling** | `Result<T, TError>` | All operations | See below |
| **Configuration** | `IOptions<T>` | Infrastructure | See below |

### Error Handling: Result Types

We use `Result<T, TError>` instead of exceptions for expected failures. Exceptions are reserved for truly exceptional (programmer error) conditions.

```csharp
// Define a Result type
public readonly struct Result<T, TError>
{
    private readonly T? _value;
    private readonly TError? _error;
    private readonly bool _isSuccess;

    private Result(T value) { _value = value; _isSuccess = true; _error = default; }
    private Result(TError error) { _error = error; _isSuccess = false; _value = default; }

    public static Result<T, TError> Ok(T value) => new(value);
    public static Result<T, TError> Fail(TError error) => new(error);

    public bool IsSuccess => _isSuccess;
    public bool IsFailure => !_isSuccess;
    public T Value => _isSuccess ? _value! : throw new InvalidOperationException("No value on failure");
    public TError Error => !_isSuccess ? _error! : throw new InvalidOperationException("No error on success");

    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<TError, TResult> onFailure)
        => _isSuccess ? onSuccess(_value!) : onFailure(_error!);
}
```

### LINQ Extensions for Monadic Sequencing

Enable LINQ query syntax for chaining Result operations:

```csharp
public static class ResultExtensions
{
    // Select (Map): Transform the success value
    public static Result<TNew, TError> Select<T, TError, TNew>(
        this Result<T, TError> result,
        Func<T, TNew> selector)
        => result.IsSuccess 
            ? Result<TNew, TError>.Ok(selector(result.Value))
            : Result<TNew, TError>.Fail(result.Error);

    // SelectMany (Bind): Chain operations that return Results
    public static Result<TNew, TError> SelectMany<T, TError, TNew>(
        this Result<T, TError> result,
        Func<T, Result<TNew, TError>> selector)
        => result.IsSuccess ? selector(result.Value) : Result<TNew, TError>.Fail(result.Error);

    // SelectMany with projection (for LINQ query syntax)
    public static Result<TResult, TError> SelectMany<T, TError, TNew, TResult>(
        this Result<T, TError> result,
        Func<T, Result<TNew, TError>> selector,
        Func<T, TNew, TResult> resultSelector)
        => result.SelectMany(x => selector(x).Select(y => resultSelector(x, y)));
}
```

### Usage: LINQ Query Syntax

```csharp
// Chain multiple operations with LINQ
public Result<TestReport, AnvilError> RunStory(string storyPath)
{
    return 
        from story in _storyLoader.Load(storyPath)
        from validated in _validator.Validate(story)
        from result in _executor.Execute(validated)
        from report in _reporter.Generate(result)
        select report;
}

// Equivalent to:
// _storyLoader.Load(storyPath)
//     .SelectMany(story => _validator.Validate(story))
//     .SelectMany(validated => _executor.Execute(validated))
//     .SelectMany(result => _reporter.Generate(result));
```

### Error Types

```csharp
// Discriminated union for errors (use records for exhaustive matching)
public abstract record AnvilError
{
    public record StoryNotFound(string Path) : AnvilError;
    public record ValidationFailed(IReadOnlyList<string> Errors) : AnvilError;
    public record AuraConnectionFailed(string Message, Exception? Inner = null) : AnvilError;
    public record ExecutionFailed(string StoryId, string Reason) : AnvilError;
}
```

### When to Use Exceptions vs Results

| Use Result<T, TError> | Use Exceptions |
|-----------------------|----------------|
| File not found | Null argument (programmer error) |
| Validation failures | Out of memory |
| Network timeouts | Stack overflow |
| Expected business errors | Assertion failures |
| User input errors | Unrecoverable states |

### Configuration Pattern

```csharp
// Options class
public class AnvilOptions
{
    public string AuraBaseUrl { get; set; } = "http://localhost:5300";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

// Registration
builder.Services.Configure<AnvilOptions>(
    builder.Configuration.GetSection("Anvil"));

// Usage
public class AuraClient
{
    private readonly AnvilOptions _options;
    
    public AuraClient(IOptions<AnvilOptions> options)
    {
        _options = options.Value;
    }
}
```

### Discovery Process (For Agents)

When implementing a new feature, agents must:

1. **Check existing patterns**: Search codebase for similar functionality
2. **Read ADRs**: Identify mandated patterns
3. **Reuse, don't reinvent**: Use existing Result types, extensions, helpers
4. **Document new patterns**: If a new cross-cutting concern is needed, create an ADR

### Anti-Patterns

| ❌ Don't | ✅ Do Instead |
|----------|---------------|
| Throw exceptions for expected errors | Return `Result<T, AnvilError>` |
| Create a new logger wrapper | Use `ILogger<T>` |
| Use try/catch for control flow | Use Result pattern with LINQ |
| Nested if/else for Result checking | Use `SelectMany` / LINQ query |
| New HTTP client implementations | Use existing `IAuraClient` |

## Consequences

**Positive**
- **POS-001**: Consistent patterns across the codebase
- **POS-002**: Explicit error handling (errors are in the type signature)
- **POS-003**: Composable operations via LINQ
- **POS-004**: No hidden control flow from exceptions
- **POS-005**: Agents can find and reuse patterns reliably

**Negative**
- **NEG-001**: Learning curve for Result pattern
- **NEG-002**: More verbose than throwing exceptions
- **NEG-003**: LINQ extensions must be implemented and maintained

## Implementation Notes

- **IMP-001**: Research phase must identify relevant cross-cutting concerns
- **IMP-002**: Plan phase must reference how each concern is handled
- **IMP-003**: Implement phase must not introduce new patterns without ADR
- **IMP-004**: Consider using a library like `LanguageExt` or `OneOf` for production Result types
- **IMP-005**: All public APIs that can fail should return `Result<T, TError>`

## References

- [ADR-001: Testing](ADR-001-testing.md)
- [ADR-002: Dependency Injection](ADR-002-dependency-injection.md)
- [ADR-003: Logging](ADR-003-logging.md)
- [Railway Oriented Programming](https://fsharpforfunandprofit.com/rop/)
- [LanguageExt Library](https://github.com/louthy/language-ext)
