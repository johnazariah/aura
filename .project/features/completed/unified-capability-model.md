# Implementation Plan: Unified Software Development Capability

**Status**: ✅ Complete  
**Completed**: 2026-01-02

## Overview

Consolidate multiple language-specific coding agents into a unified `software-development-{language}` capability model. This simplifies agent discovery and eliminates fragmented capabilities.

## Current State (Problem)

Multiple overlapping capabilities exist:

| Agent | Current Capabilities |
|-------|---------------------|
| RoslynCodingAgent | `csharp-coding`, `coding`, `refactoring`, `csharp-documentation`, `testing-csharp`, `testing` |
| coding-agent.md | `typescript-coding`, `javascript-coding`, `python-coding`, `go-coding`, `coding` |
| TreeSitterIngesterAgent | `ingest:*` (separate) |

**Issues**:
- Fragmentation: Same language has multiple capabilities (`csharp-coding` vs `testing-csharp`)
- Discovery: Hard to know which capability to request
- Duplication: All coding agents follow the same ReAct pattern

## Proposed State

Each language has ONE unified capability:

| Language | Capability | Implementation |
|----------|------------|----------------|
| C# | `software-development-csharp` | RoslynCodingAgent (class) |
| F# | `software-development-fsharp` | FSharpCodingAgent or shared |
| TypeScript | `software-development-typescript` | Config-based |
| JavaScript | `software-development-javascript` | Config-based |
| Python | `software-development-python` | Config-based |
| Go | `software-development-go` | Config-based |
| Rust | `software-development-rust` | Config-based |

## Implementation Steps

### Phase 1: Update C# Agent (2 hours)

#### Step 1.1: Rename RoslynCodingAgent Capabilities

**File**: `src/Aura.Module.Developer/Agents/RoslynCodingAgent.cs`

```csharp
public IReadOnlyList<string> Capabilities => new[]
{
    "software-development-csharp",  // Primary - replaces csharp-coding, testing-csharp, etc.
    "coding",                        // Keep for backward compatibility
    "software-development"           // Generic fallback
};
```

#### Step 1.2: Add Capability Aliases

**File**: `src/Aura.Foundation/Agents/AgentRegistry.cs`

```csharp
private static readonly Dictionary<string, string> CapabilityAliases = new()
{
    // C#
    ["csharp-coding"] = "software-development-csharp",
    ["testing-csharp"] = "software-development-csharp",
    ["csharp-documentation"] = "software-development-csharp",
    ["refactoring"] = "software-development-csharp", // When language is C#
    
    // TypeScript
    ["typescript-coding"] = "software-development-typescript",
    
    // JavaScript  
    ["javascript-coding"] = "software-development-javascript",
    
    // Python
    ["python-coding"] = "software-development-python",
    
    // Go
    ["go-coding"] = "software-development-go",
    
    // Rust
    ["rust-coding"] = "software-development-rust",
};

public IAgent? GetBestForCapability(string capability, string? language = null)
{
    // Resolve alias first
    if (CapabilityAliases.TryGetValue(capability, out var aliasedCapability))
    {
        capability = aliasedCapability;
    }
    
    // If language specified, try language-specific first
    if (!string.IsNullOrEmpty(language))
    {
        var langSpecific = $"software-development-{language.ToLowerInvariant()}";
        var agent = GetBestForCapability(langSpecific);
        if (agent != null) return agent;
    }
    
    // Existing logic...
}
```

### Phase 2: Create Config-based Agent Framework (3 hours)

#### Step 2.1: Language Configuration Schema

**File**: `src/Aura.Foundation/Agents/LanguageConfig.cs`

```csharp
namespace Aura.Foundation.Agents;

/// <summary>
/// Configuration for a language-specific software development agent.
/// </summary>
public record LanguageConfig
{
    /// <summary>Agent ID (e.g., "typescript-developer").</summary>
    public required string AgentId { get; init; }
    
    /// <summary>Primary language (e.g., "typescript").</summary>
    public required string Language { get; init; }
    
    /// <summary>Additional languages this agent handles (e.g., ["javascript"]).</summary>
    public IReadOnlyList<string> AdditionalLanguages { get; init; } = [];
    
    /// <summary>File extensions this agent handles.</summary>
    public IReadOnlyList<string> Extensions { get; init; } = [];
    
    /// <summary>Package manager (npm, pip, go, cargo).</summary>
    public string? PackageManager { get; init; }
    
    /// <summary>Command to run tests.</summary>
    public string? TestCommand { get; init; }
    
    /// <summary>Command to check types/compile.</summary>
    public string? TypeCheckCommand { get; init; }
    
    /// <summary>Command to format code.</summary>
    public string? FormatCommand { get; init; }
    
    /// <summary>Tools available to this agent.</summary>
    public IReadOnlyList<string> Tools { get; init; } = ["file.read", "file.write", "file.modify"];
    
    /// <summary>LLM model to use (null = provider default).</summary>
    public string? Model { get; init; }
}
```

#### Step 2.2: Language Configurations

**File**: `config/languages/typescript.json`

```json
{
  "agentId": "typescript-developer",
  "language": "typescript",
  "additionalLanguages": ["javascript"],
  "extensions": [".ts", ".tsx", ".js", ".jsx"],
  "packageManager": "npm",
  "testCommand": "npm test",
  "typeCheckCommand": "npx tsc --noEmit",
  "formatCommand": "npx prettier --write",
  "tools": ["file.read", "file.write", "file.modify", "shell.execute"]
}
```

**File**: `config/languages/python.json`

```json
{
  "agentId": "python-developer",
  "language": "python",
  "extensions": [".py"],
  "packageManager": "pip",
  "testCommand": "pytest",
  "typeCheckCommand": "mypy",
  "formatCommand": "black",
  "tools": ["file.read", "file.write", "file.modify", "shell.execute"]
}
```

**File**: `config/languages/go.json`

```json
{
  "agentId": "go-developer",
  "language": "go",
  "extensions": [".go"],
  "packageManager": "go",
  "testCommand": "go test ./...",
  "typeCheckCommand": "go build ./...",
  "formatCommand": "go fmt ./...",
  "tools": ["file.read", "file.write", "file.modify", "shell.execute"]
}
```

#### Step 2.3: Config-based Agent Implementation

**File**: `src/Aura.Foundation/Agents/ConfigBasedLanguageAgent.cs`

```csharp
namespace Aura.Foundation.Agents;

/// <summary>
/// A software development agent configured via JSON/YAML.
/// Uses shell commands for validation instead of native tooling.
/// </summary>
public class ConfigBasedLanguageAgent : IAgent
{
    private readonly LanguageConfig _config;
    private readonly ILlmProvider _llm;
    private readonly IToolRegistry _tools;
    private readonly IPromptRegistry _prompts;
    
    public string AgentId => _config.AgentId;
    public string Name => $"{_config.Language.ToTitleCase()} Developer";
    public string Description => $"Software development agent for {_config.Language}";
    
    public IReadOnlyList<string> Capabilities => new[]
    {
        $"software-development-{_config.Language}",
        "software-development",
        "coding"
    }.Concat(_config.AdditionalLanguages.Select(l => $"software-development-{l}"))
     .ToList();
    
    public async Task<AgentOutput> ExecuteAsync(AgentContext context, CancellationToken ct)
    {
        // Use ReAct executor with language-specific prompt
        var prompt = await _prompts.RenderAsync("software-development", new
        {
            language = _config.Language,
            packageManager = _config.PackageManager,
            testCommand = _config.TestCommand,
            typeCheckCommand = _config.TypeCheckCommand,
            task = context.Input
        }, ct);
        
        var tools = _config.Tools
            .Select(t => _tools.GetTool(t))
            .Where(t => t != null)
            .ToList();
        
        var executor = new ReActExecutor(_llm, tools, _logger);
        return await executor.ExecuteAsync(prompt, context, ct);
    }
}
```

#### Step 2.4: Language Agent Loader

**File**: `src/Aura.Foundation/Agents/LanguageAgentLoader.cs`

```csharp
namespace Aura.Foundation.Agents;

public class LanguageAgentLoader
{
    private readonly string _configPath;
    private readonly IServiceProvider _services;
    
    public LanguageAgentLoader(string configPath, IServiceProvider services)
    {
        _configPath = configPath;
        _services = services;
    }
    
    public IEnumerable<IAgent> LoadAgents()
    {
        var configDir = Path.Combine(_configPath, "languages");
        if (!Directory.Exists(configDir))
            yield break;
        
        foreach (var file in Directory.GetFiles(configDir, "*.json"))
        {
            var json = File.ReadAllText(file);
            var config = JsonSerializer.Deserialize<LanguageConfig>(json);
            
            if (config != null)
            {
                yield return new ConfigBasedLanguageAgent(
                    config,
                    _services.GetRequiredService<ILlmProviderRegistry>().GetDefault(),
                    _services.GetRequiredService<IToolRegistry>(),
                    _services.GetRequiredService<IPromptRegistry>(),
                    _services.GetRequiredService<ILogger<ConfigBasedLanguageAgent>>()
                );
            }
        }
    }
}
```

### Phase 3: Unified Prompt Template (1 hour)

**File**: `prompts/software-development.prompt`

```handlebars
---
description: Unified software development prompt for any language
tools:
  - file.read
  - file.write
  - file.modify
  - shell.execute
---
You are a {{language}} software developer. Your task is to implement, test, and validate code changes.

## Available Commands

{{#if testCommand}}
- **Run tests**: `{{testCommand}}`
{{/if}}
{{#if typeCheckCommand}}
- **Type check**: `{{typeCheckCommand}}`
{{/if}}
{{#if formatCommand}}
- **Format code**: `{{formatCommand}}`
{{/if}}
{{#if packageManager}}
- **Package manager**: `{{packageManager}}`
{{/if}}

## Task

{{task}}

## Instructions

1. Read existing code to understand the context
2. Make the required changes using file.write or file.modify
3. Run type checking to verify correctness: `{{typeCheckCommand}}`
4. Run tests to validate: `{{testCommand}}`
5. Fix any errors and repeat until all checks pass

Always use the file tools to make changes. Never just describe what to do - actually do it.
```

### Phase 4: Update Workflow Planner (1 hour)

**File**: `prompts/workflow-plan.prompt`

Update to use new capability names:

```handlebars
## Available Capabilities

For coding tasks, use `software-development-{language}`:
- `software-development-csharp` - C# with Roslyn tools
- `software-development-typescript` - TypeScript/JavaScript
- `software-development-python` - Python
- `software-development-go` - Go
- `software-development-rust` - Rust

For documentation: `documentation`
For code review: `review`
For analysis: `analysis`
```

### Phase 5: Register Agents (30 min)

**File**: `src/Aura.Module.Developer/DeveloperModule.cs`

```csharp
public void ConfigureServices(IServiceCollection services)
{
    // Existing...
    
    // Register config-based language agents
    services.AddSingleton<LanguageAgentLoader>(sp => new LanguageAgentLoader(
        Path.Combine(AppContext.BaseDirectory, "config"),
        sp
    ));
}

public void ConfigureAgents(IAgentRegistry registry)
{
    // Existing Roslyn agent (C#)
    registry.Register(new RoslynCodingAgent(...));
    
    // Load config-based agents (TypeScript, Python, Go, etc.)
    var loader = _serviceProvider.GetRequiredService<LanguageAgentLoader>();
    foreach (var agent in loader.LoadAgents())
    {
        registry.Register(agent);
    }
}
```

## Database Migration

None required - this is a code refactoring.

## Acceptance Criteria

- [ ] `software-development-csharp` capability works (RoslynCodingAgent)
- [ ] Old capabilities (`csharp-coding`, `testing-csharp`) still resolve correctly
- [ ] Config-based agents work for TypeScript, Python, Go
- [ ] Workflow planner uses new capability names
- [ ] All existing tests pass
- [ ] New integration test: multi-language workflow

## Testing Strategy

1. **Unit tests**: Capability alias resolution
2. **Unit tests**: Config loading and validation
3. **Integration tests**: End-to-end workflow with different languages
4. **Backward compatibility**: Existing workflows still work

## Rollback Plan

Keep the `CapabilityAliases` dictionary to ensure old capability names continue to work. Can be removed in a future major version.

## Related Specs

- `spec/unified-software-development-capability.md` - Design spec
- `adr/011-two-tier-capability-model.md` - Current capability architecture
