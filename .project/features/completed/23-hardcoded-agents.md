# Hardcoded Agents

**Status:** ✅ Complete  
**Last Updated:** 2025-12-12

## Overview

While markdown-based agents provide hot-reloadable flexibility, some agents benefit from being implemented directly in C#:

- **Performance**: Roslyn/TreeSitter parsing is faster than LLM calls
- **Reliability**: Deterministic parsing vs probabilistic LLM output
- **Integration**: Direct access to .NET libraries and services
- **Complexity**: Some logic is easier in code than prompts

Hardcoded agents implement `IAgent` directly and coexist with markdown agents in the same registry.

## Architecture

```
IAgentRegistry
├── Markdown Agents (from MarkdownAgentLoader)
│   ├── coding-agent (Priority: 70)
│   ├── chat-agent (Priority: 50)
│   └── generic-code-ingester (Priority: 50)
│
└── Hardcoded Agents (from module registration)
    ├── CSharpIngesterAgent (Priority: 100)
    ├── PythonIngesterAgent (Priority: 100)
    └── FallbackIngesterAgent (Priority: 0)
```

All agents are queryable the same way:
```csharp
registry.GetBestForCapability("ingest:cs");  // Returns CSharpIngesterAgent
registry.GetBestForCapability("coding");     // Returns coding-agent
```

## Creating a Hardcoded Agent

### Step 1: Implement IAgent

```csharp
public class CSharpIngesterAgent : IAgent
{
    private readonly IRoslynWorkspaceService _roslyn;
    private readonly ILogger<CSharpIngesterAgent> _logger;

    public CSharpIngesterAgent(
        IRoslynWorkspaceService roslyn,
        ILogger<CSharpIngesterAgent> logger)
    {
        _roslyn = roslyn;
        _logger = logger;
    }

    public string AgentId => "csharp-ingester";

    public AgentMetadata Metadata => new()
    {
        Name = "C# Roslyn Ingester",
        Description = "Parses C# files using Roslyn for semantic analysis",
        Priority = 100,  // High priority - specialized
        Provider = "native",  // Not using LLM
        Model = "roslyn",
        Capabilities = ["ingest:cs", "ingest:csx"],
        Tags = ["ingester", "roslyn", "csharp", "native"],
    };

    public async Task<AgentOutput> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        // Extract input from context
        var filePath = context.Properties.GetValueOrDefault("filePath") as string
            ?? throw new ArgumentException("filePath required");
        var content = context.Properties.GetValueOrDefault("content") as string
            ?? throw new ArgumentException("content required");

        _logger.LogDebug("Parsing C# file: {FilePath}", filePath);

        try
        {
            var chunks = await ParseCSharpAsync(content, filePath, cancellationToken);

            return AgentOutput.WithArtifacts(
                $"Extracted {chunks.Count} semantic chunks from {Path.GetFileName(filePath)}",
                new Dictionary<string, string>
                {
                    ["chunks"] = JsonSerializer.Serialize(chunks),
                    ["language"] = "csharp",
                    ["parser"] = "roslyn"
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse C# file: {FilePath}", filePath);
            throw new AgentException($"Roslyn parsing failed: {ex.Message}", ex);
        }
    }

    private async Task<List<SemanticChunk>> ParseCSharpAsync(
        string content,
        string filePath,
        CancellationToken ct)
    {
        // Roslyn parsing logic...
    }
}
```

### Step 2: Register in Module

```csharp
public class DeveloperModule : IAuraModule
{
    public void RegisterAgents(IAgentRegistry registry, IConfiguration config)
    {
        // Hardcoded agents need their dependencies
        // Option A: Create with new (if no DI needed)
        var fallback = new FallbackIngesterAgent();
        registry.Register(fallback);

        // Option B: Use a factory pattern for DI
        // The module receives a service provider or uses
        // a factory that's registered in ConfigureServices
    }

    public void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        // Register agent as service for DI
        services.AddSingleton<CSharpIngesterAgent>();
        
        // Register factory that creates and registers agents
        services.AddSingleton<IAgentFactory, DeveloperAgentFactory>();
    }
}
```

### Step 3: Agent Factory Pattern (Recommended)

For agents with dependencies, use a factory:

```csharp
public interface IHardcodedAgentProvider
{
    IEnumerable<IAgent> GetAgents();
}

public class DeveloperAgentProvider : IHardcodedAgentProvider
{
    private readonly IRoslynWorkspaceService _roslyn;
    private readonly ILogger<CSharpIngesterAgent> _csLogger;
    private readonly ILogger<FallbackIngesterAgent> _fallbackLogger;

    public DeveloperAgentProvider(
        IRoslynWorkspaceService roslyn,
        ILogger<CSharpIngesterAgent> csLogger,
        ILogger<FallbackIngesterAgent> fallbackLogger)
    {
        _roslyn = roslyn;
        _csLogger = csLogger;
        _fallbackLogger = fallbackLogger;
    }

    public IEnumerable<IAgent> GetAgents()
    {
        yield return new CSharpIngesterAgent(_roslyn, _csLogger);
        yield return new FallbackIngesterAgent(_fallbackLogger);
    }
}
```

Then in `AgentRegistryInitializer`:
```csharp
public async Task InitializeAsync()
{
    // Load markdown agents
    await LoadMarkdownAgentsAsync();

    // Load hardcoded agents from all providers
    var providers = _serviceProvider.GetServices<IHardcodedAgentProvider>();
    foreach (var provider in providers)
    {
        foreach (var agent in provider.GetAgents())
        {
            _registry.Register(agent);
            _logger.LogInformation("Registered hardcoded agent: {AgentId}", agent.AgentId);
        }
    }
}
```

## Provider Field

Hardcoded agents use special provider values:

| Provider | Meaning |
|----------|---------|
| `native` | Pure C# implementation, no LLM |
| `roslyn` | Uses Roslyn for analysis |
| `treesitter` | Uses TreeSitter for parsing |
| `hybrid` | Combines native + LLM |

This allows the system to distinguish:
```csharp
if (agent.Metadata.Provider == "native")
{
    // No LLM cost, run freely
}
else
{
    // May incur LLM costs, apply rate limiting
}
```

## Fallback Chain

The graceful degradation pattern:

```csharp
public class FallbackIngesterAgent : IAgent
{
    public string AgentId => "fallback-ingester";

    public AgentMetadata Metadata => new()
    {
        Name = "Fallback Ingester",
        Description = "Last resort ingester when no specialized parser exists",
        Priority = 0,  // Lowest priority
        Provider = "native",
        Capabilities = ["ingest:*"],  // Matches anything
        Tags = ["ingester", "fallback"],
    };

    public Task<AgentOutput> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        var filePath = context.Properties.GetValueOrDefault("filePath") as string ?? "unknown";
        var content = context.Properties.GetValueOrDefault("content") as string ?? "";
        var extension = Path.GetExtension(filePath);

        // Create single chunk with whole file
        var chunk = new SemanticChunk
        {
            Text = content,
            FilePath = filePath,
            ChunkType = "file",
            SymbolName = Path.GetFileName(filePath),
            StartLine = 1,
            EndLine = content.Split('\n').Length,
            Language = extension.TrimStart('.'),
            Metadata = new Dictionary<string, string>
            {
                ["warning"] = $"No specialized parser for {extension} files. " +
                              "Content indexed as plain text. " +
                              "Consider adding an ingester agent for better results."
            }
        };

        return Task.FromResult(AgentOutput.WithArtifacts(
            $"⚠️ No specialized parser for {extension}. Indexed as plain text.",
            new Dictionary<string, string>
            {
                ["chunks"] = JsonSerializer.Serialize(new[] { chunk }),
                ["fallback"] = "true"
            }));
    }
}
```

## Priority Guidelines

| Range | Use Case | Examples |
|-------|----------|----------|
| 1-9 | Reserved for user overrides | User-provided specialized |
| 10-19 | Specialized native parsers | Roslyn, TreeSitter |
| 20-49 | LLM-based specialized | Python ingester via LLM |
| 50-69 | Generic/text agents | text-ingester, markdown |
| 70-98 | Low-priority fallbacks | blank-line chunker |
| 99 | Last resort | fallback-ingester |

## When to Use Hardcoded Agents

✅ **Use hardcoded when:**
- Performance is critical (parsing thousands of files)
- Deterministic output is required
- Using specialized libraries (Roslyn, TreeSitter)
- Complex stateful logic
- Direct service integration needed

❌ **Use markdown when:**
- Flexibility and hot-reload matter
- Natural language understanding needed
- Experimentation/iteration is frequent
- The task is primarily LLM-driven
- Non-developers need to modify behavior

## Hybrid Agents

Some agents combine native processing with LLM:

```csharp
public class HybridCSharpAgent : IAgent
{
    private readonly IRoslynWorkspaceService _roslyn;
    private readonly ILlmProviderRegistry _llm;

    public AgentMetadata Metadata => new()
    {
        Provider = "hybrid",
        // ...
    };

    public async Task<AgentOutput> ExecuteAsync(AgentContext context, CancellationToken ct)
    {
        // Step 1: Use Roslyn to extract code structure (fast, reliable)
        var codeStructure = await _roslyn.AnalyzeAsync(context.Properties["filePath"]);

        // Step 2: Use LLM to generate documentation/summary (intelligent)
        var enrichedContext = context with
        {
            Properties = new Dictionary<string, object>(context.Properties)
            {
                ["codeStructure"] = codeStructure
            }
        };

        var llmProvider = _llm.GetDefaultProvider();
        var summary = await llmProvider.ChatAsync(...);

        // Step 3: Combine results
        return AgentOutput.WithArtifacts(summary, codeStructure.ToArtifacts());
    }
}
```

## Implementation Checklist

- [x] Add `IHardcodedAgentProvider` interface to Foundation
- [x] Update `AgentRegistryInitializer` to load hardcoded agents
- [x] Add `Provider` field to `AgentMetadata` (already existed)
- [x] Create `FallbackIngesterAgent` in Foundation
- [x] Create `TextIngesterAgent` in Foundation
- [x] Create `FoundationAgentProvider` in Foundation
- [x] Create `CSharpIngesterAgent` in Developer module
- [x] Register `DeveloperAgentProvider` in Developer module
- [x] Add capability pattern matching for wildcards (`ingest:*`)

## Files

| File | Purpose |
|------|---------|
| `src/Aura.Foundation/Agents/IHardcodedAgentProvider.cs` | Provider interface |
| `src/Aura.Foundation/Agents/FoundationAgentProvider.cs` | Foundation agents |
| `src/Aura.Foundation/Agents/FallbackIngesterAgent.cs` | Last resort agent |
| `src/Aura.Foundation/Agents/TextIngesterAgent.cs` | Text/markdown chunker |
| `src/Aura.Module.Developer/Agents/CSharpIngesterAgent.cs` | Roslyn-based |
| `src/Aura.Module.Developer/Agents/DeveloperAgentProvider.cs` | Registers agents |
