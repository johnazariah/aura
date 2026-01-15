// <copyright file="McpHandler.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Mcp;

using System.Text.Json;
using Aura.Foundation.Rag;
using Aura.Module.Developer.Data.Entities;
using Aura.Module.Developer.GitHub;
using Aura.Module.Developer.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles MCP (Model Context Protocol) JSON-RPC requests.
/// Exposes Aura's RAG and Code Graph capabilities to GitHub Copilot.
/// </summary>
public sealed class McpHandler
{
    private const string ProtocolVersion = "2024-11-05";
    private const string ServerName = "Aura";
    private const string ServerVersion = "1.2.0";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IRagService _ragService;
    private readonly ICodeGraphService _graphService;
    private readonly IWorkflowService _workflowService;
    private readonly IGitHubService _gitHubService;
    private readonly IRoslynWorkspaceService _roslynService;
    private readonly ILogger<McpHandler> _logger;

    private readonly Dictionary<string, Func<JsonElement?, CancellationToken, Task<object>>> _tools;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpHandler"/> class.
    /// </summary>
    public McpHandler(
        IRagService ragService,
        ICodeGraphService graphService,
        IWorkflowService workflowService,
        IGitHubService gitHubService,
        IRoslynWorkspaceService roslynService,
        ILogger<McpHandler> logger)
    {
        _ragService = ragService;
        _graphService = graphService;
        _workflowService = workflowService;
        _gitHubService = gitHubService;
        _roslynService = roslynService;
        _logger = logger;

        _tools = new Dictionary<string, Func<JsonElement?, CancellationToken, Task<object>>>
        {
            ["aura_search_code"] = SearchCodeAsync,
            ["aura_find_implementations"] = FindImplementationsAsync,
            ["aura_find_callers"] = FindCallersAsync,
            ["aura_get_type_members"] = GetTypeMembersAsync,
            ["aura_find_derived_types"] = FindDerivedTypesAsync,
            ["aura_find_usages"] = FindUsagesAsync,
            ["aura_list_classes"] = ListClassesAsync,
            ["aura_validate_compilation"] = ValidateCompilationAsync,
            ["aura_run_tests"] = RunTestsAsync,
            ["aura_list_stories"] = ListStoriesAsync,
            ["aura_get_story_context"] = GetStoryContextAsync,
            ["aura_create_story_from_issue"] = CreateStoryFromIssueAsync,
        };
    }

    /// <summary>
    /// Handles a JSON-RPC request and returns a JSON-RPC response.
    /// </summary>
    /// <param name="json">The JSON-RPC request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The JSON-RPC response.</returns>
    public async Task<string> HandleAsync(string json, CancellationToken ct = default)
    {
        JsonRpcRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<JsonRpcRequest>(json);
            if (request is null)
            {
                return SerializeResponse(ErrorResponse(null, -32700, "Parse error: null request"));
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse MCP request");
            return SerializeResponse(ErrorResponse(null, -32700, $"Parse error: {ex.Message}"));
        }

        _logger.LogDebug("MCP request: method={Method}, id={Id}", request.Method, request.Id);

        var response = request.Method switch
        {
            "initialize" => HandleInitialize(request),
            "tools/list" => HandleToolsList(request),
            "tools/call" => await HandleToolCallAsync(request, ct),
            _ => ErrorResponse(request.Id, -32601, $"Method not found: {request.Method}")
        };

        return SerializeResponse(response);
    }

    private JsonRpcResponse HandleInitialize(JsonRpcRequest request)
    {
        _logger.LogInformation("MCP client connected (initialize)");

        return new JsonRpcResponse
        {
            Id = request.Id,
            Result = new
            {
                protocolVersion = ProtocolVersion,
                capabilities = new
                {
                    tools = new { }
                },
                serverInfo = new
                {
                    name = ServerName,
                    version = ServerVersion
                }
            }
        };
    }

    private JsonRpcResponse HandleToolsList(JsonRpcRequest request)
    {
        var tools = new[]
        {
            new McpToolDefinition
            {
                Name = "aura_search_code",
                Description = "Semantic search across the indexed codebase. Returns relevant code chunks with file paths and similarity scores.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new { type = "string", description = "The search query" },
                        limit = new { type = "integer", description = "Maximum results (default 10)" }
                    },
                    required = new[] { "query" }
                }
            },
            new McpToolDefinition
            {
                Name = "aura_find_implementations",
                Description = "Find all types that implement a given interface or inherit from a base class.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        typeName = new { type = "string", description = "Interface or base class name" }
                    },
                    required = new[] { "typeName" }
                }
            },
            new McpToolDefinition
            {
                Name = "aura_find_callers",
                Description = "Find all methods that call a given method.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        methodName = new { type = "string", description = "The method name to find callers for" },
                        containingType = new { type = "string", description = "Optional: the type containing the method" }
                    },
                    required = new[] { "methodName" }
                }
            },
            new McpToolDefinition
            {
                Name = "aura_get_type_members",
                Description = "Get all members (methods, properties, fields) of a type.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        typeName = new { type = "string", description = "The type name" }
                    },
                    required = new[] { "typeName" }
                }
            },
            new McpToolDefinition
            {
                Name = "aura_find_derived_types",
                Description = "Find all types that inherit from a given base class.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        baseClassName = new { type = "string", description = "The base class name to find subclasses of" }
                    },
                    required = new[] { "baseClassName" }
                }
            },
            new McpToolDefinition
            {
                Name = "aura_find_usages",
                Description = "Find all usages/references of a symbol (class, method, property) across the codebase. Essential for refactoring.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        symbolName = new { type = "string", description = "Symbol name to find (class, method, or property name)" },
                        containingType = new { type = "string", description = "Optional: containing type for method/property search" },
                        solutionPath = new { type = "string", description = "Path to solution file (.sln)" }
                    },
                    required = new[] { "symbolName", "solutionPath" }
                }
            },
            new McpToolDefinition
            {
                Name = "aura_list_classes",
                Description = "List all classes, interfaces, and records in a project. Use to discover types before examining details.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        solutionPath = new { type = "string", description = "Path to solution file (.sln)" },
                        projectName = new { type = "string", description = "Project name to search in" },
                        namespaceFilter = new { type = "string", description = "Optional: filter by namespace (partial match)" },
                        nameFilter = new { type = "string", description = "Optional: filter by type name (partial match)" }
                    },
                    required = new[] { "solutionPath", "projectName" }
                }
            },
            new McpToolDefinition
            {
                Name = "aura_validate_compilation",
                Description = "Validate that a project compiles without errors. Use after making code changes to verify correctness.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        solutionPath = new { type = "string", description = "Path to solution file (.sln)" },
                        projectName = new { type = "string", description = "Project name to validate" },
                        includeWarnings = new { type = "boolean", description = "Include warnings in output (default: false)" }
                    },
                    required = new[] { "solutionPath", "projectName" }
                }
            },
            new McpToolDefinition
            {
                Name = "aura_run_tests",
                Description = "Run unit tests in a project. Use to validate that changes don't break existing functionality.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        projectPath = new { type = "string", description = "Path to test project (.csproj) or directory" },
                        filter = new { type = "string", description = "Optional: test filter (dotnet test --filter syntax)" },
                        timeoutSeconds = new { type = "integer", description = "Timeout in seconds (default: 120)" }
                    },
                    required = new[] { "projectPath" }
                }
            },
            new McpToolDefinition
            {
                Name = "aura_list_stories",
                Description = "List active development stories/workflows being tracked by Aura.",
                InputSchema = new
                {
                    type = "object",
                    properties = new { }
                }
            },
            new McpToolDefinition
            {
                Name = "aura_get_story_context",
                Description = "Get detailed context for a specific story, including requirements and current state.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        storyId = new { type = "string", description = "The story/workflow ID (GUID)" }
                    },
                    required = new[] { "storyId" }
                }
            },
            new McpToolDefinition
            {
                Name = "aura_create_story_from_issue",
                Description = "Create a new development story from a GitHub issue URL. Fetches issue details and optionally creates a worktree.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        issueUrl = new { type = "string", description = "GitHub issue URL (e.g., https://github.com/owner/repo/issues/123)" },
                        repositoryPath = new { type = "string", description = "Optional: local repository path for worktree creation" },
                        mode = new { type = "string", description = "Optional: 'Conversational' (default) or 'Structured'" }
                    },
                    required = new[] { "issueUrl" }
                }
            },
        };

        return new JsonRpcResponse
        {
            Id = request.Id,
            Result = new { tools }
        };
    }

    private async Task<JsonRpcResponse> HandleToolCallAsync(JsonRpcRequest request, CancellationToken ct)
    {
        string? toolName = null;
        JsonElement? args = null;

        try
        {
            if (request.Params.HasValue)
            {
                var paramsElement = request.Params.Value;
                if (paramsElement.TryGetProperty("name", out var nameElement))
                {
                    toolName = nameElement.GetString();
                }

                if (paramsElement.TryGetProperty("arguments", out var argsElement))
                {
                    args = argsElement;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse tool call params");
            return ErrorResponse(request.Id, -32602, "Invalid params");
        }

        if (string.IsNullOrEmpty(toolName))
        {
            return ErrorResponse(request.Id, -32602, "Missing tool name");
        }

        if (!_tools.TryGetValue(toolName, out var handler))
        {
            return ErrorResponse(request.Id, -32602, $"Unknown tool: {toolName}");
        }

        try
        {
            _logger.LogDebug("Executing MCP tool: {Tool}", toolName);
            var result = await handler(args, ct);
            var resultJson = JsonSerializer.Serialize(result, JsonOptions);

            return new JsonRpcResponse
            {
                Id = request.Id,
                Result = new
                {
                    content = new[]
                    {
                        new McpContent { Type = "text", Text = resultJson }
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MCP tool {Tool} failed", toolName);
            return ErrorResponse(request.Id, -32000, ex.Message);
        }
    }

    // =========================================================================
    // Tool Implementations
    // =========================================================================

    private async Task<object> SearchCodeAsync(JsonElement? args, CancellationToken ct)
    {
        var query = args?.GetProperty("query").GetString() ?? "";
        var limit = 10;
        if (args.HasValue && args.Value.TryGetProperty("limit", out var limitEl))
        {
            limit = limitEl.GetInt32();
        }

        var options = new RagQueryOptions { TopK = limit };
        var results = await _ragService.QueryAsync(query, options, ct);

        return results.Select(r => new
        {
            content = r.Text,
            filePath = r.SourcePath,
            score = r.Score,
            contentType = r.ContentType.ToString()
        });
    }

    private async Task<object> FindImplementationsAsync(JsonElement? args, CancellationToken ct)
    {
        var typeName = args?.GetProperty("typeName").GetString() ?? "";
        var results = await _graphService.FindImplementationsAsync(typeName, cancellationToken: ct);

        return results.Select(n => new
        {
            name = n.Name,
            fullName = n.FullName,
            kind = n.NodeType.ToString(),
            filePath = n.FilePath,
            line = n.LineNumber
        });
    }

    private async Task<object> FindCallersAsync(JsonElement? args, CancellationToken ct)
    {
        var methodName = args?.GetProperty("methodName").GetString() ?? "";
        string? containingType = null;
        if (args.HasValue && args.Value.TryGetProperty("containingType", out var typeEl))
        {
            containingType = typeEl.GetString();
        }

        var results = await _graphService.FindCallersAsync(methodName, containingType, cancellationToken: ct);

        return results.Select(n => new
        {
            name = n.Name,
            fullName = n.FullName,
            kind = n.NodeType.ToString(),
            filePath = n.FilePath,
            line = n.LineNumber
        });
    }

    private async Task<object> GetTypeMembersAsync(JsonElement? args, CancellationToken ct)
    {
        var typeName = args?.GetProperty("typeName").GetString() ?? "";
        var results = await _graphService.GetTypeMembersAsync(typeName, cancellationToken: ct);

        return results.Select(n => new
        {
            name = n.Name,
            kind = n.NodeType.ToString(),
            filePath = n.FilePath,
            line = n.LineNumber
        });
    }

    private async Task<object> FindDerivedTypesAsync(JsonElement? args, CancellationToken ct)
    {
        var baseClassName = args?.GetProperty("baseClassName").GetString() ?? "";
        var results = await _graphService.FindDerivedTypesAsync(baseClassName, cancellationToken: ct);

        return results.Select(n => new
        {
            name = n.Name,
            fullName = n.FullName,
            kind = n.NodeType.ToString(),
            filePath = n.FilePath,
            line = n.LineNumber
        });
    }

    private async Task<object> FindUsagesAsync(JsonElement? args, CancellationToken ct)
    {
        var symbolName = args?.GetProperty("symbolName").GetString() ?? "";
        var solutionPath = args?.GetProperty("solutionPath").GetString() ?? "";

        string? containingType = null;
        if (args.HasValue && args.Value.TryGetProperty("containingType", out var typeEl))
        {
            containingType = typeEl.GetString();
        }

        if (string.IsNullOrEmpty(solutionPath) || !File.Exists(solutionPath))
        {
            return new { error = $"Solution file not found: {solutionPath}" };
        }

        try
        {
            var solution = await _roslynService.GetSolutionAsync(solutionPath, ct);
            var usages = new List<object>();

            foreach (var project in solution.Projects)
            {
                var compilation = await project.GetCompilationAsync(ct);
                if (compilation is null) continue;

                foreach (var syntaxTree in compilation.SyntaxTrees)
                {
                    var semanticModel = compilation.GetSemanticModel(syntaxTree);
                    var root = await syntaxTree.GetRootAsync(ct);

                    // Find all identifier names matching our symbol
                    var identifiers = root.DescendantNodes()
                        .OfType<IdentifierNameSyntax>()
                        .Where(id => id.Identifier.Text == symbolName ||
                                     (containingType != null && id.Identifier.Text == symbolName));

                    foreach (var identifier in identifiers)
                    {
                        var symbol = semanticModel.GetSymbolInfo(identifier, ct).Symbol;
                        if (symbol is null) continue;

                        // Check if it matches containing type filter
                        if (containingType != null &&
                            symbol.ContainingType?.Name != containingType)
                            continue;

                        var location = identifier.GetLocation();
                        var lineSpan = location.GetLineSpan();
                        var line = lineSpan.StartLinePosition.Line + 1;
                        var lineText = (await syntaxTree.GetTextAsync(ct))
                            .Lines[lineSpan.StartLinePosition.Line]
                            .ToString()
                            .Trim();

                        usages.Add(new
                        {
                            filePath = syntaxTree.FilePath,
                            line,
                            column = lineSpan.StartLinePosition.Character + 1,
                            codeSnippet = lineText.Length > 200 ? lineText[..200] + "..." : lineText,
                            symbolKind = symbol.Kind.ToString(),
                            containingMember = symbol.ContainingSymbol?.Name
                        });

                        if (usages.Count >= 50) break; // Limit results
                    }

                    if (usages.Count >= 50) break;
                }

                if (usages.Count >= 50) break;
            }

            return new
            {
                symbolName,
                totalUsages = usages.Count,
                wasTruncated = usages.Count >= 50,
                usages
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find usages for {Symbol}", symbolName);
            return new { error = $"Failed to find usages: {ex.Message}" };
        }
    }

    private async Task<object> ListClassesAsync(JsonElement? args, CancellationToken ct)
    {
        var solutionPath = args?.GetProperty("solutionPath").GetString() ?? "";
        var projectName = args?.GetProperty("projectName").GetString() ?? "";

        string? namespaceFilter = null;
        string? nameFilter = null;
        if (args.HasValue)
        {
            if (args.Value.TryGetProperty("namespaceFilter", out var nsEl))
                namespaceFilter = nsEl.GetString();
            if (args.Value.TryGetProperty("nameFilter", out var nameEl))
                nameFilter = nameEl.GetString();
        }

        if (string.IsNullOrEmpty(solutionPath) || !File.Exists(solutionPath))
        {
            return new { error = $"Solution file not found: {solutionPath}" };
        }

        try
        {
            var solution = await _roslynService.GetSolutionAsync(solutionPath, ct);
            var project = solution.Projects.FirstOrDefault(p =>
                p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));

            if (project is null)
            {
                var available = string.Join(", ", solution.Projects.Select(p => p.Name));
                return new { error = $"Project '{projectName}' not found. Available: {available}" };
            }

            var compilation = await project.GetCompilationAsync(ct);
            if (compilation is null)
            {
                return new { error = "Failed to get compilation" };
            }

            var types = new List<object>();

            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                var root = await syntaxTree.GetRootAsync(ct);

                var typeDeclarations = root.DescendantNodes()
                    .OfType<TypeDeclarationSyntax>();

                foreach (var typeDecl in typeDeclarations)
                {
                    var symbol = semanticModel.GetDeclaredSymbol(typeDecl, ct);
                    if (symbol is not INamedTypeSymbol namedType) continue;

                    var ns = namedType.ContainingNamespace?.ToDisplayString() ?? "";
                    var name = namedType.Name;

                    // Apply filters
                    if (namespaceFilter != null && !ns.Contains(namespaceFilter, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (nameFilter != null && !name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var lineSpan = typeDecl.GetLocation().GetLineSpan();

                    types.Add(new
                    {
                        name,
                        fullName = namedType.ToDisplayString(),
                        @namespace = ns,
                        kind = typeDecl switch
                        {
                            InterfaceDeclarationSyntax => "interface",
                            RecordDeclarationSyntax => "record",
                            StructDeclarationSyntax => "struct",
                            _ => "class"
                        },
                        filePath = syntaxTree.FilePath,
                        line = lineSpan.StartLinePosition.Line + 1,
                        isAbstract = namedType.IsAbstract,
                        isSealed = namedType.IsSealed,
                        memberCount = namedType.GetMembers().Length
                    });
                }
            }

            return new
            {
                projectName = project.Name,
                totalTypes = types.Count,
                types = types.OrderBy(t => ((dynamic)t).fullName).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list classes in {Project}", projectName);
            return new { error = $"Failed to list classes: {ex.Message}" };
        }
    }

    private async Task<object> ValidateCompilationAsync(JsonElement? args, CancellationToken ct)
    {
        var solutionPath = args?.GetProperty("solutionPath").GetString() ?? "";
        var projectName = args?.GetProperty("projectName").GetString() ?? "";
        var includeWarnings = false;
        if (args.HasValue && args.Value.TryGetProperty("includeWarnings", out var warnEl))
        {
            includeWarnings = warnEl.GetBoolean();
        }

        if (string.IsNullOrEmpty(solutionPath) || !File.Exists(solutionPath))
        {
            return new { error = $"Solution file not found: {solutionPath}" };
        }

        try
        {
            var solution = await _roslynService.GetSolutionAsync(solutionPath, ct);
            var project = solution.Projects.FirstOrDefault(p =>
                p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));

            if (project is null)
            {
                var available = string.Join(", ", solution.Projects.Select(p => p.Name));
                return new { error = $"Project '{projectName}' not found. Available: {available}" };
            }

            var compilation = await project.GetCompilationAsync(ct);
            if (compilation is null)
            {
                return new { error = "Failed to get compilation" };
            }

            var diagnostics = compilation.GetDiagnostics(ct)
                .Where(d => d.Severity == DiagnosticSeverity.Error ||
                           (includeWarnings && d.Severity == DiagnosticSeverity.Warning))
                .Take(50)
                .Select(d =>
                {
                    var lineSpan = d.Location.GetLineSpan();
                    return new
                    {
                        id = d.Id,
                        severity = d.Severity.ToString(),
                        message = d.GetMessage(),
                        filePath = lineSpan.Path,
                        line = lineSpan.StartLinePosition.Line + 1,
                        column = lineSpan.StartLinePosition.Character + 1
                    };
                })
                .ToList();

            var errorCount = diagnostics.Count(d => d.severity == "Error");
            var warningCount = diagnostics.Count(d => d.severity == "Warning");

            return new
            {
                projectName = project.Name,
                success = errorCount == 0,
                errorCount,
                warningCount,
                diagnostics,
                summary = errorCount == 0
                    ? $"Project {project.Name} compiles successfully"
                    : $"Project {project.Name} has {errorCount} error(s)"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate compilation for {Project}", projectName);
            return new { error = $"Failed to validate compilation: {ex.Message}" };
        }
    }

    private async Task<object> RunTestsAsync(JsonElement? args, CancellationToken ct)
    {
        var projectPath = args?.GetProperty("projectPath").GetString() ?? "";
        string? filter = null;
        var timeoutSeconds = 120;

        if (args.HasValue)
        {
            if (args.Value.TryGetProperty("filter", out var filterEl))
                filter = filterEl.GetString();
            if (args.Value.TryGetProperty("timeoutSeconds", out var timeoutEl))
                timeoutSeconds = timeoutEl.GetInt32();
        }

        if (string.IsNullOrEmpty(projectPath))
        {
            return new { error = "projectPath is required" };
        }

        try
        {
            var arguments = $"test \"{projectPath}\" --no-restore --verbosity normal";
            if (!string.IsNullOrEmpty(filter))
            {
                arguments += $" --filter \"{filter}\"";
            }

            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            var output = new System.Text.StringBuilder();
            var error = new System.Text.StringBuilder();

            process.OutputDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) error.AppendLine(e.Data); };

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

            // Parse test results from output
            var passedMatch = System.Text.RegularExpressions.Regex.Match(outputText, @"Passed:\s*(\d+)");
            var failedMatch = System.Text.RegularExpressions.Regex.Match(outputText, @"Failed:\s*(\d+)");
            var skippedMatch = System.Text.RegularExpressions.Regex.Match(outputText, @"Skipped:\s*(\d+)");
            var totalMatch = System.Text.RegularExpressions.Regex.Match(outputText, @"Total:\s*(\d+)");

            return new
            {
                projectPath,
                success,
                exitCode = process.ExitCode,
                passed = passedMatch.Success ? int.Parse(passedMatch.Groups[1].Value) : 0,
                failed = failedMatch.Success ? int.Parse(failedMatch.Groups[1].Value) : 0,
                skipped = skippedMatch.Success ? int.Parse(skippedMatch.Groups[1].Value) : 0,
                total = totalMatch.Success ? int.Parse(totalMatch.Groups[1].Value) : 0,
                output = outputText.Length > 5000 ? outputText[..5000] + "\n... (truncated)" : outputText
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run tests for {Project}", projectPath);
            return new { error = $"Failed to run tests: {ex.Message}" };
        }
    }

    private async Task<object> ListStoriesAsync(JsonElement? args, CancellationToken ct)
    {
        // List active workflows (stories) - exclude completed/cancelled
        var workflows = await _workflowService.ListAsync(ct: ct);

        return workflows
            .Where(w => w.Status != WorkflowStatus.Completed && w.Status != WorkflowStatus.Cancelled)
            .Select(w => new
            {
                id = w.Id,
                title = w.Title,
                status = w.Status.ToString(),
                mode = w.Mode.ToString(),
                gitBranch = w.GitBranch,
                worktreePath = w.WorktreePath,
                repositoryPath = w.RepositoryPath,
                issueUrl = w.IssueUrl,
                issueNumber = w.IssueNumber,
                stepCount = w.Steps.Count,
                completedSteps = w.Steps.Count(s => s.Status == StepStatus.Completed),
                createdAt = w.CreatedAt
            });
    }

    private async Task<object> GetStoryContextAsync(JsonElement? args, CancellationToken ct)
    {
        var storyIdStr = args?.GetProperty("storyId").GetString() ?? "";
        if (!Guid.TryParse(storyIdStr, out var storyId))
        {
            return new { error = $"Invalid story ID: {storyIdStr}" };
        }

        var workflow = await _workflowService.GetByIdWithStepsAsync(storyId, ct);
        if (workflow is null)
        {
            return new { error = $"Story not found: {storyId}" };
        }

        return new
        {
            id = workflow.Id,
            title = workflow.Title,
            description = workflow.Description,
            status = workflow.Status.ToString(),
            mode = workflow.Mode.ToString(),
            issueUrl = workflow.IssueUrl,
            issueProvider = workflow.IssueProvider?.ToString(),
            issueNumber = workflow.IssueNumber,
            issueOwner = workflow.IssueOwner,
            issueRepo = workflow.IssueRepo,
            analyzedContext = workflow.AnalyzedContext,
            gitBranch = workflow.GitBranch,
            worktreePath = workflow.WorktreePath,
            repositoryPath = workflow.RepositoryPath,
            steps = workflow.Steps.OrderBy(s => s.Order).Select(s => new
            {
                id = s.Id,
                name = s.Name,
                description = s.Description,
                status = s.Status.ToString(),
                order = s.Order
            }),
            createdAt = workflow.CreatedAt,
            updatedAt = workflow.UpdatedAt
        };
    }

    private async Task<object> CreateStoryFromIssueAsync(JsonElement? args, CancellationToken ct)
    {
        var issueUrl = args?.GetProperty("issueUrl").GetString() ?? "";
        if (string.IsNullOrEmpty(issueUrl))
        {
            return new { error = "issueUrl is required" };
        }

        var parsed = _gitHubService.ParseIssueUrl(issueUrl);
        if (parsed is null)
        {
            return new { error = "Invalid GitHub issue URL. Expected format: https://github.com/owner/repo/issues/123" };
        }

        if (!_gitHubService.IsConfigured)
        {
            return new { error = "GitHub integration not configured. Set GitHub:Token in appsettings.json" };
        }

        string? repositoryPath = null;
        if (args.HasValue && args.Value.TryGetProperty("repositoryPath", out var repoEl))
        {
            repositoryPath = repoEl.GetString();
        }

        var mode = WorkflowMode.Conversational;
        if (args.HasValue && args.Value.TryGetProperty("mode", out var modeEl))
        {
            var modeStr = modeEl.GetString();
            if (!string.IsNullOrEmpty(modeStr) && Enum.TryParse<WorkflowMode>(modeStr, true, out var m))
            {
                mode = m;
            }
        }

        try
        {
            // Fetch issue from GitHub
            var issue = await _gitHubService.GetIssueAsync(parsed.Value.Owner, parsed.Value.Repo, parsed.Value.Number, ct);

            // Create workflow/story
            var workflow = await _workflowService.CreateAsync(
                issue.Title,
                issue.Body,
                repositoryPath,
                mode,
                AutomationMode.Assisted, // MCP-created workflows default to assisted mode
                issueUrl,
                ct);

            // Post a comment to the issue that work has started
            var branch = workflow.GitBranch ?? "unknown";
            await _gitHubService.PostCommentAsync(
                parsed.Value.Owner,
                parsed.Value.Repo,
                parsed.Value.Number,
                $"Started work in branch `{branch}`",
                ct);

            return new
            {
                id = workflow.Id,
                title = workflow.Title,
                description = workflow.Description,
                status = workflow.Status.ToString(),
                mode = workflow.Mode.ToString(),
                gitBranch = workflow.GitBranch,
                worktreePath = workflow.WorktreePath,
                issueUrl = workflow.IssueUrl,
                issueNumber = workflow.IssueNumber,
                createdAt = workflow.CreatedAt
            };
        }
        catch (HttpRequestException ex)
        {
            return new { error = $"Failed to fetch issue from GitHub: {ex.Message}" };
        }
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static JsonRpcResponse ErrorResponse(object? id, int code, string message) =>
        new()
        {
            Id = id,
            Error = new JsonRpcError { Code = code, Message = message }
        };

    private static string SerializeResponse(JsonRpcResponse response) =>
        JsonSerializer.Serialize(response, JsonOptions);
}
