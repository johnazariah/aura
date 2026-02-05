---
title: "ADR-014: Git Operations"
status: "Accepted"
date: "2026-01-30"
authors: "Anvil Team"
tags: ["architecture", "git", "infrastructure"]
supersedes: ""
superseded_by: ""
---

# ADR-014: Git Operations

## Status

Accepted

## Context

Anvil interacts with git in several ways:

| Operation | Purpose | Owner |
|-----------|---------|-------|
| Worktree create/remove | Isolation per story | **Aura** (per ADR-010) |
| Test repo maintenance | Fixture repositories for stories | **Anvil** |
| Submodule management | Pull test repos as submodules | **Anvil** |
| Read generated files | Validation | **Anvil** (filesystem) |

**Key insight:** Anvil needs to maintain **test repositories** as fixtures. These are sample codebases that brownfield stories operate on. They'll likely be git submodules of the Anvil repo.

### Test Repository Examples

```
anvil/
├── fixtures/
│   ├── repos/
│   │   ├── sample-dotnet-api/     # Git submodule
│   │   ├── sample-python-cli/     # Git submodule
│   │   └── sample-typescript-lib/ # Git submodule
│   └── extensions/
│       └── anvil-test-runner/     # VS Code test extension
```

### Git Operations Anvil Needs

| Operation | Use Case |
|-----------|----------|
| `git submodule update` | Pull latest test repos |
| `git submodule add` | Add new test repo |
| `git clone` | Clone test repo for one-off setup |
| `git status` | Check if repos are clean |
| `git reset --hard` | Reset test repo to known state |
| `git checkout` | Switch to specific branch/tag |

## Decision

**Anvil shells out to the `git` CLI** for git operations. No library dependency.

### Implementation

```csharp
public interface IGitOperations
{
    Task<Result<Unit, GitError>> SubmoduleUpdateAsync(
        string repoPath,
        CancellationToken ct);
    
    Task<Result<Unit, GitError>> ResetHardAsync(
        string repoPath,
        string? target = null,
        CancellationToken ct = default);
    
    Task<Result<bool, GitError>> IsCleanAsync(
        string repoPath,
        CancellationToken ct);
    
    Task<Result<Unit, GitError>> CheckoutAsync(
        string repoPath,
        string branchOrTag,
        CancellationToken ct);
}

public class GitCliOperations(ILogger<GitCliOperations> logger) : IGitOperations
{
    public async Task<Result<Unit, GitError>> SubmoduleUpdateAsync(
        string repoPath,
        CancellationToken ct)
    {
        return await RunGitAsync(
            repoPath,
            ["submodule", "update", "--init", "--recursive"],
            ct);
    }
    
    private async Task<Result<Unit, GitError>> RunGitAsync(
        string workingDirectory,
        string[] args,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);
        
        using var process = Process.Start(psi);
        if (process == null)
            return new GitError.ProcessStartFailed("Failed to start git");
        
        await process.WaitForExitAsync(ct);
        
        if (process.ExitCode != 0)
        {
            var stderr = await process.StandardError.ReadToEndAsync(ct);
            return new GitError.CommandFailed(process.ExitCode, stderr);
        }
        
        return Unit.Value;
    }
}
```

### Error Types

```csharp
public abstract record GitError(string Message)
{
    public record ProcessStartFailed(string Reason) 
        : GitError($"Failed to start git: {Reason}");
    
    public record CommandFailed(int ExitCode, string Stderr)
        : GitError($"Git command failed ({ExitCode}): {Stderr}");
    
    public record NotARepository(string Path)
        : GitError($"Not a git repository: {Path}");
}
```

### Test Repository Setup

Before running brownfield stories:

```bash
# Ensure submodules are up to date
anvil setup

# Which runs:
git submodule update --init --recursive
```

### Story Execution Flow (Brownfield)

```
1. Anvil checks fixture repo is clean
2. Anvil tells Aura: "Execute story against fixtures/repos/sample-dotnet-api"
3. Aura creates worktree from that repo
4. Aura executes story in worktree
5. Anvil validates in worktree
6. Cleanup: Aura removes worktree
7. Fixture repo remains untouched
```

### Why Shell Out vs Library?

| Approach | Pros | Cons |
|----------|------|------|
| **Shell out (chosen)** | Simple, no dependency, uses user's git config | Requires git installed, parsing output |
| **LibGit2Sharp** | Type-safe, no external dependency | Heavy library, version compatibility issues, less features than CLI |

Git CLI is already required for Aura, so no additional dependency.

## Consequences

**Positive**

- **POS-001**: No additional NuGet dependencies
- **POS-002**: Uses user's git config (credentials, SSH keys)
- **POS-003**: Full git CLI feature set available
- **POS-004**: Easy to debug (can run same commands manually)
- **POS-005**: Git is already required for Aura

**Negative**

- **NEG-001**: Requires git installed on machine
- **NEG-002**: Must parse CLI output for some operations
- **NEG-003**: Process spawning overhead (minimal)

## Alternatives Considered

### Alternative 1: Filesystem Only

- **Description**: No git operations, just file reads
- **Rejection Reason**: Need submodule management for test repositories

### Alternative 2: LibGit2Sharp

- **Description**: .NET git library
- **Rejection Reason**: Heavy dependency, version compatibility issues; can migrate later if needed

## Implementation Notes

- **IMP-001**: Create `IGitOperations` interface for testability
- **IMP-002**: `GitCliOperations` implementation shells out to `git`
- **IMP-003**: Test repos as git submodules in `fixtures/repos/`
- **IMP-004**: `anvil setup` command runs `git submodule update`
- **IMP-005**: Validate git is installed at startup
- **IMP-006**: Consider LibGit2Sharp migration if CLI parsing becomes painful

## References

- [ADR-010: Workspace Isolation](ADR-010-workspace-isolation.md)
- [Git Submodules Documentation](https://git-scm.com/book/en/v2/Git-Tools-Submodules)
- [LibGit2Sharp](https://github.com/libgit2/libgit2sharp) (future consideration)
