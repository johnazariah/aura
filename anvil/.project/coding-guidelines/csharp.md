---
title: C# Coding Guidelines
description: C# 14 and .NET 10 conventions for the Anvil test harness
maturity: stable
---

# C# Coding Guidelines

> These guidelines define **how** to write C# code in Anvil. For architectural principles (layers, dependency flow), see [principles.md](../architecture/principles.md).

## Language & Framework

| Aspect | Standard |
|--------|----------|
| Language | C# 14 |
| Framework | .NET 10 |
| Nullable | Enabled (`<Nullable>enable</Nullable>`) |
| Implicit Usings | Enabled |
| LangVersion | Preview (for C# 14 features) |

---

## Naming Conventions

### General Rules

| Element | Convention | Example |
|---------|------------|---------|
| Classes | PascalCase | `StoryRunner`, `AuraClient` |
| Interfaces | `I` prefix | `IStorySource`, `IValidator` |
| Methods | PascalCase | `ExecuteAsync`, `ValidateBuild` |
| Parameters | camelCase | `storyPath`, `timeout` |
| Local variables | camelCase | `result`, `validator` |
| Constants | PascalCase | `DefaultTimeout`, `MaxRetries` |
| Private fields | `_camelCase` | `_logger`, `_httpClient` |
| Properties | PascalCase | `StoriesPath`, `IsSuccess` |

### Async Methods

Suffix with `Async`:
```csharp
Task<Story> LoadAsync(string path);
Task<StoryResult> ExecuteAsync(Story story, CancellationToken ct);
```

---

## Type Patterns

### Primary Constructors (C# 12+)

Use for classes with injected dependencies:

```csharp
public class StoryRunner(
    IStorySource source,
    IStoryExecutor executor,
    IValidator validator,
    ILogger<StoryRunner> logger)
{
    public async Task<StoryResult> RunAsync(string storyPath)
    {
        logger.LogInformation("Running story: {StoryPath}", storyPath);
        var story = await source.LoadAsync(storyPath);
        // ...
    }
}
```

### Records for DTOs

Immutable data types:

```csharp
public record Story(
    string Id,
    string Title,
    StoryContent Content,
    IReadOnlyList<string> Tags);

public record StoryResult(
    string StoryId,
    bool IsSuccess,
    TimeSpan Duration,
    IReadOnlyList<ValidationResult> Validations);

public record ValidationResult(
    string ValidatorName,
    bool Passed,
    string? Message = null);
```

### Records with Validation

Use `init` properties for validation:

```csharp
public record StoryContent
{
    public required string Description { get; init; }
    public required IReadOnlyList<string> AcceptanceCriteria { get; init; }
    public string? ExecutionMode { get; init; }
}
```

---

## Result Pattern

### The `Result<T, TError>` Type

See [ADR-004](../ADR/ADR-004-cross-cutting-concerns.md) for the decision.

```csharp
public readonly struct Result<T, TError>
{
    private readonly T? _value;
    private readonly TError? _error;
    private readonly bool _isSuccess;

    private Result(T value)
    {
        _value = value;
        _error = default;
        _isSuccess = true;
    }

    private Result(TError error)
    {
        _value = default;
        _error = error;
        _isSuccess = false;
    }

    public bool IsSuccess => _isSuccess;
    public bool IsFailure => !_isSuccess;
    
    public T Value => _isSuccess 
        ? _value! 
        : throw new InvalidOperationException("Cannot access Value on failure Result");
    
    public TError Error => !_isSuccess 
        ? _error! 
        : throw new InvalidOperationException("Cannot access Error on success Result");

    public static Result<T, TError> Success(T value) => new(value);
    public static Result<T, TError> Failure(TError error) => new(error);
    
    public static implicit operator Result<T, TError>(T value) => Success(value);
}
```

### LINQ Monadic Extensions

Enable chaining with `Select` and `SelectMany`:

```csharp
public static class ResultExtensions
{
    public static Result<TResult, TError> Select<T, TError, TResult>(
        this Result<T, TError> result,
        Func<T, TResult> selector) =>
        result.IsSuccess
            ? Result<TResult, TError>.Success(selector(result.Value))
            : Result<TResult, TError>.Failure(result.Error);

    public static Result<TResult, TError> SelectMany<T, TError, TResult>(
        this Result<T, TError> result,
        Func<T, Result<TResult, TError>> selector) =>
        result.IsSuccess
            ? selector(result.Value)
            : Result<TResult, TError>.Failure(result.Error);

    public static Result<TResult, TError> SelectMany<T, TIntermediate, TError, TResult>(
        this Result<T, TError> result,
        Func<T, Result<TIntermediate, TError>> intermediateSelector,
        Func<T, TIntermediate, TResult> resultSelector) =>
        result.SelectMany(x => 
            intermediateSelector(x).Select(y => resultSelector(x, y)));
}
```

### Using Result with LINQ Query Syntax

```csharp
public Result<StoryResult, AnvilError> RunStory(string path)
{
    return 
        from story in LoadStory(path)
        from executed in ExecuteStory(story)
        from validated in ValidateOutput(executed)
        select new StoryResult(story.Id, validated);
}
```

### Error Types

Domain-specific error types:

```csharp
public abstract record AnvilError(string Message)
{
    public record StoryNotFound(string Path) 
        : AnvilError($"Story not found: {Path}");
    
    public record ExecutionFailed(string StoryId, string Reason)
        : AnvilError($"Story {StoryId} execution failed: {Reason}");
    
    public record ValidationFailed(string StoryId, IReadOnlyList<string> Failures)
        : AnvilError($"Story {StoryId} validation failed: {string.Join(", ", Failures)}");
    
    public record AuraApiError(int StatusCode, string Response)
        : AnvilError($"Aura API error ({StatusCode}): {Response}");
}
```

---

## Async/Await Patterns

### Always Use CancellationToken

```csharp
public async Task<Result<StoryResult, AnvilError>> ExecuteAsync(
    Story story,
    CancellationToken ct = default)
{
    ct.ThrowIfCancellationRequested();
    
    var response = await _httpClient.PostAsync(
        "/api/developer/workflows", 
        content, 
        ct);
    
    // ...
}
```

### ConfigureAwait in Library Code

```csharp
// In Anvil.Core / Anvil.Application
var result = await LoadAsync(path).ConfigureAwait(false);
```

### Avoid async void

Only allowed for event handlers. All other async methods return `Task` or `Task<T>`.

---

## Dependency Injection

### Constructor Injection with Primary Constructors

```csharp
public class AuraApiExecutor(
    HttpClient httpClient,
    IOptions<AuraOptions> options,
    ILogger<AuraApiExecutor> logger) : IStoryExecutor
{
    private readonly AuraOptions _options = options.Value;

    public async Task<Result<ExecutionOutput, AnvilError>> ExecuteAsync(
        Story story,
        CancellationToken ct)
    {
        var url = $"{_options.BaseUrl}/api/developer/workflows";
        logger.LogDebug("Calling Aura API: {Url}", url);
        // ...
    }
}
```

### Service Registration

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAnvilCore(
        this IServiceCollection services)
    {
        services.AddSingleton<IStorySource, FileStorySource>();
        services.AddSingleton<IStoryExecutor, AuraApiExecutor>();
        services.AddSingleton<StoryRunner>();
        return services;
    }
}
```

### Options Pattern

```csharp
public record AuraOptions
{
    public const string SectionName = "Anvil:Aura";
    
    public required string BaseUrl { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(1);
}

// Registration
services.Configure<AuraOptions>(
    configuration.GetSection(AuraOptions.SectionName));
```

---

## Collection Patterns

### Use Appropriate Collection Types

| Need | Type | Example |
|------|------|---------|
| Immutable return | `IReadOnlyList<T>` | `IReadOnlyList<Story> Stories` |
| Immutable construction | `ImmutableArray<T>` | `ImmutableArray.Create(items)` |
| Mutable internal | `List<T>` | `private readonly List<Story> _stories` |
| Key lookup | `Dictionary<K,V>` | `Dictionary<string, Story>` |

### Collection Expressions (C# 12+)

```csharp
// Array initialization
string[] tags = ["ci", "greenfield", "vscode"];

// List initialization
List<Story> stories = [story1, story2, story3];

// Spread operator
var combined = [..existing, ..additional];
```

### LINQ Best Practices

```csharp
// Prefer method syntax for complex queries
var failedStories = results
    .Where(r => !r.IsSuccess)
    .OrderByDescending(r => r.Duration)
    .Take(10)
    .ToList();

// Use query syntax for joins
var storyResults = 
    from story in stories
    join result in results on story.Id equals result.StoryId
    where result.IsSuccess
    select (story, result);
```

---

## Logging

### Structured Logging with Message Templates

See [ADR-003](../ADR/ADR-003-logging.md) for the decision.

```csharp
// Good: Named placeholders
logger.LogInformation("Executing story {StoryId} via {Mode}", story.Id, mode);

// Bad: String interpolation
logger.LogInformation($"Executing story {story.Id} via {mode}");
```

### Log Levels

| Level | Usage |
|-------|-------|
| `Trace` | Detailed diagnostic flow |
| `Debug` | Developer troubleshooting |
| `Information` | Key business events (story started/completed) |
| `Warning` | Recoverable issues (retry, timeout) |
| `Error` | Failures requiring attention |
| `Critical` | Application cannot continue |

### Scoped Context

```csharp
using (logger.BeginScope(new Dictionary<string, object>
{
    ["StoryId"] = story.Id,
    ["ExecutionMode"] = mode
}))
{
    logger.LogInformation("Starting execution");
    // All logs in this scope include StoryId and ExecutionMode
}
```

---

## Testing Patterns

### Test Class Structure

```csharp
public class StoryRunnerTests
{
    private readonly IStorySource _source = Substitute.For<IStorySource>();
    private readonly IStoryExecutor _executor = Substitute.For<IStoryExecutor>();
    private readonly IValidator _validator = Substitute.For<IValidator>();
    private readonly ILogger<StoryRunner> _logger = NullLogger<StoryRunner>.Instance;

    private StoryRunner CreateSut() => new(_source, _executor, _validator, _logger);

    [Fact]
    public async Task RunAsync_ValidStory_ReturnsSuccess()
    {
        // Arrange
        var story = new Story("test-1", "Test Story", new StoryContent { ... }, []);
        _source.LoadAsync("test.md").Returns(Result<Story, AnvilError>.Success(story));
        _executor.ExecuteAsync(story, Arg.Any<CancellationToken>())
            .Returns(Result<ExecutionOutput, AnvilError>.Success(new ExecutionOutput { ... }));
        _validator.ValidateAsync(Arg.Any<ExecutionOutput>())
            .Returns(new ValidationResult("Build", true));

        var sut = CreateSut();

        // Act
        var result = await sut.RunAsync("test.md");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }
}
```

### FluentAssertions

```csharp
result.IsSuccess.Should().BeTrue();
result.Value.Duration.Should().BeLessThan(TimeSpan.FromSeconds(30));
result.Value.Validations.Should().AllSatisfy(v => v.Passed.Should().BeTrue());
results.Should().ContainSingle(r => r.StoryId == "test-1");
```

### NSubstitute Patterns

```csharp
// Arrange
_source.LoadAsync(Arg.Any<string>())
    .Returns(x => Result<Story, AnvilError>.Success(
        new Story(x.Arg<string>(), "Title", content, [])));

// Verify
await _executor.Received(1).ExecuteAsync(
    Arg.Is<Story>(s => s.Id == "test-1"),
    Arg.Any<CancellationToken>());
```

---

## File I/O

### Use Abstractions

```csharp
public class FileStorySource(IFileSystem fileSystem) : IStorySource
{
    public async Task<Result<Story, AnvilError>> LoadAsync(string path)
    {
        if (!fileSystem.File.Exists(path))
            return new AnvilError.StoryNotFound(path);
        
        var content = await fileSystem.File.ReadAllTextAsync(path);
        return ParseStory(content, path);
    }
}
```

### Path Handling

```csharp
// Use Path.Combine for cross-platform paths
var fullPath = Path.Combine(_options.StoriesDirectory, relativePath);

// Normalize paths
var normalized = Path.GetFullPath(path);
```

---

## XML Documentation

### Public APIs

```csharp
/// <summary>
/// Executes a story through the specified execution mode.
/// </summary>
/// <param name="story">The story to execute.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>
/// A <see cref="Result{T,TError}"/> containing the execution output on success,
/// or an <see cref="AnvilError"/> on failure.
/// </returns>
/// <exception cref="OperationCanceledException">
/// Thrown when <paramref name="ct"/> is cancelled.
/// </exception>
public Task<Result<ExecutionOutput, AnvilError>> ExecuteAsync(
    Story story,
    CancellationToken ct = default);
```

---

## Anti-Patterns to Avoid

### ❌ Throwing for Expected Cases

```csharp
// Bad
public Story GetStory(string id)
{
    var story = _stories.FirstOrDefault(s => s.Id == id);
    if (story == null)
        throw new StoryNotFoundException(id); // Control flow exception
    return story;
}

// Good
public Result<Story, AnvilError> GetStory(string id)
{
    var story = _stories.FirstOrDefault(s => s.Id == id);
    return story is not null
        ? story
        : new AnvilError.StoryNotFound(id);
}
```

### ❌ Swallowing Exceptions

```csharp
// Bad
try { await ExecuteAsync(story); }
catch { /* silent failure */ }

// Good
try { await ExecuteAsync(story); }
catch (Exception ex)
{
    _logger.LogError(ex, "Story {StoryId} execution failed", story.Id);
    return new AnvilError.ExecutionFailed(story.Id, ex.Message);
}
```

### ❌ String Concatenation in Logs

```csharp
// Bad
_logger.LogInformation($"Running {story.Id} at {DateTime.Now}");

// Good
_logger.LogInformation("Running {StoryId} at {Timestamp}", story.Id, DateTime.Now);
```

### ❌ Mixing Async and Sync

```csharp
// Bad
var result = ExecuteAsync(story).Result; // Deadlock risk

// Good
var result = await ExecuteAsync(story);
```

---

## Summary Checklist

Before completing any code:

- [ ] Nullable reference types handled (`?`, `!`, null checks)
- [ ] Async methods have `CancellationToken` parameter
- [ ] Logging uses structured message templates
- [ ] Result types used for expected failures
- [ ] Primary constructors for DI
- [ ] Records for immutable data
- [ ] Tests follow Arrange-Act-Assert pattern
