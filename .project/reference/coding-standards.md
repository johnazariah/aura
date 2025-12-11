# Aura Coding Standards

**Version**: 1.0
**Last Updated**: 2025-11-27
**Status**: Active

## Overview

These standards define how we write code in Aura. They are synthesized from lessons learned across hve-hack, birdlet, and bird-constellation projects.

**Philosophy**: Write code that the compiler can verify. Make invalid states unrepresentable. Use the type system as documentation.

## Core Principles

### 1. Strongly-Typed Over Stringly-Typed

If the compiler can check it, the compiler should check it.

```csharp
// ❌ BAD: Stringly-typed
var data = new Dictionary<string, object>();
data["workflowId"] = guid;
data["status"] = "running";  // Typo = runtime error

// ✅ GOOD: Strongly-typed
public record WorkflowState
{
    public required Guid WorkflowId { get; init; }
    public required WorkflowStatus Status { get; init; }
}
```

### 2. Make Invalid States Unrepresentable

Design types so the compiler prevents invalid states:

```csharp
// ❌ BAD: Can represent invalid states
public class Agent
{
    public string? Name { get; set; }  // Could be null or empty
    public int Priority { get; set; }  // Could be -1 or 999
}

// ✅ GOOD: Invalid states impossible
public record Agent
{
    public required AgentName Name { get; init; }  // Validated on construction
    public required Priority Priority { get; init; }  // Enforces 1-10
}
```

### 3. Use the Type System as Documentation

Types should communicate intent:

```csharp
// ❌ BAD: What does string mean?
Task<string> ProcessAsync(string input);

// ✅ GOOD: Types tell the story
Task<Result<ProcessingOutput, ProcessingError>> ProcessAsync(ProcessingInput input);
```

## Specific Rules

### Rule 1: No Dictionary<string, object> for Known Schemas

When the schema is known and stable, use typed contracts.

```csharp
// ❌ PROHIBITED
public record AgentContext(Dictionary<string, object> Data);

// ✅ REQUIRED
public record AgentContext(
    Guid ConversationId,
    string Prompt,
    string? WorkspacePath = null);
```

**Exception**: Truly dynamic data where schema varies by context. Use sparingly.

### Rule 2: Use nameof() for Member Names

Never use string literals for members that exist in code.

```csharp
// ❌ PROHIBITED
var property = type.GetProperty("MyProperty");
logger.LogError("Failed to process {Property}", "WorkflowId");

// ✅ REQUIRED
var property = type.GetProperty(nameof(MyClass.MyProperty));
logger.LogError("Failed to process {Property}", nameof(workflow.WorkflowId));
```

### Rule 3: Enums Over String Constants

For closed sets of values, use enums.

```csharp
// ❌ PROHIBITED
public const string StatusPending = "pending";
public const string StatusRunning = "running";
public string Status { get; set; }

// ✅ REQUIRED
public enum WorkflowStatus { Pending, Running, Completed, Failed }
public WorkflowStatus Status { get; init; }
```

### Rule 4: Result<T,E> for Expected Errors

Use Result types for business logic errors. Reserve exceptions for truly exceptional cases.

```csharp
// ❌ PROHIBITED: Exceptions for expected errors
public Agent GetAgent(string id)
{
    var agent = _registry.Find(id);
    if (agent is null)
        throw new AgentNotFoundException(id);  // Expected case!
    return agent;
}

// ✅ REQUIRED: Result for expected errors
public Result<Agent, AgentError> GetAgent(string agentId)
{
    var agent = _registry.Find(agentId);
    return agent is not null
        ? Result<Agent, AgentError>.Success(agent)
        : Result<Agent, AgentError>.Failure(AgentError.NotFound(agentId));
}
```

### Rule 5: Immutability by Default

Use records and init-only properties. Mutability is opt-in.

```csharp
// ❌ PROHIBITED: Mutable by default
public class WorkflowState
{
    public Guid Id { get; set; }
    public string Status { get; set; }
    public List<Step> Steps { get; set; }
}

// ✅ REQUIRED: Immutable by default
public record WorkflowState
{
    public required Guid Id { get; init; }
    public required WorkflowStatus Status { get; init; }
    public IReadOnlyList<Step> Steps { get; init; } = [];
}
```

### Rule 6: Nullable Reference Types

Enable nullable reference types and handle nulls explicitly.

```xml
<!-- Directory.Build.props -->
<Nullable>enable</Nullable>
```

```csharp
// ❌ PROHIBITED: Ignoring nullability
public string GetName() => _agent.Name;  // Could be null!

// ✅ REQUIRED: Explicit null handling
public string? GetName() => _agent?.Name;

// Or with Result pattern
public Result<string, AgentError> GetName() =>
    _agent?.Name is { } name
        ? Result<string, AgentError>.Success(name)
        : Result<string, AgentError>.Failure(AgentError.NotFound("name"));
```

### Rule 7: Required Properties Over Constructor Madness

Use `required` keyword instead of long constructors.

```csharp
// ❌ AVOID: Long constructor
public AgentDefinition(
    string agentId,
    string name,
    string description,
    string provider,
    string model,
    double temperature,
    string systemPrompt,
    IReadOnlyList<string> capabilities,
    IReadOnlyList<string> tools) { ... }

// ✅ PREFER: Required properties
public record AgentDefinition
{
    public required string AgentId { get; init; }
    public required string Name { get; init; }
    public required string SystemPrompt { get; init; }
    public string Provider { get; init; } = "ollama";
    public string Model { get; init; } = "qwen2.5-coder:7b";
    public double Temperature { get; init; } = 0.7;
    public IReadOnlyList<string> Capabilities { get; init; } = [];
}
```

### Rule 8: Extension Methods for DI Registration

Organize service registration with extension methods.

```csharp
// ❌ PROHIBITED: Everything in Program.cs
builder.Services.AddSingleton<IAgentRegistry, AgentRegistry>();
builder.Services.AddSingleton<ILlmProviderRegistry, LlmProviderRegistry>();
builder.Services.AddSingleton<OllamaProvider>();
// ... 50 more lines

// ✅ REQUIRED: Extension methods
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAuraFoundation(this IServiceCollection services)
    {
        services.AddAgentRegistry();
        services.AddLlmProviders();
        return services;
    }
}

// Program.cs
builder.Services.AddAuraFoundation();
```

### Rule 9: No Magic Strings

All string constants must be named and centralized.

```csharp
// ❌ PROHIBITED: Magic strings
if (agent.Provider == "ollama") { }
var result = await client.GetAsync("/api/agents");

// ✅ REQUIRED: Named constants
public static class Providers
{
    public const string Ollama = "ollama";
    public const string Stub = "stub";
}

public static class ApiRoutes
{
    public const string Agents = "/api/agents";
    public const string Health = "/health";
}
```

### Rule 10: Primary Constructors for DI

Use primary constructors for classes with dependency injection.

```csharp
// ❌ OLDER STYLE
public class AgentRegistry : IAgentRegistry
{
    private readonly ILogger<AgentRegistry> _logger;
    
    public AgentRegistry(ILogger<AgentRegistry> logger)
    {
        _logger = logger;
    }
}

// ✅ PREFERRED: Primary constructor
public class AgentRegistry(ILogger<AgentRegistry> logger) : IAgentRegistry
{
    // logger is in scope
}
```

## File Organization

### Namespace = Folder Path

```
Aura.Foundation/
├── Agents/
│   ├── IAgent.cs           → namespace Aura.Foundation.Agents
│   ├── AgentRegistry.cs    → namespace Aura.Foundation.Agents
│   └── AgentError.cs       → namespace Aura.Foundation.Agents
├── Core/
│   └── Result.cs           → namespace Aura.Foundation.Core
└── Llm/
    └── ILlmProvider.cs     → namespace Aura.Foundation.Llm
```

### One Type Per File

Exception: Closely related types (e.g., a record and its error type).

### Order Within Files

1. Using statements
2. Namespace
3. Type documentation
4. Type declaration
5. Constants
6. Fields
7. Constructors
8. Properties
9. Public methods
10. Private methods

## Documentation

### XML Documentation for Public APIs

```csharp
/// <summary>
/// Executes the agent with the given context.
/// </summary>
/// <param name="context">The execution context.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>The agent output or an error.</returns>
public Task<Result<AgentOutput, AgentError>> ExecuteAsync(
    AgentContext context,
    CancellationToken cancellationToken = default);
```

### Comments for "Why", Not "What"

```csharp
// ❌ BAD: Comments that state the obvious
// Increment counter
counter++;

// ✅ GOOD: Comments that explain why
// Retry count starts at 1 because the first attempt isn't a retry
var retryCount = 1;
```

## Testing

### Test Method Naming

```csharp
// Pattern: Method_Scenario_ExpectedResult
[Fact]
public async Task ExecuteAsync_ValidContext_ReturnsSuccess()

[Fact]
public async Task ExecuteAsync_CancelledToken_ReturnsCancelledError()

[Fact]
public async Task GetAgent_UnknownId_ReturnsNotFoundError()
```

### Arrange-Act-Assert

```csharp
[Fact]
public async Task ExecuteAsync_ValidContext_ReturnsSuccess()
{
    // Arrange
    var agent = CreateTestAgent();
    var context = new AgentContext { Prompt = "test" };

    // Act
    var result = await agent.ExecuteAsync(context);

    // Assert
    result.IsSuccess.Should().BeTrue();
    result.Value.Content.Should().NotBeEmpty();
}
```

## References

- [ADR-009: Lessons from Previous Attempts](../adr/009-lessons-from-previous-attempts.md)
- [Functional Patterns](functional-patterns.md)
