// <copyright file="TypeScriptLanguageService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services;

using System.Text.Json;
using Aura.Foundation.Shell;
using Microsoft.Extensions.Logging;

/// <summary>
/// Service for TypeScript/JavaScript code refactoring operations using ts-morph.
/// Executes refactoring operations via the Node.js refactor.js script.
/// </summary>
public sealed class TypeScriptLanguageService : ITypeScriptLanguageService
{
    private readonly IProcessRunner _processRunner;
    private readonly ILogger<TypeScriptLanguageService> _logger;
    private readonly string _scriptPath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeScriptLanguageService"/> class.
    /// </summary>
    /// <param name="processRunner">Process runner for executing Node.js.</param>
    /// <param name="logger">Logger instance.</param>
    public TypeScriptLanguageService(
        IProcessRunner processRunner,
        ILogger<TypeScriptLanguageService> logger)
    {
        _processRunner = processRunner;
        _logger = logger;

        // Locate the refactor.js script
        _scriptPath = ResolveScriptPath();
        _logger.LogDebug("TypeScript refactoring script path: {ScriptPath}", _scriptPath);
    }

    /// <inheritdoc/>
    public async Task<TypeScriptRefactoringResult> RenameSymbolAsync(
        TypeScriptRenameRequest request,
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
    public async Task<TypeScriptRefactoringResult> ExtractFunctionAsync(
        TypeScriptExtractFunctionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var args = new List<string>
        {
            "extract-function",
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
    public async Task<TypeScriptRefactoringResult> ExtractVariableAsync(
        TypeScriptExtractVariableRequest request,
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
    public async Task<TypeScriptFindReferencesResult> FindReferencesAsync(
        TypeScriptFindReferencesRequest request,
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

        var (success, stdout, stderr) = await ExecuteNodeScriptAsync(args, cancellationToken);

        if (!success)
        {
            return new TypeScriptFindReferencesResult
            {
                Success = false,
                Error = stderr ?? "Node.js script execution failed",
            };
        }

        try
        {
            var result = JsonSerializer.Deserialize<TypeScriptFindReferencesResult>(stdout, JsonOptions);
            return result ?? new TypeScriptFindReferencesResult
            {
                Success = false,
                Error = "Failed to deserialize result",
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse find-references result: {Output}", stdout);
            return new TypeScriptFindReferencesResult
            {
                Success = false,
                Error = $"Failed to parse result: {ex.Message}",
            };
        }
    }

    /// <inheritdoc/>
    public async Task<TypeScriptFindDefinitionResult> FindDefinitionAsync(
        TypeScriptFindDefinitionRequest request,
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

        var (success, stdout, stderr) = await ExecuteNodeScriptAsync(args, cancellationToken);

        if (!success)
        {
            return new TypeScriptFindDefinitionResult
            {
                Success = false,
                Error = stderr ?? "Node.js script execution failed",
            };
        }

        try
        {
            var result = JsonSerializer.Deserialize<TypeScriptFindDefinitionResult>(stdout, JsonOptions);
            return result ?? new TypeScriptFindDefinitionResult
            {
                Success = false,
                Error = "Failed to deserialize result",
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse find-definition result: {Output}", stdout);
            return new TypeScriptFindDefinitionResult
            {
                Success = false,
                Error = $"Failed to parse result: {ex.Message}",
            };
        }
    }

    /// <inheritdoc/>
    public async Task<TypeScriptInspectTypeResult> InspectTypeAsync(
        TypeScriptInspectTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var args = new List<string>
        {
            "inspect-type",
            "--project", request.ProjectPath,
            "--type-name", request.TypeName,
        };

        if (!string.IsNullOrEmpty(request.FilePath))
        {
            args.AddRange(["--file", request.FilePath]);
        }

        var (success, stdout, stderr) = await ExecuteNodeScriptAsync(args, cancellationToken);

        if (!success)
        {
            return new TypeScriptInspectTypeResult
            {
                Success = false,
                Error = stderr ?? "Node.js script execution failed",
            };
        }

        try
        {
            var result = JsonSerializer.Deserialize<TypeScriptInspectTypeResult>(stdout, JsonOptions);
            return result ?? new TypeScriptInspectTypeResult
            {
                Success = false,
                Error = "Failed to deserialize result",
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse inspect-type result: {Output}", stdout);
            return new TypeScriptInspectTypeResult
            {
                Success = false,
                Error = $"Failed to parse result: {ex.Message}",
            };
        }
    }

    /// <inheritdoc/>
    public async Task<TypeScriptListTypesResult> ListTypesAsync(
        TypeScriptListTypesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var args = new List<string>
        {
            "list-types",
            "--project", request.ProjectPath,
        };

        if (!string.IsNullOrEmpty(request.NameFilter))
        {
            args.AddRange(["--name-filter", request.NameFilter]);
        }

        var (success, stdout, stderr) = await ExecuteNodeScriptAsync(args, cancellationToken);

        if (!success)
        {
            return new TypeScriptListTypesResult
            {
                Success = false,
                Error = stderr ?? "Node.js script execution failed",
            };
        }

        try
        {
            var result = JsonSerializer.Deserialize<TypeScriptListTypesResult>(stdout, JsonOptions);
            return result ?? new TypeScriptListTypesResult
            {
                Success = false,
                Error = "Failed to deserialize result",
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse list-types result: {Output}", stdout);
            return new TypeScriptListTypesResult
            {
                Success = false,
                Error = $"Failed to parse result: {ex.Message}",
            };
        }
    }

    /// <inheritdoc/>
    public async Task<TypeScriptFindCallersResult> FindCallersAsync(
        TypeScriptFindCallersRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var args = new List<string>
        {
            "find-callers",
            "--project", request.ProjectPath,
            "--file", request.FilePath,
            "--offset", request.Offset.ToString(),
        };

        var (success, stdout, stderr) = await ExecuteNodeScriptAsync(args, cancellationToken);

        if (!success)
        {
            return new TypeScriptFindCallersResult
            {
                Success = false,
                Error = stderr ?? "Node.js script execution failed",
            };
        }

        try
        {
            var result = JsonSerializer.Deserialize<TypeScriptFindCallersResult>(stdout, JsonOptions);
            return result ?? new TypeScriptFindCallersResult
            {
                Success = false,
                Error = "Failed to deserialize result",
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse find-callers result: {Output}", stdout);
            return new TypeScriptFindCallersResult
            {
                Success = false,
                Error = $"Failed to parse result: {ex.Message}",
            };
        }
    }

    /// <inheritdoc/>
    public async Task<TypeScriptFindImplementationsResult> FindImplementationsAsync(
        TypeScriptFindImplementationsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var args = new List<string>
        {
            "find-implementations",
            "--project", request.ProjectPath,
            "--file", request.FilePath,
            "--offset", request.Offset.ToString(),
        };

        var (success, stdout, stderr) = await ExecuteNodeScriptAsync(args, cancellationToken);

        if (!success)
        {
            return new TypeScriptFindImplementationsResult
            {
                Success = false,
                Error = stderr ?? "Node.js script execution failed",
            };
        }

        try
        {
            var result = JsonSerializer.Deserialize<TypeScriptFindImplementationsResult>(stdout, JsonOptions);
            return result ?? new TypeScriptFindImplementationsResult
            {
                Success = false,
                Error = "Failed to deserialize result",
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse find-implementations result: {Output}", stdout);
            return new TypeScriptFindImplementationsResult
            {
                Success = false,
                Error = $"Failed to parse result: {ex.Message}",
            };
        }
    }

    /// <inheritdoc/>
    public async Task<TypeScriptCheckResult> CheckAsync(
        TypeScriptCheckRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var args = new List<string>
        {
            "check",
            "--project", request.ProjectPath,
        };

        var (success, stdout, stderr) = await ExecuteNodeScriptAsync(args, cancellationToken);

        if (!success)
        {
            return new TypeScriptCheckResult
            {
                Success = false,
                Error = stderr ?? "Node.js script execution failed",
            };
        }

        try
        {
            var result = JsonSerializer.Deserialize<TypeScriptCheckResult>(stdout, JsonOptions);
            return result ?? new TypeScriptCheckResult
            {
                Success = false,
                Error = "Failed to deserialize result",
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse check result: {Output}", stdout);
            return new TypeScriptCheckResult
            {
                Success = false,
                Error = $"Failed to parse result: {ex.Message}",
            };
        }
    }

    private async Task<TypeScriptRefactoringResult> ExecuteRefactoringAsync(
        List<string> args,
        CancellationToken cancellationToken)
    {
        var (success, stdout, stderr) = await ExecuteNodeScriptAsync(args, cancellationToken);

        if (!success)
        {
            return new TypeScriptRefactoringResult
            {
                Success = false,
                Error = stderr ?? "Node.js script execution failed",
            };
        }

        try
        {
            var result = JsonSerializer.Deserialize<TypeScriptRefactoringResult>(stdout, JsonOptions);
            return result ?? new TypeScriptRefactoringResult
            {
                Success = false,
                Error = "Failed to deserialize result",
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse refactoring result: {Output}", stdout);
            return new TypeScriptRefactoringResult
            {
                Success = false,
                Error = $"Failed to parse result: {ex.Message}",
            };
        }
    }

    private async Task<(bool Success, string Stdout, string? Stderr)> ExecuteNodeScriptAsync(
        List<string> args,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_scriptPath))
        {
            _logger.LogError("TypeScript refactoring script not found at: {Path}", _scriptPath);
            return (false, string.Empty, $"Script not found: {_scriptPath}. Run 'npm install && npm run build' in scripts/typescript/");
        }

        var allArgs = new List<string> { _scriptPath };
        allArgs.AddRange(args);

        _logger.LogDebug("Executing: node {Args}", string.Join(" ", allArgs));

        var result = await _processRunner.RunAsync(
            "node",
            allArgs.ToArray(),
            ct: cancellationToken);

        if (result.ExitCode != 0)
        {
            _logger.LogWarning("Node script exited with code {ExitCode}: {Stderr}", result.ExitCode, result.StandardError);

            // Try to parse error from stdout (script might return JSON error)
            if (!string.IsNullOrEmpty(result.StandardOutput) && result.StandardOutput.TrimStart().StartsWith('{'))
            {
                return (true, result.StandardOutput, null);
            }

            return (false, string.Empty, result.StandardError ?? $"Exit code: {result.ExitCode}");
        }

        return (true, result.StandardOutput, null);
    }

    /// <summary>
    /// Resolves the path to the TypeScript refactor.js script using multiple strategies:
    /// 1. Development: relative to repo root from build output (bin/Debug/net10.0/)
    /// 2. Installed: relative to install root from api/ directory (C:\Program Files\Aura\api\ → ..\scripts\)
    /// 3. Fallback: relative to current working directory.
    /// </summary>
    private static string ResolveScriptPath()
    {
        const string relativePath = "scripts/typescript/dist/refactor.js";
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
