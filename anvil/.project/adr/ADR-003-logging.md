---
title: "ADR-003: Logging Strategy"
status: "Accepted"
date: "2026-01-30"
authors: "Anvil Team"
tags: ["observability", "logging", "cross-cutting"]
supersedes: ""
superseded_by: ""
---

# ADR-003: Logging Strategy

## Status

Accepted

## Context

Anvil needs consistent logging for:

- Debugging during development
- Troubleshooting test failures
- Audit trails for test runs
- Machine-readable output for CI integration

Without a logging strategy, engineers may:
- Use `Console.WriteLine()` (lost context, no levels)
- Log at inconsistent levels
- Include sensitive data in logs
- Create logs that are hard to parse

## Decision

We adopt **structured logging** using Microsoft.Extensions.Logging with **Serilog** as the provider.

### Log Levels

| Level | When to Use | Example |
|-------|-------------|---------|
| **Error** | Unexpected failure requiring attention | `Failed to connect to Aura API` |
| **Warning** | Recoverable issue, degraded behavior | `Retry succeeded after 2 attempts` |
| **Information** | Significant business events | `Story completed: 5 passed, 1 failed` |
| **Debug** | Developer troubleshooting | `Parsing story YAML from {Path}` |

### Pattern

```csharp
public class StoryRunner
{
    private readonly ILogger<StoryRunner> _logger;

    public StoryRunner(ILogger<StoryRunner> logger)
    {
        _logger = logger;
    }

    public async Task<RunResult> RunAsync(Story story, CancellationToken ct)
    {
        _logger.LogInformation("Starting story {StoryId}", story.Id);
        try
        {
            var result = await ExecuteAsync(story, ct);
            _logger.LogInformation("Story {StoryId} completed with status {Status}", 
                story.Id, result.Status);
            return result;
        }
        catch (AuraClientException ex)
        {
            _logger.LogError(ex, "Failed to execute story {StoryId}", story.Id);
            throw;
        }
    }
}
```

### What to Log

| Always Log | Never Log |
|------------|-----------|
| Operation start/end | Passwords, tokens, API keys |
| Error conditions with context | Full request/response bodies (Debug only) |
| Test results and status | Personal data |
| Performance metrics | Secrets from environment variables |

### Configuration

Logging is configured in Program.cs:

```csharp
// src/Anvil.Cli/Program.cs
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: 
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/anvil-.log", 
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSerilog();
```

### Structured Properties

Use semantic logging with named properties:

```csharp
// ✅ Good: Structured properties
_logger.LogInformation("Story {StoryId} completed in {ElapsedMs}ms", story.Id, elapsed);

// ❌ Bad: String interpolation
_logger.LogInformation($"Story {story.Id} completed in {elapsed}ms");
```

## Consequences

**Positive**
- **POS-001**: Consistent log format across all modules
- **POS-002**: Easy to filter by level and source context
- **POS-003**: Structured data enables log aggregation and querying
- **POS-004**: File logs provide persistent history for debugging
- **POS-005**: Serilog's rich sink ecosystem (JSON, Seq, etc.)

**Negative**
- **NEG-001**: Serilog adds a dependency
- **NEG-002**: Configuration is slightly more complex than Console-only

## Alternatives Considered

### Alternative 1: Console Provider Only
- **Description**: Use Microsoft.Extensions.Logging with built-in Console provider
- **Rejection Reason**: Limited formatting options; no file output; harder to query structured data

### Alternative 2: Console.WriteLine
- **Description**: Direct console output without logging framework
- **Rejection Reason**: No log levels, timestamps, or context; impossible to filter; no file persistence

### Alternative 3: NLog
- **Description**: Alternative structured logging library
- **Rejection Reason**: Serilog has better API ergonomics and is more commonly used in modern .NET

## Implementation Notes

- **IMP-001**: Inject `ILogger<T>` via constructor, never create loggers directly
- **IMP-002**: Use structured logging templates with `{PropertyName}` placeholders
- **IMP-003**: Configure minimum level via `appsettings.json` or environment variables
- **IMP-004**: Add `Serilog.Extensions.Hosting` for host integration

## References

- [Serilog Documentation](https://serilog.net/)
- [Microsoft.Extensions.Logging](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging)
- [Structured Logging Best Practices](https://messagetemplates.org/)
