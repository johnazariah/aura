# Data Model

**Status:** ✅ Complete  
**Completed:** 2025-11-27  
**Last Updated:** 2025-12-12

## Overview

Aura uses PostgreSQL for persistence, accessed via Entity Framework Core. The schema is intentionally minimal, storing only what's needed for workflow state and audit.

## Entity Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              DATA MODEL                                      │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌─────────────────┐         ┌─────────────────┐                           │
│  │    Workflow     │────────►│  WorkflowStep   │                           │
│  ├─────────────────┤   1:N   ├─────────────────┤                           │
│  │ Id              │         │ Id              │                           │
│  │ WorkItemId      │         │ WorkflowId (FK) │                           │
│  │ WorkItemTitle   │         │ Order           │                           │
│  │ Status          │         │ Name            │                           │
│  │ WorkspacePath   │         │ Capability      │                           │
│  │ GitBranch       │         │ Status          │                           │
│  │ EnrichedContext │         │ AssignedAgentId │                           │
│  │ CreatedAt       │         │ Input           │                           │
│  │ UpdatedAt       │         │ Output          │                           │
│  └─────────────────┘         │ Error           │                           │
│          │                   │ StartedAt       │                           │
│          │                   │ CompletedAt     │                           │
│          │                   └─────────────────┘                           │
│          │                                                                  │
│          │ 1:N    ┌─────────────────┐                                      │
│          └───────►│ Conversation    │                                      │
│                   ├─────────────────┤                                      │
│                   │ Id              │                                      │
│                   │ WorkflowId (FK) │                                      │
│                   │ Role            │                                      │
│                   │ Content         │                                      │
│                   │ CreatedAt       │                                      │
│                   └─────────────────┘                                      │
│                                                                              │
│  ┌─────────────────┐                                                        │
│  │ AgentExecution  │  (Audit log - optional)                               │
│  ├─────────────────┤                                                        │
│  │ Id              │                                                        │
│  │ AgentId         │                                                        │
│  │ StepId (FK?)    │                                                        │
│  │ Provider        │                                                        │
│  │ Model           │                                                        │
│  │ PromptTokens    │                                                        │
│  │ ResponseTokens  │                                                        │
│  │ DurationMs      │                                                        │
│  │ Success         │                                                        │
│  │ CreatedAt       │                                                        │
│  └─────────────────┘                                                        │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Entities

### Workflow

The root entity representing a unit of work.

```csharp
public class Workflow
{
    public Guid Id { get; set; }
    
    // Work item identity (from GitHub, ADO, or manual)
    public required string WorkItemId { get; set; }      // e.g., "github:owner/repo#123"
    public required string WorkItemTitle { get; set; }
    public string? WorkItemDescription { get; set; }
    public string? WorkItemUrl { get; set; }
    
    // Status
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Created;
    
    // Workspace
    public string? WorkspacePath { get; set; }           // e.g., "/workspaces/repo-wt-123"
    public string? GitBranch { get; set; }               // e.g., "feature/issue-123"
    
    // Enriched context (RAG output)
    public string? EnrichedContext { get; set; }         // JSON blob
    
    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public List<WorkflowStep> Steps { get; set; } = [];
    public List<Conversation> Conversations { get; set; } = [];
}

public enum WorkflowStatus
{
    Created,        // Just created
    Enriching,      // RAG ingestion in progress
    Enriched,       // Context ready
    Planning,       // Creating execution plan
    Planned,        // Steps defined
    Executing,      // Steps being executed
    Completed,      // All steps done
    Failed,         // Unrecoverable error
    Cancelled       // User cancelled
}
```

### WorkflowStep

A single step in the execution plan.

```csharp
public class WorkflowStep
{
    public Guid Id { get; set; }
    
    // Parent
    public Guid WorkflowId { get; set; }
    public Workflow Workflow { get; set; } = null!;
    
    // Step definition
    public int Order { get; set; }                       // Execution order
    public required string Name { get; set; }            // e.g., "Implement UserService"
    public required string Capability { get; set; }      // e.g., "csharp-coding"
    public string? Description { get; set; }
    
    // Status
    public StepStatus Status { get; set; } = StepStatus.Pending;
    
    // Execution
    public string? AssignedAgentId { get; set; }         // Agent that ran this
    public string? Input { get; set; }                   // JSON - context for agent
    public string? Output { get; set; }                  // JSON - agent result
    public string? Error { get; set; }
    public int Attempts { get; set; } = 0;
    
    // Timestamps
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public enum StepStatus
{
    Pending,        // Not started
    Running,        // In progress
    Completed,      // Success
    Failed,         // Error (may retry)
    Skipped         // Intentionally skipped
}
```

### Conversation

Chat messages for augmented development.

```csharp
public class Conversation
{
    public Guid Id { get; set; }
    
    // Parent
    public Guid WorkflowId { get; set; }
    public Workflow Workflow { get; set; } = null!;
    
    // Message
    public required string Role { get; set; }            // "user" or "assistant"
    public required string Content { get; set; }
    
    // Metadata
    public string? AgentId { get; set; }                 // If assistant, which agent
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

### AgentExecution (Audit)

Optional audit log for tracking LLM usage.

```csharp
public class AgentExecution
{
    public Guid Id { get; set; }
    
    // What
    public required string AgentId { get; set; }
    public Guid? StepId { get; set; }                    // Optional link to step
    
    // Provider details
    public required string Provider { get; set; }        // "ollama", "azure-openai"
    public required string Model { get; set; }
    
    // Metrics
    public int? PromptTokens { get; set; }
    public int? ResponseTokens { get; set; }
    public int DurationMs { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    
    // Timestamp
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

## DbContext

```csharp
public class AuraDbContext : DbContext
{
    public DbSet<Workflow> Workflows => Set<Workflow>();
    public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<AgentExecution> AgentExecutions => Set<AgentExecution>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Workflow
        modelBuilder.Entity<Workflow>(e =>
        {
            e.HasKey(w => w.Id);
            e.HasIndex(w => w.WorkItemId);
            e.HasIndex(w => w.Status);
            e.Property(w => w.EnrichedContext).HasColumnType("jsonb");
        });
        
        // WorkflowStep
        modelBuilder.Entity<WorkflowStep>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasOne(s => s.Workflow)
                .WithMany(w => w.Steps)
                .HasForeignKey(s => s.WorkflowId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(s => new { s.WorkflowId, s.Order });
            e.Property(s => s.Input).HasColumnType("jsonb");
            e.Property(s => s.Output).HasColumnType("jsonb");
        });
        
        // Conversation
        modelBuilder.Entity<Conversation>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasOne(c => c.Workflow)
                .WithMany(w => w.Conversations)
                .HasForeignKey(c => c.WorkflowId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(c => new { c.WorkflowId, c.CreatedAt });
        });
        
        // AgentExecution
        modelBuilder.Entity<AgentExecution>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.CreatedAt);
            e.HasIndex(a => a.AgentId);
        });
    }
}
```

## What We Removed

The current system has ~13k lines in Data. We're removing:

| Removed | Reason |
|---------|--------|
| WorkflowState entity | Merged into Workflow |
| ExecutionPlan entity | Steps are the plan |
| AgentContext entity | Passed at runtime, not persisted |
| IngestionDocument entity | Future consideration |
| CodebaseContext entity | Future consideration |
| Complex validation state | Simplified to step status |
| Iteration tracking | Simplified to Attempts count |

## Migration Strategy

1. Create new migration with simplified schema
2. Provide SQL script to migrate existing data (if needed)
3. Or: Clean slate for v2 (recommended for greenfield)

## Aspire Integration

Database provisioned via Aspire:

```csharp
// In Aura.Host/Program.cs
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .AddDatabase("aura-db");

// EF Core uses connection string from Aspire
builder.Services.AddDbContext<AuraDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("aura-db")));
```

## Open Questions

1. **pgvector** - Do we need vector embeddings for RAG? (Probably yes, later)
2. **Soft deletes** - Should we soft-delete workflows?
3. **Archival** - Strategy for old workflow data?
4. **Multi-workspace** - One workflow per workspace, or multiple?
