---
title: "ADR-002: Dependency Injection Strategy"
status: "Accepted"
date: "2026-01-30"
authors: "Anvil Team"
tags: ["architecture", "di", "testing", "composition"]
supersedes: ""
superseded_by: ""
---

# ADR-002: Dependency Injection Strategy

## Status

Accepted

## Context

Anvil requires a clear strategy for managing dependencies between components. Without consistent dependency injection:

- Components become tightly coupled
- Testing requires complex mocking
- Swapping implementations (e.g., fake Aura client vs real) becomes difficult
- Hidden dependencies make code harder to understand

## Decision

We adopt **Microsoft.Extensions.DependencyInjection** with constructor injection as the primary pattern.

### Core Principles

1. **Dependencies are explicit**: All dependencies passed via constructor
2. **Composition root**: Dependencies wired in `Program.cs` or host builder
3. **Depend on abstractions**: Services depend on interfaces, not concrete classes
4. **No service locators**: Never resolve services manually except at the composition root

### Pattern

```csharp
// ✅ Good: Constructor injection with interface
public interface IAuraClient
{
    Task<StoryResult> ExecuteStoryAsync(string storyId, CancellationToken ct);
}

public class StoryRunner
{
    private readonly IAuraClient _auraClient;
    private readonly ILogger<StoryRunner> _logger;

    public StoryRunner(IAuraClient auraClient, ILogger<StoryRunner> logger)
    {
        _auraClient = auraClient;
        _logger = logger;
    }

    public async Task<RunResult> RunAsync(Story story, CancellationToken ct)
    {
        _logger.LogInformation("Running story {StoryId}", story.Id);
        return await _auraClient.ExecuteStoryAsync(story.Id, ct);
    }
}
```

### Composition Root

Wire dependencies in Program.cs:

```csharp
// src/Anvil.Cli/Program.cs
var builder = Host.CreateApplicationBuilder(args);

// Register services
builder.Services.AddSingleton<IAuraClient, AuraHttpClient>();
builder.Services.AddSingleton<IStoryLoader, YamlStoryLoader>();
builder.Services.AddTransient<StoryRunner>();

// Build and run
var host = builder.Build();
var runner = host.Services.GetRequiredService<StoryRunner>();
await runner.RunAsync(story, CancellationToken.None);
```

### Testing

Inject fakes in tests:

```csharp
// tests/Anvil.Tests/StoryRunnerTests.cs
[Fact]
public async Task Should_ExecuteStory_When_StoryIsValid()
{
    // Arrange
    var fakeClient = new FakeAuraClient();
    fakeClient.SetupSuccess("story-1");
    var logger = NullLogger<StoryRunner>.Instance;
    var sut = new StoryRunner(fakeClient, logger);

    // Act
    var result = await sut.RunAsync(new Story { Id = "story-1" }, CancellationToken.None);

    // Assert
    result.Should().BeSuccessful();
}
```

## Consequences

**Positive**
- **POS-001**: Dependencies are visible in constructor signatures
- **POS-002**: Easy to test with fakes (no patching required)
- **POS-003**: Swapping implementations is trivial (configure in DI container)
- **POS-004**: Consistent with Aura monorepo patterns
- **POS-005**: Rich ecosystem (logging, configuration, hosting all integrate)

**Negative**
- **NEG-001**: DI container adds some overhead for a simple CLI
- **NEG-002**: Service registration can become verbose as the app grows

## Alternatives Considered

### Alternative 1: Pure Constructor Injection (No Container)
- **Description**: Manual wiring at entry point without a DI container
- **Rejection Reason**: Loses integration with Microsoft.Extensions.Logging, Configuration, and Hosting; more manual wiring as app grows

### Alternative 2: Module-Level Singletons
- **Description**: Static instances accessed globally
- **Rejection Reason**: Creates hidden global state; makes testing difficult; couples modules

### Alternative 3: Service Locator Pattern
- **Description**: Resolve dependencies from a global container anywhere in code
- **Rejection Reason**: Hides dependencies; makes code harder to understand and test

## Implementation Notes

- **IMP-001**: Every service class must accept dependencies via constructor
- **IMP-002**: Use interfaces (`I*`) for abstractions, not abstract base classes
- **IMP-003**: `Program.cs` is the composition root—wire everything there
- **IMP-004**: Never inject `IServiceProvider` except in factories
- **IMP-005**: Use `IOptions<T>` pattern for configuration

## References

- [Microsoft.Extensions.DependencyInjection](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)
- [Aura DI Patterns](../../README.md) - Monorepo conventions
