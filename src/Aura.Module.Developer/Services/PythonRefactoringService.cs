// <copyright file="PythonRefactoringService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

using System.Text.Json;
using Aura.Foundation.Shell;
using Microsoft.Extensions.Logging;

namespace Aura.Module.Developer.Services;

/// <summary>
/// Service for Python code refactoring operations using the rope library.
/// Executes refactoring operations via the Python refactor.py script.
/// </summary>
public sealed class PythonRefactoringService : IPythonRefactoringService
{
    private readonly IProcessRunner _processRunner;
    private readonly ILogger<PythonRefactoringService> _logger;
    private readonly string _scriptPath;
    private readonly string _pythonPath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="PythonRefactoringService"/> class.
    /// </summary>
    /// <param name="processRunner">Process runner for executing Python.</param>
    /// <param name="logger">Logger instance.</param>
    public PythonRefactoringService(
        IProcessRunner processRunner,
        ILogger<PythonRefactoringService> logger)
    {
        _processRunner = processRunner;
        _logger = logger;

        // Locate the refactor.py script
        _scriptPath = ResolveScriptPath();
        _pythonPath = ResolveExecutablePath("python");
        _logger.LogDebug("Python refactoring script path: {ScriptPath}, python: {PythonPath}", _scriptPath, _pythonPath);
    }

    /// <inheritdoc/>
    public async Task<PythonRefactoringResult> RenameSymbolAsync(
        PythonRenameRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var args = new List<string>
        {
            "rename",
            "--project", request.ProjectPath,
            "--file", request.FilePath,
            "--offset", request.Offset.ToString(),
            "--new-name", request.NewName,
        };

        if (request.Preview)
        {
            args.Add("--preview");
        }

        return await ExecuteRefactoringAsync(args, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<PythonRefactoringResult> ExtractMethodAsync(
        PythonExtractMethodRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var args = new List<string>
        {
            "extract-method",
            "--project", request.ProjectPath,
            "--file", request.FilePath,
            "--start-offset", request.StartOffset.ToString(),
            "--end-offset", request.EndOffset.ToString(),
            "--new-name", request.NewName,
        };

        if (request.Preview)
        {
            args.Add("--preview");
        }

        return await ExecuteRefactoringAsync(args, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<PythonRefactoringResult> ExtractVariableAsync(
        PythonExtractVariableRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var args = new List<string>
        {
            "extract-variable",
            "--project", request.ProjectPath,
            "--file", request.FilePath,
            "--start-offset", request.StartOffset.ToString(),
            "--end-offset", request.EndOffset.ToString(),
            "--new-name", request.NewName,
        };

        if (request.Preview)
        {
            args.Add("--preview");
        }

        return await ExecuteRefactoringAsync(args, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<PythonFindReferencesResult> FindReferencesAsync(
        PythonFindReferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var args = new List<string>
        {
            "find-references",
            "--project", request.ProjectPath,
            "--file", request.FilePath,
            "--offset", request.Offset.ToString(),
        };

        var (success, stdout, stderr) = await ExecutePythonScriptAsync(args, cancellationToken);

        if (!success)
        {
            return new PythonFindReferencesResult
            {
                Success = false,
                Error = stderr ?? "Python script execution failed",
            };
        }

        try
        {
            var result = JsonSerializer.Deserialize<JsonFindReferencesResult>(stdout, JsonOptions);
            if (result == null)
            {
                return new PythonFindReferencesResult
                {
                    Success = false,
                    Error = "Failed to parse refactoring result",
                };
            }

            if (!result.Success)
            {
                return new PythonFindReferencesResult
                {
                    Success = false,
                    Error = result.Error ?? "Unknown error",
                };
            }

            var references = result.References?
                .Select(r => new PythonReference
                {
                    FilePath = r.FilePath ?? string.Empty,
                    Offset = r.Offset,
                    IsDefinition = r.IsDefinition,
                    IsWrite = r.IsWrite,
                })
                .ToList() ?? [];

            return new PythonFindReferencesResult
            {
                Success = true,
                References = references,
                Count = result.Count,
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse find references result: {Output}", stdout);
            return new PythonFindReferencesResult
            {
                Success = false,
                Error = $"Failed to parse result: {ex.Message}",
            };
        }
    }

    /// <inheritdoc/>
    public async Task<PythonFindDefinitionResult> FindDefinitionAsync(
        PythonFindDefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var args = new List<string>
        {
            "find-definition",
            "--project", request.ProjectPath,
            "--file", request.FilePath,
            "--offset", request.Offset.ToString(),
        };

        var (success, stdout, stderr) = await ExecutePythonScriptAsync(args, cancellationToken);

        if (!success)
        {
            return new PythonFindDefinitionResult
            {
                Success = false,
                Error = stderr ?? "Python script execution failed",
            };
        }

        try
        {
            var result = JsonSerializer.Deserialize<JsonFindDefinitionResult>(stdout, JsonOptions);
            if (result == null)
            {
                return new PythonFindDefinitionResult
                {
                    Success = false,
                    Error = "Failed to parse refactoring result",
                };
            }

            if (!result.Success)
            {
                return new PythonFindDefinitionResult
                {
                    Success = false,
                    Error = result.Error ?? "Unknown error",
                };
            }

            return new PythonFindDefinitionResult
            {
                Success = true,
                Found = result.Found,
                FilePath = result.FilePath,
                Offset = result.Offset,
                Line = result.Line,
                Message = result.Message,
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse find definition result: {Output}", stdout);
            return new PythonFindDefinitionResult
            {
                Success = false,
                Error = $"Failed to parse result: {ex.Message}",
            };
        }
    }

    private async Task<PythonRefactoringResult> ExecuteRefactoringAsync(
        List<string> args,
        CancellationToken cancellationToken)
    {
        var (success, stdout, stderr) = await ExecutePythonScriptAsync(args, cancellationToken);

        if (!success)
        {
            return new PythonRefactoringResult
            {
                Success = false,
                Error = stderr ?? "Python script execution failed",
            };
        }

        try
        {
            var result = JsonSerializer.Deserialize<JsonRefactoringResult>(stdout, JsonOptions);
            if (result == null)
            {
                return new PythonRefactoringResult
                {
                    Success = false,
                    Error = "Failed to parse refactoring result",
                };
            }

            if (!result.Success)
            {
                return new PythonRefactoringResult
                {
                    Success = false,
                    Error = result.Error,
                    ErrorType = result.ErrorType,
                };
            }

            var fileChanges = result.FileChanges?
                .Select(fc => new PythonFileChange
                {
                    FilePath = fc.FilePath ?? string.Empty,
                    OldContent = fc.OldContent,
                    NewContent = fc.NewContent,
                })
                .ToList();

            return new PythonRefactoringResult
            {
                Success = true,
                Preview = result.Preview,
                ChangedFiles = result.ChangedFiles ?? [],
                Description = result.Description,
                FileChanges = fileChanges,
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse refactoring result: {Output}", stdout);
            return new PythonRefactoringResult
            {
                Success = false,
                Error = $"Failed to parse result: {ex.Message}",
            };
        }
    }

    private async Task<(bool Success, string Stdout, string Stderr)> ExecutePythonScriptAsync(
        List<string> args,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_scriptPath))
        {
            _logger.LogError("Python refactoring script not found at: {ScriptPath}", _scriptPath);
            return (false, string.Empty, $"Python refactoring script not found at: {_scriptPath}");
        }

        var allArgs = new List<string> { _scriptPath };
        allArgs.AddRange(args);

        _logger.LogDebug(
            "Executing Python refactoring: {Python} {Args}",
            _pythonPath,
            string.Join(" ", allArgs));

        try
        {
            var result = await _processRunner.RunAsync(
                _pythonPath,
                allArgs.ToArray(),
                options: null,
                ct: cancellationToken);

            if (result.ExitCode != 0)
            {
                _logger.LogWarning(
                    "Python refactoring script failed with exit code {ExitCode}: {Stderr}",
                    result.ExitCode,
                    result.StandardError);

                // Try to parse error from stdout (script outputs JSON even on errors)
                if (!string.IsNullOrWhiteSpace(result.StandardOutput))
                {
                    return (true, result.StandardOutput, result.StandardError);
                }

                return (false, result.StandardOutput, result.StandardError);
            }

            return (true, result.StandardOutput, result.StandardError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute Python refactoring script");
            return (false, string.Empty, ex.Message);
        }
    }

    // JSON DTOs for deserialization
    private sealed class JsonRefactoringResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string? ErrorType { get; set; }
        public bool Preview { get; set; }
        public List<string>? ChangedFiles { get; set; }
        public string? Description { get; set; }
        public List<JsonFileChange>? FileChanges { get; set; }
    }

    private sealed class JsonFileChange
    {
        public string? FilePath { get; set; }
        public string? OldContent { get; set; }
        public string? NewContent { get; set; }
    }

    private sealed class JsonFindReferencesResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public List<JsonReference>? References { get; set; }
        public int Count { get; set; }
    }

    private sealed class JsonReference
    {
        public string? FilePath { get; set; }
        public int Offset { get; set; }
        public bool IsDefinition { get; set; }
        public bool IsWrite { get; set; }
    }

    private sealed class JsonFindDefinitionResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public bool Found { get; set; }
        public string? FilePath { get; set; }
        public int? Offset { get; set; }
        public int? Line { get; set; }
        public string? Message { get; set; }
    }

    /// <summary>
    /// Resolves the full path to an executable by searching PATH and well-known installation directories.
    /// This is necessary because Windows Services may not inherit the user's PATH (e.g., nvm symlinks
    /// pointing to user profile directories are inaccessible to the service account).
    /// </summary>
    private static string ResolveExecutablePath(string name)
    {
        var isWindows = OperatingSystem.IsWindows();
        var ext = isWindows ? ".exe" : "";
        var fileName = name + ext;

        // 1. Try PATH first
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        var separator = isWindows ? ';' : ':';
        foreach (var dir in pathVar.Split(separator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        if (isWindows)
        {
            // 2. Check well-known Windows install locations
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

            // Python installs to Program Files\PythonXY or via Windows Store
            var candidate = Path.Combine(programFiles, name, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            // Try versioned Python directories (Python313, Python312, etc.)
            try
            {
                var pythonDirs = Directory.GetDirectories(programFiles, "Python*")
                    .OrderByDescending(d => d)
                    .ToArray();
                foreach (var pyDir in pythonDirs)
                {
                    candidate = Path.Combine(pyDir, fileName);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
            catch
            {
                // Ignore permission errors
            }

            // 3. Check LocalAppData (Windows Store Python, pyenv)
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData))
            {
                var storeApps = Path.Combine(localAppData, "Microsoft", "WindowsApps");
                candidate = Path.Combine(storeApps, fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        // Fallback: return bare name and let the OS resolve it
        return name;
    }

    /// <summary>
    /// Resolves the path to the Python refactor.py script using multiple strategies:
    /// 1. Development: relative to repo root from build output (bin/Debug/net10.0/)
    /// 2. Installed: relative to install root from api/ directory (C:\Program Files\Aura\api\ → ..\scripts\)
    /// 3. Fallback: relative to current working directory.
    /// </summary>
    private static string ResolveScriptPath()
    {
        const string relativePath = "scripts/python/refactor.py";
        var baseDir = AppContext.BaseDirectory;

        // Strategy 1: Development layout (bin/Debug/net10.0/ → ../../../../scripts/)
        var devPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", relativePath));
        if (File.Exists(devPath))
        {
            return devPath;
        }

        // Strategy 2: Installed layout (api/ → ../scripts/)
        var installedPath = Path.GetFullPath(Path.Combine(baseDir, "..", relativePath));
        if (File.Exists(installedPath))
        {
            return installedPath;
        }

        // Strategy 3: Fallback to working directory
        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
    }
}
