# Phase 3: Data Layer

**Duration:** 1-2 hours  
**Dependencies:** Phase 1 (Core Infrastructure)  
**Output:** Simplified database schema with EF Core

## Objective

Create a minimal data layer for workflow and step persistence.

## Tasks

### 3.1 Create Data Folder Structure

```
src/Aura/
└── Data/
    ├── AuraDbContext.cs
    ├── Entities/
    │   ├── Workflow.cs
    │   ├── WorkflowStep.cs
    │   └── Conversation.cs
    └── Migrations/
        └── (generated)
```

### 3.2 Define Entities

**Workflow.cs:**
```csharp
namespace Aura.Data.Entities;

public class Workflow
{
    public Guid Id { get; set; }
    
    // Work item identity
    public required string WorkItemId { get; set; }
    public required string WorkItemTitle { get; set; }
    public string? WorkItemDescription { get; set; }
    public string? WorkItemUrl { get; set; }
    
    // Status
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Created;
    
    // Workspace
    public string? WorkspacePath { get; set; }
    public string? GitBranch { get; set; }
    
    // Digested context (JSON)
    public string? DigestedContext { get; set; }
    
    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public List<WorkflowStep> Steps { get; set; } = [];
    public List<Conversation> Conversations { get; set; } = [];
}

public enum WorkflowStatus
{
    Created,
    Digesting,
    Digested,
    Planning,
    Planned,
    Executing,
    Completed,
    Failed,
    Cancelled
}
```

**WorkflowStep.cs:**
```csharp
namespace Aura.Data.Entities;

public class WorkflowStep
{
    public Guid Id { get; set; }
    
    // Parent
    public Guid WorkflowId { get; set; }
    public Workflow Workflow { get; set; } = null!;
    
    // Definition
    public int Order { get; set; }
    public required string Name { get; set; }
    public required string Capability { get; set; }
    public string? Description { get; set; }
    
    // Status
    public StepStatus Status { get; set; } = StepStatus.Pending;
    
    // Execution
    public string? AssignedAgentId { get; set; }
    public string? Input { get; set; }   // JSON
    public string? Output { get; set; }  // JSON
    public string? Error { get; set; }
    public int Attempts { get; set; } = 0;
    
    // Timestamps
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public enum StepStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Skipped
}
```

**Conversation.cs:**
```csharp
namespace Aura.Data.Entities;

public class Conversation
{
    public Guid Id { get; set; }
    
    // Parent
    public Guid WorkflowId { get; set; }
    public Workflow Workflow { get; set; } = null!;
    
    // Message
    public required string Role { get; set; }  // "user" or "assistant"
    public required string Content { get; set; }
    
    // Metadata
    public string? AgentId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

### 3.3 Implement DbContext

**AuraDbContext.cs:**
```csharp
namespace Aura.Data;

public class AuraDbContext : DbContext
{
    public AuraDbContext(DbContextOptions<AuraDbContext> options) : base(options) { }
    
    public DbSet<Workflow> Workflows => Set<Workflow>();
    public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Workflow
        modelBuilder.Entity<Workflow>(e =>
        {
            e.HasKey(w => w.Id);
            e.HasIndex(w => w.WorkItemId);
            e.HasIndex(w => w.Status);
            e.Property(w => w.Status).HasConversion<string>();
            e.Property(w => w.DigestedContext).HasColumnType("jsonb");
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
            e.Property(s => s.Status).HasConversion<string>();
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
    }
}
```

### 3.4 Add Package References

**Aura.csproj additions:**
```xml
<ItemGroup>
  <PackageReference Include="Microsoft.EntityFrameworkCore" Version="9.0.0" />
  <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.0" />
</ItemGroup>
```

### 3.5 Create Initial Migration

```bash
cd src/Aura
dotnet ef migrations add InitialCreate -o Data/Migrations
```

### 3.6 DI Registration Extension

**ServiceCollectionExtensions.cs (addition):**
```csharp
namespace Aura.Data;

public static class DataServiceCollectionExtensions
{
    public static IServiceCollection AddAuraData(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<AuraDbContext>(options =>
            options.UseNpgsql(connectionString));
        
        return services;
    }
    
    public static IServiceCollection AddAuraDataInMemory(
        this IServiceCollection services,
        string? databaseName = null)
    {
        services.AddDbContext<AuraDbContext>(options =>
            options.UseInMemoryDatabase(databaseName ?? $"Aura-{Guid.NewGuid()}"));
        
        return services;
    }
}
```

### 3.7 Add Repository Pattern (Optional, Minimal)

**IWorkflowRepository.cs:**
```csharp
namespace Aura.Data;

public interface IWorkflowRepository
{
    Task<Workflow?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Workflow?> GetWithStepsAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Workflow>> GetAllAsync(WorkflowStatus? status = null, CancellationToken ct = default);
    Task<Workflow> CreateAsync(Workflow workflow, CancellationToken ct = default);
    Task UpdateAsync(Workflow workflow, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public class WorkflowRepository : IWorkflowRepository
{
    private readonly AuraDbContext _db;
    
    public WorkflowRepository(AuraDbContext db) => _db = db;
    
    public async Task<Workflow?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Workflows.FindAsync([id], ct);
    }
    
    public async Task<Workflow?> GetWithStepsAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Workflows
            .Include(w => w.Steps.OrderBy(s => s.Order))
            .FirstOrDefaultAsync(w => w.Id == id, ct);
    }
    
    public async Task<IReadOnlyList<Workflow>> GetAllAsync(
        WorkflowStatus? status = null, 
        CancellationToken ct = default)
    {
        var query = _db.Workflows.AsQueryable();
        
        if (status.HasValue)
            query = query.Where(w => w.Status == status.Value);
        
        return await query
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(ct);
    }
    
    public async Task<Workflow> CreateAsync(Workflow workflow, CancellationToken ct = default)
    {
        workflow.Id = Guid.NewGuid();
        workflow.CreatedAt = DateTime.UtcNow;
        workflow.UpdatedAt = DateTime.UtcNow;
        
        _db.Workflows.Add(workflow);
        await _db.SaveChangesAsync(ct);
        
        return workflow;
    }
    
    public async Task UpdateAsync(Workflow workflow, CancellationToken ct = default)
    {
        workflow.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
    
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var workflow = await GetByIdAsync(id, ct);
        if (workflow is not null)
        {
            _db.Workflows.Remove(workflow);
            await _db.SaveChangesAsync(ct);
        }
    }
}
```

### 3.8 Add Unit Tests

```csharp
public class WorkflowRepositoryTests
{
    private readonly AuraDbContext _db;
    private readonly WorkflowRepository _repo;
    
    public WorkflowRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AuraDbContext>()
            .UseInMemoryDatabase($"Test-{Guid.NewGuid()}")
            .Options;
        _db = new AuraDbContext(options);
        _repo = new WorkflowRepository(_db);
    }
    
    [Fact]
    public async Task CreateAsync_AssignsIdAndTimestamps() { ... }
    
    [Fact]
    public async Task GetWithStepsAsync_IncludesOrderedSteps() { ... }
    
    [Fact]
    public async Task GetAllAsync_FiltersByStatus() { ... }
}
```

## Verification

1. ✅ `dotnet build src/Aura` succeeds
2. ✅ Migration generates correctly
3. ✅ Unit tests with in-memory database pass
4. ✅ Integration test with real PostgreSQL (optional)

## Deliverables

- [ ] Entity classes (Workflow, WorkflowStep, Conversation)
- [ ] `AuraDbContext` with proper configuration
- [ ] Initial EF Core migration
- [ ] `IWorkflowRepository` with basic CRUD
- [ ] DI extensions for PostgreSQL and in-memory
- [ ] Unit tests

## What We Removed (vs. Current)

| Removed | Lines | Reason |
|---------|-------|--------|
| WorkflowState entity | ~200 | Merged into Workflow |
| ExecutionPlan entity | ~150 | Steps ARE the plan |
| AgentContext entity | ~100 | Runtime only, not persisted |
| IngestionDocument | ~300 | Future consideration |
| CodebaseContext | ~400 | Future consideration |
| Complex repositories | ~2000 | Simplified to one repository |
| Validation tracking | ~500 | Simplified to Attempts count |

**Estimated reduction:** ~3,500 lines → ~300 lines
