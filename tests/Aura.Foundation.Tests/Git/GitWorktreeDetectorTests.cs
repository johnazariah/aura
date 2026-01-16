using Aura.Foundation.Git;
using Xunit;

namespace Aura.Foundation.Tests.Git;

/// <summary>
/// Unit tests for <see cref="GitWorktreeDetector"/>.
/// </summary>
public class GitWorktreeDetectorTests : IDisposable
{
    private readonly string _tempDir;

    public GitWorktreeDetectorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"GitWorktreeDetectorTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup failures in tests
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Detect_WithNullPath_ReturnsNull()
    {
        var result = GitWorktreeDetector.Detect(null!);

        Assert.Null(result);
    }

    [Fact]
    public void Detect_WithEmptyPath_ReturnsNull()
    {
        var result = GitWorktreeDetector.Detect(string.Empty);

        Assert.Null(result);
    }

    [Fact]
    public void Detect_WithNonGitDirectory_ReturnsNull()
    {
        var result = GitWorktreeDetector.Detect(_tempDir);

        Assert.Null(result);
    }

    [Fact]
    public void Detect_WithMainRepo_ReturnsMainRepoInfo()
    {
        // Arrange - create a fake main repo with .git directory
        var repoPath = Path.Combine(_tempDir, "main-repo");
        var gitDir = Path.Combine(repoPath, ".git");
        Directory.CreateDirectory(gitDir);

        // Act
        var result = GitWorktreeDetector.Detect(repoPath);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Value.IsWorktree);
        Assert.Equal(Path.GetFullPath(repoPath), result.Value.MainRepoPath);
        Assert.Equal(Path.GetFullPath(repoPath), result.Value.WorktreePath);
        Assert.Equal(gitDir, result.Value.GitDir);
    }

    [Fact]
    public void Detect_WithSubdirectoryOfMainRepo_ReturnsMainRepoInfo()
    {
        // Arrange - create a fake main repo with nested directory
        var repoPath = Path.Combine(_tempDir, "main-repo");
        var gitDir = Path.Combine(repoPath, ".git");
        var subDir = Path.Combine(repoPath, "src", "project");
        Directory.CreateDirectory(gitDir);
        Directory.CreateDirectory(subDir);

        // Act
        var result = GitWorktreeDetector.Detect(subDir);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Value.IsWorktree);
        Assert.Equal(Path.GetFullPath(repoPath), result.Value.MainRepoPath);
    }

    [Fact]
    public void Detect_WithWorktree_ReturnsWorktreeInfo()
    {
        // Arrange - create a fake main repo and worktree
        var mainRepoPath = Path.Combine(_tempDir, "main-repo");
        var mainGitDir = Path.Combine(mainRepoPath, ".git");
        var worktreesDir = Path.Combine(mainGitDir, "worktrees", "my-worktree");
        Directory.CreateDirectory(worktreesDir);

        var worktreePath = Path.Combine(_tempDir, "my-worktree");
        Directory.CreateDirectory(worktreePath);

        // Create .git file (not directory) in worktree pointing to main repo
        var gitFilePath = Path.Combine(worktreePath, ".git");
        File.WriteAllText(gitFilePath, $"gitdir: {worktreesDir}");

        // Act
        var result = GitWorktreeDetector.Detect(worktreePath);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Value.IsWorktree);
        Assert.Equal(Path.GetFullPath(worktreePath), result.Value.WorktreePath);
        Assert.Equal(Path.GetFullPath(mainRepoPath), result.Value.MainRepoPath);
        Assert.Equal(worktreesDir, result.Value.GitDir);
    }

    [Fact]
    public void Detect_WithSubdirectoryOfWorktree_ReturnsWorktreeInfo()
    {
        // Arrange - create a fake worktree with nested directory
        var mainRepoPath = Path.Combine(_tempDir, "main-repo");
        var mainGitDir = Path.Combine(mainRepoPath, ".git");
        var worktreesDir = Path.Combine(mainGitDir, "worktrees", "feature-branch");
        Directory.CreateDirectory(worktreesDir);

        var worktreePath = Path.Combine(_tempDir, "feature-branch");
        var subDir = Path.Combine(worktreePath, "src", "project");
        Directory.CreateDirectory(subDir);

        var gitFilePath = Path.Combine(worktreePath, ".git");
        File.WriteAllText(gitFilePath, $"gitdir: {worktreesDir}");

        // Act
        var result = GitWorktreeDetector.Detect(subDir);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Value.IsWorktree);
        Assert.Equal(Path.GetFullPath(worktreePath), result.Value.WorktreePath);
        Assert.Equal(Path.GetFullPath(mainRepoPath), result.Value.MainRepoPath);
    }

    [Fact]
    public void TranslatePath_FromMainRepoToWorktree_TranslatesCorrectly()
    {
        // Arrange
        var mainRepoPath = NormalizePath("/repo/main");
        var worktreePath = NormalizePath("/repo/feature-branch");
        var worktree = new DetectedWorktree(
            WorktreePath: worktreePath,
            MainRepoPath: mainRepoPath,
            GitDir: NormalizePath("/repo/main/.git/worktrees/feature-branch"),
            IsWorktree: true);

        var mainRepoFilePath = NormalizePath("/repo/main/src/Program.cs");

        // Act
        var result = GitWorktreeDetector.TranslatePath(mainRepoFilePath, worktree);

        // Assert
        Assert.Equal(NormalizePath("/repo/feature-branch/src/Program.cs"), result);
    }

    [Fact]
    public void TranslatePath_WhenNotWorktree_ReturnsOriginalPath()
    {
        // Arrange
        var repoPath = NormalizePath("/repo/main");
        var mainRepo = new DetectedWorktree(
            WorktreePath: repoPath,
            MainRepoPath: repoPath,
            GitDir: NormalizePath("/repo/main/.git"),
            IsWorktree: false);

        var filePath = NormalizePath("/repo/main/src/Program.cs");

        // Act
        var result = GitWorktreeDetector.TranslatePath(filePath, mainRepo);

        // Assert
        Assert.Equal(filePath, result);
    }

    [Fact]
    public void TranslateToMainRepo_FromWorktreeToMainRepo_TranslatesCorrectly()
    {
        // Arrange
        var mainRepoPath = NormalizePath("/repo/main");
        var worktreePath = NormalizePath("/repo/feature-branch");
        var worktree = new DetectedWorktree(
            WorktreePath: worktreePath,
            MainRepoPath: mainRepoPath,
            GitDir: NormalizePath("/repo/main/.git/worktrees/feature-branch"),
            IsWorktree: true);

        var worktreeFilePath = NormalizePath("/repo/feature-branch/src/Program.cs");

        // Act
        var result = GitWorktreeDetector.TranslateToMainRepo(worktreeFilePath, worktree);

        // Assert
        Assert.Equal(NormalizePath("/repo/main/src/Program.cs"), result);
    }

    [Fact]
    public void Detect_WithRelativeGitdirPath_ResolvesCorrectly()
    {
        // Arrange - some git versions use relative paths in .git file
        var mainRepoPath = Path.Combine(_tempDir, "main-repo");
        var mainGitDir = Path.Combine(mainRepoPath, ".git");
        var worktreesDir = Path.Combine(mainGitDir, "worktrees", "rel-worktree");
        Directory.CreateDirectory(worktreesDir);

        var worktreePath = Path.Combine(_tempDir, "rel-worktree");
        Directory.CreateDirectory(worktreePath);

        // Use relative path in .git file
        var relativeGitDir = Path.Combine("..", "main-repo", ".git", "worktrees", "rel-worktree");
        var gitFilePath = Path.Combine(worktreePath, ".git");
        File.WriteAllText(gitFilePath, $"gitdir: {relativeGitDir}");

        // Act
        var result = GitWorktreeDetector.Detect(worktreePath);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Value.IsWorktree);
        Assert.Equal(Path.GetFullPath(mainRepoPath), result.Value.MainRepoPath);
    }

    /// <summary>
    /// Normalizes a Unix-style path to the current platform's format.
    /// </summary>
    private static string NormalizePath(string unixPath)
    {
        if (OperatingSystem.IsWindows())
        {
            // Convert /repo/main to C:\repo\main for Windows
            return "C:" + unixPath.Replace('/', '\\');
        }

        return unixPath;
    }
}
