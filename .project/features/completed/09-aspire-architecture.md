# Aspire Architecture Specification

**Version:** 1.1  
**Status:** ✅ Complete  
**Last Updated:** 2025-12-12

## Overview

Aspire orchestrates **100% local infrastructure**: Ollama, PostgreSQL, RAG - all running on your machine. No cloud required. Think **"Windows Recall, but private and safe."**

## The Local Stack

```text
┌─────────────────────────────────────────────────────────────┐
│                     YOUR MACHINE                             │
│                                                              │
│  ┌─────────────────────────────────────────────────────┐    │
│  │               Aspire Dashboard                       │    │
│  │         http://localhost:15000                       │    │
│  └─────────────────────────────────────────────────────┘    │
│                            │                                 │
│      ┌─────────────────────┼─────────────────────┐          │
│      ▼                     ▼                     ▼          │
│  ┌─────────┐         ┌─────────┐         ┌─────────┐        │
│  │ Ollama  │         │Postgres │         │Aura API │        │
│  │  :11434 │         │  :5432  │         │  :5258  │        │
│  │  (GPU)  │         │  (Data) │         │  (REST) │        │
│  └─────────┘         └─────────┘         └─────────┘        │
│       │                   │                   │              │
│       └───────────────────┴───────────────────┘              │
│                           │                                  │
│                     All Local Data                           │
│              ~/aura/data  ~/aura/rag-index                  │
│                                                              │
│  ✅ No internet required   ✅ No API keys   ✅ Private      │
└─────────────────────────────────────────────────────────────┘
```

## Service Layers

```text
┌─────────────────────────────────────────────────────────────┐
│                        SKU Layer                             │
│  ┌────────────┐  ┌────────────┐  ┌────────────┐             │
│  │ Developer  │  │  Research  │  │  Personal  │             │
│  │    SKU     │  │    SKU     │  │ Assistant  │             │
│  └────────────┘  └────────────┘  └────────────┘             │
├─────────────────────────────────────────────────────────────┤
│                    Vertical Services                         │
│  ┌────────────┐  ┌────────────┐  ┌────────────┐             │
│  │ Developer  │  │  Research  │  │  Personal  │             │
│  │  Agents    │  │   Agents   │  │   Agents   │             │
│  └────────────┘  └────────────┘  └────────────┘             │
├─────────────────────────────────────────────────────────────┤
│                Foundation Services (ALL LOCAL)               │
│  ┌─────────┐  ┌─────────┐  ┌─────────┐  ┌─────────┐         │
│  │   RAG   │  │Database │  │ Ollama  │  │  Tools  │         │
│  │ (Local) │  │ (Local) │  │ (Local) │  │ (Local) │         │
│  └─────────┘  └─────────┘  └─────────┘  └─────────┘         │
├─────────────────────────────────────────────────────────────┤
│              Local Infrastructure (Containers)               │
│  ┌─────────┐  ┌─────────┐                                   │
│  │ Ollama  │  │Postgres │   Persistent volumes in ~/aura/   │
│  │  (GPU)  │  │Container│                                   │
│  └─────────┘  └─────────┘                                   │
└─────────────────────────────────────────────────────────────┘
```

## Project Structure

```text
src/
├── Aura.Foundation/              # Core services (NuGet package)
│   ├── Agents/                   # Agent registry, loading, execution
│   ├── Llm/                      # LLM abstraction
│   │   ├── ILlmProvider.cs
│   │   └── OllamaProvider.cs     # Local GPU/CPU (PRIMARY)
│   ├── Rag/                      # Local RAG pipeline
│   ├── Data/                     # EF Core, local PostgreSQL
│   ├── Tools/                    # Tool registry
│   └── Git/                      # Git worktree service
│
├── Aura.Vertical.Developer/      # Developer vertical (NuGet package)
│   ├── Agents/                   # Coded agents (Roslyn, etc.)
│   ├── Tools/                    # Dev-specific tools
│   └── ServiceExtensions.cs      # DI registration
│
├── Aura.Vertical.Research/       # Research vertical (future)
│   ├── Agents/
│   ├── Tools/
│   └── ServiceExtensions.cs
│
├── Aura.Vertical.Personal/       # Personal assistant (future)
│   ├── Agents/
│   ├── Tools/
│   └── ServiceExtensions.cs
│
├── Aura.AppHost/                 # Aspire AppHost
│   ├── Program.cs                # Local service composition
│   └── appsettings.*.json        # SKU configurations
│
└── Aura.Api/                     # API service
    ├── Program.cs
    └── Endpoints/
```

## Aspire AppHost

The AppHost composes services based on SKU configuration:

```csharp
// src/Aura.AppHost/Program.cs
var builder = DistributedApplication.CreateBuilder(args);

// ========================================
// Infrastructure Layer (always present)
// ========================================
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("aura-data")
    .WithPgAdmin();

var auraDb = postgres.AddDatabase("aura");

// LLM Infrastructure - conditional based on config
IResourceBuilder<ContainerResource>? ollama = null;
if (builder.Configuration.GetValue<bool>("Aura:Ollama:Enabled"))
{
    ollama = builder.AddContainer("ollama", "ollama/ollama")
        .WithVolume("ollama-models", "/root/.ollama")
        .WithEndpoint(11434, 11434, name: "ollama-api");
    
    // GPU support on Windows/Linux
    if (builder.Configuration.GetValue<bool>("Aura:Ollama:UseGpu"))
    {
        ollama = ollama.WithContainerRuntimeArgs("--gpus=all");
    }
}

// ========================================
// Foundation Services
// ========================================
var foundation = builder.AddProject<Projects.Aura_Api>("aura-api")
    .WithReference(auraDb)
    .WaitFor(auraDb);

if (ollama != null)
{
    foundation = foundation
        .WithReference(ollama.GetEndpoint("ollama-api"))
        .WaitFor(ollama);
}

// ========================================
// SKU Configuration
// ========================================
var sku = builder.Configuration.GetValue<string>("Aura:Sku") ?? "developer";

switch (sku.ToLowerInvariant())
{
    case "developer":
        foundation = foundation
            .WithEnvironment("AURA__VERTICALS__DEVELOPER", "true")
            .WithEnvironment("AURA__VERTICALS__RESEARCH", "false")
            .WithEnvironment("AURA__VERTICALS__PERSONAL", "false");
        break;
    
    case "research":
        foundation = foundation
            .WithEnvironment("AURA__VERTICALS__DEVELOPER", "false")
            .WithEnvironment("AURA__VERTICALS__RESEARCH", "true")
            .WithEnvironment("AURA__VERTICALS__PERSONAL", "false");
        break;
    
    case "personal":
        foundation = foundation
            .WithEnvironment("AURA__VERTICALS__DEVELOPER", "false")
            .WithEnvironment("AURA__VERTICALS__RESEARCH", "false")
            .WithEnvironment("AURA__VERTICALS__PERSONAL", "true");
        break;
    
    case "full":
        foundation = foundation
            .WithEnvironment("AURA__VERTICALS__DEVELOPER", "true")
            .WithEnvironment("AURA__VERTICALS__RESEARCH", "true")
            .WithEnvironment("AURA__VERTICALS__PERSONAL", "true");
        break;
}

builder.Build().Run();
```

## API Service Composition

The API service loads verticals based on configuration:

```csharp
// src/Aura.Api/Program.cs
var builder = WebApplication.CreateBuilder(args);

// ========================================
// Foundation Services (always loaded)
// ========================================
builder.AddServiceDefaults();  // Aspire service defaults

builder.Services.AddAuraFoundation(options =>
{
    options.DatabaseConnectionString = builder.Configuration.GetConnectionString("aura");
    options.OllamaEndpoint = builder.Configuration["OLLAMA_API_URL"];
    options.MafEndpoint = builder.Configuration["Aura:Maf:Endpoint"];
    options.DefaultLlmProvider = builder.Configuration["Aura:Llm:DefaultProvider"] ?? "ollama";
});

// ========================================
// Vertical Services (conditional)
// ========================================
if (builder.Configuration.GetValue<bool>("Aura:Verticals:Developer"))
{
    builder.Services.AddDeveloperVertical(options =>
    {
        options.AgentsPath = builder.Configuration["Aura:Developer:AgentsPath"] ?? "./agents";
        options.WorktreesPath = builder.Configuration["Aura:Developer:WorktreesPath"] ?? "./worktrees";
    });
}

if (builder.Configuration.GetValue<bool>("Aura:Verticals:Research"))
{
    builder.Services.AddResearchVertical(options =>
    {
        options.AgentsPath = builder.Configuration["Aura:Research:AgentsPath"] ?? "./research-agents";
        options.PapersIndexPath = builder.Configuration["Aura:Research:PapersIndexPath"];
    });
}

if (builder.Configuration.GetValue<bool>("Aura:Verticals:Personal"))
{
    builder.Services.AddPersonalVertical(options =>
    {
        options.AgentsPath = builder.Configuration["Aura:Personal:AgentsPath"] ?? "./personal-agents";
    });
}

var app = builder.Build();

// ========================================
// Endpoints (foundation always, verticals conditional)
// ========================================
app.MapFoundationEndpoints();  // /api/agents, /api/llm, /api/rag, etc.

if (builder.Configuration.GetValue<bool>("Aura:Verticals:Developer"))
{
    app.MapDeveloperEndpoints();  // /api/workflows, /api/git, etc.
}

if (builder.Configuration.GetValue<bool>("Aura:Verticals:Research"))
{
    app.MapResearchEndpoints();  // /api/papers, /api/synthesis, etc.
}

if (builder.Configuration.GetValue<bool>("Aura:Verticals:Personal"))
{
    app.MapPersonalEndpoints();  // /api/receipts, /api/budget, etc.
}

app.Run();
```

## SKU Configurations

### Developer SKU (appsettings.Developer.json)

```json
{
  "Aura": {
    "Sku": "developer",
    "Ollama": {
      "Enabled": true,
      "UseGpu": true
    },
    "Llm": {
      "DefaultProvider": "ollama",
      "DefaultModel": "qwen2.5-coder:7b"
    },
    "Verticals": {
      "Developer": true,
      "Research": false,
      "Personal": false
    },
    "Developer": {
      "AgentsPath": "./agents",
      "WorktreesPath": "./worktrees"
    }
  }
}
```

### Research SKU (appsettings.Research.json)

```json
{
  "Aura": {
    "Sku": "research",
    "Ollama": {
      "Enabled": true,
      "UseGpu": true
    },
    "Llm": {
      "DefaultProvider": "ollama",
      "DefaultModel": "llama3.2:8b"
    },
    "Verticals": {
      "Developer": false,
      "Research": true,
      "Personal": false
    },
    "Research": {
      "AgentsPath": "./research-agents",
      "PapersIndexPath": "./papers-index"
    }
  }
}
```

### Full SKU (appsettings.Full.json)

All verticals, local LLM only (still private):

```json
{
  "Aura": {
    "Sku": "full",
    "Ollama": {
      "Enabled": true,
      "UseGpu": true
    },
    "Llm": {
      "DefaultProvider": "ollama",
      "DefaultModel": "qwen2.5-coder:7b"
    },
    "Verticals": {
      "Developer": true,
      "Research": true,
      "Personal": true
    }
  }
}
```

### Cloud-Assisted SKU (appsettings.CloudAssisted.json)

**OPTIONAL** - for users who explicitly want cloud LLM:

```json
{
  "Aura": {
    "Sku": "cloud-assisted",
    "Ollama": {
      "Enabled": true,
      "UseGpu": true
    },
    "RemoteLlm": {
      "Enabled": true,
      "Provider": "azure-openai",
      "Endpoint": "https://your-instance.openai.azure.com",
      "ApiKey": "${AZURE_OPENAI_KEY}"
    },
    "Llm": {
      "DefaultProvider": "ollama",
      "PremiumProvider": "azure-openai",
      "PremiumModel": "gpt-4o"
    },
    "Verticals": {
      "Developer": true,
      "Research": true,
      "Personal": true
    }
  }
}
```

**Note:** Cloud-assisted is OPT-IN. Users must explicitly configure it. Local Ollama remains the default.

## LLM Service Abstraction

Local-first with optional remote fallback:

```csharp
// src/Aura.Foundation/Llm/ILlmService.cs
public interface ILlmService
{
    // Primary: Always uses local Ollama
    Task<Result<string, LlmError>> GenerateAsync(
        string prompt,
        LlmOptions? options = null,
        CancellationToken ct = default);
    
    Task<Result<string, LlmError>> ChatAsync(
        IReadOnlyList<ChatMessage> messages,
        LlmOptions? options = null,
        CancellationToken ct = default);
}

public record LlmOptions
{
    // Default: "ollama" (local, private)
    public string Provider { get; init; } = "ollama";
    
    // For users who opt-in to cloud
    public bool AllowRemote { get; init; } = false;
    
    public string? Model { get; init; }
    public float Temperature { get; init; } = 0.7f;
    public int? MaxTokens { get; init; }
}

// src/Aura.Foundation/Llm/LlmService.cs
public class LlmService(
    ILlmProviderRegistry providerRegistry,
    IOptions<LlmConfiguration> config,
    ILogger<LlmService> logger) : ILlmService
{
    public async Task<Result<string, LlmError>> GenerateAsync(
        string prompt,
        LlmOptions? options = null,
        CancellationToken ct = default)
    {
        var providerName = options?.Provider ?? config.Value.DefaultProvider;
        var provider = providerRegistry.GetProvider(providerName);
        
        if (provider is null)
        {
            // Fallback to configured fallback provider
            provider = providerRegistry.GetProvider(config.Value.FallbackProvider);
            if (provider is null)
            {
                return Result.Failure<string, LlmError>(
                    LlmError.ProviderNotFound(providerName));
            }
            logger.LogWarning("Provider {Provider} not available, using fallback {Fallback}",
                providerName, config.Value.FallbackProvider);
        }
        
        var model = options?.Model ?? config.Value.DefaultModel;
        return await provider.GenerateAsync(prompt, model, options, ct);
    }
}
```

## Vertical Service Extensions

Each vertical registers its own services:

```csharp
// src/Aura.Vertical.Developer/ServiceExtensions.cs
public static class DeveloperServiceExtensions
{
    public static IServiceCollection AddDeveloperVertical(
        this IServiceCollection services,
        Action<DeveloperOptions>? configure = null)
    {
        var options = new DeveloperOptions();
        configure?.Invoke(options);
        services.AddSingleton(Options.Create(options));
        
        // Register developer-specific agents
        services.AddSingleton<IAgent, RoslynCodingAgent>();
        services.AddSingleton<IAgent, TestingAgent>();
        
        // Register developer-specific tools
        services.AddSingleton<ITool, RoslynRefactoringTool>();
        services.AddSingleton<ITool, GitWorktreeTool>();
        
        // Register developer-specific services
        services.AddScoped<IWorkflowService, WorkflowService>();
        services.AddScoped<IGitWorktreeService, GitWorktreeService>();
        
        return services;
    }
}

// src/Aura.Vertical.Research/ServiceExtensions.cs
public static class ResearchServiceExtensions
{
    public static IServiceCollection AddResearchVertical(
        this IServiceCollection services,
        Action<ResearchOptions>? configure = null)
    {
        var options = new ResearchOptions();
        configure?.Invoke(options);
        services.AddSingleton(Options.Create(options));
        
        // Register research-specific agents
        services.AddSingleton<IAgent, PaperIndexerAgent>();
        services.AddSingleton<IAgent, SynthesisAgent>();
        services.AddSingleton<IAgent, CitationAgent>();
        
        // Register research-specific tools
        services.AddSingleton<ITool, PdfParserTool>();
        services.AddSingleton<ITool, BibtexTool>();
        
        return services;
    }
}
```

## Database Schema

Foundation schema is shared; verticals add their own tables:

```csharp
// src/Aura.Foundation/Data/AuraDbContext.cs
public class AuraDbContext : DbContext
{
    // Foundation entities (always present)
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<RagDocument> RagDocuments => Set<RagDocument>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Foundation configuration
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuraDbContext).Assembly);
        
        // Vertical configurations are applied via extension methods
    }
}

// src/Aura.Vertical.Developer/Data/DeveloperDbContextExtensions.cs
public static class DeveloperDbContextExtensions
{
    public static ModelBuilder ApplyDeveloperConfiguration(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Workflow>(entity =>
        {
            entity.ToTable("Workflows");
            entity.HasKey(e => e.Id);
            // ...
        });
        
        modelBuilder.Entity<WorkflowStep>(entity =>
        {
            entity.ToTable("WorkflowSteps");
            // ...
        });
        
        return modelBuilder;
    }
}
```

## Running Different SKUs

```bash
# Developer SKU (default)
dotnet run --project src/Aura.AppHost

# Research SKU
dotnet run --project src/Aura.AppHost -- --Aura:Sku=research

# Full SKU
dotnet run --project src/Aura.AppHost -- --Aura:Sku=full

# Custom configuration file
dotnet run --project src/Aura.AppHost -- --configuration Research
```

## Aspire Dashboard

The Aspire dashboard shows all services regardless of SKU:

```text
┌─────────────────────────────────────────────────────────────┐
│                    Aspire Dashboard                          │
├─────────────────────────────────────────────────────────────┤
│ Services:                                                    │
│   ✅ postgres        Running   localhost:5432               │
│   ✅ ollama          Running   localhost:11434              │
│   ✅ aura-api        Running   localhost:5258               │
│                                                              │
│ Endpoints:                                                   │
│   🌐 aura-api        http://localhost:5258                  │
│   🗄️ postgres        postgres://localhost:5432/aura         │
│   🤖 ollama          http://localhost:11434                  │
│                                                              │
│ Logs: [aura-api]                                            │
│   INFO  Loading Developer vertical...                        │
│   INFO  Registered 6 agents                                  │
│   INFO  Registered 4 tools                                   │
│   INFO  API ready at http://localhost:5258                   │
└─────────────────────────────────────────────────────────────┘
```

## Benefits of This Architecture

| Benefit | Description |
|---------|-------------|
| **Clean separation** | Foundation vs verticals are distinct |
| **Easy SKU management** | Just config, no code changes |
| **Testable in isolation** | Each vertical can be tested independently |
| **Deployable variations** | Ship different SKUs to different users |
| **Future-proof** | Add new verticals without touching foundation |
| **Cross-platform** | Aspire handles platform differences |
| **Observable** | Dashboard shows everything |
| **Composable** | Mix and match verticals as needed |

## Migration Path

1. **Phase 1**: Build `Aura.Foundation` with core services
2. **Phase 2**: Build `Aura.Vertical.Developer` (current functionality)
3. **Phase 3**: Create `Aura.AppHost` with SKU support
4. **Phase 4**: Delete old 17 projects
5. **Future**: Add Research/Personal verticals as separate packages
