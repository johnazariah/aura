namespace Aura.Foundation.Git;

/// <summary>
/// Cross-platform git operations.
/// </summary>
public interface IGitService
{
    /// <summary>Check if a directory is a git repository</summary>
    Task<bool> IsRepositoryAsync(string path, CancellationToken ct = default);
    
    /// <summary>Get the current branch name</summary>
    Task<GitResult<string>> GetCurrentBranchAsync(string repoPath, CancellationToken ct = default);
    
    /// <summary>Check if there are uncommitted changes</summary>
    Task<GitResult<bool>> HasUncommittedChangesAsync(string repoPath, CancellationToken ct = default);
    
    /// <summary>Create and checkout a new branch</summary>
    Task<GitResult<BranchInfo>> CreateBranchAsync(
        string repoPath, 
        string branchName, 
        string? baseBranch = null, 
        CancellationToken ct = default);
    
    /// <summary>Switch to an existing branch</summary>
    Task<GitResult<Unit>> CheckoutAsync(string repoPath, string branchName, CancellationToken ct = default);
    
    /// <summary>Stage and commit all changes</summary>
    Task<GitResult<string>> CommitAsync(string repoPath, string message, CancellationToken ct = default);
    
    /// <summary>Push the current branch</summary>
    Task<GitResult<Unit>> PushAsync(string repoPath, bool setUpstream = false, CancellationToken ct = default);
    
    /// <summary>Pull latest changes</summary>
    Task<GitResult<Unit>> PullAsync(string repoPath, CancellationToken ct = default);
    
    /// <summary>Get repository status</summary>
    Task<GitResult<RepositoryStatus>> GetStatusAsync(string repoPath, CancellationToken ct = default);
}

/// <summary>
/// Result type for git operations.
/// </summary>
public record GitResult<T>
{
    public bool Success { get; init; }
    public T? Value { get; init; }
    public string? Error { get; init; }
    
    public static GitResult<T> Ok(T value) => new() { Success = true, Value = value };
    public static GitResult<T> Fail(string error) => new() { Success = false, Error = error };
}

/// <summary>Represents a void return value</summary>
public readonly record struct Unit
{
    public static readonly Unit Value = new();
}

/// <summary>
/// Information about a git branch.
/// </summary>
public record BranchInfo
{
    public required string Name { get; init; }
    public string? RemoteName { get; init; }
    public bool IsCurrent { get; init; }
    public string? UpstreamBranch { get; init; }
    public int? AheadBy { get; init; }
    public int? BehindBy { get; init; }
}

/// <summary>
/// Git repository status.
/// </summary>
public record RepositoryStatus
{
    public required string CurrentBranch { get; init; }
    public bool IsDirty { get; init; }
    public IReadOnlyList<string> ModifiedFiles { get; init; } = [];
    public IReadOnlyList<string> UntrackedFiles { get; init; } = [];
    public IReadOnlyList<string> StagedFiles { get; init; } = [];
}
