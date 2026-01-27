# Unified Wave Orchestration

**Status:** 📋 Ready for Development
**Priority:** High (pre-1.0 simplification)
**Created:** 2026-01-26
**Updated:** 2026-01-28

## Executive Summary

The current story execution system has accumulated multiple overlapping paradigms that create confusion and maintenance burden. This spec unifies everything into a single, clear execution model.

**Before:** 5 enums, 2 task representations, 2 dispatch targets, complex interactions
**After:** 1 status enum, 1 step entity with waves, 1 execution path, clear visibility

---

## Problem Statement

### Current State Analysis

**1. Too Many Execution Modes**

The `Story` entity has accumulated overlapping fields:

| Field | Purpose | Problem |
|-------|---------|---------|
| `AutomationMode` | Assisted/Autonomous/FullAutonomous | Approval control |
| `DispatchTarget` | CopilotCli/InternalAgents | Two execution engines |
| `GateMode` | AutoProceed/PauseAlways | Gate behavior |
| `CurrentWave` | Wave orchestration | Parallel execution |
| `PatternName` | Pattern-driven | Step generation source |

**Interactions are undocumented.** What happens with `AutomationMode=Autonomous` + `DispatchTarget=CopilotCli` + `GateMode=PauseAlways`?

**2. StoryStep vs StoryTask Duality**

```
StoryStep (DB entity) → runtime conversion → StoryTask (record)
     ↓                                            ↓
  Wave column                              Status tracking
  Persisted                                 In-memory
```

Which is the source of truth? Status lives on both. This causes:
- Conversion bugs
- Stale state
- Unclear ownership

**3. Status Enum Sprawl**

| Enum | Values | Used By |
|------|--------|---------|
| `StoryStatus` | 12 values | Story entity |
| `StoryTaskStatus` | 5 values | StoryTask record |
| `StepStatus` | ~5 values | StoryStep entity |

The extension calculates "effective status" separately from the API. Status is derived differently in different contexts.

**4. Two Dispatch Targets, One Tested**

```csharp
public enum DispatchTarget
{
    CopilotCli,      // ← Primary path, well-tested
    InternalAgents,  // ← Secondary path, less coverage
}
```

Maintaining two execution engines doubles testing burden and creates subtle behavior differences.

**5. No Unified Execution Contract**

There's no single interface abstracting "execute this step":
- `GitHubCopilotDispatcher` for Copilot CLI
- `ReActExecutor` for internal agents
- Different SSE streaming approaches

---

## Design: The Unified Model

### Core Principles

1. **One entity for tasks**: `StoryStep` with `Wave` column (delete `StoryTask`)
2. **One status enum**: Merge step/task status into step
3. **One execution path**: Pick Copilot CLI (delete `InternalAgents` dispatch)
4. **Wave-first visibility**: UI shows waves, not flat step list
5. **Streaming by default**: User sees agent output in real-time

### The Simplified Model

```
Story
├── Status: Created → Analyzing → Analyzed → Planning → Planned → Executing → Completed
├── CurrentWave: int (0 = not started)
├── GateMode: AutoProceed | PauseAlways
│
└── Steps[]
    ├── Wave: int (1, 2, 3...)
    ├── Status: Pending → Running → Completed | Failed | Skipped
    ├── Name, Description, Output, Error
    └── (Steps in same wave run in parallel)
```

### Status Flow

```
Created
   ↓
Analyzing ──► Analyzed
   ↓
Planning ──► Planned
   ↓
Executing (Wave N)
   │
   ├─► all steps complete → GatePending (build/test)
   │                            │
   │                            ├─► pass → next wave (loop) or Completed
   │                            └─► fail → GateFailed
   │
   └─► any step fails → Failed
```

### Deleted Concepts

| Concept | Reason |
|---------|--------|
| `StoryTask` record | Use `StoryStep` only |
| `StoryTaskStatus` enum | Use step status |
| `OrchestratorStatus` enum | Never existed (mentioned in old specs) |
| `DispatchTarget.InternalAgents` | Use Copilot CLI only |
| `TasksJson` column | Steps are the source of truth |
| `AutomationMode` complexity | Simplify to GateMode only |

---

## Implementation Roadmap

### Phase 1: Database Cleanup (Day 1)

**Goal:** Single source of truth for steps

1. **Add `Wave` column to `StoryStep`** (if not already present)
   ```sql
   ALTER TABLE story_steps ADD COLUMN wave INT NOT NULL DEFAULT 1;
   ```

2. **Add `StepStatus` values if missing**
   - Pending, Running, Completed, Failed, Skipped

3. **Remove unused columns from `Story`**
   - Delete `TasksJson` column (not used if steps have waves)

4. **Flatten migrations**
   - Since pre-production, regenerate clean `InitialCreate`

### Phase 2: Remove StoryTask (Day 1-2)

**Goal:** Delete the parallel task abstraction

1. **Update `GitHubCopilotDispatcher`**
   - Accept `StoryStep` directly instead of converting to `StoryTask`
   - Update step status in DB, not in-memory task

2. **Delete `StoryTask.cs`**

3. **Delete `StoryTaskStatus` enum**

4. **Update `StoryService`**
   - Remove task ↔ step conversion logic
   - Operate on steps directly

### Phase 3: Simplify Dispatch (Day 2)

**Goal:** One execution path

1. **Remove `DispatchTarget` enum** (or keep only `CopilotCli`)

2. **Remove `InternalAgents` code path** from orchestrator
   - Internal agents still work for single-step chat
   - Just not for wave-based parallel execution

3. **Simplify `AutomationMode`**
   - Keep for single-step execution (Assisted vs Autonomous)
   - Wave execution always uses Copilot CLI in YOLO mode

### Phase 4: Status Unification (Day 2-3)

**Goal:** One status enum per entity

1. **Finalize `StoryStatus`**
   ```csharp
   public enum StoryStatus
   {
       Created,
       Analyzing,
       Analyzed,
       Planning,
       Planned,
       Executing,
       GatePending,
       GateFailed,
       Completed,
       Failed,
       Cancelled,
   }
   ```

2. **Finalize step status**
   ```csharp
   public enum StepStatus
   {
       Pending,
       Running,
       Completed,
       Failed,
       Skipped,
   }
   ```

3. **Remove "effective status" calculation in extension**
   - API returns authoritative status
   - Extension just displays it

### Phase 5: Wave Visibility in UI (Day 3-4)

**Goal:** Users see wave progress

1. **API response includes wave grouping**
   ```json
   {
     "status": "Executing",
     "currentWave": 2,
     "totalWaves": 4,
     "waves": [
       { "wave": 1, "status": "completed", "steps": [...] },
       { "wave": 2, "status": "running", "steps": [...] }
     ]
   }
   ```

2. **Extension wave view**
   - Group steps by wave visually
   - Show "Wave 2/4 • Running 2 tasks" in story list

3. **Progress indicator**
   - Compute from completed waves / total waves

### Phase 6: Streaming Output (Day 4-5)

**Goal:** User sees agent working

1. **SSE endpoint** `/api/developer/stories/{id}/stream`
   - Streams agent output lines
   - Streams step status changes
   - Streams gate results

2. **Extension streaming panel**
   - "Wall of flying text" showing agent activity
   - Collapsible per-step

3. **Gate pause UI**
   - Show errors on failure
   - "Add Fix Wave" button

---

## API Changes

### GET /api/developer/stories/{id}

Add wave-grouped response:

```json
{
  "id": "...",
  "status": "Executing",
  "currentWave": 2,
  "totalWaves": 4,
  "gateResult": null,
  "steps": [
    { "id": "...", "wave": 1, "status": "Completed", "name": "Setup" },
    { "id": "...", "wave": 2, "status": "Running", "name": "Implement UserService" },
    { "id": "...", "wave": 2, "status": "Pending", "name": "Implement OrderService" }
  ]
}
```

### POST /api/developer/stories/{id}/execute

Simplified behavior:
- Executes next wave (all pending steps in that wave)
- Returns SSE stream of progress
- Runs gate after wave completes
- Auto-proceeds to next wave if gate passes and `GateMode=AutoProceed`

### POST /api/developer/stories/{id}/add-fix-wave

When gate fails:
- Creates new wave with "Fix build errors" step
- Agent context includes the build errors
- Sets status back to Executing

---

## File Changes

### Delete

| File | Reason |
|------|--------|
| `src/Aura.Module.Developer/Data/Entities/StoryTask.cs` | Use StoryStep |

### Modify

| File | Change |
|------|--------|
| `src/Aura.Module.Developer/Data/Entities/Story.cs` | Remove TasksJson, simplify enums |
| `src/Aura.Module.Developer/Data/Entities/StoryStep.cs` | Ensure Wave, finalize Status |
| `src/Aura.Module.Developer/Services/GitHubCopilotDispatcher.cs` | Accept steps directly |
| `src/Aura.Module.Developer/Services/StoryService.cs` | Remove task conversion |
| `src/Aura.Api/Endpoints/DeveloperEndpoints.cs` | Wave-grouped response |
| `extension/src/providers/workflowTreeProvider.ts` | Wave grouping |
| `extension/src/providers/workflowPanelProvider.ts` | Wave UI, streaming |

### New

| File | Purpose |
|------|---------|
| `src/Aura.Api/Endpoints/StoryStreamEndpoints.cs` | SSE streaming |

---

## Success Criteria

- [ ] `StoryTask` deleted, only `StoryStep` exists
- [ ] Story has single `Status` enum with clear transitions
- [ ] Step has single `Status` enum
- [ ] Wave execution uses steps directly (no conversion)
- [ ] Extension shows wave progress ("Wave 2/4")
- [ ] Streaming output visible during execution
- [ ] Gate failures show actionable errors
- [ ] All tests pass with new model

---

## What We're NOT Building

- ❌ Complex approval workflows per step
- ❌ Multiple dispatch targets (just Copilot CLI for waves)
- ❌ Task-level dialogs
- ❌ Mandatory approval gates (just optional pause)

**Philosophy:** Show the user what's happening. Let them interrupt if needed. Don't block with dialogs.

---

## Related

- [Technical Debt Cleanup](upcoming/technical-debt-cleanup.md) - Broader cleanup items
- [Orchestrator Parallel Dispatch](completed/orchestrator-parallel-dispatch.md) - Original implementation
