using Aura.Foundation.Shell;
using Microsoft.Extensions.Logging;

namespace Aura.Foundation.Git;

/// <summary>
/// Git service using CLI commands for cross-platform compatibility.
/// </summary>
public class GitService : IGitService
{
    private readonly IProcessRunner _process;
    private readonly ILogger<GitService> _logger;

    public GitService(IProcessRunner process, ILogger<GitService> logger)
    {
        _process = process;
        _logger = logger;
    }

    public async Task<bool> IsRepositoryAsync(string path, CancellationToken ct = default)
    {
        var result = await RunGitAsync(path, ["rev-parse", "--is-inside-work-tree"], ct);
        return result.Success && result.StandardOutput.Trim() == "true";
    }

    public async Task<GitResult<string>> GetCurrentBranchAsync(string repoPath, CancellationToken ct = default)
    {
        var result = await RunGitAsync(repoPath, ["branch", "--show-current"], ct);
        if (!result.Success)
            return GitResult<string>.Fail(result.StandardError);
        
        var branch = result.StandardOutput.Trim();
        if (string.IsNullOrEmpty(branch))
        {
            // Might be in detached HEAD state
            var headResult = await RunGitAsync(repoPath, ["rev-parse", "--short", "HEAD"], ct);
            return headResult.Success 
                ? GitResult<string>.Ok($"HEAD detached at {headResult.StandardOutput.Trim()}")
                : GitResult<string>.Fail("Unable to determine current branch");
        }
        
        return GitResult<string>.Ok(branch);
    }

    public async Task<GitResult<bool>> HasUncommittedChangesAsync(string repoPath, CancellationToken ct = default)
    {
        var result = await RunGitAsync(repoPath, ["status", "--porcelain"], ct);
        if (!result.Success)
            return GitResult<bool>.Fail(result.StandardError);
        
        return GitResult<bool>.Ok(!string.IsNullOrWhiteSpace(result.StandardOutput));
    }

    public async Task<GitResult<BranchInfo>> CreateBranchAsync(
        string repoPath,
        string branchName,
        string? baseBranch = null,
        CancellationToken ct = default)
    {
        // Create the branch
        var args = baseBranch is not null 
            ? new[] { "checkout", "-b", branchName, baseBranch }
            : new[] { "checkout", "-b", branchName };
        
        var result = await RunGitAsync(repoPath, args, ct);
        if (!result.Success)
            return GitResult<BranchInfo>.Fail(result.StandardError);
        
        _logger.LogInformation("Created branch {Branch} in {Repo}", branchName, repoPath);
        
        return GitResult<BranchInfo>.Ok(new BranchInfo 
        { 
            Name = branchName,
            IsCurrent = true
        });
    }

    public async Task<GitResult<Unit>> CheckoutAsync(string repoPath, string branchName, CancellationToken ct = default)
    {
        var result = await RunGitAsync(repoPath, ["checkout", branchName], ct);
        if (!result.Success)
            return GitResult<Unit>.Fail(result.StandardError);
        
        _logger.LogInformation("Checked out branch {Branch}", branchName);
        return GitResult<Unit>.Ok(Unit.Value);
    }

    public async Task<GitResult<string>> CommitAsync(string repoPath, string message, CancellationToken ct = default)
    {
        // Stage all changes
        var stageResult = await RunGitAsync(repoPath, ["add", "-A"], ct);
        if (!stageResult.Success)
            return GitResult<string>.Fail($"Failed to stage: {stageResult.StandardError}");

        // Commit
        var commitResult = await RunGitAsync(repoPath, ["commit", "-m", message], ct);
        if (!commitResult.Success)
        {
            // git commit returns exit code 1 with message in stdout when nothing to commit
            var error = !string.IsNullOrWhiteSpace(commitResult.StandardError)
                ? commitResult.StandardError
                : commitResult.StandardOutput;

            // Check for "nothing to commit" which is a common non-error case
            if (error.Contains("nothing to commit", StringComparison.OrdinalIgnoreCase))
            {
                return GitResult<string>.Fail("Nothing to commit - working tree is clean");
            }

            return GitResult<string>.Fail(string.IsNullOrWhiteSpace(error) ? "Commit failed" : error);
        }

        // Get the commit SHA
        var shaResult = await RunGitAsync(repoPath, ["rev-parse", "HEAD"], ct);
        var sha = shaResult.Success ? shaResult.StandardOutput.Trim() : "unknown";

        _logger.LogInformation("Committed: {Sha}", sha[..Math.Min(7, sha.Length)]);
        return GitResult<string>.Ok(sha);
    }    public async Task<GitResult<Unit>> PushAsync(string repoPath, bool setUpstream = false, CancellationToken ct = default)
    {
        var args = setUpstream 
            ? new[] { "push", "-u", "origin", "HEAD" }
            : new[] { "push" };
        
        var result = await RunGitAsync(repoPath, args, ct);
        if (!result.Success)
            return GitResult<Unit>.Fail(result.StandardError);
        
        _logger.LogInformation("Pushed to remote");
        return GitResult<Unit>.Ok(Unit.Value);
    }

    public async Task<GitResult<Unit>> PullAsync(string repoPath, CancellationToken ct = default)
    {
        var result = await RunGitAsync(repoPath, ["pull"], ct);
        if (!result.Success)
            return GitResult<Unit>.Fail(result.StandardError);
        
        return GitResult<Unit>.Ok(Unit.Value);
    }

    public async Task<GitResult<RepositoryStatus>> GetStatusAsync(string repoPath, CancellationToken ct = default)
    {
        var branchResult = await GetCurrentBranchAsync(repoPath, ct);
        if (!branchResult.Success)
            return GitResult<RepositoryStatus>.Fail(branchResult.Error ?? "Failed to get branch");
        
        var statusResult = await RunGitAsync(repoPath, ["status", "--porcelain=v1"], ct);
        if (!statusResult.Success)
            return GitResult<RepositoryStatus>.Fail(statusResult.StandardError);
        
        var lines = statusResult.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        var modified = new List<string>();
        var untracked = new List<string>();
        var staged = new List<string>();
        
        foreach (var line in lines)
        {
            if (line.Length < 3) continue;
            
            var indexStatus = line[0];
            var workTreeStatus = line[1];
            var file = line[3..].Trim();
            
            if (indexStatus != ' ' && indexStatus != '?')
                staged.Add(file);
            
            if (workTreeStatus == 'M' || workTreeStatus == 'D')
                modified.Add(file);
            
            if (indexStatus == '?' && workTreeStatus == '?')
                untracked.Add(file);
        }
        
        return GitResult<RepositoryStatus>.Ok(new RepositoryStatus
        {
            CurrentBranch = branchResult.Value!,
            IsDirty = lines.Length > 0,
            ModifiedFiles = modified,
            UntrackedFiles = untracked,
            StagedFiles = staged
        });
    }

    private async Task<ProcessResult> RunGitAsync(string workDir, string[] args, CancellationToken ct)
    {
        return await _process.RunAsync("git", args, new ProcessOptions
        {
            WorkingDirectory = workDir,
            Timeout = TimeSpan.FromSeconds(30)
        }, ct);
    }
}
