---
title: "ADR-006: Environment Configuration"
status: "Accepted"
date: "2026-01-30"
authors: "Anvil Team"
tags: ["configuration", "environments", "cli"]
supersedes: ""
superseded_by: ""
---

# ADR-006: Environment Configuration

## Status

Accepted

## Context

Anvil must target the Aura service which may run at different locations:
- Local development: `http://localhost:5300`
- Different ports or hosts in CI
- Custom timeouts for slow operations

Configuration sources need to be:
- Easy to set for local development (file-based defaults)
- Overridable in CI (environment variables)
- Overridable per-invocation (CLI arguments)

## Decision

We adopt a **hybrid configuration** approach with the following precedence (highest wins):

```
CLI Arguments > Environment Variables > appsettings.json > Defaults
```

### Configuration Sources

| Source | Use Case | Example |
|--------|----------|---------|
| **appsettings.json** | Default values, checked into repo | `"AuraBaseUrl": "http://localhost:5300"` |
| **Environment Variables** | CI/CD, Docker, per-machine settings | `ANVIL__AuraBaseUrl=http://aura:5300` |
| **CLI Arguments** | One-off overrides, testing | `--aura-url http://localhost:5301` |

### Configuration Schema

```csharp
public class AnvilOptions
{
    public string AuraBaseUrl { get; set; } = "http://localhost:5300";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);
    public string StoriesPath { get; set; } = "stories";
    public string ResultsPath { get; set; } = "results";
    public string DatabasePath { get; set; } = "anvil.db";
}
```

### appsettings.json

```json
{
  "Anvil": {
    "AuraBaseUrl": "http://localhost:5300",
    "Timeout": "00:01:00",
    "StoriesPath": "stories",
    "ResultsPath": "results",
    "DatabasePath": "anvil.db"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning"
      }
    }
  }
}
```

### Environment Variables

Use double-underscore (`__`) for nested configuration:

```bash
# Linux/macOS
export ANVIL__AuraBaseUrl="http://aura-service:5300"
export ANVIL__Timeout="00:02:00"

# Windows PowerShell
$env:ANVIL__AuraBaseUrl = "http://aura-service:5300"
$env:ANVIL__Timeout = "00:02:00"
```

### CLI Arguments

```bash
anvil run --aura-url http://localhost:5301 --timeout 120 stories/my-story.yaml
```

### Implementation

```csharp
// Program.cs
var builder = Host.CreateApplicationBuilder(args);

// 1. Load appsettings.json (automatic with CreateApplicationBuilder)
// 2. Add environment variables with ANVIL__ prefix
builder.Configuration.AddEnvironmentVariables(prefix: "ANVIL__");

// 3. Add CLI argument overrides
builder.Configuration.AddCommandLine(args, new Dictionary<string, string>
{
    { "--aura-url", "Anvil:AuraBaseUrl" },
    { "--timeout", "Anvil:Timeout" },
    { "--stories", "Anvil:StoriesPath" },
    { "--results", "Anvil:ResultsPath" },
    { "--db", "Anvil:DatabasePath" },
});

// Bind to options
builder.Services.Configure<AnvilOptions>(
    builder.Configuration.GetSection("Anvil"));
```

### Accessing Configuration

```csharp
public class StoryRunner
{
    private readonly AnvilOptions _options;

    public StoryRunner(IOptions<AnvilOptions> options)
    {
        _options = options.Value;
    }

    public async Task RunAsync()
    {
        _logger.LogInformation("Connecting to Aura at {AuraUrl}", _options.AuraBaseUrl);
        // ...
    }
}
```

## Consequences

**Positive**
- **POS-001**: Sensible defaults work out of the box
- **POS-002**: CI can override via environment variables without file changes
- **POS-003**: CLI arguments allow quick testing with different settings
- **POS-004**: Standard .NET configuration pattern (familiar to developers)
- **POS-005**: IOptions<T> integrates with DI

**Negative**
- **NEG-001**: Multiple configuration sources can be confusing
- **NEG-002**: Debugging "where did this value come from?" requires understanding precedence

## Alternatives Considered

### Alternative 1: appsettings.json Only
- **Description**: All configuration in JSON files
- **Rejection Reason**: Difficult to override in CI without modifying files

### Alternative 2: CLI Arguments Only
- **Description**: Everything passed via command line
- **Rejection Reason**: Verbose for common settings; no defaults

### Alternative 3: Environment Variables Only
- **Description**: All configuration via environment variables
- **Rejection Reason**: No checked-in defaults; harder for local development

## Implementation Notes

- **IMP-001**: Use `IOptions<AnvilOptions>` for injecting configuration
- **IMP-002**: Environment variable prefix is `ANVIL__` (double underscore)
- **IMP-003**: CLI arguments use `--kebab-case` style
- **IMP-004**: Document all configuration options in README
- **IMP-005**: Validate configuration at startup (fail fast on invalid values)

## References

- [.NET Configuration](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration)
- [Options Pattern](https://learn.microsoft.com/en-us/dotnet/core/extensions/options)
- [Environment Variables Configuration](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration-providers#environment-variable-configuration-provider)
