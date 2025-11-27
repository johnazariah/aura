# ADR-006: Foundation vs Module Data Separation

## Status

Accepted

## Date

2025-11-27

## Context

Aura has a modular architecture where:

- **Foundation** is always loaded and provides core capabilities
- **Modules** (Developer, Research, Personal) are optional

The question: Which database entities belong in Foundation vs in Modules?

Initial implementation put all entities in a shared `Aura.Data` project. This created coupling issues:

- Modules couldn't be loaded independently
- Foundation depended on module-specific entities
- Single migration path for all entities

## Decision

**Foundation owns core AI assistant entities. Modules own their domain-specific entities.**

### Foundation Entities (`Aura.Foundation/Data/`)

These support the core AI assistant functionality:

| Entity | Purpose |
|--------|---------|
| `Conversation` | Chat session with an agent |
| `Message` | Individual message in a conversation |
| `AgentExecution` | Record of agent invocation |

### Module Entities (e.g., `Aura.Module.Developer/Data/`)

These are specific to the module's domain:

| Entity | Module | Purpose |
|--------|--------|---------|
| `Workflow` | Developer | Automation workflow (from GitHub issue) |
| `WorkflowStep` | Developer | Individual step in workflow execution |

### DbContext Hierarchy

```csharp
// Foundation provides base context
public class AuraDbContext : DbContext
{
    public DbSet<Conversation> Conversations { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<AgentExecution> AgentExecutions { get; set; }
    
    protected static void ConfigureFoundationEntities(ModelBuilder modelBuilder)
    {
        // Foundation entity configuration
    }
}

// Module extends with its entities
public class DeveloperDbContext : AuraDbContext
{
    public DbSet<Workflow> Workflows { get; set; }
    public DbSet<WorkflowStep> WorkflowSteps { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureFoundationEntities(modelBuilder);
        // Developer-specific configuration
    }
}
```

### Migration Strategy

- Foundation migrations in `Aura.Foundation/Data/Migrations/`
- Module migrations in respective module projects
- Modules inherit Foundation schema, add their own tables

## Consequences

### Positive

- **Clean separation** - Modules don't pollute Foundation
- **Independent loading** - Modules can be enabled/disabled without schema changes
- **Clear ownership** - Easy to know where an entity belongs
- **Focused migrations** - Each module manages its own schema changes

### Negative

- **Inheritance complexity** - DbContext inheritance requires careful configuration
- **Multiple migration paths** - Must coordinate if sharing database
- **Query spanning** - Can't easily join across module boundaries

### Mitigations

- Static `ConfigureFoundationEntities` method for inheritance
- Clear documentation of entity ownership
- Shared database with namespaced tables when needed

## Decision Criteria

**An entity belongs in Foundation if:**

- It's needed for basic AI assistant functionality
- All modules might use it (conversations, executions)
- It's part of the core user experience

**An entity belongs in a Module if:**

- It's specific to that module's domain
- Other modules don't need it
- It only makes sense in the module's context

## Alternatives Considered

### Single Shared Data Project

- **Pros**: Simple, one place for all entities
- **Cons**: Couples modules together, can't load independently
- **Rejected**: Violated composable module principle (ADR-003)

### Separate Databases Per Module

- **Pros**: Complete isolation
- **Cons**: Complex joins, multiple connections, resource overhead
- **Rejected**: Over-engineered for current needs

### Foundation-Only Entities

- **Pros**: Simplest possible approach
- **Cons**: Modules can't persist domain data
- **Rejected**: Too limiting for module capabilities

## References

- [ADR-003: Composable Modules](003-composable-modules.md)
- [spec/03-data-model.md](../spec/03-data-model.md)
