namespace Aura.Foundation.Shell;

/// <summary>
/// Cross-platform process execution.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Run a command with arguments.
    /// </summary>
    Task<ProcessResult> RunAsync(
        string command,
        string[] args,
        ProcessOptions? options = null,
        CancellationToken ct = default);
    
    /// <summary>
    /// Run a shell command string (e.g., "ls -la | grep foo").
    /// Uses the default shell for the platform.
    /// </summary>
    Task<ProcessResult> RunShellAsync(
        string shellCommand,
        ProcessOptions? options = null,
        CancellationToken ct = default);
    
    /// <summary>
    /// Get the default shell for the current platform.
    /// </summary>
    ShellInfo GetDefaultShell();
}

/// <summary>
/// Options for process execution.
/// </summary>
public record ProcessOptions
{
    /// <summary>Working directory for the process</summary>
    public string? WorkingDirectory { get; init; }
    
    /// <summary>Environment variables to set</summary>
    public IDictionary<string, string>? Environment { get; init; }
    
    /// <summary>Timeout for the process (null = no timeout)</summary>
    public TimeSpan? Timeout { get; init; }
    
    /// <summary>Whether to capture standard output</summary>
    public bool CaptureOutput { get; init; } = true;
    
    /// <summary>Whether to capture standard error</summary>
    public bool CaptureError { get; init; } = true;
    
    /// <summary>Input to write to stdin</summary>
    public string? StandardInput { get; init; }
}

/// <summary>
/// Result of process execution.
/// </summary>
public record ProcessResult
{
    /// <summary>Exit code from the process</summary>
    public required int ExitCode { get; init; }
    
    /// <summary>Captured standard output</summary>
    public string StandardOutput { get; init; } = "";
    
    /// <summary>Captured standard error</summary>
    public string StandardError { get; init; } = "";
    
    /// <summary>How long the process ran</summary>
    public TimeSpan Duration { get; init; }
    
    /// <summary>Whether the process was killed due to timeout</summary>
    public bool TimedOut { get; init; }
    
    /// <summary>Whether the process exited successfully (exit code 0)</summary>
    public bool Success => ExitCode == 0 && !TimedOut;
}

/// <summary>
/// Information about a shell.
/// </summary>
public record ShellInfo
{
    /// <summary>Path to the shell executable</summary>
    public required string Path { get; init; }
    
    /// <summary>Argument to pass a command string (e.g., "-c" for bash, "/c" for cmd)</summary>
    public required string CommandArg { get; init; }
    
    /// <summary>Shell name (e.g., "bash", "pwsh", "cmd")</summary>
    public required string Name { get; init; }
}
