# Git Worktrees

**Status:** ✅ Complete  
**Completed:** 2025-11-28  
**Last Updated:** 2025-12-12

## Overview

Aura uses Git worktrees to enable concurrent workflow execution. Each workflow gets its own worktree, allowing multiple workflows to work on the same repository simultaneously without conflicts.

## Why Worktrees?

| Approach | Problem |
|----------|---------|
| Single checkout | Concurrent workflows overwrite each other |
| Clone per workflow | Wastes disk space, slow |
| **Worktree per workflow** | Lightweight, isolated, shares git objects |

## Worktree Lifecycle

```
┌─────────────────────────────────────────────────────────────┐
│                    Worktree Lifecycle                        │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌─────────────────────────────────────────────────────┐    │
│  │ 1. WORKFLOW CREATED                                  │    │
│  │    - Create branch: feature/workflow-{id}           │    │
│  │    - Create worktree: /workspaces/{repo}-wt-{id}   │    │
│  └─────────────────────────────────────────────────────┘    │
│                          │                                   │
│                          ▼                                   │
│  ┌─────────────────────────────────────────────────────┐    │
│  │ 2. WORKFLOW EXECUTING                                │    │
│  │    - Agent writes to worktree                       │    │
│  │    - Commits after each step                        │    │
│  │    - Tests run in worktree                          │    │
│  └─────────────────────────────────────────────────────┘    │
│                          │                                   │
│                          ▼                                   │
│  ┌─────────────────────────────────────────────────────┐    │
│  │ 3. WORKFLOW COMPLETED                                │    │
│  │    - Push branch to remote                          │    │
│  │    - Create PR (via GitHub agent)                   │    │
│  │    - Optionally: remove worktree                    │    │
│  └─────────────────────────────────────────────────────┘    │
│                          │                                   │
│                          ▼                                   │
│  ┌─────────────────────────────────────────────────────┐    │
│  │ 4. CLEANUP                                           │    │
│  │    - Remove worktree when PR merged                 │    │
│  │    - Or: manual cleanup                             │    │
│  └─────────────────────────────────────────────────────┘    │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

## Worktree Service Interface

```csharp
public interface IWorktreeService
{
    /// <summary>
    /// Create a worktree for a workflow.
    /// </summary>
    Task<WorktreeInfo> CreateAsync(
        string mainRepoPath,
        Guid workflowId,
        string? baseBranch = null,  // Default: current branch
        CancellationToken ct = default);
    
    /// <summary>
    /// Get worktree info for a workflow.
    /// </summary>
    Task<WorktreeInfo?> GetAsync(Guid workflowId, CancellationToken ct = default);
    
    /// <summary>
    /// List all worktrees for a repository.
    /// </summary>
    Task<IReadOnlyList<WorktreeInfo>> ListAsync(
        string mainRepoPath,
        CancellationToken ct = default);
    
    /// <summary>
    /// Remove a worktree.
    /// </summary>
    Task RemoveAsync(Guid workflowId, bool force = false, CancellationToken ct = default);
    
    /// <summary>
    /// Commit changes in a worktree.
    /// </summary>
    Task<string> CommitAsync(
        Guid workflowId,
        string message,
        CancellationToken ct = default);
    
    /// <summary>
    /// Push worktree branch to remote.
    /// </summary>
    Task PushAsync(Guid workflowId, CancellationToken ct = default);
}

public record WorktreeInfo(
    Guid WorkflowId,
    string WorktreePath,      // e.g., "/workspaces/repo-wt-abc123"
    string BranchName,        // e.g., "feature/workflow-abc123"
    string MainRepoPath,      // e.g., "/workspaces/repo"
    bool IsClean,             // No uncommitted changes
    DateTime CreatedAt
);
```

## Implementation

Uses libgit2sharp or direct git CLI:

```csharp
public class WorktreeService : IWorktreeService
{
    private readonly string _workspacesRoot;  // e.g., "/workspaces"
    
    public async Task<WorktreeInfo> CreateAsync(
        string mainRepoPath,
        Guid workflowId,
        string? baseBranch = null,
        CancellationToken ct = default)
    {
        var branchName = $"feature/workflow-{workflowId:N}";
        var worktreePath = Path.Combine(_workspacesRoot, $"{GetRepoName(mainRepoPath)}-wt-{workflowId:N}");
        
        // Create branch from base
        baseBranch ??= await GetCurrentBranchAsync(mainRepoPath, ct);
        await RunGitAsync(mainRepoPath, $"branch {branchName} {baseBranch}", ct);
        
        // Create worktree
        await RunGitAsync(mainRepoPath, $"worktree add {worktreePath} {branchName}", ct);
        
        return new WorktreeInfo(
            workflowId,
            worktreePath,
            branchName,
            mainRepoPath,
            IsClean: true,
            DateTime.UtcNow);
    }
    
    public async Task<string> CommitAsync(
        Guid workflowId,
        string message,
        CancellationToken ct = default)
    {
        var info = await GetAsync(workflowId, ct) 
            ?? throw new InvalidOperationException($"Worktree not found: {workflowId}");
        
        await RunGitAsync(info.WorktreePath, "add -A", ct);
        await RunGitAsync(info.WorktreePath, $"commit -m \"{message}\"", ct);
        
        return await RunGitAsync(info.WorktreePath, "rev-parse HEAD", ct);
    }
}
```

## Worktree Naming Convention

| Component | Format | Example |
|-----------|--------|---------|
| Branch | `feature/workflow-{id:N}` | `feature/workflow-abc123def456` |
| Worktree path | `{repo}-wt-{id:N}` | `myrepo-wt-abc123def456` |

## Step Commit Strategy

After each successful step:

```csharp
// In step executor
var result = await agent.ExecuteAsync(context, ct);
if (result.Success)
{
    // Write files
    foreach (var file in result.Files)
    {
        await File.WriteAllTextAsync(
            Path.Combine(worktreePath, file.Path), 
            file.Content, ct);
    }
    
    // Commit
    await worktreeService.CommitAsync(
        workflowId,
        $"Step {step.Order}: {step.Name}",
        ct);
}
```

## Cleanup Policy

Options:
1. **Immediate** - Remove worktree when workflow completes
2. **On PR Merge** - Keep until PR merged (requires webhook)
3. **Manual** - User triggers cleanup
4. **TTL** - Remove worktrees older than N days

Default: Keep worktree, user can remove via API.

```http
DELETE /api/workflows/{id}/worktree
```

## Concurrent Workflow Limits

Configuration:
```json
{
  "Worktrees": {
    "MaxConcurrent": 5,
    "WorkspacesRoot": "/workspaces",
    "CleanupAfterDays": 7
  }
}
```

## Error Handling

| Scenario | Handling |
|----------|----------|
| Worktree already exists | Reuse existing |
| Branch already exists | Append suffix or fail |
| Disk space low | Fail with clear error |
| Git conflict | Surface to user, don't auto-resolve |

## What We Keep from Current Implementation

The current git worktree code (~1,500 lines) is battle-tested. Keep:
- Worktree creation/removal
- Branch management
- Commit/push operations

Simplify:
- Remove complex retry logic
- Remove unused features
- Flatten into single service class

## Open Questions

1. **Merge strategy** - Rebase or merge commits?
2. **Conflict resolution** - Auto-resolve or always surface?
3. **Branch protection** - Respect protected branch rules?
4. **Stale worktree detection** - Background cleanup job?
