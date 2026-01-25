# Unified Wave Orchestration with Progress Visibility

**Status:** 🔄 Design  
**Author:** Copilot  
**Created:** 2026-01-26

## Problem Statement

**Users think the system is hung** because they can't see what's happening.

Current issues:
1. **No wave visibility** - Extension shows steps but not the parallel wave structure
2. **Status confusion** - Two status enums, user sees "Planned" when it's actually "Running"
3. **No progress feedback** - When agent is working, UI is static
4. **Two code paths** - `StoryStep` (DB) vs `StoryTask` (JSON) causes maintenance burden

## Proposed Solution

**Unify on waves with real-time progress visibility.** Keep it simple - focus on feedback, not formal approval gates.

---

## Design Decisions

### 1. Nuke `StoryTask` JSON - Use `StoryStep` Only

Add `Wave` column to `StoryStep`. Delete the JSON blob approach entirely.

```csharp
public class StoryStep
{
    // Existing...
    public int Order { get; set; }
    
    // NEW: Wave support
    public int Wave { get; set; } = 1;  // Steps in same wave run in parallel
}
```

**Migration:**
- Add `Wave` column to `StoryStep` table (default 1 = sequential)
- Delete `TasksJson` column from `Story`
- Delete `StoryTask` record and `StoryTaskStatus` enum
- Delete `OrchestratorStatus` enum (use `StoryStatus` only)

### 2. Single Status Enum

```csharp
public enum StoryStatus
{
    Created,
    Analyzing,
    Analyzed,
    Planning,
    Planned,
    
    Executing,      // Wave is running (agents working)
    GatePending,    // Build/test gate running
    GateFailed,     // Gate failed, user can see errors
    
    Completed,
    Failed,
    Cancelled,
}
```

Remove `OrchestratorStatus` entirely.

### 3. Progress Visibility (The Real Fix)

**What users need to see:**

| State | UI Shows |
|-------|----------|
| Executing | "Wave 2/4 • Running 2 of 3 tasks..." |
| Task running | "🔄 Implementing UserService..." (animated) |
| Task done | "✓ Implementing UserService" |
| Gate running | "🔨 Building..." |
| Gate failed | "❌ Build failed - 3 errors" + error list |
| All done | "✓ Completed in 4 waves" |

**Extension changes:**
1. **Streaming agent output** - "Wall of flying text" from agents (live activity)
2. **Wave progress bar** - Visual indicator of wave progress
3. **Live step status** - Show which steps are running NOW
4. **Gate interruption** - Pause at gates for human validation (optional)

### 4. Agent Output Streaming

Show live agent output so user knows it's working:

```
┌─────────────────────────────────────────────────────────────────┐
│ Wave 2/4 • Implementing UserService                            │
├─────────────────────────────────────────────────────────────────┤
│ > Analyzing class structure...                                  │
│ > Found 3 methods to implement                                  │
│ > Calling aura.generate for GetUserById...                      │
│ > ✓ Added GetUserById method                                    │
│ > Calling aura.generate for CreateUser...                       │
│ > ✓ Added CreateUser method                                     │
│ > Calling roslyn.validate_compilation...                        │
│ > Build successful                                              │
│ > Calling git.commit...                                         │
│ █                                                               │
├─────────────────────────────────────────────────────────────────┤
│ [Pause] [Cancel]                                                │
└─────────────────────────────────────────────────────────────────┘
```

**Implementation options:**
- **SSE (Server-Sent Events)** - Extension subscribes to `/api/developer/stories/{id}/stream`
- **Polling** - Extension polls for new output lines (simpler, less real-time)
- **WebSocket** - Full duplex, but more complex

**Recommendation:** SSE - good balance of real-time and simplicity.

### 5. Gate Interruption (Optional Human Validation)

Gates pause execution and let user validate before continuing:

```
┌─────────────────────────────────────────────────────────────────┐
│ ⏸️ Gate: Build Check                                            │
├─────────────────────────────────────────────────────────────────┤
│ Wave 2 completed. Build gate running...                        │
│                                                                 │
│ ✓ Build succeeded (0 errors, 2 warnings)                        │
│                                                                 │
│ Warnings:                                                       │
│   CS0168: Variable 'ex' declared but never used                 │
│   CS8618: Non-nullable property not initialized                 │
│                                                                 │
├─────────────────────────────────────────────────────────────────┤
│ [Continue to Wave 3]  [Review Changes]  [Cancel]                │
└─────────────────────────────────────────────────────────────────┘
```

**Gate behavior (configurable per story):**

| Mode | Behavior |
|------|----------|
| `AutoProceed` | Gate passes → auto-continue to next wave |
| `PauseOnSuccess` | Gate passes → pause for human to review before continuing |
| `PauseOnFailure` | Gate fails → pause (default, always true) |

```csharp
public class Story
{
    // Existing...
    public GateMode GateMode { get; set; } = GateMode.AutoProceed;
}

public enum GateMode
{
    AutoProceed,      // Only pause on failure
    PauseAlways,      // Pause at every gate for validation
}
```

### 6. Simplified Flow

```
Planned
   │
   ▼
Executing (Wave 1)  ──► show "Wave 1/N • Running X tasks"
   │
   ├─ all tasks complete
   ▼
GatePending  ──► show "🔨 Running build gate..."
   │
   ├─ gate passes ──► next wave (loop) or Completed
   │
   └─ gate fails ──► GateFailed
                       │
                       └─► show errors, user clicks "Add Fix Wave" or "Cancel"
```

No formal approval gates. Just visibility and manual intervention when things break.

---

## API Changes

### Modified Endpoints

**GET /api/developer/stories/{id}**
```json
{
  "id": "...",
  "status": "Executing",
  "currentWave": 2,
  "totalWaves": 4,
  "steps": [
    { "id": "...", "wave": 1, "status": "Completed", "name": "Setup project" },
    { "id": "...", "wave": 2, "status": "Running", "name": "Implement UserService" },
    { "id": "...", "wave": 2, "status": "Pending", "name": "Implement OrderService" }
  ],
  "gateResult": null
}
```

**When gate fails:**
```json
{
  "status": "GateFailed",
  "currentWave": 2,
  "gateResult": {
    "type": "build",
    "passed": false,
    "errors": ["CS0103: The name 'foo' does not exist..."],
    "errorCount": 3
  }
}
```

### New Endpoint

**POST /api/developer/stories/{id}/add-fix-wave**
- Creates a new wave with a single "Fix build errors" step
- Sets status back to Executing
- Agent gets the build errors as context

---

## Extension UX

### Story List View

```
┌─────────────────────────────────────────────┐
│ 📋 Add health check timestamp              │
│    Wave 2/4 • Running 2 tasks...           │
│    ████████░░░░░░░░  50%                   │
└─────────────────────────────────────────────┘
```

### Story Detail - Wave View

```
Wave 1 ✓
├─ ✓ Setup project structure
└─ ✓ Add dependencies

Wave 2 (Running)
├─ 🔄 Implementing UserService...
├─ 🔄 Implementing OrderService...
└─ ○ Add unit tests

Wave 3 (Pending)
└─ ○ Integration tests
```

### Gate Failed View

```
┌─────────────────────────────────────────────┐
│ ❌ Build Failed - 3 errors                  │
├─────────────────────────────────────────────┤
│ CS0103: 'HealthResponse' does not exist    │
│   > Program.cs line 42                      │
│                                             │
│ CS0029: Cannot convert DateTime to string   │
│   > Program.cs line 45                      │
├─────────────────────────────────────────────┤
│  [Add Fix Wave]  [View Full Output] [Cancel]│
└─────────────────────────────────────────────┘
```

---

## Implementation Steps

### Backend
1. [ ] Add `Wave` column to `StoryStep` (migration)
2. [ ] Add `GateMode` column to `Story` (migration)
3. [ ] Delete `TasksJson`, `OrchestratorStatus`, `StoryTask`
4. [ ] Simplify `StoryStatus` enum (add GatePending, GateFailed)
5. [ ] Update orchestrator to use `StoryStep.Wave`
6. [ ] Add `gateResult` to API response
7. [ ] Add `/add-fix-wave` endpoint
8. [ ] Add SSE endpoint `/stories/{id}/stream` for live output

### Extension
9. [ ] Add wave grouping to step display
10. [ ] Add progress bar to story list
11. [ ] Add streaming output panel ("wall of flying text")
12. [ ] Add gate pause UI with Continue/Review/Cancel
13. [ ] Add gate failed UI with errors + Fix Wave button

---

## What We're NOT Building

- ❌ Complex approval workflows
- ❌ Task-level approval dialogs
- ❌ Mandatory approval gates
- ❌ Per-wave approval prompts

**Philosophy:** Show the user what's happening (streaming output). Let them interrupt at gates if they want. Don't block them with dialogs.

---

## Related Documents

- [ADR-009: Lessons from Previous Attempts](.project/adr/009-lessons-from-previous-attempts.md)
- [Orchestrator Parallel Dispatch](.project/features/upcoming/orchestrator-parallel-dispatch.md)
