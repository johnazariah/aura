// <copyright file="CodeValidateTool.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Tools;

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Aura.Foundation.Shell;
using Microsoft.Extensions.Logging;

/// <summary>
/// Input for the code.validate tool.
/// </summary>
public record CodeValidateInput
{
    /// <summary>Path to project or solution. Auto-detected if omitted.</summary>
    public string? ProjectPath { get; init; }

    /// <summary>Language to validate. Auto-detected if omitted.</summary>
    public string? Language { get; init; }

    /// <summary>Working directory (injected by framework).</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>Timeout in seconds for the build command.</summary>
    public int TimeoutSeconds { get; init; } = 60;
}

/// <summary>
/// A single validation error or warning.
/// </summary>
public record CodeValidationDiagnostic
{
    /// <summary>File path (relative to workspace).</summary>
    public required string File { get; init; }

    /// <summary>Line number (1-based).</summary>
    public int Line { get; init; }

    /// <summary>Column number (1-based).</summary>
    public int Column { get; init; }

    /// <summary>Error/warning code (e.g., CS1002, TS2304).</summary>
    public string? Code { get; init; }

    /// <summary>Error or Warning.</summary>
    public required string Severity { get; init; }

    /// <summary>Diagnostic message.</summary>
    public required string Message { get; init; }
}

/// <summary>
/// Output from the code.validate tool.
/// </summary>
public record CodeValidateOutput
{
    /// <summary>Whether compilation succeeded with no errors.</summary>
    public required bool Success { get; init; }

    /// <summary>Detected or specified language.</summary>
    public required string Language { get; init; }

    /// <summary>Number of errors.</summary>
    public int ErrorCount { get; init; }

    /// <summary>Number of warnings.</summary>
    public int WarningCount { get; init; }

    /// <summary>List of diagnostics (errors and warnings).</summary>
    public required IReadOnlyList<CodeValidationDiagnostic> Diagnostics { get; init; }

    /// <summary>Command that was executed.</summary>
    public required string Command { get; init; }

    /// <summary>Execution time in milliseconds.</summary>
    public long DurationMs { get; init; }
}

/// <summary>
/// Unified code validation tool that supports multiple languages.
/// Compiles/type-checks the project and returns errors.
/// </summary>
public sealed partial class CodeValidateTool : TypedToolBase<CodeValidateInput, CodeValidateOutput>
{
    private readonly IProcessRunner _processRunner;
    private readonly ILogger<CodeValidateTool> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeValidateTool"/> class.
    /// </summary>
    public CodeValidateTool(
        IProcessRunner processRunner,
        ILogger<CodeValidateTool> logger)
    {
        _processRunner = processRunner;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override string ToolId => "code.validate";

    /// <inheritdoc/>
    public override string Name => "Validate Code";

    /// <inheritdoc/>
    public override string Description => """
        Compile or type-check the project and report errors. MUST be called after any code 
        changes before finishing a task. Supports C# (dotnet build), TypeScript (tsc --noEmit), 
        Python (python -m py_compile), and Go (go build). Auto-detects language from project files.
        """;

    /// <inheritdoc/>
    public override IReadOnlyList<string> Categories => ["code", "validation"];

    /// <inheritdoc/>
    public override bool RequiresConfirmation => false;

    /// <inheritdoc/>
    public override async Task<ToolResult<CodeValidateOutput>> ExecuteAsync(
        CodeValidateInput input,
        CancellationToken ct = default)
    {
        var workingDir = input.WorkingDirectory ?? Environment.CurrentDirectory;
        var language = input.Language ?? DetectLanguage(workingDir);

        _logger.LogInformation(
            "Validating code in {WorkingDir}, language: {Language}",
            workingDir, language);

        var stopwatch = Stopwatch.StartNew();

        var (command, args) = GetBuildCommand(language, workingDir, input.ProjectPath);
        if (command is null)
        {
            return ToolResult<CodeValidateOutput>.Fail(
                $"Unknown language '{language}'. Supported: csharp, typescript, python, go, rust.");
        }

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(input.TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            var options = new ProcessOptions
            {
                WorkingDirectory = workingDir,
                Timeout = TimeSpan.FromSeconds(input.TimeoutSeconds),
            };

            var result = await _processRunner.RunAsync(
                command,
                args.Split(' ', StringSplitOptions.RemoveEmptyEntries),
                options,
                linkedCts.Token);

            stopwatch.Stop();

            var diagnostics = ParseDiagnostics(language, result.StandardOutput + "\n" + result.StandardError, workingDir);
            var errorCount = diagnostics.Count(d => d.Severity == "Error");
            var warningCount = diagnostics.Count(d => d.Severity == "Warning");
            var success = result.ExitCode == 0 && errorCount == 0;

            // Note: ValidationTracker updates are handled by ReActExecutor.TrackFileChangeIfApplicable

            var output = new CodeValidateOutput
            {
                Success = success,
                Language = language,
                ErrorCount = errorCount,
                WarningCount = warningCount,
                Diagnostics = diagnostics,
                Command = $"{command} {args}",
                DurationMs = stopwatch.ElapsedMilliseconds,
            };

            _logger.LogInformation(
                "Validation complete: {Success}, {Errors} errors, {Warnings} warnings in {Duration}ms",
                success, errorCount, warningCount, stopwatch.ElapsedMilliseconds);

            return ToolResult<CodeValidateOutput>.Ok(output);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            return ToolResult<CodeValidateOutput>.Fail(
                $"Build timed out after {input.TimeoutSeconds} seconds.");
        }
    }

    /// <summary>
    /// Detect the primary language of a workspace from project files.
    /// </summary>
    private static string DetectLanguage(string workingDir)
    {
        // Check for solution/project files in priority order
        if (Directory.GetFiles(workingDir, "*.sln", SearchOption.TopDirectoryOnly).Length > 0 ||
            Directory.GetFiles(workingDir, "*.csproj", SearchOption.AllDirectories).Length > 0)
        {
            return "csharp";
        }

        if (File.Exists(Path.Combine(workingDir, "tsconfig.json")))
        {
            return "typescript";
        }

        if (File.Exists(Path.Combine(workingDir, "go.mod")))
        {
            return "go";
        }

        if (File.Exists(Path.Combine(workingDir, "pyproject.toml")) ||
            File.Exists(Path.Combine(workingDir, "setup.py")) ||
            File.Exists(Path.Combine(workingDir, "requirements.txt")))
        {
            return "python";
        }

        if (File.Exists(Path.Combine(workingDir, "Cargo.toml")))
        {
            return "rust";
        }

        // Default fallback
        return "unknown";
    }

    /// <summary>
    /// Get the build command for a language.
    /// </summary>
    private static (string? Command, string Args) GetBuildCommand(
        string language,
        string workingDir,
        string? projectPath)
    {
        return language.ToLowerInvariant() switch
        {
            "csharp" or "c#" => GetCSharpCommand(workingDir, projectPath),
            "typescript" or "ts" => ("npx", "tsc --noEmit"),
            "javascript" or "js" => ("npx", "tsc --noEmit --allowJs"),
            "python" or "py" => GetPythonCommand(workingDir),
            "go" => ("go", "build ./..."),
            "rust" => ("cargo", "check"),
            _ => (null, string.Empty),
        };
    }

    private static (string Command, string Args) GetCSharpCommand(string workingDir, string? projectPath)
    {
        // Find solution or project file
        if (!string.IsNullOrEmpty(projectPath))
        {
            return ("dotnet", $"build \"{projectPath}\" --no-restore");
        }

        var solutions = Directory.GetFiles(workingDir, "*.sln", SearchOption.TopDirectoryOnly);
        if (solutions.Length > 0)
        {
            return ("dotnet", $"build \"{solutions[0]}\" --no-restore");
        }

        // Fall back to dotnet build in current directory
        return ("dotnet", "build --no-restore");
    }

    private static (string Command, string Args) GetPythonCommand(string workingDir)
    {
        // Check if mypy is available (better type checking)
        // For now, use py_compile for basic syntax checking
        // TODO: Detect and use mypy if configured
        var pythonFiles = Directory.GetFiles(workingDir, "*.py", SearchOption.AllDirectories)
            .Where(f => !f.Contains("venv") && !f.Contains("__pycache__") && !f.Contains(".venv"))
            .Take(50) // Limit to avoid very long command lines
            .ToList();

        if (pythonFiles.Count == 0)
        {
            return ("python", "-c \"print('No Python files found')\"");
        }

        // Check multiple files with py_compile
        var files = string.Join(" ", pythonFiles.Select(f => $"\"{f}\""));
        return ("python", $"-m py_compile {files}");
    }

    /// <summary>
    /// Parse build output into structured diagnostics.
    /// </summary>
    private List<CodeValidationDiagnostic> ParseDiagnostics(
        string language,
        string output,
        string workingDir)
    {
        return language.ToLowerInvariant() switch
        {
            "csharp" or "c#" => ParseCSharpDiagnostics(output, workingDir),
            "typescript" or "ts" or "javascript" or "js" => ParseTypeScriptDiagnostics(output, workingDir),
            "python" or "py" => ParsePythonDiagnostics(output, workingDir),
            "go" => ParseGoDiagnostics(output, workingDir),
            "rust" => ParseRustDiagnostics(output, workingDir),
            _ => [],
        };
    }

    /// <summary>
    /// Parse C# dotnet build output.
    /// Format: path\to\file.cs(line,col): error CS1234: message
    /// </summary>
    private List<CodeValidationDiagnostic> ParseCSharpDiagnostics(string output, string workingDir)
    {
        var diagnostics = new List<CodeValidationDiagnostic>();
        var regex = CSharpDiagnosticRegex();

        foreach (Match match in regex.Matches(output))
        {
            var file = MakeRelative(match.Groups["file"].Value, workingDir);
            var line = int.TryParse(match.Groups["line"].Value, out var l) ? l : 0;
            var col = int.TryParse(match.Groups["col"].Value, out var c) ? c : 0;
            var severity = match.Groups["severity"].Value.Equals("error", StringComparison.OrdinalIgnoreCase)
                ? "Error"
                : "Warning";
            var code = match.Groups["code"].Value;
            var message = match.Groups["message"].Value.Trim();

            diagnostics.Add(new CodeValidationDiagnostic
            {
                File = file,
                Line = line,
                Column = col,
                Code = code,
                Severity = severity,
                Message = message,
            });
        }

        return diagnostics;
    }

    /// <summary>
    /// Parse TypeScript tsc output.
    /// Format: path/to/file.ts(line,col): error TS1234: message
    /// </summary>
    private List<CodeValidationDiagnostic> ParseTypeScriptDiagnostics(string output, string workingDir)
    {
        var diagnostics = new List<CodeValidationDiagnostic>();
        var regex = TypeScriptDiagnosticRegex();

        foreach (Match match in regex.Matches(output))
        {
            var file = MakeRelative(match.Groups["file"].Value, workingDir);
            var line = int.TryParse(match.Groups["line"].Value, out var l) ? l : 0;
            var col = int.TryParse(match.Groups["col"].Value, out var c) ? c : 0;
            var code = match.Groups["code"].Value;
            var message = match.Groups["message"].Value.Trim();

            diagnostics.Add(new CodeValidationDiagnostic
            {
                File = file,
                Line = line,
                Column = col,
                Code = code,
                Severity = "Error",
                Message = message,
            });
        }

        return diagnostics;
    }

    /// <summary>
    /// Parse Python py_compile/mypy output.
    /// Format: file.py:line: error: message
    /// </summary>
    private List<CodeValidationDiagnostic> ParsePythonDiagnostics(string output, string workingDir)
    {
        var diagnostics = new List<CodeValidationDiagnostic>();
        var regex = PythonDiagnosticRegex();

        foreach (Match match in regex.Matches(output))
        {
            var file = MakeRelative(match.Groups["file"].Value, workingDir);
            var line = int.TryParse(match.Groups["line"].Value, out var l) ? l : 0;
            var message = match.Groups["message"].Value.Trim();

            diagnostics.Add(new CodeValidationDiagnostic
            {
                File = file,
                Line = line,
                Column = 0,
                Code = null,
                Severity = "Error",
                Message = message,
            });
        }

        return diagnostics;
    }

    /// <summary>
    /// Parse Go build output.
    /// Format: ./path/to/file.go:line:col: message
    /// </summary>
    private List<CodeValidationDiagnostic> ParseGoDiagnostics(string output, string workingDir)
    {
        var diagnostics = new List<CodeValidationDiagnostic>();
        var regex = GoDiagnosticRegex();

        foreach (Match match in regex.Matches(output))
        {
            var file = MakeRelative(match.Groups["file"].Value, workingDir);
            var line = int.TryParse(match.Groups["line"].Value, out var l) ? l : 0;
            var col = int.TryParse(match.Groups["col"].Value, out var c) ? c : 0;
            var message = match.Groups["message"].Value.Trim();

            diagnostics.Add(new CodeValidationDiagnostic
            {
                File = file,
                Line = line,
                Column = col,
                Code = null,
                Severity = "Error",
                Message = message,
            });
        }

        return diagnostics;
    }

    /// <summary>
    /// Parse Rust cargo check output.
    /// </summary>
    private List<CodeValidationDiagnostic> ParseRustDiagnostics(string output, string workingDir)
    {
        var diagnostics = new List<CodeValidationDiagnostic>();
        var regex = RustDiagnosticRegex();

        foreach (Match match in regex.Matches(output))
        {
            var severity = match.Groups["severity"].Value.Equals("error", StringComparison.OrdinalIgnoreCase)
                ? "Error"
                : "Warning";
            var file = MakeRelative(match.Groups["file"].Value, workingDir);
            var line = int.TryParse(match.Groups["line"].Value, out var l) ? l : 0;
            var col = int.TryParse(match.Groups["col"].Value, out var c) ? c : 0;
            var message = match.Groups["message"].Value.Trim();

            diagnostics.Add(new CodeValidationDiagnostic
            {
                File = file,
                Line = line,
                Column = col,
                Code = null,
                Severity = severity,
                Message = message,
            });
        }

        return diagnostics;
    }

    /// <summary>
    /// Make an absolute path relative to the working directory.
    /// </summary>
    private static string MakeRelative(string path, string workingDir)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        // Normalize separators
        path = path.Replace('\\', '/');
        workingDir = workingDir.Replace('\\', '/');

        // Remove leading ./
        if (path.StartsWith("./", StringComparison.Ordinal))
        {
            path = path[2..];
        }

        // Try to make relative
        if (Path.IsPathRooted(path) && path.StartsWith(workingDir, StringComparison.OrdinalIgnoreCase))
        {
            path = path[(workingDir.Length + 1)..];
        }

        return path;
    }

    [GeneratedRegex(@"(?<file>[^\s(]+)\((?<line>\d+),(?<col>\d+)\):\s*(?<severity>error|warning)\s+(?<code>CS\d+):\s*(?<message>.+)$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex CSharpDiagnosticRegex();

    [GeneratedRegex(@"(?<file>[^\s(]+)\((?<line>\d+),(?<col>\d+)\):\s*error\s+(?<code>TS\d+):\s*(?<message>.+)$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex TypeScriptDiagnosticRegex();

    [GeneratedRegex(@"(?<file>[^\s:]+):(?<line>\d+):\s*(?<message>.+)$", RegexOptions.Multiline)]
    private static partial Regex PythonDiagnosticRegex();

    [GeneratedRegex(@"(?<file>\./[^\s:]+):(?<line>\d+):(?<col>\d+):\s*(?<message>.+)$", RegexOptions.Multiline)]
    private static partial Regex GoDiagnosticRegex();

    [GeneratedRegex(@"(?<severity>error|warning)(?:\[E\d+\])?:\s*(?<message>.+?)\s*-->\s*(?<file>[^\s:]+):(?<line>\d+):(?<col>\d+)", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex RustDiagnosticRegex();
}
