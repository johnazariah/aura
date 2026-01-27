# Foundation Module Specification

**Status:** Reference Documentation
**Created:** 2026-01-28

The Foundation module (`Aura.Foundation`) provides core services used by all vertical modules. It is the shared infrastructure layer.

## Service Registration

All Foundation services are registered via `AddAuraFoundation()`:

```csharp
public static IServiceCollection AddAuraFoundation(
    this IServiceCollection services,
    IConfiguration configuration)
{
    services.AddSingleton<IFileSystem, FileSystem>();    // Abstractions
    services.AddShellServices(configuration);            // Process execution
    services.AddGitServices(configuration);              // Git operations
    services.AddToolServices(configuration);             // Tool registry + ReAct
    services.AddLlmServices(configuration);              // LLM providers
    services.AddRagServices(configuration);              // RAG + Code Graph
    services.AddConversationServices(configuration);     // Chat persistence
    services.AddAgentServices(configuration);            // Agent framework
    services.AddPromptServices(configuration);           // Handlebars templates
    services.AddSingleton<StartupTaskRunner>();          // Startup tasks
    return services;
}
```

---

## 1. Agent Framework

### 1.1 Agent Definition

Agents are defined by markdown files with YAML frontmatter:

```markdown
# Agent Name

Description of the agent.

## Metadata

- **Priority**: 50
- **Reflection**: true

## Capabilities

- capability-one
- capability-two

## Languages

- csharp
- python

## Tools

- file.read
- file.write
- shell.execute

## System Prompt

You are an expert...
```

### 1.2 Key Interfaces

**IAgent** - Core execution contract:
```csharp
public interface IAgent
{
    string AgentId { get; }
    AgentMetadata Metadata { get; }
    Task<AgentOutput> ExecuteAsync(AgentContext context, CancellationToken ct);
}
```

**IAgentRegistry** - Agent discovery and routing:
```csharp
public interface IAgentRegistry
{
    IAgent? GetAgent(string agentId);
    IReadOnlyList<IAgent> GetAllAgents();
    IReadOnlyList<IAgent> GetAgentsByCapability(string capability);
    IAgent? SelectAgentForCapability(string capability, IReadOnlyList<string>? languages = null);
    void RegisterAgent(IAgent agent);
    void UnregisterAgent(string agentId);
}
```

**IAgentLoader** - Loads agents from markdown files:
```csharp
public interface IAgentLoader
{
    AgentDefinition? LoadFromFile(string filePath);
    IReadOnlyList<AgentDefinition> LoadFromDirectory(string directoryPath);
}
```

### 1.3 Agent Metadata

```csharp
public sealed record AgentMetadata(
    string Name,
    string Description,
    IReadOnlyList<string> Capabilities,
    int Priority,                           // Lower = more specialized
    IReadOnlyList<string> Languages,        // Empty = polyglot
    string Provider,                         // ollama, azure, openai
    string? Model,                          // null = use provider default
    double Temperature,
    IReadOnlyList<string> Tools,
    IReadOnlyList<string> Tags,
    bool Reflection,                         // Enable self-critique
    string? ReflectionPrompt,
    string? ReflectionModel);
```

### 1.4 Agent Selection Algorithm

1. Filter agents with matching capability
2. Filter by language if specified (polyglot agents match all)
3. Sort by priority ascending (lower = more specialized)
4. Return first match

### 1.5 Hardcoded Agents

Some agents require compile-time implementation:

```csharp
public interface IHardcodedAgentProvider
{
    IReadOnlyList<IAgent> GetAgents();
}
```

Foundation provides:
- `FallbackIngesterAgent` - Indexes unknown file types

Developer module provides:
- `RoslynCodingAgent` - C# development with Roslyn integration

### 1.6 Agent Reflection

Agents can self-critique before returning:

```csharp
public interface IAgentReflectionService
{
    Task<string> ReflectAsync(
        string originalPrompt,
        string response,
        AgentMetadata metadata,
        CancellationToken ct);
}
```

Enabled by `Reflection: true` in agent definition.

---

## 2. LLM Provider Framework

### 2.1 Provider Interface

```csharp
public interface ILlmProvider
{
    string ProviderId { get; }
    string DisplayName { get; }
    bool IsAvailable { get; }
    
    Task<LlmResponse> GenerateAsync(
        LlmRequest request,
        CancellationToken ct);
    
    IAsyncEnumerable<LlmStreamChunk> StreamAsync(
        LlmRequest request,
        CancellationToken ct);
}
```

### 2.2 Provider Registry

```csharp
public interface ILlmProviderRegistry
{
    ILlmProvider GetProvider(string providerId);
    ILlmProvider GetDefaultProvider();
    IReadOnlyList<ILlmProvider> GetAllProviders();
    void RegisterProvider(ILlmProvider provider);
}
```

### 2.3 Supported Providers

| Provider | Class | Configuration |
|----------|-------|---------------|
| `ollama` | `OllamaProvider` | `Ollama:BaseUrl`, `Ollama:DefaultModel` |
| `azure` | `AzureOpenAiProvider` | `AzureOpenAi:Endpoint`, `AzureOpenAi:ApiKey`, `AzureOpenAi:DeploymentName` |
| `openai` | `OpenAiProvider` | `OpenAi:ApiKey`, `OpenAi:DefaultModel` |
| `stub` | `StubLlmProvider` | For testing |

### 2.4 LLM Request/Response

```csharp
public record LlmRequest
{
    public required string Model { get; init; }
    public required IReadOnlyList<LlmMessage> Messages { get; init; }
    public double Temperature { get; init; } = 0.7;
    public int? MaxTokens { get; init; }
    public IReadOnlyList<ToolDefinition>? Tools { get; init; }
    public JsonSchema? ResponseSchema { get; init; }  // Structured output
}

public record LlmResponse
{
    public required string Content { get; init; }
    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }
    public int TokensUsed { get; init; }
    public string? FinishReason { get; init; }
}
```

### 2.5 Structured Output

Providers can enforce JSON schema on responses:

```csharp
public record JsonSchema
{
    public required string Name { get; init; }
    public required object Schema { get; init; }
    public bool Strict { get; init; } = true;
}
```

---

## 3. RAG Pipeline

### 3.1 RAG Service Interface

```csharp
public interface IRagService
{
    Task IndexAsync(RagContent content, CancellationToken ct);
    Task<int> IndexBatchAsync(IReadOnlyList<RagContent> contents, CancellationToken ct);
    Task<int> IndexDirectoryAsync(string directoryPath, RagIndexOptions? options, CancellationToken ct);
    Task<bool> RemoveAsync(string contentId, CancellationToken ct);
    Task<IReadOnlyList<RagResult>> QueryAsync(string query, RagQueryOptions? options, CancellationToken ct);
    Task<RagStats> GetStatsAsync(CancellationToken ct);
    Task ClearAsync(CancellationToken ct);
    Task<bool> IsHealthyAsync(CancellationToken ct);
}
```

### 3.2 Content Ingestors

Specialized processors for different file types:

```csharp
public interface IContentIngestor
{
    string IngestorId { get; }
    IReadOnlyList<string> SupportedExtensions { get; }
    RagContentType ContentType { get; }
    bool CanIngest(string filePath);
    Task<IReadOnlyList<IngestedChunk>> IngestAsync(string filePath, string content, CancellationToken ct);
}
```

**Built-in Ingestors:**
- `PlainTextIngestor` - `.txt`, `.log`
- `MarkdownIngestor` - `.md` (splits by headings)
- `CodeIngestor` - Base for code files

### 3.3 Code Ingestors (Extended)

For code files, ingestors produce both RAG chunks and code graph elements:

```csharp
public interface ICodeIngestor : IContentIngestor
{
    Task<CodeIngestionResult> IngestCodeAsync(
        string filePath,
        string content,
        string workspacePath,
        CancellationToken ct);
}

public record CodeIngestionResult(
    IReadOnlyList<IngestedChunk> Chunks,
    IReadOnlyList<CodeNode> Nodes,
    IReadOnlyList<CodeEdge> Edges);
```

**Developer Module Ingestors:**
- `RoslynCodeIngestor` - C# via Roslyn
- Tree-sitter ingestors for: Python, TypeScript, Rust, Go, F#, etc.

### 3.4 Embedding Generation

```csharp
public interface IEmbeddingProvider
{
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct);
    Task<IReadOnlyList<float[]>> GenerateBatchAsync(IReadOnlyList<string> texts, CancellationToken ct);
}
```

Ollama implements this interface using the embedding model (default: `nomic-embed-text`).

### 3.5 Background Indexing

```csharp
public interface IBackgroundIndexer
{
    string EnqueueDirectory(string directoryPath, RagIndexOptions? options);
    BackgroundJobStatus? GetJobStatus(string jobId);
    IReadOnlyList<BackgroundJobStatus> GetAllJobs();
    void CancelJob(string jobId);
}
```

---

## 4. Code Graph Service

### 4.1 Interface

```csharp
public interface ICodeGraphService
{
    Task<IReadOnlyList<CodeNode>> FindImplementationsAsync(string interfaceName, string? repositoryPath, CancellationToken ct);
    Task<IReadOnlyList<CodeNode>> FindDerivedTypesAsync(string baseClassName, string? repositoryPath, CancellationToken ct);
    Task<IReadOnlyList<CodeNode>> FindCallersAsync(string methodName, string? containingTypeName, string? repositoryPath, CancellationToken ct);
    Task<IReadOnlyList<CodeNode>> GetTypeMembersAsync(string typeName, string? repositoryPath, CancellationToken ct);
    Task<IReadOnlyList<CodeNode>> FindNodesAsync(string name, CodeNodeType? nodeType, string? repositoryPath, CancellationToken ct);
    Task<CodeGraphStats> GetStatsAsync(string? repositoryPath, CancellationToken ct);
}
```

### 4.2 Code Node Types

```csharp
public enum CodeNodeType
{
    Project,
    Namespace,
    Class,
    Interface,
    Struct,
    Record,
    Enum,
    Method,
    Property,
    Field,
    Event,
    Delegate,
    Constructor,
    Function,    // For non-OOP languages
    Module,      // For module-based languages
}
```

### 4.3 Code Edge Types

```csharp
public enum CodeEdgeType
{
    Contains,       // Parent → Child (namespace → class)
    Implements,     // Class → Interface
    Inherits,       // Class → BaseClass
    Calls,          // Method → Method
    References,     // Any → Any (usage)
    Returns,        // Method → Type
    Overrides,      // Method → Method
}
```

---

## 5. Tool Framework

### 5.1 Tool Definition

```csharp
public record ToolDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required JsonSchema InputSchema { get; init; }
    public bool RequiresConfirmation { get; init; }
    public IReadOnlyList<string> Categories { get; init; } = [];
}
```

### 5.2 Tool Registry

```csharp
public interface IToolRegistry
{
    IReadOnlyList<ToolDefinition> GetAllTools();
    ToolDefinition? GetTool(string toolId);
    IReadOnlyList<ToolDefinition> GetByCategory(string category);
    Task<ToolResult> ExecuteAsync(ToolInput input, CancellationToken ct);
    void RegisterTool(ToolDefinition tool);
    void RegisterTool<TInput, TOutput>(ITool<TInput, TOutput> tool);
    bool UnregisterTool(string toolId);
}
```

### 5.3 Built-In Tools

| Tool ID | Purpose |
|---------|---------|
| `file.read` | Read file contents |
| `file.write` | Create/overwrite file |
| `file.modify` | Patch file with diff |
| `file.list` | List directory contents |
| `file.exists` | Check file existence |
| `shell.execute` | Run shell commands |
| `check_token_budget` | Query remaining context budget |
| `spawn_subagent` | Delegate to child agent |
| `pattern.list` | List available patterns |
| `pattern.load` | Load pattern with language overlay |

### 5.4 ReAct Executor

Executes agents with tool usage via reasoning loop:

```csharp
public interface IReActExecutor
{
    Task<ReActResult> ExecuteAsync(
        string task,
        IReadOnlyList<ToolDefinition> availableTools,
        ILlmProvider llm,
        ReActOptions? options,
        CancellationToken ct);
}

public record ReActOptions
{
    public int MaxSteps { get; init; } = 10;
    public string? Model { get; init; }
    public double Temperature { get; init; } = 0.2;
    public string? WorkingDirectory { get; init; }
    public bool RequireConfirmation { get; init; } = true;
    public bool UseStructuredOutput { get; init; } = false;
    public int TokenBudget { get; init; } = 100_000;
    public bool RetryOnFailure { get; init; } = false;
    public int MaxRetries { get; init; } = 3;
}
```

### 5.5 Token Budget Tracking

```csharp
public class TokenTracker
{
    public int Budget { get; }
    public int Used { get; private set; }
    public int Remaining => Budget - Used;
    public double PercentUsed => (double)Used / Budget * 100;
    
    public void AddUsage(int tokens);
    public bool HasBudget(int requiredTokens);
}
```

---

## 6. Git Services

### 6.1 Git Service

```csharp
public interface IGitService
{
    Task<string?> GetCurrentBranchAsync(string repositoryPath, CancellationToken ct);
    Task<string?> GetCurrentCommitAsync(string repositoryPath, CancellationToken ct);
    Task<bool> IsRepositoryAsync(string path, CancellationToken ct);
    Task<IReadOnlyList<string>> GetChangedFilesAsync(string repositoryPath, CancellationToken ct);
    Task CommitAsync(string repositoryPath, string message, CancellationToken ct);
    Task PushAsync(string repositoryPath, string? remote, CancellationToken ct);
}
```

### 6.2 Worktree Service

```csharp
public interface IGitWorktreeService
{
    Task<WorktreeInfo> CreateWorktreeAsync(string repositoryPath, string branchName, CancellationToken ct);
    Task RemoveWorktreeAsync(string worktreePath, CancellationToken ct);
    Task<IReadOnlyList<WorktreeInfo>> ListWorktreesAsync(string repositoryPath, CancellationToken ct);
    string? TranslatePathToWorktree(string mainRepoPath, string worktreePath);
    bool IsWorktreePath(string path);
    string? GetMainRepositoryPath(string worktreePath);
}
```

---

## 7. Prompt Templates

### 7.1 Prompt Registry

```csharp
public interface IPromptRegistry
{
    string? GetTemplate(string templateName);
    string Render(string templateName, object model);
    IReadOnlyList<string> GetAllTemplateNames();
    void RegisterTemplate(string name, string template);
}
```

### 7.2 Template Format

Templates use Handlebars syntax:

```handlebars
---
description: Execute a development step
ragQueries:
  - "project structure"
tools:
  - file.read
  - file.write
---

Execute this step:

## Step
Name: {{stepName}}
Description: {{stepDescription}}

{{#if revisionFeedback}}
## Revision Required
{{revisionFeedback}}
{{/if}}
```

Frontmatter (YAML between `---`) defines:
- `description` - Template purpose
- `ragQueries` - Queries to run for context
- `tools` - Tools available to the agent

---

## 8. Conversation Persistence

### 8.1 Conversation Service

```csharp
public interface IConversationService
{
    Task<Conversation> CreateAsync(string agentId, string? title, string? repositoryPath, CancellationToken ct);
    Task<Conversation?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Message> AddMessageAsync(Guid conversationId, MessageRole role, string content, CancellationToken ct);
    Task<IReadOnlyList<Conversation>> ListAsync(string? agentId, CancellationToken ct);
}
```

### 8.2 RAG Context Persistence

Messages can store their RAG context for auditability:

```csharp
public class MessageRagContext
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public string Query { get; set; }
    public string ContentId { get; set; }
    public int ChunkIndex { get; set; }
    public string ChunkContent { get; set; }
    public double Score { get; set; }
    public string? SourcePath { get; set; }
}
```

---

## 9. Startup Tasks

### 9.1 Startup Task Interface

```csharp
public interface IStartupTask
{
    string Name { get; }
    int Order { get; }  // Lower = earlier
    Task ExecuteAsync(CancellationToken ct);
}
```

### 9.2 Task Runner

Executes all registered `IStartupTask` in order on application startup.

Foundation registers:
- Agent registry initialization
- LLM provider initialization
- Prompt registry initialization
- Tool registry initialization

Developer module registers:
- Code ingestor registration
- Language agent registration
