# Code-Aware Chat

**Status:** ✅ Complete  
**Completed:** 2025-12-13  
**Last Updated:** 2025-12-13

## Overview

Enhance agent chat to include both RAG (semantic search) and Code Graph (structural intelligence), giving developers a comprehensive understanding of their codebase before creating workflows.

## Problem Statement

Currently, when chatting with an agent via the Aura panel:

| Feature | Status | What It Provides |
|---------|--------|------------------|
| **RAG** | ✅ Enabled | Semantic search - finds similar code by meaning |
| **Code Graph** | ❌ Not used | Structural queries - implementations, callers, type hierarchy |

A developer asking "How is IWorkflowService implemented?" gets:
- ✅ Code chunks that mention "workflow" or "service" (semantic match)
- ❌ No actual implementations of the interface (structural match)

This forces developers to manually query the Code Graph API or use IDE features before they can create informed workflows.

## Solution

Enhance `RagEnrichedExecutor` to optionally include Code Graph context alongside RAG results.

### User Experience

1. **Chat Panel** - New toggle: "Include Code Structure" (alongside existing RAG toggle)
2. **Smart Detection** - Auto-detect structural queries and include relevant graph data
3. **Combined Context** - Agent receives both semantic matches AND structural relationships

### Example Interaction

**User asks:** "Show me how WorkflowExecutor handles step failures"

**Current behavior (RAG only):**
```
Context includes:
- Code chunks mentioning "failure", "error", "WorkflowExecutor"
- May miss the actual error handling code if terms differ
```

**Enhanced behavior (RAG + Code Graph):**
```
Context includes:
- Semantic matches for "failure", "error handling"
- WorkflowExecutor class structure (methods, properties)
- Methods that call WorkflowExecutor.ExecuteAsync
- Exception types caught/thrown
- Related types (WorkflowStep, StepResult, etc.)
```

## Technical Design

### 1. Extend AgentContext

```csharp
public record AgentContext
{
    // Existing
    public string Prompt { get; init; }
    public string? WorkspacePath { get; init; }
    public string? RagContext { get; init; }
    public IReadOnlyList<RagResult>? RagResults { get; init; }
    
    // New
    public string? CodeGraphContext { get; init; }
    public IReadOnlyList<CodeNode>? RelevantNodes { get; init; }
    public IReadOnlyList<CodeEdge>? RelevantEdges { get; init; }
}
```

### 2. Create ICodeGraphEnricher

```csharp
public interface ICodeGraphEnricher
{
    /// <summary>
    /// Extracts relevant Code Graph context for a prompt.
    /// </summary>
    Task<CodeGraphEnrichment> EnrichAsync(
        string prompt,
        string? workspacePath,
        CodeGraphEnrichmentOptions? options = null,
        CancellationToken cancellationToken = default);
}

public record CodeGraphEnrichment(
    string FormattedContext,
    IReadOnlyList<CodeNode> Nodes,
    IReadOnlyList<CodeEdge> Edges);

public record CodeGraphEnrichmentOptions
{
    /// <summary>Max nodes to include in context.</summary>
    public int MaxNodes { get; init; } = 10;
    
    /// <summary>Include callers/callees for methods.</summary>
    public bool IncludeCallGraph { get; init; } = true;
    
    /// <summary>Include type hierarchy (base/derived).</summary>
    public bool IncludeTypeHierarchy { get; init; } = true;
    
    /// <summary>Include implementations for interfaces.</summary>
    public bool IncludeImplementations { get; init; } = true;
}
```

### 3. Implement CodeGraphEnricher

```csharp
public class CodeGraphEnricher : ICodeGraphEnricher
{
    private readonly ICodeGraphService _codeGraph;
    private readonly ILogger<CodeGraphEnricher> _logger;

    public async Task<CodeGraphEnrichment> EnrichAsync(
        string prompt,
        string? workspacePath,
        CodeGraphEnrichmentOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new();
        
        // 1. Extract potential type/method names from prompt
        var symbols = ExtractSymbolNames(prompt);
        
        // 2. Find matching nodes
        var nodes = new List<CodeNode>();
        var edges = new List<CodeEdge>();
        
        foreach (var symbol in symbols.Take(5)) // Limit to avoid explosion
        {
            var matchingNodes = await _codeGraph.FindNodesByNameAsync(
                symbol, workspacePath, cancellationToken);
            
            foreach (var node in matchingNodes.Take(options.MaxNodes))
            {
                nodes.Add(node);
                
                if (options.IncludeImplementations && node.NodeType == CodeNodeType.Interface)
                {
                    var impls = await _codeGraph.GetImplementationsAsync(node.Id, cancellationToken);
                    nodes.AddRange(impls);
                }
                
                if (options.IncludeTypeHierarchy && node.NodeType == CodeNodeType.Class)
                {
                    var derived = await _codeGraph.GetDerivedTypesAsync(node.Id, cancellationToken);
                    nodes.AddRange(derived);
                }
                
                if (options.IncludeCallGraph && node.NodeType == CodeNodeType.Method)
                {
                    var callers = await _codeGraph.GetCallersAsync(node.Id, cancellationToken);
                    edges.AddRange(callers);
                }
            }
        }
        
        // 3. Format as context string
        var context = FormatCodeGraphContext(nodes, edges);
        
        return new CodeGraphEnrichment(context, nodes, edges);
    }
    
    private static IEnumerable<string> ExtractSymbolNames(string prompt)
    {
        // Extract PascalCase words that look like type/method names
        var matches = Regex.Matches(prompt, @"\b([A-Z][a-z]+(?:[A-Z][a-z]+)+)\b");
        return matches.Select(m => m.Value).Distinct();
    }
    
    private static string FormatCodeGraphContext(
        IReadOnlyList<CodeNode> nodes,
        IReadOnlyList<CodeEdge> edges)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Code Structure");
        sb.AppendLine();
        
        // Group by type
        var types = nodes.Where(n => n.NodeType is CodeNodeType.Class or CodeNodeType.Interface);
        var methods = nodes.Where(n => n.NodeType == CodeNodeType.Method);
        
        foreach (var type in types)
        {
            sb.AppendLine($"### {type.NodeType}: {type.Name}");
            sb.AppendLine($"File: {type.FilePath}:{type.StartLine}");
            
            var typeMembers = methods.Where(m => m.ParentId == type.Id);
            if (typeMembers.Any())
            {
                sb.AppendLine("Methods:");
                foreach (var m in typeMembers)
                {
                    sb.AppendLine($"  - {m.Name}");
                }
            }
            sb.AppendLine();
        }
        
        if (edges.Any())
        {
            sb.AppendLine("### Relationships");
            foreach (var edge in edges.Take(20))
            {
                sb.AppendLine($"- {edge.SourceName} → {edge.TargetName} ({edge.EdgeType})");
            }
        }
        
        return sb.ToString();
    }
}
```

### 4. Update RagEnrichedExecutor

```csharp
public sealed class RagEnrichedExecutor : IRagEnrichedExecutor
{
    private readonly ICodeGraphEnricher _codeGraphEnricher; // NEW
    
    public async Task<AgentOutput> ExecuteAsync(
        string agentId,
        string prompt,
        string? workspacePath = null,
        bool? useRag = null,
        bool? useCodeGraph = null,  // NEW
        RagQueryOptions? ragOptions = null,
        CancellationToken cancellationToken = default)
    {
        var agent = _agentRegistry.GetAgent(agentId)
            ?? throw new AgentException(AgentErrorCode.NotFound, $"Agent '{agentId}' not found");

        var context = new AgentContext(prompt, WorkspacePath: workspacePath);
        
        // RAG enrichment (existing)
        if (useRag ?? _options.EnabledByDefault)
        {
            context = await BuildRagEnrichedContextAsync(prompt, workspacePath, ragOptions, cancellationToken);
        }
        
        // Code Graph enrichment (NEW)
        if (useCodeGraph ?? _options.CodeGraphEnabledByDefault)
        {
            var graphEnrichment = await _codeGraphEnricher.EnrichAsync(
                prompt, workspacePath, null, cancellationToken);
            
            context = context with
            {
                CodeGraphContext = graphEnrichment.FormattedContext,
                RelevantNodes = graphEnrichment.Nodes,
                RelevantEdges = graphEnrichment.Edges
            };
        }

        return await agent.ExecuteAsync(context, cancellationToken);
    }
}
```

### 5. Update Extension

**File:** `extension/src/providers/chatWindowProvider.ts`

Add Code Graph toggle alongside RAG toggle:

```typescript
// Track state
let useRag = true;
let useCodeGraph = true;  // NEW

// Handle toggle
case 'toggleCodeGraph':
    useCodeGraph = data.enabled;
    break;

// Pass to API
const ragResponse = await this._apiService.executeAgentWithRag(
    agent.id,
    message,
    workspacePath,
    5,
    useCodeGraph  // NEW parameter
);
```

**File:** `extension/src/services/auraApiService.ts`

```typescript
async executeAgentWithRag(
    agentId: string,
    prompt: string,
    workspacePath?: string,
    topK: number = 5,
    useCodeGraph: boolean = true  // NEW
): Promise<RagExecuteResult> {
    const response = await this.httpClient.post(
        `${this.getBaseUrl()}/api/agents/${agentId}/execute/rag`,
        { prompt, workspacePath, useRag: true, topK, useCodeGraph },  // NEW
        { timeout: this.getExecutionTimeout() }
    );
    return response.data;
}
```

## UI Mockup

```
┌─────────────────────────────────────────────────────────────┐
│ Chat: coding-agent                                    [×]   │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  [User] How is IWorkflowService implemented?                │
│                                                             │
│  [Assistant]                                                │
│  Based on the code structure, IWorkflowService is           │
│  implemented by WorkflowService in:                         │
│                                                             │
│  📁 src/Aura.Module.Developer/Services/WorkflowService.cs   │
│                                                             │
│  Key methods:                                               │
│  - CreateAsync() - Creates a new workflow                   │
│  - ExecuteStepAsync() - Runs a single step                  │
│  - FinalizeAsync() - Commits and creates PR                 │
│                                                             │
│  The service is used by:                                    │
│  - WorkflowPanelProvider (extension)                        │
│  - POST /api/developer/workflows (API)                      │
│                                                             │
├─────────────────────────────────────────────────────────────┤
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ Ask a question...                                       │ │
│ └─────────────────────────────────────────────────────────┘ │
│                                                             │
│ [✓] RAG Search    [✓] Code Structure    [Send]             │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

## Implementation Tasks

### Task 1: ICodeGraphEnricher Interface (30 min)

**File:** `src/Aura.Foundation/CodeGraph/ICodeGraphEnricher.cs` (new)

- Define interface and records
- Add to DI registration

### Task 2: CodeGraphEnricher Implementation (2 hours)

**File:** `src/Aura.Foundation/CodeGraph/CodeGraphEnricher.cs` (new)

- Symbol extraction from prompts
- Node/edge retrieval from ICodeGraphService
- Context formatting

### Task 3: Update RagEnrichedExecutor (1 hour)

**File:** `src/Aura.Foundation/Agents/RagEnrichedExecutor.cs`

- Add ICodeGraphEnricher dependency
- Add useCodeGraph parameter
- Combine RAG + Code Graph contexts

### Task 4: Update API Endpoint (30 min)

**File:** `src/Aura.Api/Program.cs`

- Add `useCodeGraph` to ExecuteWithRagRequest
- Pass through to executor

### Task 5: Update Extension Chat UI (1 hour)

**Files:**
- `extension/src/providers/chatWindowProvider.ts` - Add toggle and state
- `extension/src/services/auraApiService.ts` - Add parameter
- Add toggle to chat panel HTML

### Task 6: Tests (1 hour)

**File:** `tests/Aura.Foundation.Tests/CodeGraph/CodeGraphEnricherTests.cs` (new)

- Symbol extraction tests
- Context formatting tests
- Integration with mock ICodeGraphService

## Files to Modify

| File | Change |
|------|--------|
| `Aura.Foundation/CodeGraph/ICodeGraphEnricher.cs` | New interface |
| `Aura.Foundation/CodeGraph/CodeGraphEnricher.cs` | New implementation |
| `Aura.Foundation/Agents/RagEnrichedExecutor.cs` | Add Code Graph enrichment |
| `Aura.Foundation/ServiceCollectionExtensions.cs` | Register new service |
| `Aura.Api/Program.cs` | Update request model |
| `extension/src/providers/chatWindowProvider.ts` | Add toggle |
| `extension/src/services/auraApiService.ts` | Add parameter |

## Success Criteria

- [ ] Chat panel has "Code Structure" toggle (default: on)
- [ ] Queries mentioning type/method names include structural context
- [ ] Context includes: type definitions, implementations, callers
- [ ] Works alongside RAG (both can be enabled)
- [ ] Graceful fallback if Code Graph not indexed
- [ ] Response includes source file paths (clickable in extension)

## Future Enhancements

1. **Smart Detection** - Auto-enable Code Graph for structural queries ("find implementations", "who calls X")
2. **Focused Queries** - Right-click symbol → "Ask Aura about this"
3. **Visual Graph** - Show relationship diagram in response
4. **Cross-repo** - Query Code Graph across multiple indexed repos
