# Agent Architecture Specification

**Version:** 1.0  
**Status:** Draft  
**Last Updated:** 2025-11-26

## Overview

Agents are the fundamental building blocks of Aura. Every capability—coding, testing, analysis, integration—is implemented as an agent with a consistent interface.

## Agent Interface

```csharp
public interface IAgent
{
    AgentMetadata Metadata { get; }
    Task<AgentResult> ExecuteAsync(AgentContext context, CancellationToken ct = default);
}

public record AgentMetadata
{
    public required string Id { get; init; }           // Unique identifier
    public required string Name { get; init; }         // Human-readable name
    public required string[] Capabilities { get; init; } // What this agent can do
    public int Priority { get; init; } = 50;           // Lower = selected first
    public string Provider { get; init; } = "ollama";  // LLM provider
    public string Model { get; init; } = "qwen2.5-coder:7b";
    public string? Description { get; init; }
    public string? Version { get; init; }
}

public record AgentContext
{
    public required Guid WorkflowId { get; init; }
    public required string WorkItemId { get; init; }
    public required string WorkItemTitle { get; init; }
    public string? WorkspacePath { get; init; }
    public string? TaskDescription { get; init; }
    public IReadOnlyDictionary<string, object> Data { get; init; } = new Dictionary<string, object>();
    public IngestionContext? Ingestion { get; init; }  // RAG context
}

public record AgentResult
{
    public required bool Success { get; init; }
    public string? Output { get; init; }
    public string? Error { get; init; }
    public IReadOnlyDictionary<string, object> Artifacts { get; init; } = new Dictionary<string, object>();
}
```

## Agent Types

### 1. Markdown Agents (ConfigurableAgent)

Defined in `agents/*.md` files. Loaded at startup and hot-reloaded on change.

```markdown
# Python Coding Agent

## Metadata

- **Name**: Python Coding Agent
- **Priority**: 60
- **Provider**: ollama
- **Model**: qwen2.5-coder:7b

## Capabilities

- coding
- python
- python-coding

## System Prompt

You are an expert Python developer...

{{context.TaskDescription}}

## Output Format

Return Python code in a markdown code block.
```

### 2. Coded Agents

Implemented in C# for complex logic beyond LLM prompts.

| Agent | Purpose | Why Coded? |
|-------|---------|------------|
| RoslynAgent | C# code generation with compilation | Needs Roslyn SDK |
| GitHubAgent | Issue/PR sync | REST API integration |
| ADOAgent | Azure DevOps sync | REST API integration |
| PipelineMonitor | Build status tracking | Webhook handling |

### 3. Hybrid Agents

Coded agents that use LLM internally.

```csharp
public class RoslynAgent : IAgent
{
    private readonly ILlmProvider _llm;
    private readonly CodeValidator _validator;
    
    public async Task<AgentResult> ExecuteAsync(AgentContext context, CancellationToken ct)
    {
        // 1. Call LLM for code generation
        var code = await _llm.GenerateAsync(BuildPrompt(context), Metadata.Model);
        
        // 2. Use Roslyn to compile and validate
        var validation = await _validator.ValidateAsync(code);
        
        // 3. Iterate if needed
        while (!validation.Success && iteration < 5)
        {
            code = await _llm.GenerateAsync(BuildFixPrompt(code, validation.Errors));
            validation = await _validator.ValidateAsync(code);
            iteration++;
        }
        
        return new AgentResult { Success = validation.Success, Output = code };
    }
}
```

## Agent Registry

Central registry for all agents. Supports hot-reload and runtime registration.

```csharp
public interface IAgentRegistry
{
    // Registration
    void Register(IAgent agent);
    void Unregister(string agentId);
    
    // Discovery
    IReadOnlyList<IAgent> GetAll();
    IAgent? GetById(string agentId);
    IReadOnlyList<IAgent> GetByCapability(string capability);
    IReadOnlyList<IAgent> GetByCapabilities(params string[] capabilities);
    
    // Hot-reload
    Task WatchFolderAsync(string path, CancellationToken ct);
    event EventHandler<AgentRegistryChangedEventArgs>? Changed;
}
```

### Capability Matching

Agents are selected by capability with priority as tiebreaker:

```csharp
// Request: I need an agent that can do "csharp-coding"
var candidates = registry.GetByCapability("csharp-coding");
// Returns: [RoslynAgent(pri=60), CSharpAgent(pri=70), CodingAgent(pri=90)]

// First match (lowest priority number) wins
var selected = candidates.First(); // RoslynAgent
```

### Capability Vocabulary

| Capability | Description | Example Agents |
|------------|-------------|----------------|
| `coding` | General code generation | CodingAgent |
| `csharp-coding` | C# specific | RoslynAgent, CSharpAgent |
| `python-coding` | Python specific | PythonAgent |
| `testing` | Test generation | TestingAgent |
| `documentation` | Doc generation | DocumentationAgent |
| `requirements-analysis` | Break down requirements | BusinessAnalystAgent |
| `issue-sync` | Sync with issue trackers | GitHubAgent, ADOAgent |
| `pr-management` | PR creation/monitoring | GitHubAgent |
| `pipeline-monitoring` | Build status | PipelineMonitorAgent |
| `ingestion` | RAG content processing | CodeIngestor, DocIngestor |

## Agent Lifecycle

```
┌─────────────────────────────────────────────────────────────┐
│                    Agent Lifecycle                           │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌─────────┐      ┌─────────┐      ┌─────────┐             │
│  │ DEFINED │ ───► │ LOADED  │ ───► │ ACTIVE  │             │
│  │         │      │         │      │         │             │
│  │ .md file│      │ Parsed  │      │ In      │             │
│  │ or code │      │ to      │      │ registry│             │
│  │         │      │ IAgent  │      │         │             │
│  └─────────┘      └─────────┘      └─────────┘             │
│       │                                 │                   │
│       │ File change detected            │ Unregister        │
│       ▼                                 ▼                   │
│  ┌─────────┐                      ┌─────────┐              │
│  │ RELOAD  │                      │ REMOVED │              │
│  └─────────┘                      └─────────┘              │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

## Hot-Reload Mechanism

1. **File Watcher** monitors `agents/` folder
2. **On Change**: Parse .md file → Create ConfigurableAgent → Replace in registry
3. **On Delete**: Unregister agent
4. **On Add**: Register new agent
5. **Events**: `Changed` event fires with diff

```csharp
// Startup
await registry.WatchFolderAsync("agents/", ct);

// Runtime
registry.Changed += (sender, args) =>
{
    foreach (var added in args.Added)
        logger.LogInformation("Agent added: {Name}", added.Metadata.Name);
    foreach (var removed in args.Removed)
        logger.LogInformation("Agent removed: {Name}", removed.Metadata.Name);
};
```

## API Registration

For coded agents or external agents:

```http
POST /api/agents
Content-Type: application/json

{
  "name": "Custom Python Agent",
  "capabilities": ["coding", "python-coding"],
  "priority": 55,
  "provider": "ollama",
  "model": "codellama:13b",
  "systemPrompt": "You are an expert Python developer..."
}
```

## Agent Execution Model

```
┌─────────────────────────────────────────────────────────────┐
│                    Execution Flow                            │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│   Caller                    Registry                Agent   │
│     │                          │                      │     │
│     │  GetByCapability("X")    │                      │     │
│     │─────────────────────────►│                      │     │
│     │                          │                      │     │
│     │  [Agent1, Agent2, ...]   │                      │     │
│     │◄─────────────────────────│                      │     │
│     │                          │                      │     │
│     │  (Select first or user override)                │     │
│     │                          │                      │     │
│     │  ExecuteAsync(context)                          │     │
│     │─────────────────────────────────────────────────►     │
│     │                          │                      │     │
│     │                          │    (LLM call, tools) │     │
│     │                          │                      │     │
│     │  AgentResult                                    │     │
│     │◄─────────────────────────────────────────────────     │
│     │                          │                      │     │
└─────────────────────────────────────────────────────────────┘
```

## Open Questions

1. **Capability inheritance** - Should `csharp-coding` imply `coding`?
2. **Agent versioning** - How to handle breaking changes in agent definitions?
3. **Agent dependencies** - Can agents declare dependencies on other agents?
4. **Concurrent execution limits** - Max parallel agents per provider?
