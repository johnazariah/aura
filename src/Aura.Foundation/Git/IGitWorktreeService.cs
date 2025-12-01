namespace Aura.Foundation.Git;

/// <summary>
/// Service for managing git worktrees for concurrent workflow isolation.
/// </summary>
public interface IGitWorktreeService
{
    /// <summary>Create a new worktree for a branch</summary>
    Task<GitResult<WorktreeInfo>> CreateAsync(
        string repoPath,
        string branchName,
        string? worktreePath = null,
        string? baseBranch = null,
        CancellationToken ct = default);
    
    /// <summary>Remove a worktree</summary>
    Task<GitResult<Unit>> RemoveAsync(string worktreePath, bool force = false, CancellationToken ct = default);
    
    /// <summary>List all worktrees for a repository</summary>
    Task<GitResult<IReadOnlyList<WorktreeInfo>>> ListAsync(string repoPath, CancellationToken ct = default);
    
    /// <summary>Get worktree info by path</summary>
    Task<GitResult<WorktreeInfo>> GetAsync(string worktreePath, CancellationToken ct = default);
    
    /// <summary>Prune stale worktrees</summary>
    Task<GitResult<Unit>> PruneAsync(string repoPath, CancellationToken ct = default);
}

/// <summary>
/// Information about a git worktree.
/// </summary>
public record WorktreeInfo
{
    /// <summary>Path to the worktree directory</summary>
    public required string Path { get; init; }
    
    /// <summary>Branch checked out in this worktree</summary>
    public required string Branch { get; init; }
    
    /// <summary>HEAD commit SHA</summary>
    public string? CommitSha { get; init; }
    
    /// <summary>Whether this is the main worktree</summary>
    public bool IsMainWorktree { get; init; }
    
    /// <summary>Whether the worktree is locked</summary>
    public bool IsLocked { get; init; }
    
    /// <summary>Lock reason if locked</summary>
    public string? LockReason { get; init; }
}
