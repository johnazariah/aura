# Orchestrator Parallel Dispatch

**Status:** 🚧 In Progress
**Started:** 2026-01-24

## Overview

Build Aura's orchestrator that dispatches parallelizable tasks to GitHub Copilot CLI agents running in YOLO mode. This enables efficient parallel execution of independent development tasks within a story.

## User Flow

1. `New-AuraStory` → creates story + git worktree
2. `Start-AuraStoryAnalysis` → analyzes codebase context
3. `Start-AuraStoryDecompose` → LLM breaks story into tasks with dependencies
4. `Start-AuraStory` → dispatches to N × GH Copilot CLI agents (YOLO mode)
5. Quality gates (build/test) run automatically between waves
6. `Complete-AuraStory` → merge ceremony (verification, commit, PR)

## Implementation Status

### ✅ Completed

#### Data Model Changes

Added orchestration fields to `Story` entity:

```csharp
// Orchestration fields
public string? TasksJson { get; set; }           // JSON array of StoryTask
public OrchestratorStatus OrchestratorStatus { get; set; }
public int CurrentWave { get; set; }             // 0 = not started
public int MaxParallelism { get; set; }          // Default: 4
```

New `StoryTask` record type in `Data/Entities/StoryTask.cs`:

```csharp
public record StoryTask(
    string Id,                    // e.g., "task-1"
    string Title,                 // e.g., "Add validation to UserService"
    string Description,           // Detailed prompt for the agent
    int Wave,                     // Execution wave (1, 2, 3...)
    string[] DependsOn,           // Task IDs this depends on
    StoryTaskStatus Status,       // Pending, Running, Completed, Failed
    string? AgentSessionId,       // GH Copilot CLI session ID
    string? Output,               // Agent output/result
    string? Error,                // Error message if failed
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt
);

public enum StoryTaskStatus { Pending, Running, Completed, Failed, Skipped }

public enum OrchestratorStatus
{
    NotDecomposed,    // Story created but not decomposed
    Decomposed,       // Tasks created, ready to run
    Running,          // Currently executing tasks
    WaitingForGate,   // Waiting for build/test gate
    Completed,        // All tasks done
    Failed            // Unrecoverable failure
}
```

#### API Endpoints

| Endpoint | Description |
|----------|-------------|
| `POST /api/developer/stories/{id}/decompose` | Decompose story into parallel tasks |
| `POST /api/developer/stories/{id}/run` | Execute next wave of tasks |
| `GET /api/developer/stories/{id}/orchestrator-status` | Get task progress |

#### Services

- `IGitHubCopilotDispatcher` / `GitHubCopilotDispatcher` - Dispatches tasks to GH Copilot CLI
- `IQualityGateService` / `QualityGateService` - Runs build/test gates between waves
- Extended `IStoryService` with `DecomposeAsync`, `RunAsync`, `GetOrchestratorStatusAsync`

#### PowerShell Module (`scripts/Aura-Story.psm1`)

| Function | Description |
|----------|-------------|
| `New-AuraStory` | Create story with worktree |
| `Get-AuraStory` | Get story by ID |
| `Get-AuraStories` | List all stories |
| `Start-AuraStoryAnalysis` | Analyze codebase context |
| `Start-AuraStoryDecompose` | Decompose into tasks |
| `Start-AuraStory` | Run next wave |
| `Get-AuraStoryStatus` | Get orchestrator status |
| `Invoke-AuraStoryFull` | Run complete workflow |
| `Complete-AuraStory` | Merge ceremony |
| `Remove-AuraStoryWorktree` | Cleanup worktree |

## Usage Example

```powershell
Import-Module .\scripts\Aura-Story.psm1

# Create a story with worktree
$story = New-AuraStory -Title "Add user validation" -RepositoryPath "C:\repos\myapp"

# Analyze codebase
Start-AuraStoryAnalysis -Id $story.id

# Decompose into parallel tasks
$tasks = Start-AuraStoryDecompose -Id $story.id -MaxParallelism 4

# Run all waves (automatically runs quality gates)
Invoke-AuraStoryFull -Id $story.id

# Or run wave by wave manually
do {
    $result = Start-AuraStory -Id $story.id
} while (-not $result.isComplete -and -not $result.error)

# Complete and create PR
Complete-AuraStory -Id $story.id -CreatePullRequest $true

# After PR is merged, cleanup
Remove-AuraStoryWorktree -Id $story.id
```

## Week 1 MVP Scope
    Failed,
    Skipped
}

public enum OrchestratorStatus
{
    NotDecomposed,        // Story created but not decomposed
    Decomposed,           // Tasks created, ready to run
    Running,              // Currently executing tasks
    WaitingForGate,       // Waiting for build/test gate
    Completed,            // All tasks done
    Failed                // Unrecoverable failure
}
```

### 2. Decomposition Endpoint

```
POST /api/stories/{id}/decompose
```

Request body:
```json
{
  "maxParallelism": 4,           // Max concurrent agents
  "includeTests": true           // Include test tasks
}
```

Response:
```json
{
  "storyId": "...",
  "tasks": [
    {
      "id": "task-1",
      "title": "...",
      "wave": 1,
      "dependsOn": []
    }
  ],
  "waveCount": 3
}
```

### 3. PowerShell CLI Wrappers

```powershell
# Create a new story with worktree
New-AuraStory -Title "Add user validation" -RepositoryPath "C:\repos\myapp"

# Decompose story into tasks
Start-AuraStoryDecompose -StoryId <guid> -MaxParallelism 4

# Run story (future - Week 2)
Start-AuraStory -StoryId <guid>

# Complete story (future - Week 2)
Complete-AuraStory -StoryId <guid>
```

## Key Files

- `src/Aura.Module.Developer/Data/Entities/Story.cs` - Entity
- `src/Aura.Module.Developer/Data/Entities/StoryTask.cs` - New task type
- `src/Aura.Module.Developer/Services/IStoryService.cs` - Service interface
- `src/Aura.Module.Developer/Services/StoryService.cs` - Service implementation
- `src/Aura.Api/Program.cs` - API endpoints
- `scripts/Aura-Story.psm1` - PowerShell module (new)

## Dependencies

- GitHub Copilot CLI installed and authenticated
- Git worktree support
- LLM provider configured for decomposition

## Week 2 Scope (Future)

- `POST /api/stories/{id}/run` - Start parallel execution
- `POST /api/stories/{id}/complete` - Merge ceremony
- Build/test quality gates between waves
- Agent session management
- Progress streaming via SSE

## Open Questions

1. How to handle agent failures mid-wave?
2. Should we support partial wave completion?
3. How to aggregate agent outputs for dependent tasks?
