// <copyright file="ValidateCompilationTool.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Tools;

using Aura.Foundation.Tools;
using Aura.Module.Developer.Services;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

/// <summary>
/// Input for the validate_compilation tool.
/// </summary>
public record ValidateCompilationInput
{
    /// <summary>Project name to validate</summary>
    public required string ProjectName { get; init; }

    /// <summary>Working directory to find the solution file (defaults to current directory)</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>Include warnings (not just errors)</summary>
    public bool IncludeWarnings { get; init; }

    /// <summary>Maximum number of diagnostics to return</summary>
    public int MaxDiagnostics { get; init; } = 50;
}

/// <summary>
/// Information about a compilation diagnostic.
/// </summary>
public record DiagnosticInfo
{
    /// <summary>Diagnostic ID (e.g., CS0103)</summary>
    public required string Id { get; init; }

    /// <summary>Severity (Error, Warning, Info)</summary>
    public required string Severity { get; init; }

    /// <summary>Diagnostic message</summary>
    public required string Message { get; init; }

    /// <summary>File path where the diagnostic occurred</summary>
    public string? FilePath { get; init; }

    /// <summary>Line number (1-indexed)</summary>
    public int? Line { get; init; }

    /// <summary>Column number</summary>
    public int? Column { get; init; }
}

/// <summary>
/// Output from the validate_compilation tool.
/// </summary>
public record ValidateCompilationOutput
{
    /// <summary>Project name validated</summary>
    public required string ProjectName { get; init; }

    /// <summary>Whether compilation succeeded with no errors</summary>
    public bool Success { get; init; }

    /// <summary>Number of errors</summary>
    public int ErrorCount { get; init; }

    /// <summary>Number of warnings</summary>
    public int WarningCount { get; init; }

    /// <summary>List of diagnostics</summary>
    public required IReadOnlyList<DiagnosticInfo> Diagnostics { get; init; }

    /// <summary>Summary message</summary>
    public required string Summary { get; init; }
}

/// <summary>
/// Validates that a project compiles without errors.
/// Use after making code changes to verify syntax and type correctness.
/// </summary>
public class ValidateCompilationTool : TypedToolBase<ValidateCompilationInput, ValidateCompilationOutput>
{
    private readonly IRoslynWorkspaceService _workspace;
    private readonly ILogger<ValidateCompilationTool> _logger;

    public ValidateCompilationTool(
        IRoslynWorkspaceService workspace,
        ILogger<ValidateCompilationTool> logger)
    {
        _workspace = workspace;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override string ToolId => "roslyn.validate_compilation";

    /// <inheritdoc/>
    public override string Name => "Validate Compilation";

    /// <inheritdoc/>
    public override string Description => """
        Validates that a project compiles without errors. Returns compilation diagnostics
        including error messages and file locations. Use this after modifying code to
        verify there are no syntax or type errors. The working directory is set automatically
        to the workflow's repository path.
        """;

    /// <inheritdoc/>
    public override IReadOnlyList<string> Categories => ["roslyn", "validation"];

    /// <inheritdoc/>
    public override bool RequiresConfirmation => false;

    /// <inheritdoc/>
    public override async Task<ToolResult<ValidateCompilationOutput>> ExecuteAsync(
        ValidateCompilationInput input,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Validating compilation for project: {ProjectName}", input.ProjectName);

        try
        {
            // First, clear the workspace cache to ensure we get fresh compilation
            _workspace.ClearCache();

            // Find the project - resolve relative paths against current directory
            var workingDir = input.WorkingDirectory ?? Environment.CurrentDirectory;
            var searchDirectory = Path.IsPathRooted(workingDir)
                ? workingDir
                : Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, workingDir));

            _logger.LogInformation("Searching for solution in: {SearchDirectory}", searchDirectory);

            var solutionPath = _workspace.FindSolutionFile(searchDirectory);
            if (solutionPath is null)
            {
                return ToolResult<ValidateCompilationOutput>.Fail(
                    $"No solution file found in '{searchDirectory}'");
            }

            var solution = await _workspace.GetSolutionAsync(solutionPath, ct);
            var project = solution.Projects.FirstOrDefault(p =>
                p.Name.Equals(input.ProjectName, StringComparison.OrdinalIgnoreCase));

            if (project is null)
            {
                return ToolResult<ValidateCompilationOutput>.Fail(
                    $"Project '{input.ProjectName}' not found. Use list_projects to see available projects.");
            }

            // Get compilation
            var compilation = await project.GetCompilationAsync(ct);
            if (compilation is null)
            {
                return ToolResult<ValidateCompilationOutput>.Fail(
                    $"Failed to compile project '{input.ProjectName}'");
            }

            // Get diagnostics
            var allDiagnostics = compilation.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error ||
                           (input.IncludeWarnings && d.Severity == DiagnosticSeverity.Warning))
                .Take(input.MaxDiagnostics)
                .Select(BuildDiagnosticInfo)
                .ToList();

            var errorCount = allDiagnostics.Count(d => d.Severity == "Error");
            var warningCount = allDiagnostics.Count(d => d.Severity == "Warning");

            var summary = errorCount == 0
                ? warningCount == 0
                    ? $"Project '{input.ProjectName}' compiled successfully with no errors or warnings."
                    : $"Project '{input.ProjectName}' compiled with {warningCount} warning(s)."
                : $"Project '{input.ProjectName}' has {errorCount} error(s) and {warningCount} warning(s).";

            var output = new ValidateCompilationOutput
            {
                ProjectName = project.Name,
                Success = errorCount == 0,
                ErrorCount = errorCount,
                WarningCount = warningCount,
                Diagnostics = allDiagnostics,
                Summary = summary,
            };

            _logger.LogInformation("Validation complete: {Summary}", summary);
            return ToolResult<ValidateCompilationOutput>.Ok(output);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate compilation for {ProjectName}", input.ProjectName);
            return ToolResult<ValidateCompilationOutput>.Fail($"Failed to validate compilation: {ex.Message}");
        }
    }

    private static DiagnosticInfo BuildDiagnosticInfo(Diagnostic diagnostic)
    {
        var location = diagnostic.Location;
        var lineSpan = location.GetLineSpan();

        return new DiagnosticInfo
        {
            Id = diagnostic.Id,
            Severity = diagnostic.Severity.ToString(),
            Message = diagnostic.GetMessage(),
            FilePath = lineSpan.Path,
            Line = lineSpan.IsValid ? lineSpan.StartLinePosition.Line + 1 : null,
            Column = lineSpan.IsValid ? lineSpan.StartLinePosition.Character + 1 : null,
        };
    }
}
