# Foundation Layer Specification

**Version:** 1.1  
**Status:** Draft  
**Last Updated:** 2025-11-26

## Overview

The Aura Foundation is a **local-first, privacy-safe AI knowledge infrastructure**. Think of it as **"Windows Recall, but local and safe"** - your data never leaves your machine.

## The Privacy Promise

```text
┌─────────────────────────────────────────────────────────────┐
│                    YOUR DATA STAYS LOCAL                     │
│                                                              │
│  ✅ LLM runs locally (Ollama on GPU/CPU)                    │
│  ✅ Database is local (PostgreSQL on your machine)          │
│  ✅ RAG index is local (embeddings stored locally)          │
│  ✅ Files stay on your filesystem                           │
│  ✅ Works offline - no internet required                    │
│                                                              │
│  ❌ No cloud uploads                                        │
│  ❌ No telemetry                                            │
│  ❌ No API keys required                                    │
│  ❌ No external dependencies for core functionality         │
│                                                              │
│  Optional cloud integration (explicit opt-in only):         │
│  - Remote LLM providers (Azure OpenAI, Anthropic)           │
│  - GitHub/Azure DevOps sync                                 │
└─────────────────────────────────────────────────────────────┘
```

## What It Does

The Foundation is a **local AI operating system**:

- **Index anything** - code, documents, receipts, papers, notes
- **Query naturally** - ask questions about your indexed content
- **Execute agents** - hot-reloadable capabilities for any domain
- **Persist state** - local PostgreSQL for structured data
- **Run tools** - file operations, shell commands, git

Vertical applications (Developer, Research, Personal) are just agent collections that use these foundation services.

## Design Goals

### Local-First, Always

Every component has a local implementation:

| Component | Local Implementation |
|-----------|---------------------|
| **LLM** | Ollama (GPU/CPU) |
| **Embeddings** | Ollama (nomic-embed-text) |
| **Vector Store** | pgvector in local PostgreSQL |
| **Database** | PostgreSQL (Docker or native) |
| **File Access** | Direct filesystem |
| **Git** | Local repos and worktrees |

### Cross-Platform

```text
Foundation API
     │
     ▼
┌─────────────────────────────────────┐
│          .NET Abstractions          │
│  - IFileSystem (System.IO.Abstractions)
│  - IProcessRunner (cross-platform)
│  - IServiceHost (console/service/daemon)
└─────────────────────────────────────┘
     │
     ▼
┌─────────────────────────────────────┐
│        Platform Adapters            │
│  Windows │   macOS   │    Linux     │
└─────────────────────────────────────┘
```

No platform-specific code in the core library. All platform variations handled via:

1. **Dependency injection** - inject platform-specific implementations
2. **Conditional compilation** - only in host/deployment projects
3. **Optional features** - tray icon, native notifications

### Extensibility Model

```text
┌─────────────────────────────────────────────────────────────┐
│                     Vertical: Developer                      │
│  agents/coding-agent.md                                      │
│  agents/testing-agent.md                                     │
│  agents/documentation-agent.md                               │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                     Foundation APIs                          │
│                                                              │
│  IAgentRegistry.GetByCapability("code-generation")          │
│  ILlmProvider.ChatAsync(messages)                           │
│  IRagService.IndexAsync(content)                            │
│  IRagService.QueryAsync(question)                           │
│  DbContext.Workflows.AddAsync(workflow)                     │
│  IGitWorktreeService.CreateAsync(repo, branch)              │
└─────────────────────────────────────────────────────────────┘
```

Any vertical can use any foundation service. Verticals are just agent collections.

## Foundation Services

### 1. Agent Registry

Manages agent discovery, loading, and execution.

```csharp
public interface IAgentRegistry
{
    // Discovery
    IReadOnlyList<AgentDefinition> GetAllAgents();
    AgentDefinition? GetAgent(string agentId);
    IReadOnlyList<AgentDefinition> GetByCapability(string capability);
    
    // Execution
    Task<AgentResult> ExecuteAsync(
        string agentId, 
        AgentContext context, 
        CancellationToken ct = default);
    
    // Hot-reload
    void RegisterAgent(AgentDefinition agent);
    void UnregisterAgent(string agentId);
    event EventHandler<AgentChangedEventArgs>? AgentChanged;
}
```

Agents can be:

- **Markdown-based** - loaded from `agents/*.md` files
- **Code-based** - registered via DI (`IAgent` implementations)
- **Remote** - HTTP endpoints (future)

### 2. LLM Providers

Abstracts LLM communication for cross-provider compatibility.

```csharp
public interface ILlmProvider
{
    string ProviderId { get; }
    
    Task<Result<string, LlmError>> GenerateAsync(
        string prompt,
        LlmOptions? options = null,
        CancellationToken ct = default);
    
    Task<Result<string, LlmError>> ChatAsync(
        IReadOnlyList<ChatMessage> messages,
        LlmOptions? options = null,
        CancellationToken ct = default);
    
    Task<bool> IsModelAvailableAsync(string model, CancellationToken ct = default);
    Task<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken ct = default);
}
```

**Local-First Implementation:**

| Provider | Location | Status | Models |
|----------|----------|--------|--------|
| **Ollama** | 🏠 Local (GPU/CPU) | ✅ Primary | qwen2.5-coder, llama3.2, etc. |
| **Azure OpenAI** | ☁️ Remote (opt-in) | 🔮 Future | gpt-4o, o1-preview |
| **Anthropic** | ☁️ Remote (opt-in) | 🔮 Future | claude-3.5-sonnet |

**Ollama is the PRIMARY and DEFAULT provider.** Remote providers are optional add-ons for users who explicitly configure them.

### 3. RAG Pipeline (Local)

Indexes and queries content with semantic search. **All data stored locally.**

```csharp
public interface IRagService
{
    // Indexing - stores in local vector DB
    Task IndexAsync(RagContent content, CancellationToken ct = default);
    Task IndexDirectoryAsync(string path, RagIndexOptions options, CancellationToken ct = default);
    Task RemoveAsync(string contentId, CancellationToken ct = default);
    
    // Querying - uses local embeddings (Ollama)
    Task<IReadOnlyList<RagResult>> QueryAsync(
        string question,
        RagQueryOptions? options = null,
        CancellationToken ct = default);
    
    // Management
    Task<RagStats> GetStatsAsync(CancellationToken ct = default);
    Task RebuildIndexAsync(CancellationToken ct = default);
}

public record RagContent(
    string ContentId,
    string Text,
    RagContentType Type,
    Dictionary<string, string> Metadata);

public enum RagContentType
{
    Code,
    Documentation,
    Markdown,
    PlainText,
    Pdf,
    Receipt,      // For financial vertical
    Paper,        // For research vertical
    Custom
}
```

The RAG service is content-agnostic - it indexes whatever you give it. The vertical determines what content types are relevant.

### 4. Database

PostgreSQL via EF Core, with a schema that supports any vertical.

```csharp
// Foundation entities
public record Workflow { ... }       // Generic workflow
public record WorkflowStep { ... }   // Generic step
public record Conversation { ... }   // Chat history
public record Agent { ... }          // Agent metadata cache

// Extension pattern for verticals
public record WorkflowMetadata
{
    public Guid WorkflowId { get; init; }
    public string VerticalType { get; init; }  // "developer", "research", etc.
    public JsonDocument Data { get; init; }     // Vertical-specific data
}
```

Verticals can:

1. Use the base entities as-is
2. Add metadata via the extension pattern
3. Define additional entities (via EF Core model extensions)

### 5. Tool Registry

Manages external tools that agents can invoke.

```csharp
public interface IToolRegistry
{
    IReadOnlyList<ToolDefinition> GetAllTools();
    ToolDefinition? GetTool(string toolId);
    
    Task<ToolResult> ExecuteAsync(
        string toolId,
        ToolInput input,
        CancellationToken ct = default);
    
    void RegisterTool(ToolDefinition tool);
}

public record ToolDefinition(
    string ToolId,
    string Name,
    string Description,
    JsonSchema InputSchema,
    Func<ToolInput, CancellationToken, Task<ToolResult>> Handler);
```

Built-in tools:

| Tool | Description | Platform |
|------|-------------|----------|
| `file.read` | Read file contents | All |
| `file.write` | Write file contents | All |
| `file.list` | List directory contents | All |
| `shell.execute` | Run shell command | All |
| `http.request` | Make HTTP request | All |
| `git.status` | Get git status | All |
| `git.commit` | Create commit | All |

### 6. Git Worktree Service

Manages git worktrees for concurrent workflows.

```csharp
public interface IGitWorktreeService
{
    Task<Result<WorktreeInfo, GitError>> CreateAsync(
        string repoPath,
        string branchName,
        CancellationToken ct = default);
    
    Task<Result<Unit, GitError>> RemoveAsync(
        string worktreePath,
        CancellationToken ct = default);
    
    Task<IReadOnlyList<WorktreeInfo>> ListAsync(
        string repoPath,
        CancellationToken ct = default);
}
```

## Vertical Applications

A vertical is a collection of:

1. **Agent definitions** - markdown files in a folder
2. **Optional tools** - additional tool registrations
3. **Optional entities** - additional database tables
4. **Optional UI** - extension panels/commands

### Developer Vertical (Current)

```text
verticals/developer/
├── agents/
│   ├── coding-agent.md
│   ├── testing-agent.md
│   ├── documentation-agent.md
│   ├── business-analyst-agent.md
│   ├── issue-enrichment-agent.md
│   └── pr-health-monitor-agent.md
├── tools/
│   └── roslyn-tools.cs         # Roslyn-based code tools
└── extension/
    └── developer-panels.ts      # VS Code UI
```

### Research Vertical (Future)

```text
verticals/research/
├── agents/
│   ├── paper-indexer-agent.md   # Indexes PDFs/papers
│   ├── synthesis-agent.md       # Summarizes multiple papers
│   ├── citation-agent.md        # Manages citations
│   └── literature-review-agent.md
├── tools/
│   └── pdf-tools.cs             # PDF parsing
└── extension/
    └── research-panels.ts       # Research UI
```

### Financial Vertical (Future)

```text
verticals/financial/
├── agents/
│   ├── receipt-parser-agent.md  # OCR + parsing
│   ├── expense-tracker-agent.md # Categorization
│   ├── budget-agent.md          # Analysis
│   └── report-agent.md          # Generate reports
├── tools/
│   └── ocr-tools.cs             # OCR integration
└── extension/
    └── finance-panels.ts        # Finance UI
```

## Cross-Platform Considerations

### File Paths

```csharp
// Always use Path.Combine, never hardcode separators
var agentPath = Path.Combine(basePath, "agents", "coding-agent.md");

// Use System.IO.Abstractions for testability
public class AgentLoader(IFileSystem fileSystem)
{
    public async Task<AgentDefinition> LoadAsync(string path)
    {
        var content = await fileSystem.File.ReadAllTextAsync(path);
        return Parse(content);
    }
}
```

### Process Execution

```csharp
// Use cross-platform process runner
public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(
        string command,
        string[] args,
        ProcessOptions? options = null,
        CancellationToken ct = default);
}

// Shell detection
public static class ShellHelper
{
    public static string GetDefaultShell() => 
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "cmd.exe"
            : "/bin/bash";
}
```

### Service Hosting

```csharp
// Generic host pattern - works everywhere
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddAuraFoundation();
builder.Services.AddAuraDeveloperVertical();

var host = builder.Build();

// On Windows, can run as Windows Service
// On Linux, can run as systemd service
// On macOS, can run as launchd daemon
// Or just run as console app anywhere
await host.RunAsync();
```

## Configuration

```json
{
  "Aura": {
    "Foundation": {
      "DatabaseConnectionString": "Host=localhost;Database=aura",
      "LlmProvider": "ollama",
      "OllamaEndpoint": "http://localhost:11434",
      "RagIndexPath": "./rag-index"
    },
    "Verticals": {
      "Developer": {
        "Enabled": true,
        "AgentsPath": "./agents",
        "WorktreesPath": "./worktrees"
      },
      "Research": {
        "Enabled": false,
        "AgentsPath": "./research-agents"
      }
    }
  }
}
```

## API Namespacing

Foundation APIs are vertical-agnostic:

```text
/api/agents           # All agents from all verticals
/api/llm/chat         # Direct LLM chat
/api/rag/query        # RAG queries
/api/workflows        # All workflows

# Vertical-specific (optional filtering)
/api/agents?vertical=developer
/api/workflows?vertical=research
```

## Future Extensibility

### Plugin System

```csharp
// Verticals can be distributed as NuGet packages
public interface IAuraVertical
{
    string VerticalId { get; }
    string Name { get; }
    
    void ConfigureServices(IServiceCollection services);
    void ConfigureAgents(IAgentRegistry registry);
    void ConfigureTools(IToolRegistry registry);
}

// Registration
builder.Services.AddAuraVertical<DeveloperVertical>();
builder.Services.AddAuraVertical<ResearchVertical>();
```

### Agent Marketplace (Future)

```text
aura install agent coding-agent-v2
aura install vertical research-assistant
```

Agents as downloadable packages, versioned and shareable.

## Summary

The Foundation layer provides:

| Service | Purpose |
|---------|---------|
| **Agent Registry** | Discover, load, execute agents |
| **LLM Providers** | Abstract LLM communication |
| **RAG Pipeline** | Index and query content |
| **Database** | Persist state (PostgreSQL) |
| **Tool Registry** | External tool execution |
| **Git Worktrees** | Concurrent workflow isolation |

Verticals provide:

| Component | Purpose |
|-----------|---------|
| **Agents** | Domain-specific capabilities |
| **Tools** | Domain-specific tooling |
| **Entities** | Domain-specific data |
| **UI** | Domain-specific interface |

This separation means:

1. Foundation can evolve independently
2. Verticals can be added/removed without changing core
3. Cross-platform by design
4. Same foundation serves many use cases
