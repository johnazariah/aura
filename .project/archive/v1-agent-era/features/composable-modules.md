# Composable Modules

**Status:** ✅ Complete  
**Completed:** 2025-11-28  
**Last Updated:** 2025-12-12

## Overview

Aura is built from **independent, composable modules**. There are no fixed SKUs - users configure which modules to load. A research assistant doesn't need coding agents. A personal finance tracker doesn't need git worktrees.

## The Composition Model

```text
┌─────────────────────────────────────────────────────────────┐
│                    User Configuration                        │
│                                                              │
│  "I want: research + personal, but not developer"           │
│                                                              │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    Module Loader                             │
│                                                              │
│  foreach (module in config.EnabledModules)                  │
│      services.AddModule(module);                            │
│                                                              │
└─────────────────────────────────────────────────────────────┘
                              │
          ┌───────────────────┼───────────────────┐
          ▼                   ▼                   ▼
    ┌──────────┐        ┌──────────┐        ┌──────────┐
    │ Research │        │ Personal │        │ Developer│
    │  Module  │        │  Module  │        │  Module  │
    │    ✅    │        │    ✅    │        │    ❌    │
    └──────────┘        └──────────┘        └──────────┘
          │                   │
          ▼                   ▼
┌─────────────────────────────────────────────────────────────┐
│                    Foundation (Always)                       │
│  Agents │ LLM (Ollama) │ RAG │ Database │ Tools             │
└─────────────────────────────────────────────────────────────┘
```

## Module Contract

Every module implements a simple interface:

```csharp
public interface IAuraModule
{
    /// <summary>
    /// Unique identifier for this module (e.g., "developer", "research")
    /// </summary>
    string ModuleId { get; }
    
    /// <summary>
    /// Human-readable name
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Description of what this module provides
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// Other modules this depends on (empty = only foundation)
    /// </summary>
    IReadOnlyList<string> Dependencies { get; }
    
    /// <summary>
    /// Register services with DI container
    /// </summary>
    void ConfigureServices(IServiceCollection services, IConfiguration config);
    
    /// <summary>
    /// Register API endpoints
    /// </summary>
    void MapEndpoints(IEndpointRouteBuilder endpoints);
    
    /// <summary>
    /// Register agents from this module
    /// </summary>
    void RegisterAgents(IAgentRegistry registry, IConfiguration config);
}
```

## Module Independence

**Critical rule: Modules must not depend on other modules.**

Each module depends ONLY on the Foundation:

```text
                    ┌─────────────────┐
                    │   Foundation    │
                    │  (always loaded)│
                    └─────────────────┘
                            ▲
          ┌─────────────────┼─────────────────┐
          │                 │                 │
    ┌─────┴─────┐     ┌─────┴─────┐     ┌─────┴─────┐
    │ Developer │     │  Research │     │  Personal │
    │  Module   │     │   Module  │     │   Module  │
    └───────────┘     └───────────┘     └───────────┘
    
    NO horizontal dependencies between modules!
```

## Foundation Services (Always Available)

Every module can use these foundation services:

```csharp
// Agent execution
IAgentRegistry agentRegistry;
await agentRegistry.ExecuteAsync("my-agent", context);

// LLM (local Ollama)
ILlmService llm;
await llm.ChatAsync(messages);

// RAG (local index)
IRagService rag;
await rag.IndexAsync(content);
await rag.QueryAsync("what is X?");

// Database (local PostgreSQL)
AuraDbContext db;
await db.SaveChangesAsync();

// Tools
IToolRegistry tools;
await tools.ExecuteAsync("file.read", input);
```

## Module Examples

### Developer Module

```csharp
public class DeveloperModule : IAuraModule
{
    public string ModuleId => "developer";
    public string Name => "Developer Workflow";
    public string Description => "Code automation, testing, git worktrees";
    public IReadOnlyList<string> Dependencies => [];  // Only foundation
    
    public void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        // Developer-specific services
        services.AddScoped<IWorkflowService, WorkflowService>();
        services.AddScoped<IGitWorktreeService, GitWorktreeService>();
        
        // Developer-specific tools
        services.AddSingleton<ITool, RoslynRefactoringTool>();
        services.AddSingleton<ITool, GitCommitTool>();
    }
    
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/dev");
        
        group.MapGet("/workflows", GetWorkflows);
        group.MapPost("/workflows", CreateWorkflow);
        group.MapPost("/workflows/{id}/enrich", EnrichWorkflow);
        group.MapPost("/workflows/{id}/plan", PlanWorkflow);
        group.MapPost("/workflows/{id}/steps/{stepId}/execute", ExecuteStep);
        group.MapGet("/worktrees", ListWorktrees);
        group.MapPost("/worktrees", CreateWorktree);
    }
    
    public void RegisterAgents(IAgentRegistry registry, IConfiguration config)
    {
        var agentsPath = config["Aura:Modules:Developer:AgentsPath"] ?? "./agents/developer";
        registry.LoadAgentsFromDirectory(agentsPath);
        
        // Register coded agents
        registry.RegisterAgent(new RoslynCodingAgent());
    }
}
```

### Research Module

```csharp
public class ResearchModule : IAuraModule
{
    public string ModuleId => "research";
    public string Name => "Research Assistant";
    public string Description => "Paper management, synthesis, citations";
    public IReadOnlyList<string> Dependencies => [];  // Only foundation
    
    public void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        // Research-specific services
        services.AddScoped<IPaperService, PaperService>();
        services.AddScoped<ICitationService, CitationService>();
        
        // Research-specific tools
        services.AddSingleton<ITool, PdfParserTool>();
        services.AddSingleton<ITool, BibtexTool>();
    }
    
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/research");
        
        group.MapGet("/papers", ListPapers);
        group.MapPost("/papers/import", ImportPaper);
        group.MapPost("/papers/{id}/index", IndexPaper);
        group.MapPost("/synthesis", SynthesizePapers);
        group.MapGet("/citations", ListCitations);
    }
    
    public void RegisterAgents(IAgentRegistry registry, IConfiguration config)
    {
        var agentsPath = config["Aura:Modules:Research:AgentsPath"] ?? "./agents/research";
        registry.LoadAgentsFromDirectory(agentsPath);
    }
}
```

### Personal Module

```csharp
public class PersonalModule : IAuraModule
{
    public string ModuleId => "personal";
    public string Name => "Personal Assistant";
    public string Description => "Financial tracking, receipts, general knowledge";
    public IReadOnlyList<string> Dependencies => [];  // Only foundation
    
    public void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        // Personal-specific services
        services.AddScoped<IReceiptService, ReceiptService>();
        services.AddScoped<IBudgetService, BudgetService>();
        
        // Personal-specific tools
        services.AddSingleton<ITool, OcrTool>();
    }
    
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/personal");
        
        group.MapGet("/receipts", ListReceipts);
        group.MapPost("/receipts/scan", ScanReceipt);
        group.MapGet("/budget", GetBudget);
        group.MapPost("/query", QueryPersonalKnowledge);
    }
    
    public void RegisterAgents(IAgentRegistry registry, IConfiguration config)
    {
        var agentsPath = config["Aura:Modules:Personal:AgentsPath"] ?? "./agents/personal";
        registry.LoadAgentsFromDirectory(agentsPath);
    }
}
```

## Configuration

Users specify which modules to load:

```json
{
  "Aura": {
    "Modules": {
      "Enabled": ["research", "personal"],
      
      "Research": {
        "AgentsPath": "./agents/research",
        "PapersDirectory": "~/Documents/Papers"
      },
      "Personal": {
        "AgentsPath": "./agents/personal",
        "ReceiptsDirectory": "~/Documents/Receipts"
      }
    }
  }
}
```

Or via command line:

```bash
# Research + Personal (no developer)
dotnet run -- --Aura:Modules:Enabled:0=research --Aura:Modules:Enabled:1=personal

# Just developer
dotnet run -- --Aura:Modules:Enabled:0=developer

# Everything
dotnet run -- --Aura:Modules:Enabled:0=developer --Aura:Modules:Enabled:1=research --Aura:Modules:Enabled:2=personal
```

## Module Discovery

Modules are discovered via:

1. **Assembly scanning** - Find all `IAuraModule` implementations
2. **NuGet packages** - `Aura.Module.Developer`, `Aura.Module.Research`, etc.
3. **Plugin directory** - Drop DLLs in `./modules/`

```csharp
// src/Aura.Api/Program.cs
var builder = WebApplication.CreateBuilder(args);

// Foundation is always loaded
builder.Services.AddAuraFoundation();

// Discover and load enabled modules
var moduleLoader = new ModuleLoader();
var enabledModules = builder.Configuration
    .GetSection("Aura:Modules:Enabled")
    .Get<string[]>() ?? ["developer"];  // Default to developer

foreach (var moduleId in enabledModules)
{
    var module = moduleLoader.GetModule(moduleId);
    if (module is null)
    {
        throw new InvalidOperationException($"Module '{moduleId}' not found");
    }
    
    module.ConfigureServices(builder.Services, builder.Configuration);
}

var app = builder.Build();

// Map foundation endpoints
app.MapFoundationEndpoints();

// Map module endpoints
foreach (var moduleId in enabledModules)
{
    var module = moduleLoader.GetModule(moduleId);
    module!.MapEndpoints(app);
}

// Register module agents
var agentRegistry = app.Services.GetRequiredService<IAgentRegistry>();
foreach (var moduleId in enabledModules)
{
    var module = moduleLoader.GetModule(moduleId);
    module!.RegisterAgents(agentRegistry, builder.Configuration);
}

app.Run();
```

## Project Structure

```text
src/
├── Aura.Foundation/              # Core services (always loaded)
│   ├── Agents/
│   ├── Llm/
│   ├── Rag/
│   ├── Data/
│   └── Tools/
│
├── Aura.Module.Developer/        # Developer module (optional)
│   ├── DeveloperModule.cs        # IAuraModule implementation
│   ├── Services/
│   ├── Tools/
│   ├── Agents/
│   └── Endpoints/
│
├── Aura.Module.Research/         # Research module (optional)
│   ├── ResearchModule.cs
│   ├── Services/
│   ├── Tools/
│   └── Endpoints/
│
├── Aura.Module.Personal/         # Personal module (optional)
│   ├── PersonalModule.cs
│   ├── Services/
│   ├── Tools/
│   └── Endpoints/
│
├── Aura.Api/                     # API host
│   └── Program.cs                # Module loader
│
└── Aura.AppHost/                 # Aspire host
    └── Program.cs                # Infrastructure orchestration
```

## Module NuGet Packages

Modules can be distributed as NuGet packages:

```xml
<!-- Aura.Module.Developer.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <PackageId>Aura.Module.Developer</PackageId>
    <Version>1.0.0</Version>
    <Description>Developer workflow module for Aura</Description>
  </PropertyGroup>
  
  <ItemGroup>
    <ProjectReference Include="..\Aura.Foundation\Aura.Foundation.csproj" />
  </ItemGroup>
</Project>
```

Users install modules they need:

```bash
# Install just the modules you need
dotnet add package Aura.Foundation
dotnet add package Aura.Module.Research
dotnet add package Aura.Module.Personal
# Note: NOT installing Aura.Module.Developer
```

## Aspire AppHost

Aspire just orchestrates infrastructure - modules are loaded by the API:

```csharp
// src/Aura.AppHost/Program.cs
var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure (always needed)
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("aura-data");

var auraDb = postgres.AddDatabase("aura");

var ollama = builder.AddContainer("ollama", "ollama/ollama")
    .WithVolume("ollama-models", "/root/.ollama")
    .WithEndpoint(11434, 11434, name: "ollama-api");

if (builder.Configuration.GetValue<bool>("Aura:Ollama:UseGpu"))
{
    ollama = ollama.WithContainerRuntimeArgs("--gpus=all");
}

// API service (loads modules based on config)
var api = builder.AddProject<Projects.Aura_Api>("aura-api")
    .WithReference(auraDb)
    .WithReference(ollama.GetEndpoint("ollama-api"))
    .WaitFor(auraDb)
    .WaitFor(ollama);

// Pass module configuration through
var enabledModules = builder.Configuration
    .GetSection("Aura:Modules:Enabled")
    .Get<string[]>() ?? [];

for (int i = 0; i < enabledModules.Length; i++)
{
    api = api.WithEnvironment($"Aura__Modules__Enabled__{i}", enabledModules[i]);
}

builder.Build().Run();
```

## Benefits

| Benefit | Description |
|---------|-------------|
| **True decoupling** | Research module has zero knowledge of coding agents |
| **Smaller deployments** | Only ship what users need |
| **Independent development** | Teams can work on modules in isolation |
| **Easy extension** | Third parties can create modules |
| **No code changes** | Add modules via config, not code |
| **Future-proof** | New modules don't affect existing ones |

## Agent Organization

Each module has its own agent folder:

```text
agents/
├── developer/                    # Developer module agents
│   ├── coding-agent.md
│   ├── testing-agent.md
│   ├── documentation-agent.md
│   └── business-analyst-agent.md
│
├── research/                     # Research module agents
│   ├── paper-indexer-agent.md
│   ├── synthesis-agent.md
│   └── citation-agent.md
│
└── personal/                     # Personal module agents
    ├── receipt-parser-agent.md
    ├── expense-tracker-agent.md
    └── general-assistant-agent.md
```

When you enable `research`, only `agents/research/*.md` gets loaded.

## Database Isolation

Modules can define their own tables without affecting others:

```csharp
// Foundation schema (always)
public class AuraDbContext : DbContext
{
    public DbSet<Agent> Agents { get; set; }
    public DbSet<Conversation> Conversations { get; set; }
    public DbSet<RagDocument> RagDocuments { get; set; }
}

// Developer module extends it
public class DeveloperDbContext : AuraDbContext
{
    public DbSet<Workflow> Workflows { get; set; }
    public DbSet<WorkflowStep> WorkflowSteps { get; set; }
}

// Research module extends it differently
public class ResearchDbContext : AuraDbContext
{
    public DbSet<Paper> Papers { get; set; }
    public DbSet<Citation> Citations { get; set; }
}
```

EF Core migrations are per-module, so loading Research doesn't create Workflow tables.

## Summary

**No fixed SKUs. Just composable modules.**

- Foundation is always there (LLM, RAG, DB, Agents, Tools)
- Modules are independent and optional
- Users enable what they need via config
- Modules can be distributed as NuGet packages
- Zero coupling between modules
