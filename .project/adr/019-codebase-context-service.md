# ADR 019: Codebase Context Service for Developer Module

## Status

Accepted

## Date

2025-12-05

## Context

The Developer Module needs to provide agents with comprehensive understanding of a codebase to perform tasks like:

- **Code grokking**: Understanding project structure, class hierarchies, dependencies
- **Code manipulation**: Generating, modifying, refactoring files with awareness of existing patterns
- **Code navigation**: Finding usages, implementations, callers
- **Documentation**: Writing accurate docs that reference real projects and types

Currently, the system has two separate knowledge sources:

1. **RAG (Text Embeddings)**: Indexes file contents as text chunks, enables semantic search
2. **Code Graph (Roslyn)**: Indexes structural information - projects, namespaces, classes, methods, relationships

**Problem**: Step execution only injects RAG context into agents. When documenting a multi-project solution like BrightSword (with Crucible, Feber, Squid, SwissKnife packages), the agent only sees whichever project has the best-matching markdown documentation, missing the full picture.

### Analysis of Options

| Option | Description | Pros | Cons |
|--------|-------------|------|------|
| A. Inject Graph in WorkflowService | Add graph queries directly in step execution | Quick, minimal code | Scattered logic, not reusable |
| B. Graph-Informed RAG | Query graph first, use names as RAG terms | Leverages both systems | Two-phase, may still miss code |
| C. Unified Context Service | New `ICodebaseContextService` abstraction | Clean, extensible, testable | New abstraction |
| D. Graph Summary in RAG | Create summary chunks during indexing | Works with existing flow | Stale if graph changes |

## Decision

Implement **Option C: `ICodebaseContextService`** as a first-class abstraction in the Developer Module.

### Rationale

1. **Core capability, not incidental**: Code understanding is fundamental to developer agents, not a one-off need
2. **Multiple consumers**: Coding agent, review agent, documentation agent, refactor agent all need codebase context
3. **Extensible**: Can add call graphs, git history, test coverage, file tree later
4. **Testable**: Mock for agent unit tests
5. **Configurable**: Different agents need different levels of detail

## Design

### Interface

```csharp
namespace Aura.Module.Developer.Services;

public interface ICodebaseContextService
{
    /// <summary>
    /// Gets comprehensive codebase context for an agent.
    /// </summary>
    Task<CodebaseContext> GetContextAsync(
        string workspacePath,
        CodebaseContextOptions options,
        CancellationToken ct = default);
}

public record CodebaseContext
{
    /// <summary>Project structure summary.</summary>
    public required string ProjectStructure { get; init; }
    
    /// <summary>Relevant code/docs from semantic search.</summary>
    public string? SemanticContext { get; init; }
    
    /// <summary>Type information if requested.</summary>
    public string? TypeContext { get; init; }
    
    /// <summary>Formats all context for LLM consumption.</summary>
    public string ToPromptContext();
}

public record CodebaseContextOptions
{
    /// <summary>RAG queries for semantic search.</summary>
    public IReadOnlyList<string>? RagQueries { get; init; }
    
    /// <summary>Include project structure from code graph.</summary>
    public bool IncludeProjectStructure { get; init; } = true;
    
    /// <summary>Include namespace hierarchy.</summary>
    public bool IncludeNamespaces { get; init; } = false;
    
    /// <summary>Specific types to get details for.</summary>
    public IReadOnlyList<string>? FocusTypes { get; init; }
    
    /// <summary>Maximum RAG results to include.</summary>
    public int MaxRagResults { get; init; } = 10;
}
```

### Implementation

```csharp
public class CodebaseContextService : ICodebaseContextService
{
    private readonly ICodeGraphService _graphService;
    private readonly IRagService _ragService;
    
    public async Task<CodebaseContext> GetContextAsync(
        string workspacePath,
        CodebaseContextOptions options,
        CancellationToken ct = default)
    {
        var tasks = new List<Task>();
        
        string? projectStructure = null;
        string? semanticContext = null;
        string? typeContext = null;
        
        // Get project structure from graph
        if (options.IncludeProjectStructure)
        {
            projectStructure = await GetProjectStructureAsync(workspacePath, ct);
        }
        
        // Get semantic context from RAG
        if (options.RagQueries?.Count > 0)
        {
            semanticContext = await GetSemanticContextAsync(
                workspacePath, options.RagQueries, options.MaxRagResults, ct);
        }
        
        // Get type details if requested
        if (options.FocusTypes?.Count > 0)
        {
            typeContext = await GetTypeContextAsync(
                workspacePath, options.FocusTypes, ct);
        }
        
        return new CodebaseContext
        {
            ProjectStructure = projectStructure ?? "No project structure available",
            SemanticContext = semanticContext,
            TypeContext = typeContext,
        };
    }
    
    private async Task<string> GetProjectStructureAsync(string workspacePath, CancellationToken ct)
    {
        // Query graph for Solution -> Project nodes
        // Format as structured text for LLM
    }
}
```

### Integration with WorkflowService

```csharp
// Before
var ragContext = await GetRagContextForStepAsync(queries, workspacePath, ct);
var context = new AgentContext(prompt) { RagContext = ragContext };

// After
var codebaseContext = await _codebaseContextService.GetContextAsync(
    workspacePath,
    new CodebaseContextOptions 
    { 
        RagQueries = queries,
        IncludeProjectStructure = true,
    },
    ct);
var context = new AgentContext(prompt) { RagContext = codebaseContext.ToPromptContext() };
```

### Output Format

The `ToPromptContext()` method formats context for LLM consumption:

```markdown
## Workspace Structure

### Solution: BrightSword.sln

#### Projects
- **BrightSword.Crucible** - C:\work\BrightSword\BrightSword.Crucible
- **BrightSword.Feber** - C:\work\BrightSword\BrightSword.Feber
- **BrightSword.Squid** - C:\work\BrightSword\BrightSword.Squid
- **BrightSword.SwissKnife** - C:\work\BrightSword\BrightSword.SwissKnife

#### Project Dependencies
- BrightSword.Feber → BrightSword.SwissKnife

---

## Relevant Code and Documentation

[RAG results here]
```

## Consequences

### Positive

- Agents get comprehensive codebase understanding
- Single abstraction for all developer agents to use
- Easy to extend with new context types (git history, test results, etc.)
- Testable in isolation
- Configuration via options allows agents to request appropriate detail level

### Negative

- New service to maintain
- Additional database queries (graph + RAG) per step execution
- Need to balance context size vs. LLM token limits

### Neutral

- Existing `IRagService` and `ICodeGraphService` remain unchanged
- `AgentContext.RagContext` continues to be the injection point (no interface changes)

## Implementation Plan

1. Create `ICodebaseContextService` interface and `CodebaseContextOptions`/`CodebaseContext` records
2. Implement `CodebaseContextService` with graph + RAG integration
3. Register in DI in Developer Module
4. Update `WorkflowService` to use `ICodebaseContextService` instead of direct RAG calls
5. Test with BrightSword workflow to verify all projects appear in context

## Related ADRs

- ADR 016: Configurable RAG Queries
- ADR 017: Case-Insensitive Path Handling
- ADR 018: Prompt Template Architecture
