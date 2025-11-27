# ADR-003: Composable Module System

## Status

Accepted

## Date

2025-11-25

## Context

Aura aims to serve multiple use cases:

- **Developer workflows** - Git automation, code generation, PR management
- **Research assistance** - Paper indexing, synthesis, citations
- **Personal finance** - Receipt tracking, budgeting, expense categorization
- **General knowledge** - Note-taking, document search, Q&A

These domains have very different requirements:

- A researcher doesn't need git worktree management
- A developer doesn't need receipt OCR
- A personal user doesn't need code compilation tools

The question: How do we support diverse use cases without shipping bloated software?

## Decision

**Aura uses a composable module system. Users enable only what they need.**

### Architecture

```text
┌─────────────────────────────────────────────────────────────┐
│                    Vertical Modules                          │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐       │
│  │   Developer  │  │   Research   │  │   Personal   │  ...  │
│  │    Module    │  │    Module    │  │    Module    │       │
│  │  (optional)  │  │  (optional)  │  │  (optional)  │       │
│  └──────────────┘  └──────────────┘  └──────────────┘       │
├─────────────────────────────────────────────────────────────┤
│                Aura.Foundation (always loaded)               │
│   Agents │ LLM │ RAG │ Database │ Tools │ Modules           │
└─────────────────────────────────────────────────────────────┘
```

### Module Contract

```csharp
public interface IAuraModule
{
    string ModuleId { get; }           // e.g., "developer"
    string Name { get; }               // e.g., "Developer Automation"
    string Description { get; }
    IReadOnlyList<string> Dependencies { get; }  // Should be empty!
    
    void ConfigureServices(IServiceCollection services, IConfiguration config);
    void RegisterAgents(IAgentRegistry registry, IConfiguration config);
}
```

### Configuration

```json
{
  "Aura": {
    "Modules": {
      "Enabled": ["developer", "research"]
    }
  }
}
```

### Critical Rule: No Module-to-Module Dependencies

Modules depend ONLY on Foundation. Never on each other.

```text
✅ Developer → Foundation
✅ Research → Foundation
❌ Developer → Research (FORBIDDEN)
```

This ensures modules can be enabled/disabled independently.

## Consequences

### Positive

- **Minimal footprint** - Users only load what they use
- **Clean separation** - Each domain isolated in its own assembly
- **Easy extension** - New modules don't affect existing ones
- **Testability** - Modules can be tested in isolation
- **Clear ownership** - Each module has defined boundaries

### Negative

- **No cross-module features** - Can't easily build features spanning modules
- **Duplication risk** - Similar concepts might be implemented differently
- **Discovery challenge** - Users must know what modules exist

### Mitigations

- Foundation provides shared primitives (agents, tools, data)
- Module registry with descriptions and capabilities
- VS Code extension shows available modules

## Alternatives Considered

### Monolithic Application

- **Pros**: Simple deployment, shared code
- **Cons**: Bloated for single-use-case users, coupling
- **Rejected**: Conflicts with composability goal

### Plugin System (Dynamic Loading)

- **Pros**: Maximum flexibility, third-party plugins
- **Cons**: Complex versioning, security concerns, harder debugging
- **Rejected**: Over-engineered for current needs; can evolve to this later

### Feature Flags

- **Pros**: Single binary, runtime toggling
- **Cons**: All code shipped regardless, testing matrix explosion
- **Rejected**: Doesn't achieve minimal footprint goal

## References

- [spec/10-composable-modules.md](../spec/10-composable-modules.md) - Detailed specification
