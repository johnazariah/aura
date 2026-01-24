using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Aura.Foundation.Tools;
using Microsoft.Extensions.Logging;

namespace Aura.Foundation.Shell;

/// <summary>
/// Cross-platform process runner implementation.
/// </summary>
public class ProcessRunner(ILogger<ProcessRunner> logger) : IProcessRunner
{
    private readonly ILogger<ProcessRunner> _logger = logger;
    private readonly ShellInfo _defaultShell = DetectDefaultShell();

    public async Task<ProcessResult> RunAsync(
        string command,
        string[] args,
        ProcessOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new ProcessOptions();
        var sw = Stopwatch.StartNew();

        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = options.CaptureOutput,
            RedirectStandardError = options.CaptureError,
            RedirectStandardInput = options.StandardInput is not null,
            WorkingDirectory = options.WorkingDirectory ?? Directory.GetCurrentDirectory()
        };

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        if (options.Environment is not null)
        {
            foreach (var (key, value) in options.Environment)
            {
                startInfo.Environment[key] = value;
            }
        }

        _logger.LogDebug("Running: {Command} {Args}", command, string.Join(" ", args));

        try
        {
            using var process = new Process { StartInfo = startInfo };

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            if (options.CaptureOutput)
            {
                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data is not null)
                        stdout.AppendLine(e.Data);
                };
            }

            if (options.CaptureError)
            {
                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data is not null)
                        stderr.AppendLine(e.Data);
                };
            }

            process.Start();

            if (options.CaptureOutput) process.BeginOutputReadLine();
            if (options.CaptureError) process.BeginErrorReadLine();

            if (options.StandardInput is not null)
            {
                await process.StandardInput.WriteAsync(options.StandardInput);
                process.StandardInput.Close();
            }

            // Wait with timeout
            using var timeoutCts = options.Timeout.HasValue
                ? new CancellationTokenSource(options.Timeout.Value)
                : new CancellationTokenSource();

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            try
            {
                await process.WaitForExitAsync(linkedCts.Token);
                sw.Stop();

                return new ProcessResult
                {
                    ExitCode = process.ExitCode,
                    StandardOutput = stdout.ToString().TrimEnd(),
                    StandardError = stderr.ToString().TrimEnd(),
                    Duration = sw.Elapsed,
                    TimedOut = false
                };
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                // Timeout
                try { process.Kill(entireProcessTree: true); } catch { }
                sw.Stop();

                _logger.LogWarning("Process timed out: {Command}", command);

                return new ProcessResult
                {
                    ExitCode = -1,
                    StandardOutput = stdout.ToString().TrimEnd(),
                    StandardError = stderr.ToString().TrimEnd(),
                    Duration = sw.Elapsed,
                    TimedOut = true
                };
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Failed to run process: {Command}", command);

            return new ProcessResult
            {
                ExitCode = -1,
                StandardError = ex.Message,
                Duration = sw.Elapsed
            };
        }
    }

    public async Task<ProcessResult> RunShellAsync(
        string shellCommand,
        ProcessOptions? options = null,
        CancellationToken ct = default)
    {
        var shell = _defaultShell;
        return await RunAsync(shell.Path, [shell.CommandArg, shellCommand], options, ct);
    }

    public ShellInfo GetDefaultShell() => _defaultShell;

    private static ShellInfo DetectDefaultShell()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Prefer PowerShell Core, fall back to Windows PowerShell, then cmd
            var pwshPath = FindExecutable("pwsh");
            if (pwshPath is not null)
            {
                return new ShellInfo { Path = pwshPath, CommandArg = "-Command", Name = "pwsh" };
            }

            var powershellPath = FindExecutable("powershell");
            if (powershellPath is not null)
            {
                return new ShellInfo { Path = powershellPath, CommandArg = "-Command", Name = "powershell" };
            }

            return new ShellInfo { Path = "cmd.exe", CommandArg = "/c", Name = "cmd" };
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Check SHELL env var first
            var shell = EnvHelper.GetOrDefault("SHELL", string.Empty);
            if (!string.IsNullOrEmpty(shell) && File.Exists(shell))
            {
                var name = Path.GetFileName(shell);
                return new ShellInfo { Path = shell, CommandArg = "-c", Name = name };
            }

            // Fall back to bash or sh
            if (File.Exists("/bin/bash"))
                return new ShellInfo { Path = "/bin/bash", CommandArg = "-c", Name = "bash" };

            return new ShellInfo { Path = "/bin/sh", CommandArg = "-c", Name = "sh" };
        }

        // Unknown platform, try sh
        return new ShellInfo { Path = "sh", CommandArg = "-c", Name = "sh" };
    }

    private static string? FindExecutable(string name)
    {
        var pathVar = EnvHelper.GetOrDefault("PATH", string.Empty);
        var separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';
        var extension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";

        foreach (var dir in pathVar.Split(separator))
        {
            var fullPath = Path.Combine(dir, name + extension);
            if (File.Exists(fullPath))
                return fullPath;
        }

        return null;
    }
}
