# Code Review: Technical Debt and Simplification Opportunities

**Status:** Reference Documentation
**Created:** 2026-01-28

This document identifies areas of technical debt, inconsistencies, and opportunities for simplification discovered during the codebase analysis. These should be addressed during a rewrite.

---

## 1. High-Priority Simplifications

### 1.1 Dual Naming: "Story" vs "Workflow"

**Problem:** The codebase uses both "Story" and "Workflow" terminology inconsistently:
- Entity: `Story`, `StoryStep`, `StoryStatus`
- API paths: `/api/developer/stories/...`
- UI labels: "Workflows" view
- Extension: `WorkflowTreeProvider`, `WorkflowPanelProvider`
- Documentation: Mixed usage

**Impact:** Confusing for developers, creates mental overhead.

**Recommendation:** Standardize on "Story" throughout:
- Rename extension providers: `StoryTreeProvider`, `StoryPanelProvider`
- Rename UI labels: "Stories" view
- Update all documentation

**Files Affected:**
- `extension/src/providers/workflowTreeProvider.ts`
- `extension/src/providers/workflowPanelProvider.ts`
- `extension/package.json` (view names, commands)

### 1.2 McpHandler is 4775 Lines

**Problem:** `McpHandler.cs` contains all MCP tool implementations in a single massive file.

**Impact:** 
- Hard to navigate and maintain
- Long compilation times for changes
- All tools tightly coupled

**Recommendation:** Split into separate tool classes:
```
src/Aura.Api/Mcp/
├── McpHandler.cs           # Routing only (~200 lines)
├── Tools/
│   ├── SearchTool.cs
│   ├── NavigateTool.cs
│   ├── InspectTool.cs
│   ├── RefactorTool.cs
│   ├── GenerateTool.cs
│   ├── ValidateTool.cs
│   ├── WorkflowTool.cs
│   ├── PatternTool.cs
│   ├── WorkspaceTool.cs
│   ├── EditTool.cs
│   ├── TreeTool.cs
│   └── DocsTool.cs
```

### 1.3 DeveloperEndpoints is 1344 Lines

**Problem:** Single file contains all developer API endpoints.

**Impact:** Similar to McpHandler - hard to navigate.

**Recommendation:** Already partially addressed by endpoint files, but could be further split:
```
src/Aura.Api/Endpoints/Developer/
├── StoryEndpoints.cs       # CRUD
├── StoryLifecycleEndpoints.cs  # analyze, plan, execute
├── StepEndpoints.cs        # Step operations
├── IssueEndpoints.cs       # GitHub integration
```

---

## 2. Architecture Inconsistencies

### 2.1 Mixed Service Lifetimes

**Problem:** Inconsistent DI lifetime usage across modules:
- Most services: `AddSingleton`
- Some services: `AddScoped` (especially those needing DbContext)
- No clear pattern for when to use which

**Impact:** Potential memory leaks, confusion about thread safety.

**Recommendation:** Establish clear guidelines:
- **Singleton:** Stateless services, caches, registries
- **Scoped:** Services that use DbContext, per-request state
- **Transient:** Short-lived, lightweight objects

### 2.2 JSON Serialization Inconsistency

**Problem:** Multiple JSON serialization approaches:
- EF Core stores JSON as strings with manual serialization
- API returns anonymous objects
- Some places use `JsonSerializer.Serialize()`
- Different naming policies in different places

**Recommendation:** Standardize on:
- `camelCase` for all API responses
- Strongly-typed DTOs for API contracts
- JSON columns with proper EF Core mapping

### 2.3 Path Handling Inconsistency

**Problem:** Despite `PathNormalizer`, paths are handled inconsistently:
- Some places lowercase, some don't
- Some use forward slashes, some backslashes
- Extension normalizes separately from API

**Recommendation:** 
- Always normalize at API boundary
- Store normalized paths in database
- Document the normalization rules clearly

---

## 3. Code Duplication

### 3.1 Status Calculation in Multiple Places

**Problem:** Story status is calculated in:
- `DeveloperEndpoints.cs` (`GetEffectiveStatus` method)
- `StoryService.cs`
- Extension `auraApiService.ts`

**Recommendation:** Move status calculation to a single location (entity method or service).

### 3.2 Tool Registration Pattern

**Problem:** Each tool requires boilerplate registration:
```csharp
var listProjects = services.GetRequiredService<ListProjectsTool>();
toolRegistry.RegisterTool<ListProjectsInput, ListProjectsOutput>(listProjects);
```

**Recommendation:** Auto-register tools via reflection or source generators:
```csharp
[AuraTool]
public class ListProjectsTool : ITool<ListProjectsInput, ListProjectsOutput>
```

### 3.3 Ingestor Selection Logic

**Problem:** Multiple places check file extensions to select ingestors.

**Recommendation:** Centralize in `IngestorRegistry.GetIngestor(filePath)`.

---

## 4. Missing Abstractions

### 4.1 No Clear API Contracts Project

**Problem:** API request/response types are defined inline or as anonymous objects.

**Impact:** 
- Extension has to duplicate type definitions
- No compile-time checking between API and clients
- OpenAPI generation is incomplete

**Recommendation:** Create `Aura.Api.Contracts` project with:
- Request records
- Response records
- Shared enums

### 4.2 No Event System

**Problem:** Components communicate through direct method calls. No publish/subscribe for events like:
- Indexing complete
- Story status changed
- Agent execution finished

**Impact:** Tight coupling, hard to add new subscribers.

**Recommendation:** Add lightweight event bus:
```csharp
public interface IAuraEvents
{
    IObservable<IndexingCompleted> IndexingCompleted { get; }
    IObservable<StoryStatusChanged> StoryStatusChanged { get; }
}
```

---

## 5. Testing Gaps

### 5.1 Integration Test Coverage

**Problem:** Integration tests exist but coverage is uneven:
- Good: Agent framework, RAG pipeline
- Weak: MCP tools, API endpoints
- Missing: End-to-end workflow tests

**Recommendation:** Add integration test suites for:
- Each MCP tool operation
- Story lifecycle (create → analyze → plan → execute → complete)
- Error handling paths

### 5.2 No Contract Tests

**Problem:** No tests verify API contracts between:
- API and Extension
- API and MCP clients

**Recommendation:** Add consumer-driven contract tests.

---

## 6. Dead Code / Unused Features

### 6.1 Guardian System

**Status:** Partially implemented, not actively used.

**Problem:** 
- Guardian definitions exist in YAML
- Infrastructure exists (`GuardianExecutor`, `GuardianScheduler`)
- No actual production usage

**Recommendation:** Either complete the feature or remove the infrastructure.

### 6.2 Multiple Dispatch Targets

**Problem:** `DispatchTarget` enum supports `CopilotCli` and `InternalAgents`, but only `CopilotCli` is fully implemented.

**Recommendation:** Document which is the primary path, consider deprecating the other.

### 6.3 StoryTask Entity

**Problem:** `StoryTask` exists but appears unused - stories use steps directly.

**Recommendation:** Remove if not needed, or document the intended use.

---

## 7. Naming Inconsistencies

### 7.1 Agent vs Agent Definition vs Agent Metadata

**Problem:** Confusing hierarchy:
- `IAgent` - runtime agent
- `AgentDefinition` - parsed markdown
- `AgentMetadata` - subset for routing
- `AgentInfo` - API response type

**Recommendation:** Clarify naming:
- `Agent` - runtime instance
- `AgentSpec` - loaded definition
- `AgentMetadata` - keep as-is
- `AgentSummary` - API response

### 7.2 Rag vs CodeGraph

**Problem:** Two separate indexing systems with similar operations:
- `IRagService` for text search
- `ICodeGraphService` for structural queries

**Recommendation:** Consider unified interface with different backends, or clearer separation.

---

## 8. Extension Issues

### 8.1 Large `extension.ts` File

**Problem:** Main extension file is 1479 lines with mixed responsibilities.

**Recommendation:** Extract to:
- `activation.ts` - activation logic
- `commands/` - command handlers
- `state.ts` - context management

### 8.2 No State Management

**Problem:** Extension state is managed through global variables and VS Code context.

**Recommendation:** Consider simple state container for:
- Current story
- Health status
- Onboarding state

---

## 9. Configuration Complexity

### 9.1 Multiple Configuration Sources

**Problem:** Configuration comes from:
- `appsettings.json`
- Environment variables
- Aspire injection
- VS Code settings
- Agent markdown files
- Guardian YAML files
- Pattern markdown files

**Impact:** Hard to understand what configuration is active.

**Recommendation:** Document configuration precedence clearly, provide configuration dump endpoint.

---

## 10. Dependency Concerns

### 10.1 Heavy Roslyn Dependencies

**Problem:** Full Roslyn workspace loading for every operation.

**Impact:** High memory usage, slow startup.

**Recommendation:** 
- Lazy workspace loading
- Workspace caching with invalidation
- Consider alternative for simple operations

### 10.2 Tree-sitter Binary Dependencies

**Problem:** Tree-sitter requires native binaries per platform/architecture.

**Impact:** Complex build process, potential runtime failures.

**Recommendation:** Document clearly, test on all target platforms.

---

## 11. Recommendations for Rewrite

### 11.1 Start Fresh With

1. **Clear module boundaries** - Foundation, Developer, API as separate projects with minimal coupling
2. **Unified naming** - Pick "Story" or "Workflow" and use everywhere
3. **API contracts project** - Shared types for API, Extension, tests
4. **Event-driven architecture** - Loose coupling between components
5. **Smaller files** - Target <500 lines per file

### 11.2 Keep From Current Implementation

1. **Agent definition format** - Markdown with YAML frontmatter is intuitive
2. **ReAct execution pattern** - Works well for tool-using agents
3. **Prompt template system** - Handlebars is flexible
4. **Pattern system** - Good abstraction for multi-step operations
5. **Worktree isolation** - Excellent for parallel work

### 11.3 Reconsider

1. **Single Program.cs for endpoints** - Split into endpoint files
2. **Singleton McpHandler** - Extract tool implementations
3. **Multiple indexing systems** - Consider unification
4. **Guardian system** - Complete or remove
5. **Dispatch target abstraction** - May be premature

---

## 12. Migration Path

If incrementally refactoring rather than rewriting:

### Phase 1: Naming (Low Risk)
- Rename Workflow → Story in extension
- Update documentation

### Phase 2: Structure (Medium Risk)
- Split McpHandler into tool classes
- Split DeveloperEndpoints
- Extract API contracts

### Phase 3: Architecture (Higher Risk)
- Add event system
- Unify path handling
- Improve DI lifetime consistency
