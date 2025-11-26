# Phase 5: Git Worktrees

**Duration:** 1-2 hours  
**Dependencies:** Phase 3 (Data Layer)  
**Output:** Git worktree service for concurrent workflow execution

## Objective

Port and simplify the existing git worktree functionality for isolated workflow execution.

## Tasks

### 5.1 Create Git Folder Structure

```
src/Aura/
└── Git/
    ├── IWorktreeService.cs
    ├── WorktreeService.cs
    ├── WorktreeInfo.cs
    └── GitException.cs
```

### 5.2 Define Interfaces

**IWorktreeService.cs:**
```csharp
namespace Aura.Git;

public interface IWorktreeService
{
    Task<WorktreeInfo> CreateAsync(
        string mainRepoPath,
        Guid workflowId,
        string? baseBranch = null,
        CancellationToken ct = default);
    
    Task<WorktreeInfo?> GetAsync(Guid workflowId, CancellationToken ct = default);
    
    Task<IReadOnlyList<WorktreeInfo>> ListAsync(
        string mainRepoPath,
        CancellationToken ct = default);
    
    Task RemoveAsync(Guid workflowId, bool force = false, CancellationToken ct = default);
    
    Task<string> CommitAsync(
        Guid workflowId,
        string message,
        CancellationToken ct = default);
    
    Task PushAsync(Guid workflowId, string? remote = null, CancellationToken ct = default);
    
    Task<bool> HasChangesAsync(Guid workflowId, CancellationToken ct = default);
}
```

**WorktreeInfo.cs:**
```csharp
namespace Aura.Git;

public record WorktreeInfo(
    Guid WorkflowId,
    string WorktreePath,
    string BranchName,
    string MainRepoPath,
    bool IsClean,
    DateTime CreatedAt);
```

### 5.3 Implement WorktreeService

**WorktreeService.cs:**
```csharp
namespace Aura.Git;

public class WorktreeService : IWorktreeService
{
    private readonly string _workspacesRoot;
    private readonly ILogger<WorktreeService> _logger;
    private readonly Dictionary<Guid, WorktreeInfo> _cache = new();
    
    public WorktreeService(IConfiguration config, ILogger<WorktreeService> logger)
    {
        _workspacesRoot = config["Git:WorkspacesRoot"] 
            ?? Path.Combine(Path.GetTempPath(), "aura-workspaces");
        _logger = logger;
        
        Directory.CreateDirectory(_workspacesRoot);
    }
    
    public async Task<WorktreeInfo> CreateAsync(
        string mainRepoPath,
        Guid workflowId,
        string? baseBranch = null,
        CancellationToken ct = default)
    {
        var repoName = Path.GetFileName(mainRepoPath);
        var branchName = $"aura/workflow-{workflowId:N}";
        var worktreePath = Path.Combine(_workspacesRoot, $"{repoName}-{workflowId:N}");
        
        _logger.LogInformation(
            "Creating worktree for workflow {WorkflowId} at {Path}",
            workflowId, worktreePath);
        
        // Get base branch if not specified
        baseBranch ??= await GetCurrentBranchAsync(mainRepoPath, ct);
        
        // Create branch
        await RunGitAsync(mainRepoPath, $"branch {branchName} {baseBranch}", ct);
        
        // Create worktree
        await RunGitAsync(mainRepoPath, $"worktree add \"{worktreePath}\" {branchName}", ct);
        
        var info = new WorktreeInfo(
            workflowId,
            worktreePath,
            branchName,
            mainRepoPath,
            IsClean: true,
            DateTime.UtcNow);
        
        _cache[workflowId] = info;
        
        _logger.LogInformation(
            "Created worktree {Path} on branch {Branch}",
            worktreePath, branchName);
        
        return info;
    }
    
    public Task<WorktreeInfo?> GetAsync(Guid workflowId, CancellationToken ct = default)
    {
        return Task.FromResult(_cache.GetValueOrDefault(workflowId));
    }
    
    public async Task<IReadOnlyList<WorktreeInfo>> ListAsync(
        string mainRepoPath,
        CancellationToken ct = default)
    {
        var output = await RunGitAsync(mainRepoPath, "worktree list --porcelain", ct);
        var worktrees = new List<WorktreeInfo>();
        
        // Parse porcelain output
        // Format:
        // worktree /path/to/worktree
        // HEAD abc123
        // branch refs/heads/branch-name
        // (blank line)
        
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        string? currentPath = null;
        string? currentBranch = null;
        
        foreach (var line in lines)
        {
            if (line.StartsWith("worktree "))
            {
                currentPath = line[9..];
            }
            else if (line.StartsWith("branch refs/heads/"))
            {
                currentBranch = line[18..];
                
                if (currentPath is not null && currentBranch?.StartsWith("aura/workflow-") == true)
                {
                    var idPart = currentBranch["aura/workflow-".Length..];
                    if (Guid.TryParse(idPart, out var workflowId))
                    {
                        worktrees.Add(new WorktreeInfo(
                            workflowId,
                            currentPath,
                            currentBranch,
                            mainRepoPath,
                            await IsCleanAsync(currentPath, ct),
                            File.GetCreationTimeUtc(currentPath)));
                    }
                }
                
                currentPath = null;
                currentBranch = null;
            }
        }
        
        return worktrees;
    }
    
    public async Task RemoveAsync(Guid workflowId, bool force = false, CancellationToken ct = default)
    {
        var info = await GetAsync(workflowId, ct);
        if (info is null)
        {
            _logger.LogWarning("Worktree not found for workflow {WorkflowId}", workflowId);
            return;
        }
        
        _logger.LogInformation("Removing worktree {Path}", info.WorktreePath);
        
        var forceFlag = force ? " --force" : "";
        await RunGitAsync(info.MainRepoPath, $"worktree remove \"{info.WorktreePath}\"{forceFlag}", ct);
        
        // Delete branch
        await RunGitAsync(info.MainRepoPath, $"branch -D {info.BranchName}", ct);
        
        _cache.Remove(workflowId);
    }
    
    public async Task<string> CommitAsync(
        Guid workflowId,
        string message,
        CancellationToken ct = default)
    {
        var info = await GetAsync(workflowId, ct) 
            ?? throw new GitException($"Worktree not found for workflow {workflowId}");
        
        // Stage all changes
        await RunGitAsync(info.WorktreePath, "add -A", ct);
        
        // Check if there are changes
        var status = await RunGitAsync(info.WorktreePath, "status --porcelain", ct);
        if (string.IsNullOrWhiteSpace(status))
        {
            _logger.LogDebug("No changes to commit in {Path}", info.WorktreePath);
            return await GetHeadCommitAsync(info.WorktreePath, ct);
        }
        
        // Commit
        var escapedMessage = message.Replace("\"", "\\\"");
        await RunGitAsync(info.WorktreePath, $"commit -m \"{escapedMessage}\"", ct);
        
        var commitHash = await GetHeadCommitAsync(info.WorktreePath, ct);
        _logger.LogInformation("Committed {Hash} in {Path}", commitHash[..7], info.WorktreePath);
        
        return commitHash;
    }
    
    public async Task PushAsync(Guid workflowId, string? remote = null, CancellationToken ct = default)
    {
        var info = await GetAsync(workflowId, ct)
            ?? throw new GitException($"Worktree not found for workflow {workflowId}");
        
        remote ??= "origin";
        
        await RunGitAsync(info.WorktreePath, $"push -u {remote} {info.BranchName}", ct);
        _logger.LogInformation("Pushed {Branch} to {Remote}", info.BranchName, remote);
    }
    
    public async Task<bool> HasChangesAsync(Guid workflowId, CancellationToken ct = default)
    {
        var info = await GetAsync(workflowId, ct);
        if (info is null) return false;
        
        return !await IsCleanAsync(info.WorktreePath, ct);
    }
    
    // Helper methods
    
    private async Task<string> RunGitAsync(string workingDir, string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = args,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        using var process = Process.Start(psi) 
            ?? throw new GitException("Failed to start git process");
        
        var output = await process.StandardOutput.ReadToEndAsync(ct);
        var error = await process.StandardError.ReadToEndAsync(ct);
        
        await process.WaitForExitAsync(ct);
        
        if (process.ExitCode != 0)
        {
            _logger.LogError("Git command failed: git {Args}\n{Error}", args, error);
            throw new GitException($"Git command failed: {error}");
        }
        
        return output.Trim();
    }
    
    private async Task<string> GetCurrentBranchAsync(string repoPath, CancellationToken ct)
    {
        return await RunGitAsync(repoPath, "rev-parse --abbrev-ref HEAD", ct);
    }
    
    private async Task<string> GetHeadCommitAsync(string repoPath, CancellationToken ct)
    {
        return await RunGitAsync(repoPath, "rev-parse HEAD", ct);
    }
    
    private async Task<bool> IsCleanAsync(string repoPath, CancellationToken ct)
    {
        var status = await RunGitAsync(repoPath, "status --porcelain", ct);
        return string.IsNullOrWhiteSpace(status);
    }
}
```

**GitException.cs:**
```csharp
namespace Aura.Git;

public class GitException : Exception
{
    public GitException(string message) : base(message) { }
    public GitException(string message, Exception inner) : base(message, inner) { }
}
```

### 5.4 Integrate with Workflow Endpoints

Add to `WorkflowEndpoints.cs`:

```csharp
// After CreateWorkflow, optionally create worktree
private static async Task<IResult> CreateWorkflowWithWorktree(
    CreateWorkflowRequest request,
    IWorkflowRepository repo,
    IWorktreeService worktrees)
{
    var workflow = new Workflow
    {
        WorkItemId = request.WorkItemId,
        WorkItemTitle = request.WorkItemTitle,
        WorkItemDescription = request.WorkItemDescription,
        WorkspacePath = request.WorkspacePath
    };
    
    await repo.CreateAsync(workflow);
    
    // Create worktree if workspace path provided
    if (request.WorkspacePath is not null)
    {
        var worktree = await worktrees.CreateAsync(
            request.WorkspacePath, 
            workflow.Id);
        
        workflow.WorkspacePath = worktree.WorktreePath;
        workflow.GitBranch = worktree.BranchName;
        await repo.UpdateAsync(workflow);
    }
    
    return Results.Created($"/api/workflows/{workflow.Id}", ToDetail(workflow));
}
```

### 5.5 DI Registration

**ServiceCollectionExtensions.cs (addition):**
```csharp
namespace Aura.Git;

public static class GitServiceCollectionExtensions
{
    public static IServiceCollection AddAuraGit(this IServiceCollection services)
    {
        services.AddSingleton<IWorktreeService, WorktreeService>();
        return services;
    }
}
```

### 5.6 Add Unit Tests

```csharp
public class WorktreeServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly WorktreeService _service;
    
    public WorktreeServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"aura-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        
        // Initialize test repo
        InitializeTestRepo();
        
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Git:WorkspacesRoot"] = _tempDir
            })
            .Build();
        
        var logger = NullLogger<WorktreeService>.Instance;
        _service = new WorktreeService(config, logger);
    }
    
    [Fact]
    public async Task CreateAsync_CreatesWorktreeAndBranch()
    {
        var workflowId = Guid.NewGuid();
        var mainRepoPath = Path.Combine(_tempDir, "main-repo");
        
        var info = await _service.CreateAsync(mainRepoPath, workflowId);
        
        info.WorkflowId.Should().Be(workflowId);
        Directory.Exists(info.WorktreePath).Should().BeTrue();
        info.BranchName.Should().StartWith("aura/workflow-");
    }
    
    [Fact]
    public async Task CommitAsync_CommitsChanges()
    {
        var workflowId = Guid.NewGuid();
        var info = await _service.CreateAsync(_mainRepoPath, workflowId);
        
        // Create a file
        await File.WriteAllTextAsync(
            Path.Combine(info.WorktreePath, "test.txt"), 
            "Hello, World!");
        
        var hash = await _service.CommitAsync(workflowId, "Add test file");
        
        hash.Should().NotBeNullOrEmpty();
        (await _service.HasChangesAsync(workflowId)).Should().BeFalse();
    }
    
    [Fact]
    public async Task RemoveAsync_RemovesWorktreeAndBranch()
    {
        var workflowId = Guid.NewGuid();
        var info = await _service.CreateAsync(_mainRepoPath, workflowId);
        
        await _service.RemoveAsync(workflowId);
        
        Directory.Exists(info.WorktreePath).Should().BeFalse();
        (await _service.GetAsync(workflowId)).Should().BeNull();
    }
    
    public void Dispose()
    {
        // Clean up temp directory
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* ignore */ }
    }
    
    private void InitializeTestRepo()
    {
        var repoPath = Path.Combine(_tempDir, "main-repo");
        Directory.CreateDirectory(repoPath);
        
        Process.Start(new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "init",
            WorkingDirectory = repoPath,
            UseShellExecute = false
        })?.WaitForExit();
        
        Process.Start(new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "commit --allow-empty -m \"Initial commit\"",
            WorkingDirectory = repoPath,
            UseShellExecute = false
        })?.WaitForExit();
    }
}
```

## Verification

1. ✅ `dotnet build src/Aura` succeeds
2. ✅ Unit tests with real git pass
3. ✅ Create worktree, make changes, commit works
4. ✅ Remove worktree cleans up branch

## Deliverables

- [ ] `IWorktreeService` interface
- [ ] `WorktreeService` implementation
- [ ] Integration with workflow creation
- [ ] Unit tests with real git repos

## Configuration

```json
{
  "Git": {
    "WorkspacesRoot": "/workspaces",
    "DefaultRemote": "origin"
  }
}
```

## What We Simplified

| Removed | Reason |
|---------|--------|
| Complex retry logic | Simple fail-fast |
| Worktree pool | On-demand creation |
| Branch protection checks | Not needed for v1 |
| Merge/rebase helpers | User handles conflicts |

**Estimated reduction:** ~1,500 lines → ~300 lines
