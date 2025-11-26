# Phase 1: Core Infrastructure

**Duration:** 2-3 hours  
**Dependencies:** None  
**Output:** `Aura/` project with agent infrastructure

## Objective

Create the core agent infrastructure: registry, loading, and execution.

## Tasks

### 1.1 Create Project Structure

```bash
dotnet new classlib -n Aura -o src/Aura
```

**Files to create:**

```
src/Aura/
├── Aura.csproj
├── Agents/
│   ├── IAgent.cs
│   ├── AgentMetadata.cs
│   ├── AgentContext.cs
│   ├── AgentResult.cs
│   ├── IAgentRegistry.cs
│   ├── AgentRegistry.cs
│   ├── MarkdownAgentLoader.cs
│   └── ConfigurableAgent.cs
└── GlobalUsings.cs
```

### 1.2 Define Core Interfaces

**IAgent.cs:**
```csharp
namespace Aura.Agents;

public interface IAgent
{
    AgentMetadata Metadata { get; }
    Task<AgentResult> ExecuteAsync(AgentContext context, CancellationToken ct = default);
}
```

**AgentMetadata.cs:**
```csharp
namespace Aura.Agents;

public record AgentMetadata
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string[] Capabilities { get; init; }
    public int Priority { get; init; } = 50;
    public string Provider { get; init; } = "ollama";
    public string Model { get; init; } = "qwen2.5-coder:7b";
    public float Temperature { get; init; } = 0.7f;
    public string? Description { get; init; }
    public string? Version { get; init; }
}
```

**AgentContext.cs:**
```csharp
namespace Aura.Agents;

public record AgentContext
{
    public required Guid WorkflowId { get; init; }
    public required string WorkItemId { get; init; }
    public required string WorkItemTitle { get; init; }
    public string? WorkspacePath { get; init; }
    public string? TaskDescription { get; init; }
    public IReadOnlyDictionary<string, object> Data { get; init; } = 
        new Dictionary<string, object>();
}
```

**AgentResult.cs:**
```csharp
namespace Aura.Agents;

public record AgentResult
{
    public required bool Success { get; init; }
    public string? Output { get; init; }
    public string? Error { get; init; }
    public IReadOnlyDictionary<string, object> Artifacts { get; init; } = 
        new Dictionary<string, object>();
    
    public static AgentResult Ok(string output) => 
        new() { Success = true, Output = output };
    
    public static AgentResult Fail(string error) => 
        new() { Success = false, Error = error };
}
```

### 1.3 Implement Agent Registry

**IAgentRegistry.cs:**
```csharp
namespace Aura.Agents;

public interface IAgentRegistry
{
    void Register(IAgent agent);
    bool Unregister(string agentId);
    
    IReadOnlyList<IAgent> GetAll();
    IAgent? GetById(string agentId);
    IReadOnlyList<IAgent> GetByCapability(string capability);
    
    Task LoadFromFolderAsync(string path, CancellationToken ct = default);
    Task WatchFolderAsync(string path, CancellationToken ct = default);
    
    event EventHandler<AgentRegistryChangedEventArgs>? Changed;
}

public record AgentRegistryChangedEventArgs(
    IReadOnlyList<IAgent> Added,
    IReadOnlyList<IAgent> Removed,
    IReadOnlyList<IAgent> Updated
);
```

**AgentRegistry.cs:**
```csharp
namespace Aura.Agents;

public class AgentRegistry : IAgentRegistry, IDisposable
{
    private readonly Dictionary<string, IAgent> _agents = new(StringComparer.OrdinalIgnoreCase);
    private readonly ReaderWriterLockSlim _lock = new();
    private FileSystemWatcher? _watcher;
    private readonly ILogger<AgentRegistry> _logger;
    private readonly MarkdownAgentLoader _loader;
    
    public event EventHandler<AgentRegistryChangedEventArgs>? Changed;
    
    public AgentRegistry(MarkdownAgentLoader loader, ILogger<AgentRegistry> logger)
    {
        _loader = loader;
        _logger = logger;
    }
    
    public void Register(IAgent agent)
    {
        _lock.EnterWriteLock();
        try
        {
            _agents[agent.Metadata.Id] = agent;
            _logger.LogInformation("Registered agent: {Id} ({Name})", 
                agent.Metadata.Id, agent.Metadata.Name);
        }
        finally { _lock.ExitWriteLock(); }
        
        Changed?.Invoke(this, new([agent], [], []));
    }
    
    public IReadOnlyList<IAgent> GetByCapability(string capability)
    {
        _lock.EnterReadLock();
        try
        {
            return _agents.Values
                .Where(a => a.Metadata.Capabilities.Contains(capability, StringComparer.OrdinalIgnoreCase))
                .OrderBy(a => a.Metadata.Priority)
                .ToList();
        }
        finally { _lock.ExitReadLock(); }
    }
    
    public async Task LoadFromFolderAsync(string path, CancellationToken ct = default)
    {
        var files = Directory.GetFiles(path, "*.md");
        foreach (var file in files)
        {
            try
            {
                var agent = await _loader.LoadAsync(file, ct);
                if (agent != null) Register(agent);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load agent from {File}", file);
            }
        }
    }
    
    public async Task WatchFolderAsync(string path, CancellationToken ct = default)
    {
        await LoadFromFolderAsync(path, ct);
        
        _watcher = new FileSystemWatcher(path, "*.md")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
        };
        
        _watcher.Changed += async (s, e) => await ReloadAgentAsync(e.FullPath, ct);
        _watcher.Created += async (s, e) => await ReloadAgentAsync(e.FullPath, ct);
        _watcher.Deleted += (s, e) => Unregister(Path.GetFileNameWithoutExtension(e.Name!));
        
        _watcher.EnableRaisingEvents = true;
    }
    
    public void Dispose() => _watcher?.Dispose();
}
```

### 1.4 Implement Markdown Agent Loader

**MarkdownAgentLoader.cs:**
```csharp
namespace Aura.Agents;

public class MarkdownAgentLoader
{
    private readonly ILlmProviderRegistry _providers;
    
    public MarkdownAgentLoader(ILlmProviderRegistry providers)
    {
        _providers = providers;
    }
    
    public async Task<IAgent?> LoadAsync(string filePath, CancellationToken ct = default)
    {
        var content = await File.ReadAllTextAsync(filePath, ct);
        var definition = Parse(content, filePath);
        
        if (definition == null) return null;
        
        return new ConfigurableAgent(definition, _providers);
    }
    
    private AgentDefinition? Parse(string content, string filePath)
    {
        // Parse markdown sections:
        // # Agent Name
        // ## Metadata (table or list)
        // ## Capabilities (list)
        // ## System Prompt (content)
        
        // Implementation: regex or simple line parsing
        // Port from existing AgentDefinitionParser with simplification
    }
}

public record AgentDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string[] Capabilities { get; init; }
    public int Priority { get; init; } = 50;
    public string Provider { get; init; } = "ollama";
    public string Model { get; init; } = "qwen2.5-coder:7b";
    public float Temperature { get; init; } = 0.7f;
    public required string SystemPrompt { get; init; }
    public string? Description { get; init; }
}
```

### 1.5 Implement ConfigurableAgent

**ConfigurableAgent.cs:**
```csharp
namespace Aura.Agents;

public class ConfigurableAgent : IAgent
{
    private readonly AgentDefinition _definition;
    private readonly ILlmProviderRegistry _providers;
    
    public AgentMetadata Metadata { get; }
    
    public ConfigurableAgent(AgentDefinition definition, ILlmProviderRegistry providers)
    {
        _definition = definition;
        _providers = providers;
        
        Metadata = new AgentMetadata
        {
            Id = definition.Id,
            Name = definition.Name,
            Capabilities = definition.Capabilities,
            Priority = definition.Priority,
            Provider = definition.Provider,
            Model = definition.Model,
            Temperature = definition.Temperature,
            Description = definition.Description
        };
    }
    
    public async Task<AgentResult> ExecuteAsync(AgentContext context, CancellationToken ct = default)
    {
        var provider = _providers.GetProviderOrDefault(Metadata.Provider);
        var prompt = RenderPrompt(context);
        
        try
        {
            var response = await provider.GenerateAsync(
                prompt, 
                Metadata.Model, 
                new GenerateOptions { Temperature = Metadata.Temperature },
                ct);
            
            return AgentResult.Ok(response);
        }
        catch (Exception ex)
        {
            return AgentResult.Fail(ex.Message);
        }
    }
    
    private string RenderPrompt(AgentContext context)
    {
        var prompt = _definition.SystemPrompt;
        
        // Simple template substitution
        prompt = prompt.Replace("{{context.WorkflowId}}", context.WorkflowId.ToString());
        prompt = prompt.Replace("{{context.WorkItemId}}", context.WorkItemId);
        prompt = prompt.Replace("{{context.WorkItemTitle}}", context.WorkItemTitle);
        prompt = prompt.Replace("{{context.TaskDescription}}", context.TaskDescription ?? "");
        prompt = prompt.Replace("{{context.WorkspacePath}}", context.WorkspacePath ?? "");
        
        return prompt;
    }
}
```

### 1.6 Add Unit Tests

**AgentRegistryTests.cs:**
```csharp
public class AgentRegistryTests
{
    [Fact]
    public void Register_AddsAgentToRegistry() { ... }
    
    [Fact]
    public void GetByCapability_ReturnsSortedByPriority() { ... }
    
    [Fact]
    public void GetByCapability_CaseInsensitive() { ... }
    
    [Fact]
    public void Unregister_RemovesAgent() { ... }
}

public class MarkdownAgentLoaderTests
{
    [Fact]
    public async Task LoadAsync_ParsesValidMarkdown() { ... }
    
    [Fact]
    public async Task LoadAsync_ReturnsNullForInvalidMarkdown() { ... }
}

public class ConfigurableAgentTests
{
    [Fact]
    public async Task ExecuteAsync_CallsProvider() { ... }
    
    [Fact]
    public void RenderPrompt_SubstitutesPlaceholders() { ... }
}
```

## Verification

1. ✅ `dotnet build src/Aura` succeeds
2. ✅ `dotnet test` - all unit tests pass
3. ✅ Manual: Load agents from `agents/` folder
4. ✅ Manual: `GetByCapability("coding")` returns expected agents

## Deliverables

- [ ] `src/Aura/Aura.csproj` with package references
- [ ] Core interfaces and types
- [ ] `AgentRegistry` with hot-reload support
- [ ] `MarkdownAgentLoader` parsing agent definitions
- [ ] `ConfigurableAgent` executing via LLM provider
- [ ] Unit tests for all components

## Notes

- The `ILlmProviderRegistry` dependency will be implemented in Phase 2
- For now, use a stub/mock provider to test agent execution
- Port the regex parsing from existing `AgentDefinitionParser`
