using System.Text.Json;
using Aura.Api.Mcp.Tools;
using Aura.Api.Services;
using Aura.Foundation.Data.Entities;
using Aura.Foundation.Git;
using Aura.Foundation.Rag;
using Aura.Module.Developer.Data.Entities;
using Aura.Module.Developer.GitHub;
using Aura.Module.Developer.Services;
using Aura.Module.Developer.Services.Testing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.Extensions.Logging;
using RefactoringParameterInfo = Aura.Module.Developer.Services.ParameterInfo;

namespace Aura.Api.Mcp;

public sealed partial class McpHandler
{
    // =========================================================================
    // Python Refactoring Tool Handlers (Phase 6)
    // =========================================================================
    private async Task<object> PythonRenameAsync(JsonElement? args, CancellationToken ct)
    {
        var projectPath = args.GetStringOrDefault("projectPath");
        var filePath = args.GetStringOrDefault("filePath");
        var offset = args.GetInt32OrDefault("offset");
        var newName = args.GetStringOrDefault("newName");
        var preview = args.HasValue && args.Value.TryGetProperty("preview", out var prevEl) && prevEl.GetBoolean();
        var result = await _pythonRefactoringService.RenameSymbolAsync(new PythonRenameRequest { ProjectPath = projectPath, FilePath = filePath, Offset = offset, NewName = newName, Preview = preview }, ct);
        return new
        {
            success = result.Success,
            error = result.Error,
            preview = result.Preview,
            changedFiles = result.ChangedFiles,
            description = result.Description,
            fileChanges = result.FileChanges?.Select(fc => new { fc.FilePath, fc.OldContent, fc.NewContent })
        };
    }

    private async Task<object> PythonExtractMethodAsync(JsonElement? args, CancellationToken ct)
    {
        var projectPath = args.GetStringOrDefault("projectPath");
        var filePath = args.GetStringOrDefault("filePath");
        var startOffset = args.GetInt32OrDefault("startOffset");
        var endOffset = args.GetInt32OrDefault("endOffset");
        var newName = args.GetStringOrDefault("newName");
        var preview = args.HasValue && args.Value.TryGetProperty("preview", out var prevEl) && prevEl.GetBoolean();
        var result = await _pythonRefactoringService.ExtractMethodAsync(new PythonExtractMethodRequest { ProjectPath = projectPath, FilePath = filePath, StartOffset = startOffset, EndOffset = endOffset, NewName = newName, Preview = preview }, ct);
        return new
        {
            success = result.Success,
            error = result.Error,
            preview = result.Preview,
            changedFiles = result.ChangedFiles,
            description = result.Description,
            fileChanges = result.FileChanges?.Select(fc => new { fc.FilePath, fc.OldContent, fc.NewContent })
        };
    }

    private async Task<object> PythonExtractVariableAsync(JsonElement? args, CancellationToken ct)
    {
        var projectPath = args.GetStringOrDefault("projectPath");
        var filePath = args.GetStringOrDefault("filePath");
        var startOffset = args.GetInt32OrDefault("startOffset");
        var endOffset = args.GetInt32OrDefault("endOffset");
        var newName = args.GetStringOrDefault("newName");
        var preview = args.HasValue && args.Value.TryGetProperty("preview", out var prevEl) && prevEl.GetBoolean();
        var result = await _pythonRefactoringService.ExtractVariableAsync(new PythonExtractVariableRequest { ProjectPath = projectPath, FilePath = filePath, StartOffset = startOffset, EndOffset = endOffset, NewName = newName, Preview = preview }, ct);
        return new
        {
            success = result.Success,
            error = result.Error,
            preview = result.Preview,
            changedFiles = result.ChangedFiles,
            description = result.Description,
            fileChanges = result.FileChanges?.Select(fc => new { fc.FilePath, fc.OldContent, fc.NewContent })
        };
    }

    private async Task<object> PythonFindReferencesAsync(JsonElement? args, CancellationToken ct)
    {
        var projectPath = args.GetStringOrDefault("projectPath");
        var filePath = args.GetStringOrDefault("filePath");
        var offset = args.GetInt32OrDefault("offset");
        var result = await _pythonRefactoringService.FindReferencesAsync(new PythonFindReferencesRequest { ProjectPath = projectPath, FilePath = filePath, Offset = offset }, ct);
        return new
        {
            success = result.Success,
            error = result.Error,
            count = result.Count,
            references = result.References.Select(r => new { filePath = r.FilePath, offset = r.Offset, isDefinition = r.IsDefinition, isWrite = r.IsWrite })
        };
    }

    private async Task<object> PythonFindDefinitionAsync(JsonElement? args, CancellationToken ct)
    {
        var projectPath = args.GetStringOrDefault("projectPath");
        var filePath = args.GetStringOrDefault("filePath");
        var offset = args.GetInt32OrDefault("offset");
        var result = await _pythonRefactoringService.FindDefinitionAsync(new PythonFindDefinitionRequest { ProjectPath = projectPath, FilePath = filePath, Offset = offset }, ct);
        return new
        {
            success = result.Success,
            error = result.Error,
            found = result.Found,
            filePath = result.FilePath,
            offset = result.Offset,
            line = result.Line,
            message = result.Message
        };
    }

    // =========================================================================
    // TypeScript/JavaScript Refactoring Tool Handlers
    // =========================================================================
    private async Task<object> TypeScriptRenameAsync(JsonElement? args, CancellationToken ct)
    {
        var projectPath = args.GetStringOrDefault("projectPath");
        var filePath = args.GetStringOrDefault("filePath");
        var offset = args.GetInt32OrDefault("offset");
        var newName = args.GetStringOrDefault("newName");
        var preview = args.HasValue && args.Value.TryGetProperty("preview", out var prevEl) && prevEl.GetBoolean();
        var result = await _typeScriptService.RenameSymbolAsync(new TypeScriptRenameRequest { ProjectPath = projectPath, FilePath = filePath, Offset = offset, NewName = newName, Preview = preview }, ct);
        return new
        {
            success = result.Success,
            error = result.Error,
            preview = result.Preview,
            changedFiles = result.ChangedFiles,
            description = result.Description
        };
    }

    private async Task<object> TypeScriptExtractFunctionAsync(JsonElement? args, CancellationToken ct)
    {
        var projectPath = args.GetStringOrDefault("projectPath");
        var filePath = args.GetStringOrDefault("filePath");
        var startOffset = args.GetInt32OrDefault("startOffset");
        var endOffset = args.GetInt32OrDefault("endOffset");
        var newName = args.GetStringOrDefault("newName");
        var preview = args.HasValue && args.Value.TryGetProperty("preview", out var prevEl) && prevEl.GetBoolean();
        var result = await _typeScriptService.ExtractFunctionAsync(new TypeScriptExtractFunctionRequest { ProjectPath = projectPath, FilePath = filePath, StartOffset = startOffset, EndOffset = endOffset, NewName = newName, Preview = preview }, ct);
        return new
        {
            success = result.Success,
            error = result.Error,
            preview = result.Preview,
            changedFiles = result.ChangedFiles,
            description = result.Description
        };
    }

    private async Task<object> TypeScriptExtractVariableAsync(JsonElement? args, CancellationToken ct)
    {
        var projectPath = args.GetStringOrDefault("projectPath");
        var filePath = args.GetStringOrDefault("filePath");
        var startOffset = args.GetInt32OrDefault("startOffset");
        var endOffset = args.GetInt32OrDefault("endOffset");
        var newName = args.GetStringOrDefault("newName");
        var preview = args.HasValue && args.Value.TryGetProperty("preview", out var prevEl) && prevEl.GetBoolean();
        var result = await _typeScriptService.ExtractVariableAsync(new TypeScriptExtractVariableRequest { ProjectPath = projectPath, FilePath = filePath, StartOffset = startOffset, EndOffset = endOffset, NewName = newName, Preview = preview }, ct);
        return new
        {
            success = result.Success,
            error = result.Error,
            preview = result.Preview,
            changedFiles = result.ChangedFiles,
            description = result.Description
        };
    }

    private async Task<object> TypeScriptFindReferencesAsync(JsonElement? args, CancellationToken ct)
    {
        var projectPath = args.GetStringOrDefault("projectPath");
        var filePath = args.GetStringOrDefault("filePath");
        var offset = args.GetInt32OrDefault("offset");
        var result = await _typeScriptService.FindReferencesAsync(new TypeScriptFindReferencesRequest { ProjectPath = projectPath, FilePath = filePath, Offset = offset }, ct);
        return new
        {
            success = result.Success,
            error = result.Error,
            count = result.Count,
            references = result.References?.Select(r => new { filePath = r.File, line = r.Line, column = r.Column, text = r.Text })
        };
    }

    private async Task<object> TypeScriptFindDefinitionAsync(JsonElement? args, CancellationToken ct)
    {
        var projectPath = args.GetStringOrDefault("projectPath");
        var filePath = args.GetStringOrDefault("filePath");
        var offset = args.GetInt32OrDefault("offset");
        var result = await _typeScriptService.FindDefinitionAsync(new TypeScriptFindDefinitionRequest { ProjectPath = projectPath, FilePath = filePath, Offset = offset }, ct);
        return new
        {
            success = result.Success,
            error = result.Error,
            found = result.Found,
            filePath = result.FilePath,
            line = result.Line,
            column = result.Column,
            offset = result.Offset,
            message = result.Message
        };
    }

    private async Task<object> TypeScriptInspectTypeAsync(JsonElement? args, CancellationToken ct)
    {
        var projectPath = args.GetStringOrDefault("projectPath");
        var typeName = args.GetStringOrDefault("typeName");
        string? filePath = null;
        if (args.HasValue && args.Value.TryGetProperty("filePath", out var fpEl))
        {
            filePath = fpEl.GetString();
        }

        var result = await _typeScriptService.InspectTypeAsync(
            new TypeScriptInspectTypeRequest { ProjectPath = projectPath, TypeName = typeName, FilePath = filePath },
            ct);
        return new
        {
            success = result.Success,
            error = result.Error,
            typeName = result.TypeName,
            kind = result.Kind,
            filePath = result.FilePath,
            line = result.Line,
            members = result.Members?.Select(m => new
            {
                name = m.Name,
                kind = m.Kind,
                type = m.Type,
                visibility = m.Visibility,
                isStatic = m.IsStatic,
                isAsync = m.IsAsync,
                line = m.Line,
            }),
        };
    }

    private async Task<object> TypeScriptListTypesAsync(JsonElement? args, CancellationToken ct)
    {
        var projectPath = args.GetStringOrDefault("projectPath");
        string? nameFilter = null;
        if (args.HasValue && args.Value.TryGetProperty("nameFilter", out var nfEl))
        {
            nameFilter = nfEl.GetString();
        }

        var result = await _typeScriptService.ListTypesAsync(
            new TypeScriptListTypesRequest { ProjectPath = projectPath, NameFilter = nameFilter },
            ct);
        return new
        {
            success = result.Success,
            error = result.Error,
            count = result.Count,
            types = result.Types?.Select(t => new
            {
                name = t.Name,
                kind = t.Kind,
                filePath = t.FilePath,
                line = t.Line,
                isExported = t.IsExported,
                memberCount = t.MemberCount,
            }),
        };
    }

    private async Task<object> TypeScriptFindCallersAsync(JsonElement? args, CancellationToken ct)
    {
        var projectPath = args.GetStringOrDefault("projectPath");
        var filePath = args.GetStringOrDefault("filePath");
        var offset = args.GetInt32OrDefault("offset");
        var result = await _typeScriptService.FindCallersAsync(
            new TypeScriptFindCallersRequest { ProjectPath = projectPath, FilePath = filePath, Offset = offset },
            ct);
        return new
        {
            success = result.Success,
            error = result.Error,
            count = result.Count,
            callers = result.Callers?.Select(c => new
            {
                file = c.File,
                line = c.Line,
                column = c.Column,
                name = c.Name,
                kind = c.Kind,
                text = c.Text,
            }),
        };
    }

    private async Task<object> TypeScriptFindImplementationsAsync(JsonElement? args, CancellationToken ct)
    {
        var projectPath = args.GetStringOrDefault("projectPath");
        var filePath = args.GetStringOrDefault("filePath");
        var offset = args.GetInt32OrDefault("offset");
        var result = await _typeScriptService.FindImplementationsAsync(
            new TypeScriptFindImplementationsRequest { ProjectPath = projectPath, FilePath = filePath, Offset = offset },
            ct);
        return new
        {
            success = result.Success,
            error = result.Error,
            count = result.Count,
            implementations = result.Implementations?.Select(i => new
            {
                name = i.Name,
                kind = i.Kind,
                file = i.File,
                line = i.Line,
                column = i.Column,
            }),
        };
    }

    private async Task<object> TypeScriptCheckAsync(JsonElement? args, CancellationToken ct)
    {
        var projectPath = args.GetStringOrDefault("projectPath");

        if (string.IsNullOrEmpty(projectPath))
        {
            return new { error = "projectPath is required for TypeScript compilation checking" };
        }

        var result = await _typeScriptService.CheckAsync(
            new TypeScriptCheckRequest { ProjectPath = projectPath },
            ct);
        return new
        {
            success = result.Success,
            error = result.Error,
            compilationSucceeded = result.CompilationSucceeded,
            errorCount = result.ErrorCount,
            warningCount = result.WarningCount,
            diagnostics = result.Diagnostics?.Select(d => new
            {
                filePath = d.FilePath,
                line = d.Line,
                column = d.Column,
                severity = d.Severity,
                code = d.Code,
                message = d.Message,
            }),
            summary = result.Success
                ? (result.CompilationSucceeded
                    ? "TypeScript compilation succeeded"
                    : $"TypeScript compilation failed with {result.ErrorCount} error(s) and {result.WarningCount} warning(s)")
                : result.Error,
        };
    }

    private async Task<object> TypeScriptRunTestsAsync(JsonElement? args, CancellationToken ct)
    {
        var projectPath = args.GetStringOrDefault("projectPath");
        var timeoutSeconds = 120;

        if (args.HasValue && args.Value.TryGetProperty("timeoutSeconds", out var timeoutEl))
        {
            timeoutSeconds = timeoutEl.GetInt32();
        }

        string? filter = null;
        if (args.HasValue && args.Value.TryGetProperty("filter", out var filterEl))
        {
            filter = filterEl.GetString();
        }

        if (string.IsNullOrEmpty(projectPath))
        {
            return new { error = "projectPath is required for TypeScript test execution" };
        }

        // Detect test runner from package.json
        var packageJsonPath = Path.Combine(projectPath, "package.json");
        if (!File.Exists(packageJsonPath))
        {
            return new { error = $"package.json not found at {projectPath}" };
        }

        string runner;
        string arguments;
        try
        {
            var packageJson = await File.ReadAllTextAsync(packageJsonPath, ct);
            var doc = JsonDocument.Parse(packageJson);

            // Check devDependencies and dependencies for test runner
            var hasVitest = HasDependency(doc, "vitest");
            var hasJest = HasDependency(doc, "jest");

            if (hasVitest)
            {
                runner = "vitest";
                arguments = "npx vitest run --reporter=json";
                if (!string.IsNullOrEmpty(filter))
                {
                    arguments += $" -t \"{filter}\"";
                }
            }
            else if (hasJest)
            {
                runner = "jest";
                arguments = "npx jest --json";
                if (!string.IsNullOrEmpty(filter))
                {
                    arguments += $" -t \"{filter}\"";
                }
            }
            else
            {
                return new { error = "No test runner detected. Install vitest or jest in your project." };
            }
        }
        catch (Exception ex)
        {
            return new { error = $"Failed to read package.json: {ex.Message}" };
        }

        try
        {
            // Determine shell and shell argument based on OS
            string shell;
            string shellArg;
            if (OperatingSystem.IsWindows())
            {
                shell = "cmd";
                shellArg = "/c";
            }
            else
            {
                shell = "/bin/sh";
                shellArg = "-c";
            }

            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = shell,
                    Arguments = $"{shellArg} \"{arguments}\"",
                    WorkingDirectory = projectPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            var output = new System.Text.StringBuilder();
            var error = new System.Text.StringBuilder();

            process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null) output.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null) error.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var completed = await Task.Run(() => process.WaitForExit(timeoutSeconds * 1000), ct);
            if (!completed)
            {
                process.Kill();
                return new { error = $"Test run timed out after {timeoutSeconds} seconds" };
            }

            var outputText = output.ToString();
            var success = process.ExitCode == 0;

            // Parse JSON output from test runner
            return ParseTestRunnerOutput(runner, outputText, success, process.ExitCode, projectPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run TypeScript tests for {Project}", projectPath);
            return new { error = $"Failed to run tests: {ex.Message}" };
        }
    }

    private static bool HasDependency(JsonDocument doc, string packageName)
    {
        if (doc.RootElement.TryGetProperty("devDependencies", out var devDeps) &&
            devDeps.TryGetProperty(packageName, out _))
        {
            return true;
        }

        if (doc.RootElement.TryGetProperty("dependencies", out var deps) &&
            deps.TryGetProperty(packageName, out _))
        {
            return true;
        }

        return false;
    }

    private static object ParseTestRunnerOutput(string runner, string output, bool success, int exitCode, string projectPath)
    {
        try
        {
            // Try to extract JSON from output (test runners may prefix with non-JSON text)
            var jsonStart = output.IndexOf('{');
            if (jsonStart < 0)
            {
                // Fallback — no JSON found
                return new
                {
                    projectPath,
                    runner,
                    success,
                    exitCode,
                    passed = 0,
                    failed = 0,
                    skipped = 0,
                    total = 0,
                    output = TruncateOutput(output),
                };
            }

            var jsonText = output[jsonStart..];
            using var doc = JsonDocument.Parse(jsonText);

            if (runner == "vitest")
            {
                return ParseVitestOutput(doc, success, exitCode, projectPath);
            }
            else
            {
                return ParseJestOutput(doc, success, exitCode, projectPath);
            }
        }
        catch (JsonException)
        {
            // JSON parse failed — return raw output
            return new
            {
                projectPath,
                runner,
                success,
                exitCode,
                passed = 0,
                failed = 0,
                skipped = 0,
                total = 0,
                output = TruncateOutput(output),
            };
        }
    }

    private static object ParseVitestOutput(JsonDocument doc, bool success, int exitCode, string projectPath)
    {
        var root = doc.RootElement;
        var passed = 0;
        var failed = 0;
        var skipped = 0;
        var total = 0;

        if (root.TryGetProperty("numPassedTests", out var p)) passed = p.GetInt32();
        if (root.TryGetProperty("numFailedTests", out var f)) failed = f.GetInt32();
        if (root.TryGetProperty("numPendingTests", out var s)) skipped = s.GetInt32();
        if (root.TryGetProperty("numTotalTests", out var t)) total = t.GetInt32();

        return new
        {
            projectPath,
            runner = "vitest",
            success,
            exitCode,
            passed,
            failed,
            skipped,
            total,
        };
    }

    private static object ParseJestOutput(JsonDocument doc, bool success, int exitCode, string projectPath)
    {
        var root = doc.RootElement;
        var passed = 0;
        var failed = 0;
        var skipped = 0;
        var total = 0;

        if (root.TryGetProperty("numPassedTests", out var p)) passed = p.GetInt32();
        if (root.TryGetProperty("numFailedTests", out var f)) failed = f.GetInt32();
        if (root.TryGetProperty("numPendingTests", out var s)) skipped = s.GetInt32();
        if (root.TryGetProperty("numTotalTests", out var t)) total = t.GetInt32();

        return new
        {
            projectPath,
            runner = "jest",
            success,
            exitCode,
            passed,
            failed,
            skipped,
            total,
        };
    }

    private static string TruncateOutput(string output)
    {
        return output.Length > 10000 ? output[..10000] + "\n... (truncated)" : output;
    }
}
