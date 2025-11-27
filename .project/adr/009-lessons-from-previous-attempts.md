# ADR-009: Lessons Learned from Previous Attempts

## Status

Accepted

## Date

2025-11-27

## Context

Aura is not our first attempt at building an AI-powered development assistant. Three previous projects inform our current design:

1. **hve-hack** (Agent Orchestrator) - Full-featured orchestration system, ~38k lines, 17 projects
2. **birdlet** - RAG-focused research/dev platform with A2A protocol, 8 projects
3. **bird-constellation** (Owlet) - Document indexing Windows service, 6 projects

Each project taught valuable lessons about what works and what doesn't. This ADR captures those lessons to avoid repeating mistakes.

## Decision

**Learn from all three projects. Adopt proven patterns. Avoid repeated pitfalls.**

---

## Project Analysis

### hve-hack (Agent Orchestrator)

**What it was**: A comprehensive AI agent orchestration system for automating software development. GitHub issue → AI agents → Pull request.

**Scale**: 17 projects, ~38,000 lines of C#, 220+ tests

#### Project Structure (17 projects!)

```text
AgentOrchestrator.Agents       # Agent implementations
AgentOrchestrator.AgentService # Agent hosting
AgentOrchestrator.AI           # AI abstractions
AgentOrchestrator.Api          # REST API
AgentOrchestrator.AppHost      # Aspire orchestration
AgentOrchestrator.CodeAnalysis # Static analysis
AgentOrchestrator.Contracts    # Shared interfaces
AgentOrchestrator.Core         # Core services
AgentOrchestrator.Data         # EF Core + PostgreSQL
AgentOrchestrator.Git          # Git operations
AgentOrchestrator.Installer    # WiX installer
AgentOrchestrator.Orchestration # Workflow engine
AgentOrchestrator.Plugins      # Plugin system
AgentOrchestrator.Providers    # LLM providers
AgentOrchestrator.Roslyn       # Roslyn analysis
AgentOrchestrator.ServiceDefaults # Aspire config
AgentOrchestrator.Tray         # System tray app
```

#### What Worked Well ✅

| Pattern | Implementation | Adopt in Aura? |
|---------|----------------|----------------|
| **Markdown agent format** | Agents defined in `.md` files with metadata sections | ✅ Already adopted |
| **Provider registry** | `ILlmProvider` + `ILlmProviderRegistry` for pluggable LLMs | ✅ Already adopted |
| **Async/Task pattern** | `Task<T>` is already a monad with success/failure/async | ✅ Use idiomatically |
| **Strongly-typed contracts** | Detailed coding standards, no `Dictionary<string,object>` | ✅ Follow standards |
| **Git worktree service** | Clean abstraction for worktree operations | ✅ Port to Developer module |
| **Agent metadata model** | `AgentDefinition` with provider, model, temperature, capabilities | ✅ Already adopted |

#### What Failed ❌

| Anti-Pattern | Problem | Avoid in Aura |
|--------------|---------|---------------|
| **17 separate projects** | Every change touched 5+ projects. Refactoring nightmare. | ❌ Keep to 4-6 projects |
| **IExecutionPlanner** | 521-line orchestrator with planning, replanning, phase management | ❌ Let user orchestrate |
| **AgentOutputValidator** | Complex validation of agent outputs that was ultimately removed | ❌ Trust agent output |
| **Dictionary<string,object> Data** | `AgentContext.Data` property was stringly-typed bag of values | ❌ Use typed properties |
| **WorkflowTelemetry everywhere** | Premature observability instrumentation in every method | ❌ Add when needed |
| **Plugin discovery service** | Complex runtime plugin loading that was rarely used | ❌ Simpler module system |
| **Multiple abstraction layers** | Contracts → Core → Orchestration → Agents → API | ❌ Flatter hierarchy |

#### Key Code Example: Over-Engineering

```csharp
// hve-hack: WorkflowOrchestrator.cs (521 lines!)
public class WorkflowOrchestrator(
    IExecutionPlanner planner,
    IAgentSelector agentSelector,
    CodeBlockExtractor codeBlockExtractor,
    CodeValidationTool validationTool,
    ILogger<WorkflowOrchestrator> logger) : IWorkflowOrchestrator
{
    private const int MaxIterations = 20;

    public async Task<Result<WorkflowState>> ExecuteWorkflowAsync(...)
    {
        // Phase 1: Planning (if not already planned)
        if (state.CurrentPlan is null)
        {
            using var planningActivity = WorkflowTelemetry.StartPlanning(state);
            var planResult = await CreateExecutionPlanAsync(state, ct);
            // ... 500 more lines of orchestration complexity
        }
    }
}

// Aura: Let agents be simple, let users orchestrate
public class ConfigurableAgent(AgentDefinition definition, ILlmProvider provider)
{
    public async Task<AgentOutput> ExecuteAsync(AgentContext context, CancellationToken ct)
    {
        var response = await provider.ChatAsync(messages, definition.Model, definition.Temperature, ct);
        return response.Match(
            success => AgentOutput.Success(success),
            error => AgentOutput.Error(error.Message));
    }
}
```

---

### birdlet

**What it was**: A RAG-focused platform for research and development workflows with agent-to-agent communication.

**Scale**: 8 projects, split between main API and agent packs

#### Project Structure

```text
src/
├── Birdlet.Api           # Main API (779 lines in Program.cs!)
├── Birdlet.AppHost       # Aspire orchestration
├── Birdlet.AppHost.Research # Research-only AppHost
├── Birdlet.Ingest        # Document ingestion
├── Birdlet.Rag           # RAG abstraction layer
├── Birdlet.ServiceDefaults # Aspire config
├── Birdlet.Shared        # Shared POCOs
└── Agents/
    ├── Research.Fetch    # Paper discovery
    ├── Research.Understand # Document analysis
    └── Research.Write    # Draft generation
```

#### What Worked Well ✅

| Pattern | Implementation | Adopt in Aura? |
|---------|----------------|----------------|
| **Profile-based loading** | `BIRDLET_PROFILE=dev\|research\|all` controls what loads | ✅ Consider for modules |
| **Agent packs by domain** | `Agents/Research/`, `Agents/Dev/` organized by use case | ✅ Similar to modules |
| **RAG as central service** | `RagService` with `SearchAsync`, `BuildContextAsync` | ✅ Foundation component |
| **Tool router** | Central `ToolRouter` dispatches to tool implementations | ✅ Consider for tools |
| **Project registry** | `ProjectRegistry` manages per-project configuration | ⚠️ Maybe later |

#### What Failed ❌

| Anti-Pattern | Problem | Avoid in Aura |
|--------------|---------|---------------|
| **779-line Program.cs** | All service registration in one giant file | ❌ Use extension methods |
| **A2A protocol complexity** | HTTP-based agent-to-agent communication added overhead | ❌ Keep agents simple |
| **Python + C# agents** | Research agents in Python required separate start scripts | ❌ Stick to .NET |
| **Manual singleton registration** | Every tool registered twice (as ITool and concrete type) | ❌ Use DI conventions |
| **Premature RAG infrastructure** | pgvector, embeddings, chunking before basic features worked | ❌ Build incrementally |

#### Key Code Example: Registration Bloat

```csharp
// birdlet: Program.cs - every service manually registered
builder.Services.AddSingleton<SearchDocsTool>();
builder.Services.AddSingleton<ReadFileTool>();
builder.Services.AddSingleton<ListDocsTool>();
builder.Services.AddSingleton<GetDocTool>();
builder.Services.AddSingleton<UpsertDocTool>();
builder.Services.AddSingleton<ListTasksLocalTool>();
builder.Services.AddSingleton<CreateTaskLocalTool>();
builder.Services.AddSingleton<UpdateTaskLocalTool>();
builder.Services.AddSingleton<RunDevCycleTool>();
builder.Services.AddSingleton<ITool>(sp => sp.GetRequiredService<SearchDocsTool>());
builder.Services.AddSingleton<ITool>(sp => sp.GetRequiredService<ReadFileTool>());
// ... repeated for every tool

// Aura: Use extension methods and conventions
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAuraFoundation(this IServiceCollection services)
    {
        services.AddSingleton<IAgentRegistry, AgentRegistry>();
        services.AddSingleton<ILlmProviderRegistry, LlmProviderRegistry>();
        // Clean, organized, one place
        return services;
    }
}
```

---

### bird-constellation (Owlet)

**What it was**: A document indexing and search Windows service with focus on production deployment.

**Scale**: 6 projects, well-structured with clear separation

#### Project Structure

```text
src/
├── Owlet.AppHost          # Aspire orchestration
├── Owlet.Service          # Pure Windows service
├── Owlet.Core             # Business logic
├── Owlet.Api              # Web API with Carter
├── Owlet.Infrastructure   # Data access
└── Owlet.ServiceDefaults  # Aspire config
```

#### What Worked Well ✅

| Pattern | Implementation | Adopt in Aura? |
|---------|----------------|----------------|
| **6 projects, clear boundaries** | Core, Api, Infrastructure, Service, AppHost | ✅ Similar structure |
| **ADR documentation** | Formal Architecture Decision Records | ✅ Already adopted |
| **Task<T> as monad** | Built-in async monad handles success, failure, async in one type | ✅ Use instead of Result<T> |
| **Dual hosting model** | Pure service for prod, Aspire for dev | ⚠️ Maybe for prod |
| **Functional programming ADR** | Explicit decision to use modern C# patterns | ✅ Follow patterns |
| **Installer-first thinking** | ADR-007: "No choices. No composition dialog." | ⚠️ When ready for prod |

#### What to Avoid ⚠️

| Pattern | Reason | Aura Approach |
|---------|--------|---------------|
| **Windows-only focus** | Uses Windows Service extensively | Cross-platform first |
| **SQLite default** | Simpler but limits vector search | PostgreSQL + pgvector |
| **MSI installer priority** | Important but not for early dev | Focus on dev experience first |

#### Key Insight: Task<T> is Already a Monad

```csharp
// Task<T> already provides:
// - Async computation (deferred execution)
// - Success case (completed with value)
// - Failure case (faulted with exception)
// - Railway-oriented programming via await (exceptions propagate)

// Instead of:
public Task<Result<Data>> GetDataAsync() { ... }

// Just use:
public async Task<Data> GetDataAsync()
{
    var response = await httpClient.GetAsync(url);  // throws on failure
    return await response.Content.ReadFromJsonAsync<Data>();
}

// Composition works naturally:
public async Task<ProcessedData> ProcessAsync()
{
    var a = await GetAAsync();      // failure propagates
    var b = await GetBAsync(a);     // failure propagates
    var c = await GetCAsync(b);     // failure propagates
    return Combine(a, b, c);        // only reached on success
}

// When you need to handle errors explicitly:
try
{
    var data = await GetDataAsync();
    return Ok(data);
}
catch (HttpRequestException ex)
{
    logger.LogWarning(ex, "Failed to fetch data");
    return NotFound();
}
```

**Why not Result<T>?**

- `Task<T>` is built-in, no custom types needed
- `await` provides natural railway semantics
- Exceptions are already the error channel
- LINQ works with `Task<T>` via async streams
- Less ceremony, more idiomatic C#

---

## Synthesized Guidelines for Aura

### DO ✅

1. **Keep project count low** (4-6 projects max)
   - Foundation, Api, AppHost, ServiceDefaults, optional modules

2. **Use markdown agent definitions** (from hve-hack)
   - Hot-reloadable, human-readable, version-controllable

3. **Implement provider registry pattern** (from hve-hack)
   - Pluggable LLM providers with fallback

4. **Follow coding standards** (from hve-hack)
   - Strongly-typed, enums over strings, `nameof()`, no magic strings

5. **Use extension methods for DI** (avoid birdlet's bloat)
   - `AddAuraFoundation()`, `AddDeveloperModule()`, clean organization

6. **Document decisions with ADRs** (from Owlet)
   - Capture why, not just what

7. **Use Task<T> as the async monad** (simplified from Owlet)
   - `Task<T>` already handles async, success, and failure (via exceptions)
   - No need for custom `Result<T>` - use idiomatic C# async/await
   - Exceptions propagate naturally through the railway

8. **Profile-based module loading** (from birdlet)
   - `AURA_MODULES=developer,research` environment variable

9. **RAG as Foundation service** (from birdlet)
   - Shared indexing, consistent retrieval across modules

10. **Aspire for development** (from all three)
    - One-command startup, observability, container management

### DON'T ❌

1. **Don't create 17 projects** (hve-hack mistake)
   - Resist the urge to split every concern into its own assembly

2. **Don't build complex orchestration** (hve-hack mistake)
   - No IExecutionPlanner, no WorkflowOrchestrator with replanning
   - Let users orchestrate; agents just execute

3. **Don't validate agent output** (hve-hack mistake)
   - AgentOutputValidator was ultimately removed as unnecessary

4. **Don't use Dictionary<string,object>** (hve-hack mistake)
   - Use typed properties, even if it means more code

5. **Don't put everything in Program.cs** (birdlet mistake)
   - Use extension methods, organize by concern

6. **Don't mix languages** (birdlet mistake)
   - Python agents required separate processes, scripts, coordination
   - Stick to .NET for simplicity

7. **Don't build A2A protocol early** (birdlet mistake)
   - Agent-to-agent communication can come later if needed

8. **Don't optimize for installer first** (Owlet focus)
   - Development experience matters more in early phases

9. **Don't add telemetry everywhere** (hve-hack mistake)
   - WorkflowTelemetry.StartPlanning() in every method is noise
   - Add observability where it provides value

10. **Don't build plugin systems** (hve-hack mistake)
    - Plugin discovery and hot-loading adds complexity
    - Static module system is sufficient

---

## Consequences

### Positive

- **Faster development** - Avoid known dead ends
- **Cleaner architecture** - Learn from 3 attempts at the same problem
- **Right-sized complexity** - 4-6 projects, not 17
- **Proven patterns** - Adopt what actually worked
- **Documented decisions** - ADRs prevent re-debating settled issues

### Negative

- **Potential over-correction** - Some "mistakes" might have been right for their context
- **Lost innovation** - Being conservative might miss novel approaches
- **Bias toward simplicity** - Some complexity is necessary

### Mitigations

- This ADR is a guide, not absolute rules
- Revisit decisions if context changes
- New ADRs can supersede old ones

## References

- [hve-hack repository](file:///c:/work/hve-hack) - Reference for patterns to port
- [birdlet repository](file:///c:/work/birdlet) - Reference for RAG patterns
- [bird-constellation repository](file:///c:/work/bird-constellation) - Reference for clean structure
- [ORIGIN-STORY.md](../ORIGIN-STORY.md) - Narrative of the pivot decision
- [hve-hack CODING-STANDARDS.md](file:///c:/work/hve-hack/docs/CODING-STANDARDS.md) - Detailed coding guidelines
