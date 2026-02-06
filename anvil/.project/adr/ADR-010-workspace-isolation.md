---
title: "ADR-010: Workspace Isolation Strategy"
status: "Accepted"
date: "2026-01-30"
authors: "Anvil Team"
tags: ["architecture", "git", "worktrees", "isolation"]
supersedes: ""
superseded_by: ""
---

# ADR-010: Workspace Isolation Strategy

## Status

Accepted

## Context

Each story execution requires an isolated workspace where code can be generated, built, and tested without interfering with other runs. Key requirements:

- **Isolation**: Multiple story runs must not conflict
- **Reproducibility**: Each run starts from a clean state
- **Debuggability**: Failed runs should be inspectable
- **Concurrency**: Support parallel execution (future)
- **Git integration**: Generated code needs proper version control

Aura already has git worktree support for story execution. The question is: who owns workspace management?

## Decision

**Anvil delegates all workspace/worktree management to Aura.** Workspace isolation is a first-class Aura capability, not an Anvil concern.

### Anvil's Responsibilities

| Responsibility | Description |
|----------------|-------------|
| **Request execution** | Tell Aura: "Execute story X against repo Y" |
| **Receive worktree path** | Aura returns the path where code was generated |
| **Validate output** | Run build/test validation in that worktree |
| **Request cleanup** | Tell Aura to remove worktree on success |
| **Track results** | Store worktree path in results for debugging |

### Anvil Does NOT

- ❌ Create git repositories
- ❌ Manage worktrees directly
- ❌ Clone repos
- ❌ Handle git operations (beyond reading for validation)

### Aura Requirements (Contract)

Anvil requires Aura to provide these capabilities:

| Requirement | API Expectation |
|-------------|-----------------|
| **Worktree creation** | Aura creates isolated worktree per story execution |
| **Path in response** | Execution response includes `worktreePath` field |
| **Multiple concurrent** | Must support multiple worktrees for same base repo |
| **Cleanup endpoint** | `DELETE /api/developer/worktrees/{id}` or similar |
| **Worktree listing** | `GET /api/developer/worktrees` for debugging/cleanup |

### Expected API Flow

```
1. Anvil → Aura: POST /api/developer/workflows
   Body: { story content, targetRepository: "https://..." }

2. Aura creates worktree, executes story steps

3. Aura → Anvil: Response includes:
   {
     "id": "workflow-123",
     "worktreePath": "/path/to/.worktrees/workflow-123-abc",
     "status": "completed",
     ...
   }

4. Anvil validates: build, tests, expected files in worktreePath

5. On success: Anvil → Aura: DELETE /api/developer/worktrees/workflow-123
   On failure: Preserve for debugging, log path in results
```

### Worktree Lifecycle

```
┌─────────────────┐
│  Anvil: Start   │
│  Story Run      │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Aura: Create   │
│  Worktree       │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Aura: Execute  │
│  Story Steps    │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Anvil: Validate│
│  (build/test)   │
└────────┬────────┘
         │
    ┌────┴────┐
    ▼         ▼
┌───────┐ ┌───────┐
│Success│ │Failure│
└───┬───┘ └───┬───┘
    │         │
    ▼         ▼
┌───────┐ ┌───────┐
│Cleanup│ │Preserve│
│Request│ │for Debug│
└───────┘ └───────┘
```

### Configuration

Anvil configuration for workspace-related settings:

```json
{
  "Anvil": {
    "PreserveFailedWorkspaces": true,
    "WorkspaceCleanupDays": 7
  }
}
```

### Cleanup Commands

```bash
# List all worktrees (via Aura API)
anvil worktrees list

# Cleanup old worktrees
anvil worktrees cleanup --older-than 7d

# Cleanup specific worktree
anvil worktrees remove workflow-123
```

## Consequences

**Positive**

- **POS-001**: Anvil stays simple—no git operations
- **POS-002**: Single source of truth for worktree management (Aura)
- **POS-003**: Aura's worktree logic is tested via Anvil
- **POS-004**: Workspace isolation is a proper Aura feature, not a test hack
- **POS-005**: Supports future concurrency (Aura handles conflicts)

**Negative**

- **NEG-001**: Anvil depends on Aura's worktree API being complete
- **NEG-002**: Aura must expose worktree path in API responses
- **NEG-003**: Cleanup coordination requires API calls

## Alternatives Considered

### Alternative 1: Anvil Manages Worktrees

- **Description**: Anvil creates worktrees, passes path to Aura
- **Rejection Reason**: Duplicates logic, Aura already has this capability, violates single responsibility

### Alternative 2: Temp Folders (No Git)

- **Description**: Use `Path.GetTempPath()` for each run
- **Rejection Reason**: Loses git history, can't test brownfield scenarios properly, no worktree isolation

### Alternative 3: Docker Containers

- **Description**: Each story runs in isolated container
- **Rejection Reason**: Heavy, complex orchestration, VS Code testing doesn't work well in containers

## Implementation Notes

- **IMP-001**: Aura API must return `worktreePath` in workflow responses
- **IMP-002**: Aura must implement worktree cleanup endpoint
- **IMP-003**: Anvil stores worktree path in `StoryResult` for debugging
- **IMP-004**: Failed worktrees preserved by default (configurable)
- **IMP-005**: Document Aura worktree API in Anvil's handoff requirements

## Aura API Requirements (For Aura Team)

This ADR establishes requirements that Aura must fulfill:

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/developer/workflows` | POST | Must accept `targetRepository` and return `worktreePath` |
| `/api/developer/workflows/{id}` | GET | Must include `worktreePath` in response |
| `/api/developer/worktrees` | GET | List all worktrees for cleanup |
| `/api/developer/worktrees/{id}` | DELETE | Remove specific worktree |

## References

- [Git Worktrees Documentation](https://git-scm.com/docs/git-worktree)
- [ADR-008: Story Source Strategy](ADR-008-story-source-strategy.md)
- [Aura Workflow API](../../docs/aura-api.md)
