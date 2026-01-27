# Orchestrator Parallel Dispatch

**Status:** ✅ Complete
**Completed:** 2026-01-28
**Started:** 2026-01-24

## Overview

Aura's orchestrator dispatches parallelizable tasks to GitHub Copilot CLI agents running in YOLO mode. Stories are decomposed into waves of tasks that can execute in parallel, with quality gates (build/test) between waves.

## User Flow

1. `New-AuraStory` → creates story + git worktree
2. `Start-AuraStoryAnalysis` → analyzes codebase context
3. `Start-AuraStoryDecompose` → LLM breaks story into tasks with dependencies
4. `Start-AuraStory` → dispatches to N × GH Copilot CLI agents (YOLO mode)
5. Quality gates run automatically between waves
6. `Complete-AuraStory` → merge ceremony (verification, commit, PR)

## Implementation

### Data Model

**File:** `src/Aura.Module.Developer/Data/Entities/StoryTask.cs`

```csharp
public record StoryTask(
    string Id,
    string Title,
    string Description,
    int Wave,
    string[] DependsOn,
    StoryTaskStatus Status = StoryTaskStatus.Pending,
    string? AgentSessionId = null,
    string? Output = null,
    string? Error = null,
    string? ToolImprovementProposal = null,
    DateTimeOffset? StartedAt = null,
    DateTimeOffset? CompletedAt = null);

public enum StoryTaskStatus { Pending, Running, Completed, Failed, Skipped }
```

### Services

**File:** `src/Aura.Module.Developer/Services/GitHubCopilotDispatcher.cs`

- `DispatchTaskAsync()` - Dispatch single task to Copilot CLI
- `DispatchTasksAsync()` - Dispatch multiple tasks with parallelism limit
- Uses `SemaphoreSlim` for controlled parallelism
- Returns task results with output/error

**File:** `src/Aura.Module.Developer/Services/QualityGateService.cs`

- Runs build/test gates between waves
- Structured error reporting

### API Endpoints

| Endpoint | Description |
|----------|-------------|
| `POST /api/developer/stories/{id}/decompose` | Decompose story into parallel tasks |
| `POST /api/developer/stories/{id}/run` | Execute next wave of tasks |
| `GET /api/developer/stories/{id}/orchestrator-status` | Get task progress |

### PowerShell Module

**File:** `scripts/Aura-Story.psm1`

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

# Analyze and decompose
Start-AuraStoryAnalysis -Id $story.id
$tasks = Start-AuraStoryDecompose -Id $story.id -MaxParallelism 4

# Run all waves
Invoke-AuraStoryFull -Id $story.id

# Complete and create PR
Complete-AuraStory -Id $story.id -CreatePullRequest $true
```

## Files Changed

- `src/Aura.Module.Developer/Data/Entities/StoryTask.cs` (new)
- `src/Aura.Module.Developer/Services/GitHubCopilotDispatcher.cs` (new)
- `src/Aura.Module.Developer/Services/QualityGateService.cs` (new)
- `src/Aura.Module.Developer/Services/StoryService.cs` (decompose, run)
- `src/Aura.Api/Endpoints/DeveloperEndpoints.cs` (endpoints)
- `scripts/Aura-Story.psm1` (new)

## Dependencies

- GitHub Copilot CLI installed and authenticated
- Git worktree support
- LLM provider configured for decomposition
