using Aura.Foundation.Shell;
using Microsoft.Extensions.Logging;

namespace Aura.Foundation.Git;

/// <summary>
/// Git service using CLI commands for cross-platform compatibility.
/// </summary>
public class GitService(IProcessRunner process, ILogger<GitService> logger) : IGitService
{
    private readonly IProcessRunner _process = process;
    private readonly ILogger<GitService> _logger = logger;

    public async Task<bool> IsRepositoryAsync(string path, CancellationToken ct = default)
    {
        var result = await RunGitAsync(path, ["rev-parse", "--is-inside-work-tree"], ct);

        // Handle "dubious ownership" error by adding to safe.directory
        if (!result.Success && result.StandardError.Contains("dubious ownership", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Adding {Path} to git safe.directory to fix ownership issue", path);
            var safeResult = await _process.RunAsync("git",
                ["config", "--global", "--add", "safe.directory", path.Replace('\\', '/')],
                new ProcessOptions { Timeout = TimeSpan.FromSeconds(10) }, ct);

            if (safeResult.Success)
            {
                // Retry the check
                result = await RunGitAsync(path, ["rev-parse", "--is-inside-work-tree"], ct);
            }
            else
            {
                _logger.LogWarning("Failed to add safe.directory: {Error}", safeResult.StandardError);
            }
        }

        if (!result.Success)
        {
            _logger.LogWarning("IsRepositoryAsync failed for {Path}: ExitCode={ExitCode}, StdErr={StdErr}",
                path, result.ExitCode, result.StandardError);
        }
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

    public async Task<GitResult<Unit>> DeleteBranchAsync(string repoPath, string branchName, bool force = false, CancellationToken ct = default)
    {
        var flag = force ? "-D" : "-d";
        var result = await RunGitAsync(repoPath, ["branch", flag, branchName], ct);
        if (!result.Success)
            return GitResult<Unit>.Fail(result.StandardError);

        _logger.LogInformation("Deleted branch {Branch} in {Repo}", branchName, repoPath);
        return GitResult<Unit>.Ok(Unit.Value);
    }

    public async Task<GitResult<string>> CommitAsync(string repoPath, string message, bool skipHooks = false, CancellationToken ct = default)
    {
        // Stage all changes
        var stageResult = await RunGitAsync(repoPath, ["add", "-A"], ct);
        if (!stageResult.Success)
            return GitResult<string>.Fail($"Failed to stage: {stageResult.StandardError}");

        // Commit (--no-verify skips pre-commit and commit-msg hooks for automated workflows)
        var commitArgs = skipHooks
            ? new[] { "commit", "--no-verify", "-m", message }
            : new[] { "commit", "-m", message };
        var commitResult = await RunGitAsync(repoPath, commitArgs, ct);
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
    }
    public async Task<GitResult<Unit>> PushAsync(string repoPath, bool setUpstream = false, bool forcePush = false, string? githubToken = null, CancellationToken ct = default)
    {
        // If we have a token, inject it into the remote URL for non-interactive auth.
        // GH_TOKEN env var only works for `gh` CLI, not `git push`.
        string? authenticatedUrl = null;
        if (!string.IsNullOrEmpty(githubToken))
        {
            var remoteResult = await GetRemoteUrlAsync(repoPath, ct);
            if (remoteResult.Success && remoteResult.Value is not null)
            {
                var remoteUrl = remoteResult.Value;
                if (remoteUrl.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
                {
                    authenticatedUrl = remoteUrl.Replace(
                        "https://github.com/",
                        $"https://x-access-token:{githubToken}@github.com/",
                        StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        var argsList = new List<string> { "push" };

        if (forcePush)
        {
            argsList.Add("--force-with-lease");
        }

        if (authenticatedUrl is not null)
        {
            if (setUpstream)
            {
                argsList.AddRange(["-u", authenticatedUrl, "HEAD"]);
            }
            else
            {
                argsList.Add(authenticatedUrl);
            }
        }
        else
        {
            if (setUpstream)
            {
                argsList.AddRange(["-u", "origin", "HEAD"]);
            }
        }

        // Use a longer timeout for push (large repos, slow connections)
        var options = new ProcessOptions
        {
            WorkingDirectory = repoPath,
            Timeout = TimeSpan.FromSeconds(60)
        };
        var result = await _process.RunAsync("git", argsList.ToArray(), options, ct);
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

    public async Task<GitResult<string>> GetRemoteUrlAsync(string repoPath, CancellationToken ct = default)
    {
        var result = await RunGitAsync(repoPath, ["remote", "get-url", "origin"], ct);
        if (!result.Success)
            return GitResult<string>.Fail(result.StandardError);

        return GitResult<string>.Ok(result.StandardOutput.Trim());
    }

    public async Task<GitResult<PullRequestInfo>> CreatePullRequestAsync(
        string repoPath,
        string title,
        string? body = null,
        string? baseBranch = null,
        bool draft = true,
        IEnumerable<string>? labels = null,
        string? githubToken = null,
        CancellationToken ct = default)
    {
        // Build gh pr create command
        var args = new List<string> { "pr", "create", "--title", title };

        if (!string.IsNullOrEmpty(body))
        {
            args.AddRange(["--body", body]);
        }

        if (!string.IsNullOrEmpty(baseBranch))
        {
            args.AddRange(["--base", baseBranch]);
        }

        if (draft)
        {
            args.Add("--draft");
        }

        if (labels is not null)
        {
            foreach (var label in labels)
            {
                // Ensure label exists (create if missing, ignore if already exists)
                await EnsureLabelExistsAsync(repoPath, label, githubToken, ct);
                args.AddRange(["--label", label]);
            }
        }

        // Build options with token if provided
        var options = new ProcessOptions
        {
            WorkingDirectory = repoPath,
            Timeout = TimeSpan.FromSeconds(60)
        };

        if (!string.IsNullOrEmpty(githubToken))
        {
            options = options with
            {
                Environment = new Dictionary<string, string>
                {
                    ["GH_TOKEN"] = githubToken,
                    ["GITHUB_TOKEN"] = githubToken
                }
            };
        }

        // Run gh command
        var result = await _process.RunAsync("gh", args.ToArray(), options, ct);

        if (!result.Success)
        {
            var error = !string.IsNullOrWhiteSpace(result.StandardError)
                ? result.StandardError
                : result.StandardOutput;

            // Check for common errors
            if (error.Contains("gh auth login", StringComparison.OrdinalIgnoreCase))
            {
                return GitResult<PullRequestInfo>.Fail("GitHub CLI not authenticated. Run 'gh auth login' first.");
            }

            if (error.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
                // Try to get existing PR URL
                var prUrlResult = await GetExistingPullRequestUrlAsync(repoPath, ct);
                if (prUrlResult.Success && prUrlResult.Value is not null)
                {
                    return GitResult<PullRequestInfo>.Ok(new PullRequestInfo
                    {
                        Number = ExtractPrNumber(prUrlResult.Value),
                        Url = prUrlResult.Value,
                        State = "open",
                        IsDraft = draft,
                        Title = title
                    });
                }
                return GitResult<PullRequestInfo>.Fail("A pull request already exists for this branch.");
            }

            return GitResult<PullRequestInfo>.Fail(error);
        }

        // gh pr create outputs the PR URL on success
        var prUrl = result.StandardOutput.Trim();
        _logger.LogInformation("Created PR: {Url}", prUrl);

        return GitResult<PullRequestInfo>.Ok(new PullRequestInfo
        {
            Number = ExtractPrNumber(prUrl),
            Url = prUrl,
            State = "open",
            IsDraft = draft,
            Title = title
        });
    }

    private async Task<GitResult<string>> GetExistingPullRequestUrlAsync(string repoPath, CancellationToken ct)
    {
        var result = await _process.RunAsync("gh", ["pr", "view", "--json", "url", "-q", ".url"], new ProcessOptions
        {
            WorkingDirectory = repoPath,
            Timeout = TimeSpan.FromSeconds(30)
        }, ct);

        if (!result.Success)
            return GitResult<string>.Fail(result.StandardError);

        return GitResult<string>.Ok(result.StandardOutput.Trim());
    }

    private static int ExtractPrNumber(string prUrl)
    {
        // URL format: https://github.com/owner/repo/pull/123
        var parts = prUrl.Split('/');
        if (parts.Length > 0 && int.TryParse(parts[^1], out var number))
            return number;
        return 0;
    }

    public async Task<GitResult<string>> SquashCommitsAsync(
        string repoPath,
        string baseBranch,
        string message,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Squashing commits on current branch since {BaseBranch}", baseBranch);

        // First, ensure the base branch reference is up to date
        // Get the merge base (where the current branch diverged from base)
        var mergeBaseResult = await RunGitAsync(repoPath, ["merge-base", baseBranch, "HEAD"], ct);
        if (!mergeBaseResult.Success)
        {
            return GitResult<string>.Fail($"Failed to find merge base with {baseBranch}: {mergeBaseResult.StandardError}");
        }

        var mergeBase = mergeBaseResult.StandardOutput.Trim();
        _logger.LogDebug("Merge base with {BaseBranch}: {MergeBase}", baseBranch, mergeBase);

        // Count commits to squash
        var commitCountResult = await RunGitAsync(repoPath, ["rev-list", "--count", $"{mergeBase}..HEAD"], ct);
        if (!commitCountResult.Success)
        {
            return GitResult<string>.Fail($"Failed to count commits: {commitCountResult.StandardError}");
        }

        var commitCount = int.Parse(commitCountResult.StandardOutput.Trim());
        if (commitCount <= 1)
        {
            _logger.LogInformation("Only {Count} commit(s) since base, no squash needed", commitCount);
            // Return current HEAD as the "squashed" commit
            var headResult = await RunGitAsync(repoPath, ["rev-parse", "HEAD"], ct);
            return headResult.Success
                ? GitResult<string>.Ok(headResult.StandardOutput.Trim())
                : GitResult<string>.Fail($"Failed to get HEAD: {headResult.StandardError}");
        }

        _logger.LogInformation("Squashing {Count} commits into one", commitCount);

        // Perform soft reset to merge base, keeping all changes staged
        var resetResult = await RunGitAsync(repoPath, ["reset", "--soft", mergeBase], ct);
        if (!resetResult.Success)
        {
            return GitResult<string>.Fail($"Failed to reset: {resetResult.StandardError}");
        }

        // Commit all staged changes with the new message (skip hooks — this is an automated squash)
        var commitResult = await RunGitAsync(repoPath, ["commit", "--no-verify", "-m", message], ct);
        if (!commitResult.Success)
        {
            return GitResult<string>.Fail($"Failed to commit squashed changes: {commitResult.StandardError}");
        }

        // Get the new commit SHA
        var newHeadResult = await RunGitAsync(repoPath, ["rev-parse", "HEAD"], ct);
        if (!newHeadResult.Success)
        {
            return GitResult<string>.Fail($"Failed to get new HEAD: {newHeadResult.StandardError}");
        }

        var newSha = newHeadResult.StandardOutput.Trim();
        _logger.LogInformation("Squashed {Count} commits into {Sha}", commitCount, newSha[..7]);

        return GitResult<string>.Ok(newSha);
    }

    public async Task<GitResult<string>> GetDefaultBranchAsync(string repoPath, CancellationToken ct = default)
    {
        // Try to get the default branch from origin/HEAD
        var result = await RunGitAsync(repoPath, ["symbolic-ref", "refs/remotes/origin/HEAD", "--short"], ct);
        if (result.Success)
        {
            var branch = result.StandardOutput.Trim();
            // Remove "origin/" prefix if present
            if (branch.StartsWith("origin/", StringComparison.OrdinalIgnoreCase))
            {
                branch = branch["origin/".Length..];
            }
            return GitResult<string>.Ok(branch);
        }

        // Fallback: check if 'main' exists
        var mainResult = await RunGitAsync(repoPath, ["show-ref", "--verify", "refs/heads/main"], ct);
        if (mainResult.Success)
        {
            return GitResult<string>.Ok("main");
        }

        // Fallback: check if 'master' exists
        var masterResult = await RunGitAsync(repoPath, ["show-ref", "--verify", "refs/heads/master"], ct);
        if (masterResult.Success)
        {
            return GitResult<string>.Ok("master");
        }

        return GitResult<string>.Fail("Unable to determine default branch");
    }

    public async Task<GitResult<string>> GetHeadCommitAsync(string repoPath, CancellationToken ct = default)
    {
        var result = await RunGitAsync(repoPath, ["rev-parse", "HEAD"], ct);
        if (!result.Success)
            return GitResult<string>.Fail(result.StandardError);

        return GitResult<string>.Ok(result.StandardOutput.Trim());
    }

    public async Task<GitResult<int>> CountCommitsSinceAsync(string repoPath, string commitSha, CancellationToken ct = default)
    {
        // Count commits from the given SHA to HEAD (exclusive of the SHA itself)
        var result = await RunGitAsync(repoPath, ["rev-list", "--count", $"{commitSha}..HEAD"], ct);
        if (!result.Success)
        {
            // If the SHA doesn't exist, return -1 to indicate unknown
            if (result.StandardError.Contains("unknown revision") ||
                result.StandardError.Contains("bad revision"))
            {
                return GitResult<int>.Ok(-1);
            }
            return GitResult<int>.Fail(result.StandardError);
        }

        if (int.TryParse(result.StandardOutput.Trim(), out var count))
        {
            return GitResult<int>.Ok(count);
        }

        return GitResult<int>.Fail("Unable to parse commit count");
    }

    public async Task<GitResult<DateTimeOffset>> GetCommitTimestampAsync(string repoPath, string commitSha, CancellationToken ct = default)
    {
        var result = await RunGitAsync(repoPath, ["show", "-s", "--format=%cI", commitSha], ct);
        if (!result.Success)
            return GitResult<DateTimeOffset>.Fail(result.StandardError);

        if (DateTimeOffset.TryParse(result.StandardOutput.Trim(), out var timestamp))
        {
            return GitResult<DateTimeOffset>.Ok(timestamp);
        }

        return GitResult<DateTimeOffset>.Fail("Unable to parse commit timestamp");
    }

    public async Task<GitResult<IReadOnlyList<string>>> GetTrackedFilesAsync(string repoPath, CancellationToken ct = default)
    {
        // Use git ls-files to get all tracked files (respects .gitignore)
        var result = await RunGitAsync(repoPath, ["ls-files", "--cached", "--others", "--exclude-standard"], ct);
        if (!result.Success)
            return GitResult<IReadOnlyList<string>>.Fail(result.StandardError);

        var files = result.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        return GitResult<IReadOnlyList<string>>.Ok(files);
    }

    private async Task<ProcessResult> RunGitAsync(string workDir, string[] args, CancellationToken ct)
    {
        return await RunGitAsync(workDir, args, githubToken: null, ct);
    }

    private async Task<ProcessResult> RunGitAsync(string workDir, string[] args, string? githubToken, CancellationToken ct)
    {
        var options = new ProcessOptions
        {
            WorkingDirectory = workDir,
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Add GitHub token to environment if provided
        if (!string.IsNullOrEmpty(githubToken))
        {
            options = options with
            {
                Environment = new Dictionary<string, string>
                {
                    ["GH_TOKEN"] = githubToken,
                    ["GITHUB_TOKEN"] = githubToken
                }
            };
        }

        return await _process.RunAsync("git", args, options, ct);
    }

    /// <summary>
    /// Ensures a GitHub label exists in the repository, creating it if missing.
    /// </summary>
    private async Task EnsureLabelExistsAsync(string repoPath, string label, string? githubToken, CancellationToken ct)
    {
        var options = new ProcessOptions
        {
            WorkingDirectory = repoPath,
            Timeout = TimeSpan.FromSeconds(15)
        };

        if (!string.IsNullOrEmpty(githubToken))
        {
            options = options with
            {
                Environment = new Dictionary<string, string>
                {
                    ["GH_TOKEN"] = githubToken,
                    ["GITHUB_TOKEN"] = githubToken
                }
            };
        }

        // Try to create the label - gh label create is idempotent (fails gracefully if exists)
        var result = await _process.RunAsync("gh", [
            "label", "create", label,
            "--description", "Generated by Aura AI workflows",
            "--color", "6366f1",  // Indigo color
            "--force"  // Update if exists (avoids error)
        ], options, ct);

        if (result.Success)
        {
            _logger.LogDebug("Ensured label '{Label}' exists", label);
        }
        else
        {
            // Log but don't fail - label creation is best-effort
            _logger.LogDebug("Could not ensure label '{Label}': {Error}", label, result.StandardError);
        }
    }
}
