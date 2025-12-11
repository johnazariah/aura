# Functional Programming Patterns in Aura

**Version**: 1.0
**Last Updated**: 2025-11-27

## Overview

Aura embraces functional programming patterns for robust, maintainable code. This document explains the patterns we use and how to apply them.

## The Result Pattern

### Why Result<T,E>?

Exceptions are for **exceptional** situations - things that shouldn't happen in normal program flow. Expected errors (user not found, validation failed, network timeout) should be represented in the type system.

```csharp
// ❌ Using exceptions for expected errors
public Agent GetAgent(string id)
{
    var agent = _db.Find(id);
    if (agent is null)
        throw new AgentNotFoundException(id);  // Caller must know to catch this
    return agent;
}

// ✅ Using Result for expected errors
public Result<Agent, AgentError> GetAgent(string id)
{
    var agent = _db.Find(id);
    return agent is not null
        ? Result<Agent, AgentError>.Success(agent)
        : Result<Agent, AgentError>.Failure(AgentError.NotFound(id));
}
```

### Result Types in Aura

```csharp
// Full Result with typed value and error
Result<TValue, TError>

// Result with typed error but no value (for void operations)
Result<TError>

// Simple Result with string error (for quick prototyping)
SimpleResult<TValue>
Result
```

### Core Operations

#### Map - Transform the success value

```csharp
Result<Agent, AgentError> agentResult = GetAgent("coding-agent");

// Transform Agent to AgentMetadata if successful
Result<AgentMetadata, AgentError> metadataResult = agentResult.Map(a => a.Metadata);
```

#### Bind (FlatMap) - Chain operations that return Results

```csharp
Result<AgentOutput, AgentError> result = GetAgent("coding-agent")
    .Bind(agent => agent.ExecuteAsync(context))
    .Bind(output => ValidateOutput(output));
```

#### Match - Safely extract values

```csharp
string message = result.Match(
    onSuccess: output => $"Generated: {output.Content}",
    onFailure: error => $"Failed: {error.Message}");
```

#### Tap - Side effects without changing the result

```csharp
Result<Agent, AgentError> result = GetAgent("coding-agent")
    .Tap(agent => _logger.LogInformation("Found agent {Id}", agent.Id))
    .Tap(agent => _metrics.RecordAgentAccess(agent.Id));
```

## Railway-Oriented Programming

Think of Result operations as railway tracks. Success stays on the happy path; failure diverts to the error track.

```
GetAgent ──┬──> Transform ──┬──> Execute ──┬──> Return Success
           │                │              │
           └── Error ───────┴── Error ─────┴── Return Failure
```

```csharp
public async Task<Result<string, AgentError>> ProcessRequestAsync(string agentId, string prompt)
{
    return await GetAgent(agentId)                        // Get agent or fail
        .Map(agent => new AgentContext { Prompt = prompt }) // Build context
        .Bind(ctx => agent.ExecuteAsync(ctx))              // Execute or fail
        .Map(output => output.Content);                    // Extract content
}
```

## Immutability Patterns

### Records with `with` Expressions

```csharp
public record AgentContext
{
    public required string Prompt { get; init; }
    public string? WorkspacePath { get; init; }
    public IReadOnlyList<Message> History { get; init; } = [];
}

// Create new instance with modifications
var updated = context with 
{ 
    History = context.History.Append(newMessage).ToList() 
};
```

### Immutable Collections

```csharp
// ❌ Mutable collections
public List<string> Capabilities { get; set; }

// ✅ Immutable collections
public IReadOnlyList<string> Capabilities { get; init; } = [];
public ImmutableList<string> Capabilities { get; init; } = [];
```

### Builder Pattern for Complex Objects

```csharp
public record AgentDefinition
{
    public required string AgentId { get; init; }
    public required string SystemPrompt { get; init; }
    // ... many properties

    public static Builder CreateBuilder(string agentId) => new(agentId);

    public sealed class Builder(string agentId)
    {
        private string _systemPrompt = "";
        private string _provider = "ollama";
        private List<string> _capabilities = [];

        public Builder WithSystemPrompt(string prompt) 
        { 
            _systemPrompt = prompt; 
            return this; 
        }

        public Builder WithProvider(string provider) 
        { 
            _provider = provider; 
            return this; 
        }

        public Builder WithCapability(string capability) 
        { 
            _capabilities.Add(capability); 
            return this; 
        }

        public AgentDefinition Build() => new()
        {
            AgentId = agentId,
            SystemPrompt = _systemPrompt,
            Provider = _provider,
            Capabilities = _capabilities.ToList()
        };
    }
}
```

## Option Pattern (for nullable values)

When you need to distinguish between "no value" and "null value":

```csharp
// Using nullable with explicit handling
public string? TryGetValue(string key)
{
    return _dict.TryGetValue(key, out var value) ? value : null;
}

// Pattern matching for null safety
if (TryGetValue("key") is { } value)
{
    // value is definitely not null here
    Process(value);
}
```

## Discriminated Unions (Algebraic Data Types)

For types that can be one of several variants:

```csharp
public abstract record AgentResult
{
    private AgentResult() { }  // Sealed hierarchy

    public sealed record Success(AgentOutput Output) : AgentResult;
    public sealed record Failure(AgentError Error) : AgentResult;
    public sealed record Cancelled() : AgentResult;
}

// Usage with pattern matching
var message = result switch
{
    AgentResult.Success s => $"Output: {s.Output.Content}",
    AgentResult.Failure f => $"Error: {f.Error.Message}",
    AgentResult.Cancelled => "Operation was cancelled",
    _ => throw new InvalidOperationException("Unknown result type")
};
```

## Composition Patterns

### Function Composition

```csharp
// Compose small functions into larger ones
Func<string, string> normalize = s => s.Trim().ToLowerInvariant();
Func<string, string> sanitize = s => Regex.Replace(s, @"[^\w\s]", "");
Func<string, string> process = s => sanitize(normalize(s));
```

### Pipeline Pattern

```csharp
public static class PipelineExtensions
{
    public static TOut Pipe<TIn, TOut>(this TIn input, Func<TIn, TOut> func) 
        => func(input);
}

// Usage
var result = input
    .Pipe(Normalize)
    .Pipe(Validate)
    .Pipe(Transform)
    .Pipe(Save);
```

## Async Patterns

### Async Result Operations

```csharp
public static class ResultExtensions
{
    public static async Task<Result<TNew, TError>> MapAsync<T, TNew, TError>(
        this Result<T, TError> result,
        Func<T, Task<TNew>> mapper)
    {
        if (result.IsFailure)
            return Result<TNew, TError>.Failure(result.Error);
        
        var newValue = await mapper(result.Value);
        return Result<TNew, TError>.Success(newValue);
    }

    public static async Task<Result<TNew, TError>> BindAsync<T, TNew, TError>(
        this Result<T, TError> result,
        Func<T, Task<Result<TNew, TError>>> binder)
    {
        if (result.IsFailure)
            return Result<TNew, TError>.Failure(result.Error);
        
        return await binder(result.Value);
    }
}
```

### Task Extensions for Results

```csharp
public static async Task<Result<T, TError>> ToResultAsync<T, TError>(
    this Task<T> task,
    Func<Exception, TError> errorMapper)
{
    try
    {
        var value = await task;
        return Result<T, TError>.Success(value);
    }
    catch (Exception ex)
    {
        return Result<T, TError>.Failure(errorMapper(ex));
    }
}
```

## Error Type Design

### Typed Errors with Factory Methods

```csharp
public sealed record AgentError(AgentErrorCode Code, string Message, string? Details = null)
{
    // Factory methods for common errors
    public static AgentError NotFound(string agentId) =>
        new(AgentErrorCode.NotFound, $"Agent '{agentId}' not found");

    public static AgentError ProviderUnavailable(string provider) =>
        new(AgentErrorCode.ProviderUnavailable, $"Provider '{provider}' is not available");

    public static AgentError Cancelled() =>
        new(AgentErrorCode.Cancelled, "Operation was cancelled");
}

public enum AgentErrorCode
{
    Unknown = 0,
    NotFound,
    ProviderUnavailable,
    ExecutionFailed,
    Cancelled,
    Timeout
}
```

### Error Mapping Between Layers

```csharp
public Result<AgentOutput, AgentError> Execute(AgentContext context)
{
    return _llmProvider.ChatAsync(messages)
        .MapError(MapLlmError);  // Convert LlmError to AgentError
}

private static AgentError MapLlmError(LlmError error) => error.Code switch
{
    LlmErrorCode.Unavailable => AgentError.ProviderUnavailable(error.Message),
    LlmErrorCode.ModelNotFound => AgentError.ExecutionFailed(error.Message),
    LlmErrorCode.Timeout => AgentError.Timeout(30),
    _ => AgentError.ExecutionFailed(error.Message, error.Details)
};
```

## When NOT to Use Functional Patterns

### Simple CRUD Operations

```csharp
// Don't over-engineer simple operations
public async Task<Agent?> GetAgentByIdAsync(Guid id)
{
    return await _db.Agents.FindAsync(id);
}
```

### Performance-Critical Code

```csharp
// Avoid allocations in hot paths
// Use ValueTask, Span<T>, stackalloc when appropriate
```

### When Team Isn't Familiar

Start simple, introduce patterns gradually. Training > Cleverness.

## References

- [Railway Oriented Programming](https://fsharpforfunandprofit.com/rop/)
- [Parse, don't validate](https://lexi-lambda.github.io/blog/2019/11/05/parse-don-t-validate/)
- [Making Impossible States Impossible](https://www.youtube.com/watch?v=IcgmSRJHu_8)
- [ADR-009: Lessons from Previous Attempts](../adr/009-lessons-from-previous-attempts.md)
