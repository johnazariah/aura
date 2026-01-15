using System.Text.RegularExpressions;
using Aura.Foundation.Shell;
using Microsoft.Extensions.Logging;

namespace Aura.Foundation.Git;

/// <summary>
/// Git worktree service using CLI commands.
/// </summary>
public class GitWorktreeService(IProcessRunner process, ILogger<GitWorktreeService> logger) : IGitWorktreeService
{
    private readonly IProcessRunner _process = process;
    private readonly ILogger<GitWorktreeService> _logger = logger;

    public async Task<GitResult<WorktreeInfo>> CreateAsync(
        string repoPath,
        string branchName,
        string? worktreePath = null,
        string? baseBranch = null,
        CancellationToken ct = default)
    {
        // Default worktree path: parallel to repo with branch name
        worktreePath ??= Path.Combine(
            Path.GetDirectoryName(repoPath) ?? repoPath,
            $"{Path.GetFileName(repoPath)}-{SanitizeBranchName(branchName)}");

        // Check if worktree already exists
        if (Directory.Exists(worktreePath))
        {
            _logger.LogWarning("Worktree path already exists: {Path}", worktreePath);
            return GitResult<WorktreeInfo>.Fail($"Worktree path already exists: {worktreePath}");
        }

        // Build args: git worktree add [-b branch] path [base]
        var args = new List<string> { "worktree", "add" };

        // Check if branch exists
        var branchExistsResult = await RunGitAsync(repoPath,
            ["show-ref", "--verify", "--quiet", $"refs/heads/{branchName}"], ct);

        if (!branchExistsResult.Success)
        {
            // Branch doesn't exist, create it
            args.Add("-b");
            args.Add(branchName);
        }

        args.Add(worktreePath);

        if (baseBranch is not null)
            args.Add(baseBranch);
        else if (!branchExistsResult.Success)
            args.Add(branchName); // Use branch name itself if we're creating it

        // Actually for new branch with base, we need different args
        if (!branchExistsResult.Success && baseBranch is not null)
        {
            args = ["worktree", "add", "-b", branchName, worktreePath, baseBranch];
        }
        else if (!branchExistsResult.Success)
        {
            args = ["worktree", "add", "-b", branchName, worktreePath];
        }
        else
        {
            args = ["worktree", "add", worktreePath, branchName];
        }

        var result = await RunGitAsync(repoPath, args.ToArray(), ct);
        if (!result.Success)
            return GitResult<WorktreeInfo>.Fail(result.StandardError);

        _logger.LogInformation("Created worktree: {Path} for branch {Branch}", worktreePath, branchName);

        // Get commit SHA
        var shaResult = await RunGitAsync(worktreePath, ["rev-parse", "HEAD"], ct);

        return GitResult<WorktreeInfo>.Ok(new WorktreeInfo
        {
            Path = worktreePath,
            Branch = branchName,
            CommitSha = shaResult.Success ? shaResult.StandardOutput.Trim() : null,
            IsMainWorktree = false
        });
    }

    public async Task<GitResult<Unit>> RemoveAsync(string worktreePath, bool force = false, CancellationToken ct = default)
    {
        var args = force
            ? new[] { "worktree", "remove", "--force", worktreePath }
            : new[] { "worktree", "remove", worktreePath };

        // We need to find the main repo to run the command
        // First try to get the main worktree
        var gitDirResult = await RunGitAsync(worktreePath, ["rev-parse", "--git-common-dir"], ct);
        if (!gitDirResult.Success)
        {
            _logger.LogWarning("Could not find git directory for worktree: {Path}", worktreePath);
            return GitResult<Unit>.Fail("Could not find git directory");
        }

        var gitDir = gitDirResult.StandardOutput.Trim();
        var repoPath = Path.GetDirectoryName(gitDir) ?? worktreePath;

        var result = await RunGitAsync(repoPath, args, ct);
        if (!result.Success)
            return GitResult<Unit>.Fail(result.StandardError);

        _logger.LogInformation("Removed worktree: {Path}", worktreePath);
        return GitResult<Unit>.Ok(Unit.Value);
    }

    public async Task<GitResult<IReadOnlyList<WorktreeInfo>>> ListAsync(string repoPath, CancellationToken ct = default)
    {
        var result = await RunGitAsync(repoPath, ["worktree", "list", "--porcelain"], ct);
        if (!result.Success)
            return GitResult<IReadOnlyList<WorktreeInfo>>.Fail(result.StandardError);

        var worktrees = new List<WorktreeInfo>();
        var lines = result.StandardOutput.Split('\n');

        string? currentPath = null;
        string? currentBranch = null;
        string? currentSha = null;
        bool isLocked = false;
        string? lockReason = null;
        bool isBare = false;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                // End of worktree entry
                if (currentPath is not null && !isBare)
                {
                    worktrees.Add(new WorktreeInfo
                    {
                        Path = currentPath,
                        Branch = currentBranch ?? "HEAD",
                        CommitSha = currentSha,
                        IsMainWorktree = worktrees.Count == 0,
                        IsLocked = isLocked,
                        LockReason = lockReason
                    });
                }
                currentPath = null;
                currentBranch = null;
                currentSha = null;
                isLocked = false;
                lockReason = null;
                isBare = false;
                continue;
            }

            if (line.StartsWith("worktree "))
                currentPath = line[9..];
            else if (line.StartsWith("HEAD "))
                currentSha = line[5..];
            else if (line.StartsWith("branch refs/heads/"))
                currentBranch = line[18..];
            else if (line == "bare")
                isBare = true;
            else if (line.StartsWith("locked"))
                isLocked = true;
            else if (line.StartsWith("lock-reason "))
                lockReason = line[12..];
        }

        // Don't forget the last entry if file doesn't end with blank line
        if (currentPath is not null && !isBare)
        {
            worktrees.Add(new WorktreeInfo
            {
                Path = currentPath,
                Branch = currentBranch ?? "HEAD",
                CommitSha = currentSha,
                IsMainWorktree = worktrees.Count == 0,
                IsLocked = isLocked,
                LockReason = lockReason
            });
        }

        return GitResult<IReadOnlyList<WorktreeInfo>>.Ok(worktrees);
    }

    public async Task<GitResult<WorktreeInfo>> GetAsync(string worktreePath, CancellationToken ct = default)
    {
        // Get the common git dir to find the main repo
        var gitDirResult = await RunGitAsync(worktreePath, ["rev-parse", "--git-common-dir"], ct);
        if (!gitDirResult.Success)
            return GitResult<WorktreeInfo>.Fail("Not a git worktree");

        var gitDir = gitDirResult.StandardOutput.Trim();
        var repoPath = Path.GetDirectoryName(gitDir) ?? worktreePath;

        var listResult = await ListAsync(repoPath, ct);
        if (!listResult.Success)
            return GitResult<WorktreeInfo>.Fail(listResult.Error ?? "Failed to list worktrees");

        var normalizedPath = Path.GetFullPath(worktreePath);
        var worktree = listResult.Value?.FirstOrDefault(w =>
            Path.GetFullPath(w.Path).Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));

        return worktree is not null
            ? GitResult<WorktreeInfo>.Ok(worktree)
            : GitResult<WorktreeInfo>.Fail($"Worktree not found: {worktreePath}");
    }

    public async Task<GitResult<string>> GetMainRepositoryPathAsync(string gitPath, CancellationToken ct = default)
    {
        // Get the common git directory (shared by main repo and all worktrees)
        var gitDirResult = await RunGitAsync(gitPath, ["rev-parse", "--git-common-dir"], ct);
        if (!gitDirResult.Success)
            return GitResult<string>.Fail("Not a git repository");

        var gitDir = Path.GetFullPath(Path.Combine(gitPath, gitDirResult.StandardOutput.Trim()));

        // The main repo is the parent of the .git directory
        // For main repos: gitDir = /path/to/repo/.git -> parent = /path/to/repo
        // For worktrees: gitDir = /path/to/repo/.git -> already points to main .git
        var mainRepoPath = Path.GetDirectoryName(gitDir);

        if (string.IsNullOrEmpty(mainRepoPath) || !Directory.Exists(mainRepoPath))
            return GitResult<string>.Fail($"Could not resolve main repository from: {gitPath}");

        return GitResult<string>.Ok(mainRepoPath);
    }

    public async Task<GitResult<Unit>> PruneAsync(string repoPath, CancellationToken ct = default)
    {
        var result = await RunGitAsync(repoPath, ["worktree", "prune"], ct);
        if (!result.Success)
            return GitResult<Unit>.Fail(result.StandardError);

        _logger.LogInformation("Pruned stale worktrees in {Repo}", repoPath);
        return GitResult<Unit>.Ok(Unit.Value);
    }

    private async Task<ProcessResult> RunGitAsync(string workDir, string[] args, CancellationToken ct)
    {
        return await _process.RunAsync("git", args, new ProcessOptions
        {
            WorkingDirectory = workDir,
            Timeout = TimeSpan.FromSeconds(60)
        }, ct);
    }

    private static string SanitizeBranchName(string branchName)
    {
        // Remove refs/heads/ prefix if present
        if (branchName.StartsWith("refs/heads/"))
            branchName = branchName[11..];

        // Replace invalid characters with dashes
        return Regex.Replace(branchName, @"[^a-zA-Z0-9_-]", "-").Trim('-');
    }
}
