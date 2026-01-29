# Technical Debt Cleanup (Pre-1.0)

**Status:** 📋 Planned
**Priority:** High
**Created:** 2026-01-28

## Overview

This document catalogs technical debt discovered during codebase analysis. Since we're pre-production, we can clean this up before shipping 1.0. Items are organized by category and estimated effort.

---

## 1. Story/Workflow Execution Complexity

**Effort:** 5 days | **Priority:** Critical

See [Unified Wave Orchestration](unified-wave-orchestration.md) for detailed spec.

### Summary

| Delete | Reason |
|--------|--------|
| `StoryTask` record | Duplicate of StoryStep |
| `StoryTaskStatus` enum | Merged into StepStatus |
| `DispatchTarget.InternalAgents` | Maintain one execution path |
| `TasksJson` column | Steps are source of truth |
| "Effective status" logic in extension | API is authoritative |

### Files Affected
- `src/Aura.Module.Developer/Data/Entities/StoryTask.cs` (delete)
- `src/Aura.Module.Developer/Data/Entities/Story.cs` (simplify)
- `src/Aura.Module.Developer/Services/GitHubCopilotDispatcher.cs` (use steps)
- `extension/src/services/auraApiService.ts` (remove effective status)

---

## 2. Naming Inconsistency: Story vs Workflow

**Status:** ✅ Complete (2026-01-29)

Standardized on "Story" terminology throughout the extension:

- [x] Rename `workflowTreeProvider.ts` → `storyTreeProvider.ts`
- [x] Rename `workflowPanelProvider.ts` → `storyPanelProvider.ts`
- [x] Update extension `package.json` view titles: "Workflows" → "Stories"
- [x] Rename API service types: `Workflow` → `Story`, `WorkflowStep` → `StoryStep`
- [x] Rename API service methods: `createWorkflow` → `createStory`, `sendWorkflowChat` → `sendStoryChat`, etc.
- [x] Update UI strings in extension.ts, learn-more.md, uiTest.ts
- [ ] Update copyright headers in C# files (deferred - low priority)
- [ ] Rename internal C# variables `_workflowService` → `_storyService` (deferred - internal only)

---

## 3. Large Files to Split

**Effort:** 2-3 days | **Priority:** Medium

### McpHandler.cs (~4700 lines)

Split into tool classes:

```
src/Aura.Api/Mcp/
├── McpHandler.cs           # Routing only (~200 lines)
├── Tools/
│   ├── SearchTool.cs       # aura_search
│   ├── NavigateTool.cs     # aura_navigate
│   ├── InspectTool.cs      # aura_inspect
│   ├── RefactorTool.cs     # aura_refactor
│   ├── GenerateTool.cs     # aura_generate
│   ├── ValidateTool.cs     # aura_validate
│   ├── WorkflowTool.cs     # aura_workflow
│   ├── PatternTool.cs      # aura_pattern
│   ├── WorkspaceTool.cs    # aura_workspace
│   ├── EditTool.cs         # aura_edit
│   ├── TreeTool.cs         # aura_tree, aura_get_node
│   └── DocsTool.cs         # aura_docs
```

### DeveloperEndpoints.cs (~1300 lines)

Already partially split. Further split:

```
src/Aura.Api/Endpoints/Developer/
├── StoryEndpoints.cs       # CRUD
├── StoryExecutionEndpoints.cs  # analyze, plan, execute
├── StepEndpoints.cs        # Step operations
├── IssueEndpoints.cs       # GitHub integration
```

### extension.ts (~1500 lines)

Split into:

```
extension/src/
├── extension.ts            # Activation, command registration
├── activation.ts           # Setup logic
├── commands/
│   ├── storyCommands.ts
│   ├── indexCommands.ts
│   └── chatCommands.ts
├── state.ts                # Context management
```

---

## 4. Database Schema Cleanup

**Effort:** 1 day | **Priority:** High (do before production)

### Column Naming

Mix of PascalCase and snake_case:

| Current | Should Be |
|---------|-----------|
| `CurrentWave` | `current_wave` |
| `GitBranch` | `git_branch` |
| `OrchestratorStatus` | (delete) |

### Migration Cleanup

Since we have zero production users:

1. Delete all existing migrations
2. Create single clean `InitialCreate` migration per context
3. Document the final schema

### Table Naming

| Current | Should Be |
|---------|-----------|
| `workflows` | `stories` |
| `workflow_steps` | `story_steps` |

---

## 5. Dead Code Removal

**Effort:** 1 day | **Priority:** Low

### Guardian System

Partially implemented, not actively used:

- [ ] Complete implementation, or
- [ ] Remove infrastructure: `GuardianExecutor`, `GuardianScheduler`, YAML files

### StoryTask Entity

If unified wave orchestration proceeds:

- [ ] Delete `StoryTask.cs`
- [ ] Delete `StoryTaskStatus` enum

### Dispatch Target Abstraction

If we standardize on Copilot CLI:

- [ ] Remove `DispatchTarget.InternalAgents`
- [ ] Simplify dispatcher to single path

---

## 6. API Inconsistencies

**Effort:** 2 days | **Priority:** Medium

### Path Parameter vs Query Parameter

Some endpoints use path params, some use query:

```
DELETE /api/workspace?path=...     ← query
DELETE /api/code-graph/{path}      ← path (but URL-encoded)
GET /api/rag/stats                 ← no path needed
```

**Fix:** Standardize on path params with proper encoding.

### Overlapping Endpoints

| Endpoint | Overlaps With |
|----------|---------------|
| `DELETE /api/rag` | `DELETE /api/workspace?path=` |
| `POST /api/rag/index` | `POST /api/workspace/onboard` |

**Fix:** See [API Harmonization](upcoming/api-review-harmonization.md) for consolidation plan.

---

## 7. Extension State Management

**Effort:** 1 day | **Priority:** Low

### Problem

Extension state is scattered:
- Global variables
- VS Code context
- Service instances

### Fix

Create simple state container:

```typescript
interface AuraState {
    currentStory: Story | null;
    healthStatus: HealthStatus;
    onboardingComplete: boolean;
    workspacePath: string | null;
}

class StateManager {
    private state: AuraState;
    onChange(callback: (state: AuraState) => void): void;
}
```

---

## 8. Missing Abstractions

**Effort:** 2 days | **Priority:** Medium

### API Contracts Project

Create `Aura.Api.Contracts` with shared types:

```csharp
// Request/response records
public record CreateStoryRequest(string Title, string? Description);
public record StoryResponse(Guid Id, string Title, StoryStatus Status, ...);

// Shared enums
public enum StoryStatus { ... }
```

Benefits:
- Compile-time checking between API and tests
- Generate TypeScript types for extension
- Complete OpenAPI spec

### Event System

Components communicate through direct method calls. Add lightweight events:

```csharp
public interface IAuraEvents
{
    IObservable<IndexingCompleted> OnIndexingCompleted { get; }
    IObservable<StoryStatusChanged> OnStoryStatusChanged { get; }
    IObservable<StepCompleted> OnStepCompleted { get; }
}
```

---

## 9. Test Coverage Gaps

**Effort:** 3-5 days | **Priority:** Medium

### Missing Coverage

| Area | Current | Target |
|------|---------|--------|
| MCP Tools | ~20% | 80% |
| API Endpoints | ~30% | 80% |
| Story Execution | ~40% | 90% |
| Extension | ~10% | 50% |

### Add Integration Tests

- [ ] Story lifecycle: create → analyze → plan → execute → complete
- [ ] Wave execution with gates
- [ ] Gate failure recovery
- [ ] Error handling paths

---

## 10. Configuration Complexity

**Effort:** 1 day | **Priority:** Low

### Problem

Configuration from 7+ sources:
- `appsettings.json`
- Environment variables
- Aspire injection
- VS Code settings
- Agent markdown
- Guardian YAML
- Pattern markdown

### Fix

1. Document configuration precedence
2. Add `/config/dump` debug endpoint
3. Validate configuration on startup with clear errors

---

## Execution Order

### Week 1: Critical (Pre-1.0 Blockers)

1. **Database Schema Cleanup** (1 day)
   - Flatten migrations
   - Rename tables/columns
   - Single source of truth

2. **Unified Wave Orchestration** (5 days)
   - Delete StoryTask
   - Simplify status enums
   - One dispatch path

### Week 2: Quality

3. **File Splits** (3 days)
   - McpHandler → tool classes
   - DeveloperEndpoints → domain files
   - extension.ts → modules

4. **Naming Consistency** (1 day)
   - Story vs Workflow

5. **Dead Code Removal** (1 day)

### Week 3: Polish

6. **API Harmonization** (2 days)

7. **Test Coverage** (5 days)

### Defer to Post-1.0

- Event system
- API contracts project
- Extension state management
- Configuration documentation

---

## How to Use This Document

1. **Before starting work:** Check if the file you're touching is listed here
2. **During code review:** Flag additions to files marked for deletion
3. **Sprint planning:** Pull items from Week 1/2/3 sections
4. **After each item:** Update checkbox and add completion date

---

## Related Documents

- [Unified Wave Orchestration](upcoming/unified-wave-orchestration.md) - Story execution cleanup
- [API Harmonization](upcoming/api-review-harmonization.md) - API consistency
- [Database Schema Cleanup](upcoming/database-schema-cleanup.md) - Schema normalization
- [Code Review Findings](../feature-parity-spec/08-code-review.md) - Original analysis
