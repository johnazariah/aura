// <copyright file="McpHandler.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Mcp;

using System.Text.Json;
using Aura.Foundation.Git;
using Aura.Foundation.Rag;
using Aura.Module.Developer.Data.Entities;
using Aura.Module.Developer.GitHub;
using Aura.Module.Developer.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.Extensions.Logging;
using RefactoringParameterInfo = Aura.Module.Developer.Services.ParameterInfo;

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
    private readonly IRoslynRefactoringService _refactoringService;
    private readonly IPythonRefactoringService _pythonRefactoringService;
    private readonly IGitWorktreeService _worktreeService;
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
        IRoslynRefactoringService refactoringService,
        IPythonRefactoringService pythonRefactoringService,
        IGitWorktreeService worktreeService,
        ILogger<McpHandler> logger)
    {
        _ragService = ragService;
        _graphService = graphService;
        _workflowService = workflowService;
        _gitHubService = gitHubService;
        _roslynService = roslynService;
        _refactoringService = refactoringService;
        _pythonRefactoringService = pythonRefactoringService;
        _worktreeService = worktreeService;
        _logger = logger;

        // Phase 7: Consolidated meta-tools (28 tools → 8 tools)
        _tools = new Dictionary<string, Func<JsonElement?, CancellationToken, Task<object>>>
        {
            ["aura_search"] = SearchAsync,
            ["aura_navigate"] = NavigateAsync,
            ["aura_inspect"] = InspectAsync,
            ["aura_refactor"] = RefactorAsync,
            ["aura_generate"] = GenerateAsync,
            ["aura_validate"] = ValidateAsync,
            ["aura_workflow"] = WorkflowAsync,
            ["aura_architect"] = ArchitectAsync,
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
        // Phase 7: Consolidated to 8 intent-based meta-tools (from 28)
        var tools = new[]
        {
            // =================================================================
            // aura_search - Semantic and structural code search
            // =================================================================
            new McpToolDefinition
            {
                Name = "aura_search",
                Description = "Semantic search across the indexed codebase. Returns relevant code chunks with file paths and similarity scores. Exact symbol matches are boosted. (Read)",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new { type = "string", description = "The search query (concept, symbol name, or keyword)" },
                        workspacePath = new { type = "string", description = "Path to the current workspace or worktree. Used to filter results to the correct repository." },
                        limit = new { type = "integer", description = "Maximum results (default 10)" },
                        contentType = new
                        {
                            type = "string",
                            description = "Filter by content type",
                            @enum = new[] { "code", "docs", "config", "all" }
                        }
                    },
                    required = new[] { "query" }
                }
            },

            // =================================================================
            // aura_navigate - Find code elements and relationships
            // =================================================================
            new McpToolDefinition
            {
                Name = "aura_navigate",
                Description = "Find code elements and their relationships: callers, implementations, derived types, usages, references. (Read)",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        operation = new
                        {
                            type = "string",
                            description = "Navigation operation type",
                            @enum = new[] { "callers", "implementations", "derived_types", "usages", "by_attribute", "extension_methods", "by_return_type", "references", "definition" }
                        },
                        symbolName = new { type = "string", description = "Symbol name to navigate from (method, type, property)" },
                        containingType = new { type = "string", description = "Optional: type containing the symbol (for disambiguation)" },
                        solutionPath = new { type = "string", description = "Path to solution file (.sln) - required for most C# operations" },
                        filePath = new { type = "string", description = "Path to file - required for Python operations (references, definition)" },
                        offset = new { type = "integer", description = "Character offset in file - required for Python operations" },
                        projectPath = new { type = "string", description = "Project root path - required for Python operations" },
                        attributeName = new { type = "string", description = "Attribute name for by_attribute operation (e.g., 'HttpGet', 'Test')" },
                        targetType = new { type = "string", description = "Target type for extension_methods or by_return_type operations" },
                        targetKind = new
                        {
                            type = "string",
                            description = "Filter by symbol kind for by_attribute",
                            @enum = new[] { "method", "class", "property", "all" }
                        }
                    },
                    required = new[] { "operation" }
                }
            },

            // =================================================================
            // aura_inspect - Examine code structure
            // =================================================================
            new McpToolDefinition
            {
                Name = "aura_inspect",
                Description = "Examine code structure: type members, class listings, project exploration. (Read)",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        operation = new
                        {
                            type = "string",
                            description = "Inspection operation type",
                            @enum = new[] { "type_members", "list_types" }
                        },
                        typeName = new { type = "string", description = "Type name for type_members operation" },
                        solutionPath = new { type = "string", description = "Path to solution file (.sln)" },
                        projectName = new { type = "string", description = "Project name for list_types operation" },
                        namespaceFilter = new { type = "string", description = "Filter by namespace (partial match)" },
                        nameFilter = new { type = "string", description = "Filter by type name (partial match)" }
                    },
                    required = new[] { "operation" }
                }
            },

            // =================================================================
            // aura_refactor - Transform existing code
            // =================================================================
            new McpToolDefinition
            {
                Name = "aura_refactor",
                Description = "Transform existing code: rename symbols, change signatures, extract methods/variables/interfaces, safe delete. Auto-detects language from filePath. (Write)",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        operation = new
                        {
                            type = "string",
                            description = "Refactoring operation type",
                            @enum = new[] { "rename", "change_signature", "extract_interface", "extract_method", "extract_variable", "safe_delete" }
                        },
                        symbolName = new { type = "string", description = "Symbol to refactor" },
                        newName = new { type = "string", description = "New name for rename, extract_method, extract_variable, extract_interface" },
                        containingType = new { type = "string", description = "Type containing the symbol (for C# disambiguation)" },
                        solutionPath = new { type = "string", description = "Path to solution file (.sln) - for C# operations" },
                        filePath = new { type = "string", description = "Path to file containing the code" },
                        projectPath = new { type = "string", description = "Project root - for Python operations" },
                        offset = new { type = "integer", description = "Character offset for Python rename" },
                        startOffset = new { type = "integer", description = "Start offset for Python extract operations" },
                        endOffset = new { type = "integer", description = "End offset for Python extract operations" },
                        members = new
                        {
                            type = "array",
                            items = new { type = "string" },
                            description = "Member names for extract_interface"
                        },
                        addParameters = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    name = new { type = "string" },
                                    type = new { type = "string" },
                                    defaultValue = new { type = "string" }
                                }
                            },
                            description = "Parameters to add for change_signature"
                        },
                        removeParameters = new
                        {
                            type = "array",
                            items = new { type = "string" },
                            description = "Parameter names to remove for change_signature"
                        },
                        preview = new { type = "boolean", description = "If true, return changes without applying (default: false)" }
                    },
                    required = new[] { "operation" }
                }
            },

            // =================================================================
            // aura_generate - Create new code elements
            // =================================================================
            new McpToolDefinition
            {
                Name = "aura_generate",
                Description = "Generate new code: implement interfaces, generate constructors, add properties/methods. (Write)",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        operation = new
                        {
                            type = "string",
                            description = "Generation operation type",
                            @enum = new[] { "implement_interface", "constructor", "property", "method" }
                        },
                        className = new { type = "string", description = "Target class name" },
                        solutionPath = new { type = "string", description = "Path to solution file (.sln)" },
                        interfaceName = new { type = "string", description = "Interface to implement (for implement_interface)" },
                        explicitImplementation = new { type = "boolean", description = "Use explicit interface implementation (default: false)" },
                        members = new
                        {
                            type = "array",
                            items = new { type = "string" },
                            description = "Field/property names for constructor initialization"
                        },
                        propertyName = new { type = "string", description = "Name for new property" },
                        propertyType = new { type = "string", description = "Type for new property" },
                        hasGetter = new { type = "boolean", description = "Include getter (default: true)" },
                        hasSetter = new { type = "boolean", description = "Include setter (default: true)" },
                        initialValue = new { type = "string", description = "Initial value for property" },
                        methodName = new { type = "string", description = "Name for new method" },
                        returnType = new { type = "string", description = "Return type for new method" },
                        parameters = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    name = new { type = "string" },
                                    type = new { type = "string" },
                                    defaultValue = new { type = "string" }
                                }
                            },
                            description = "Method parameters"
                        },
                        accessModifier = new { type = "string", description = "Access modifier (default: 'public')" },
                        isAsync = new { type = "boolean", description = "Whether method is async" },
                        body = new { type = "string", description = "Optional method body code" },
                        preview = new { type = "boolean", description = "If true, return changes without applying (default: false)" }
                    },
                    required = new[] { "operation", "className", "solutionPath" }
                }
            },

            // =================================================================
            // aura_validate - Check code correctness
            // =================================================================
            new McpToolDefinition
            {
                Name = "aura_validate",
                Description = "Validate code: check compilation, run tests. Use after code changes. (Read)",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        operation = new
                        {
                            type = "string",
                            description = "Validation operation type",
                            @enum = new[] { "compilation", "tests" }
                        },
                        solutionPath = new { type = "string", description = "Path to solution file (.sln) - for compilation" },
                        projectName = new { type = "string", description = "Project name - for compilation" },
                        projectPath = new { type = "string", description = "Path to test project - for tests" },
                        includeWarnings = new { type = "boolean", description = "Include warnings in compilation output (default: false)" },
                        filter = new { type = "string", description = "Test filter (dotnet test --filter syntax)" },
                        timeoutSeconds = new { type = "integer", description = "Timeout in seconds for tests (default: 120)" }
                    },
                    required = new[] { "operation" }
                }
            },

            // =================================================================
            // aura_workflow - Manage development workflows
            // =================================================================
            new McpToolDefinition
            {
                Name = "aura_workflow",
                Description = "Manage Aura development workflows/stories: list, get details, create from GitHub issues. (CRUD)",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        operation = new
                        {
                            type = "string",
                            description = "Workflow operation type",
                            @enum = new[] { "list", "get", "create" }
                        },
                        storyId = new { type = "string", description = "Story ID (GUID) - for get operation" },
                        issueUrl = new { type = "string", description = "GitHub issue URL - for create operation" },
                        repositoryPath = new { type = "string", description = "Local repository path for worktree creation" },
                        mode = new
                        {
                            type = "string",
                            description = "Workflow mode",
                            @enum = new[] { "Conversational", "Structured" }
                        }
                    },
                    required = new[] { "operation" }
                }
            },

            // =================================================================
            // aura_architect - Whole-codebase architectural analysis (placeholder)
            // =================================================================
            new McpToolDefinition
            {
                Name = "aura_architect",
                Description = "Analyze codebase architecture: dependencies, layer violations, public API surface. (Read/Write) [Coming Soon]",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        operation = new
                        {
                            type = "string",
                            description = "Architecture operation type",
                            @enum = new[] { "dependencies", "layer_check", "public_api" }
                        },
                        projectPath = new { type = "string", description = "Path to project or solution" },
                        targetLayer = new { type = "string", description = "Target layer for layer_check" }
                    },
                    required = new[] { "operation" }
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
    // Phase 7: Meta-Tool Routers (8 consolidated tools)
    // =========================================================================

    /// <summary>
    /// aura_search - Semantic and structural code search (was aura_search_code).
    /// </summary>
    private async Task<object> SearchAsync(JsonElement? args, CancellationToken ct)
    {
        // Delegates to existing SearchCodeAsync logic
        return await SearchCodeAsync(args, ct);
    }

    /// <summary>
    /// aura_navigate - Find code elements and relationships.
    /// Routes to: callers, implementations, derived_types, usages, by_attribute, extension_methods, by_return_type, references, definition.
    /// </summary>
    private async Task<object> NavigateAsync(JsonElement? args, CancellationToken ct)
    {
        var operation = args?.GetProperty("operation").GetString()
            ?? throw new ArgumentException("operation is required");

        return operation switch
        {
            "callers" => await FindCallersAsync(args, ct),
            "implementations" => await FindImplementationsFromNavigate(args, ct),
            "derived_types" => await FindDerivedTypesFromNavigate(args, ct),
            "usages" => await FindUsagesAsync(args, ct),
            "by_attribute" => await FindByAttributeFromNavigate(args, ct),
            "extension_methods" => await FindExtensionMethodsFromNavigate(args, ct),
            "by_return_type" => await FindByReturnTypeFromNavigate(args, ct),
            "references" => await PythonFindReferencesAsync(args, ct),
            "definition" => await PythonFindDefinitionAsync(args, ct),
            _ => throw new ArgumentException($"Unknown navigate operation: {operation}")
        };
    }

    // Navigation helpers - adapt from old parameter names to new unified schema
    private async Task<object> FindImplementationsFromNavigate(JsonElement? args, CancellationToken ct)
    {
        var typeName = args?.GetProperty("symbolName").GetString() ?? "";
        var results = await _graphService.FindImplementationsAsync(typeName, cancellationToken: ct);
        return results.Select(n => new { name = n.Name, fullName = n.FullName, kind = n.NodeType.ToString(), filePath = n.FilePath, line = n.LineNumber });
    }

    private async Task<object> FindDerivedTypesFromNavigate(JsonElement? args, CancellationToken ct)
    {
        var baseClassName = args?.GetProperty("symbolName").GetString() ?? "";
        var results = await _graphService.FindDerivedTypesAsync(baseClassName, cancellationToken: ct);
        return results.Select(n => new { name = n.Name, fullName = n.FullName, kind = n.NodeType.ToString(), filePath = n.FilePath, line = n.LineNumber });
    }

    private async Task<object> FindByAttributeFromNavigate(JsonElement? args, CancellationToken ct)
    {
        // Delegate to existing implementation - just need to remap attributeName from symbolName if needed
        return await FindByAttributeAsync(args, ct);
    }

    private async Task<object> FindExtensionMethodsFromNavigate(JsonElement? args, CancellationToken ct)
    {
        // Remap targetType to extendedTypeName for existing implementation
        return await FindExtensionMethodsAsync(args, ct);
    }

    private async Task<object> FindByReturnTypeFromNavigate(JsonElement? args, CancellationToken ct)
    {
        // Remap targetType to returnTypeName for existing implementation
        return await FindByReturnTypeAsync(args, ct);
    }

    /// <summary>
    /// aura_inspect - Examine code structure.
    /// Routes to: type_members, list_types.
    /// </summary>
    private async Task<object> InspectAsync(JsonElement? args, CancellationToken ct)
    {
        var operation = args?.GetProperty("operation").GetString()
            ?? throw new ArgumentException("operation is required");

        return operation switch
        {
            "type_members" => await GetTypeMembersAsync(args, ct),
            "list_types" => await ListClassesFromInspect(args, ct),
            _ => throw new ArgumentException($"Unknown inspect operation: {operation}")
        };
    }

    private async Task<object> ListClassesFromInspect(JsonElement? args, CancellationToken ct)
    {
        // Adapts from new schema to existing ListClassesAsync
        return await ListClassesAsync(args, ct);
    }

    /// <summary>
    /// aura_refactor - Transform existing code.
    /// Routes to: rename, change_signature, extract_interface, extract_method, extract_variable, safe_delete.
    /// Auto-detects language from filePath extension.
    /// </summary>
    private async Task<object> RefactorAsync(JsonElement? args, CancellationToken ct)
    {
        var operation = args?.GetProperty("operation").GetString()
            ?? throw new ArgumentException("operation is required");

        // Detect language from filePath if provided
        var isPython = false;
        if (args.HasValue && args.Value.TryGetProperty("filePath", out var filePathEl))
        {
            var filePath = filePathEl.GetString() ?? "";
            isPython = filePath.EndsWith(".py", StringComparison.OrdinalIgnoreCase);
        }

        return operation switch
        {
            "rename" when isPython => await PythonRenameAsync(args, ct),
            "rename" => await RenameSymbolAsync(args, ct),
            "change_signature" => await ChangeSignatureAsync(args, ct),
            "extract_interface" => await ExtractInterfaceFromRefactor(args, ct),
            "extract_method" when isPython => await PythonExtractMethodAsync(args, ct),
            "extract_variable" when isPython => await PythonExtractVariableAsync(args, ct),
            "safe_delete" => await SafeDeleteAsync(args, ct),
            "extract_method" => throw new NotSupportedException("C# extract_method not yet implemented. Use Python files or manual extraction."),
            "extract_variable" => throw new NotSupportedException("C# extract_variable not yet implemented. Use Python files or manual extraction."),
            _ => throw new ArgumentException($"Unknown refactor operation: {operation}")
        };
    }

    private async Task<object> ExtractInterfaceFromRefactor(JsonElement? args, CancellationToken ct)
    {
        // Map from new schema (symbolName as class name, newName as interface name) to old schema
        var className = args?.GetProperty("symbolName").GetString()
            ?? throw new ArgumentException("symbolName (class name) is required for extract_interface");
        var interfaceName = args?.GetProperty("newName").GetString()
            ?? throw new ArgumentException("newName (interface name) is required for extract_interface");
        var solutionPath = args?.GetProperty("solutionPath").GetString()
            ?? throw new ArgumentException("solutionPath is required for extract_interface");

        var preview = false;
        if (args.HasValue && args.Value.TryGetProperty("preview", out var previewEl))
        {
            preview = previewEl.GetBoolean();
        }

        var members = new List<string>();
        if (args.HasValue && args.Value.TryGetProperty("members", out var membersEl))
        {
            foreach (var m in membersEl.EnumerateArray())
            {
                if (m.GetString() is string memberName)
                    members.Add(memberName);
            }
        }

        var result = await _refactoringService.ExtractInterfaceAsync(
            new ExtractInterfaceRequest
            {
                ClassName = className,
                InterfaceName = interfaceName,
                SolutionPath = solutionPath,
                Members = members.Count > 0 ? members : null,
                Preview = preview
            },
            ct);

        return new
        {
            success = result.Success,
            filesModified = result.ModifiedFiles,
            message = result.Error ?? $"Extracted interface {interfaceName} from {className}",
            preview = preview ? result.Preview : null
        };
    }

    /// <summary>
    /// aura_generate - Create new code elements.
    /// Routes to: implement_interface, constructor, property, method.
    /// </summary>
    private async Task<object> GenerateAsync(JsonElement? args, CancellationToken ct)
    {
        var operation = args?.GetProperty("operation").GetString()
            ?? throw new ArgumentException("operation is required");

        return operation switch
        {
            "implement_interface" => await ImplementInterfaceAsync(args, ct),
            "constructor" => await GenerateConstructorAsync(args, ct),
            "property" => await AddPropertyAsync(args, ct),
            "method" => await AddMethodAsync(args, ct),
            _ => throw new ArgumentException($"Unknown generate operation: {operation}")
        };
    }

    /// <summary>
    /// aura_validate - Check code correctness.
    /// Routes to: compilation, tests.
    /// </summary>
    private async Task<object> ValidateAsync(JsonElement? args, CancellationToken ct)
    {
        var operation = args?.GetProperty("operation").GetString()
            ?? throw new ArgumentException("operation is required");

        return operation switch
        {
            "compilation" => await ValidateCompilationAsync(args, ct),
            "tests" => await RunTestsAsync(args, ct),
            _ => throw new ArgumentException($"Unknown validate operation: {operation}")
        };
    }

    /// <summary>
    /// aura_workflow - Manage development workflows.
    /// Routes to: list, get, create.
    /// </summary>
    private async Task<object> WorkflowAsync(JsonElement? args, CancellationToken ct)
    {
        var operation = args?.GetProperty("operation").GetString()
            ?? throw new ArgumentException("operation is required");

        return operation switch
        {
            "list" => await ListStoriesAsync(args, ct),
            "get" => await GetStoryContextAsync(args, ct),
            "create" => await CreateStoryFromIssueAsync(args, ct),
            _ => throw new ArgumentException($"Unknown workflow operation: {operation}")
        };
    }

    /// <summary>
    /// aura_architect - Whole-codebase architectural analysis.
    /// Placeholder for future implementation.
    /// </summary>
    private Task<object> ArchitectAsync(JsonElement? args, CancellationToken ct)
    {
        var operation = args?.GetProperty("operation").GetString()
            ?? throw new ArgumentException("operation is required");

        return Task.FromResult<object>(new
        {
            success = false,
            message = $"aura_architect operation '{operation}' is not yet implemented. Coming in a future release.",
            availableOperations = new[] { "dependencies", "layer_check", "public_api" }
        });
    }

    // =========================================================================
    // Existing Tool Implementations (used by meta-tool routers)
    // =========================================================================

    /// <summary>
    /// Resolves a workspacePath (which may be a worktree) to the main repository path.
    /// This ensures RAG queries use the indexed base repository, not the worktree.
    /// </summary>
    private async Task<string?> ResolveToMainRepositoryAsync(string? workspacePath, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(workspacePath))
            return null;

        var result = await _worktreeService.GetMainRepositoryPathAsync(workspacePath, ct);
        if (result.Success && result.Value is not null)
        {
            if (!result.Value.Equals(workspacePath, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Resolved worktree {WorktreePath} to main repository {MainRepoPath}",
                    workspacePath, result.Value);
            }

            return result.Value;
        }

        // Not a git repo or failed to resolve - return original path
        _logger.LogDebug("Could not resolve {WorkspacePath} to main repository, using as-is", workspacePath);
        return workspacePath;
    }

    private async Task<object> SearchCodeAsync(JsonElement? args, CancellationToken ct)
    {
        var query = args?.GetProperty("query").GetString() ?? "";
        var limit = 10;
        if (args.HasValue && args.Value.TryGetProperty("limit", out var limitEl))
        {
            limit = limitEl.GetInt32();
        }

        // Parse workspacePath and resolve to main repository if it's a worktree
        string? sourcePathPrefix = null;
        if (args.HasValue && args.Value.TryGetProperty("workspacePath", out var workspaceEl))
        {
            var workspacePath = workspaceEl.GetString();
            sourcePathPrefix = await ResolveToMainRepositoryAsync(workspacePath, ct);
        }

        // Parse contentType filter
        string? contentTypeFilter = null;
        if (args.HasValue && args.Value.TryGetProperty("contentType", out var contentTypeEl))
        {
            contentTypeFilter = contentTypeEl.GetString();
        }

        // Map contentType string to RagContentType list
        var contentTypes = contentTypeFilter switch
        {
            "code" => new[] { RagContentType.Code },
            "docs" => new[] { RagContentType.Markdown, RagContentType.PlainText },
            "config" => new[] { RagContentType.PlainText }, // JSON/YAML indexed as PlainText
            _ => null // "all" or unspecified
        };

        var options = new RagQueryOptions
        {
            TopK = limit,
            ContentTypes = contentTypes,
            SourcePathPrefix = sourcePathPrefix
        };

        // Phase 1.2: Check code graph for exact symbol match first
        var exactMatches = await _graphService.FindNodesAsync(query, cancellationToken: ct);
        var exactMatchResults = exactMatches
            .Take(3) // Limit to top 3 exact matches
            .Select(n => new
            {
                content = $"[EXACT MATCH] {n.NodeType}: {n.FullName}",
                filePath = n.FilePath,
                line = n.LineNumber,
                score = 1.0, // Perfect match
                contentType = "Code",
                isExactMatch = true
            })
            .ToList();

        var ragResults = await _ragService.QueryAsync(query, options, ct);
        var semanticResults = ragResults.Select(r => new
        {
            content = r.Text,
            filePath = r.SourcePath,
            line = (int?)null,
            score = r.Score,
            contentType = r.ContentType.ToString(),
            isExactMatch = false
        });

        // Combine: exact matches first, then semantic results (deduplicated)
        var exactFilePaths = exactMatchResults.Select(e => e.filePath).ToHashSet();
        var combinedResults = exactMatchResults
            .Concat(semanticResults.Where(s => !exactFilePaths.Contains(s.filePath)))
            .Take(limit);

        return combinedResults;
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

    private async Task<object> FindByAttributeAsync(JsonElement? args, CancellationToken ct)
    {
        var attributeName = args?.GetProperty("attributeName").GetString() ?? "";
        var solutionPath = args?.GetProperty("solutionPath").GetString() ?? "";
        string? targetKind = null;
        if (args.HasValue && args.Value.TryGetProperty("targetKind", out var kindEl))
        {
            targetKind = kindEl.GetString();
        }

        if (string.IsNullOrEmpty(solutionPath) || !File.Exists(solutionPath))
        {
            return new { error = $"Solution file not found: {solutionPath}" };
        }

        // Normalize attribute name (remove Attribute suffix if present, add if not for matching)
        var normalizedName = attributeName.EndsWith("Attribute", StringComparison.Ordinal)
            ? attributeName[..^9]
            : attributeName;

        try
        {
            var solution = await _roslynService.GetSolutionAsync(solutionPath, ct);
            var results = new List<object>();

            foreach (var project in solution.Projects)
            {
                var compilation = await project.GetCompilationAsync(ct);
                if (compilation is null) continue;

                foreach (var syntaxTree in compilation.SyntaxTrees)
                {
                    var semanticModel = compilation.GetSemanticModel(syntaxTree);
                    var root = await syntaxTree.GetRootAsync(ct);

                    // Find all nodes with attributes
                    var nodesWithAttributes = root.DescendantNodes()
                        .Where(n => n is MemberDeclarationSyntax or ParameterSyntax)
                        .Where(n =>
                        {
                            var attrs = n switch
                            {
                                MethodDeclarationSyntax m => m.AttributeLists,
                                ClassDeclarationSyntax c => c.AttributeLists,
                                PropertyDeclarationSyntax p => p.AttributeLists,
                                FieldDeclarationSyntax f => f.AttributeLists,
                                ParameterSyntax param => param.AttributeLists,
                                _ => default
                            };
                            return attrs.Count > 0;
                        });

                    foreach (var node in nodesWithAttributes)
                    {
                        var attrs = node switch
                        {
                            MethodDeclarationSyntax m => m.AttributeLists,
                            ClassDeclarationSyntax c => c.AttributeLists,
                            PropertyDeclarationSyntax p => p.AttributeLists,
                            FieldDeclarationSyntax f => f.AttributeLists,
                            ParameterSyntax param => param.AttributeLists,
                            _ => default
                        };

                        foreach (var attrList in attrs)
                        {
                            foreach (var attr in attrList.Attributes)
                            {
                                var attrName = attr.Name.ToString();
                                // Match: HttpGet, HttpGetAttribute, [HttpGet], etc.
                                if (attrName.Equals(normalizedName, StringComparison.OrdinalIgnoreCase) ||
                                    attrName.Equals(normalizedName + "Attribute", StringComparison.OrdinalIgnoreCase))
                                {
                                    var nodeKind = node switch
                                    {
                                        MethodDeclarationSyntax => "method",
                                        ClassDeclarationSyntax => "class",
                                        PropertyDeclarationSyntax => "property",
                                        FieldDeclarationSyntax => "field",
                                        ParameterSyntax => "parameter",
                                        _ => "other"
                                    };

                                    // Apply target kind filter
                                    if (targetKind != null && targetKind != "all" &&
                                        !nodeKind.Equals(targetKind, StringComparison.OrdinalIgnoreCase))
                                        continue;

                                    var nodeName = node switch
                                    {
                                        MethodDeclarationSyntax m => m.Identifier.Text,
                                        ClassDeclarationSyntax c => c.Identifier.Text,
                                        PropertyDeclarationSyntax p => p.Identifier.Text,
                                        FieldDeclarationSyntax f => f.Declaration.Variables.FirstOrDefault()?.Identifier.Text ?? "",
                                        ParameterSyntax param => param.Identifier.Text,
                                        _ => ""
                                    };

                                    var location = node.GetLocation();
                                    var lineSpan = location.GetLineSpan();

                                    results.Add(new
                                    {
                                        name = nodeName,
                                        kind = nodeKind,
                                        attribute = attrName,
                                        filePath = syntaxTree.FilePath,
                                        line = lineSpan.StartLinePosition.Line + 1
                                    });

                                    if (results.Count >= 100) break;
                                }
                            }
                            if (results.Count >= 100) break;
                        }
                        if (results.Count >= 100) break;
                    }
                    if (results.Count >= 100) break;
                }
                if (results.Count >= 100) break;
            }

            return new
            {
                attributeName = normalizedName,
                totalResults = results.Count,
                wasTruncated = results.Count >= 100,
                results
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find by attribute {Attribute}", attributeName);
            return new { error = $"Failed to find by attribute: {ex.Message}" };
        }
    }

    private async Task<object> FindExtensionMethodsAsync(JsonElement? args, CancellationToken ct)
    {
        var extendedTypeName = args?.GetProperty("extendedTypeName").GetString() ?? "";
        var solutionPath = args?.GetProperty("solutionPath").GetString() ?? "";

        if (string.IsNullOrEmpty(solutionPath) || !File.Exists(solutionPath))
        {
            return new { error = $"Solution file not found: {solutionPath}" };
        }

        try
        {
            var solution = await _roslynService.GetSolutionAsync(solutionPath, ct);
            var results = new List<object>();

            foreach (var project in solution.Projects)
            {
                var compilation = await project.GetCompilationAsync(ct);
                if (compilation is null) continue;

                foreach (var syntaxTree in compilation.SyntaxTrees)
                {
                    var semanticModel = compilation.GetSemanticModel(syntaxTree);
                    var root = await syntaxTree.GetRootAsync(ct);

                    // Find all static classes (extension methods must be in static classes)
                    var staticClasses = root.DescendantNodes()
                        .OfType<ClassDeclarationSyntax>()
                        .Where(c => c.Modifiers.Any(m => m.Text == "static"));

                    foreach (var staticClass in staticClasses)
                    {
                        var extensionMethods = staticClass.Members
                            .OfType<MethodDeclarationSyntax>()
                            .Where(m => m.Modifiers.Any(mod => mod.Text == "static") &&
                                       m.ParameterList.Parameters.Count > 0 &&
                                       m.ParameterList.Parameters[0].Modifiers.Any(mod => mod.Text == "this"));

                        foreach (var method in extensionMethods)
                        {
                            var firstParam = method.ParameterList.Parameters[0];
                            var paramTypeName = firstParam.Type?.ToString() ?? "";

                            // Check if this extends the requested type
                            if (paramTypeName.Contains(extendedTypeName, StringComparison.OrdinalIgnoreCase))
                            {
                                var location = method.GetLocation();
                                var lineSpan = location.GetLineSpan();

                                results.Add(new
                                {
                                    methodName = method.Identifier.Text,
                                    containingClass = staticClass.Identifier.Text,
                                    extendedType = paramTypeName,
                                    returnType = method.ReturnType.ToString(),
                                    parameters = method.ParameterList.Parameters.Skip(1)
                                        .Select(p => $"{p.Type} {p.Identifier}").ToList(),
                                    filePath = syntaxTree.FilePath,
                                    line = lineSpan.StartLinePosition.Line + 1
                                });

                                if (results.Count >= 100) break;
                            }
                        }
                        if (results.Count >= 100) break;
                    }
                    if (results.Count >= 100) break;
                }
                if (results.Count >= 100) break;
            }

            return new
            {
                extendedTypeName,
                totalResults = results.Count,
                wasTruncated = results.Count >= 100,
                results
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find extension methods for {Type}", extendedTypeName);
            return new { error = $"Failed to find extension methods: {ex.Message}" };
        }
    }

    private async Task<object> FindByReturnTypeAsync(JsonElement? args, CancellationToken ct)
    {
        var returnTypeName = args?.GetProperty("returnTypeName").GetString() ?? "";
        var solutionPath = args?.GetProperty("solutionPath").GetString() ?? "";

        if (string.IsNullOrEmpty(solutionPath) || !File.Exists(solutionPath))
        {
            return new { error = $"Solution file not found: {solutionPath}" };
        }

        try
        {
            var solution = await _roslynService.GetSolutionAsync(solutionPath, ct);
            var results = new List<object>();

            foreach (var project in solution.Projects)
            {
                var compilation = await project.GetCompilationAsync(ct);
                if (compilation is null) continue;

                foreach (var syntaxTree in compilation.SyntaxTrees)
                {
                    var root = await syntaxTree.GetRootAsync(ct);

                    // Find all methods
                    var methods = root.DescendantNodes()
                        .OfType<MethodDeclarationSyntax>();

                    foreach (var method in methods)
                    {
                        var methodReturnType = method.ReturnType.ToString();

                        // Check if return type matches (including Task<T> unwrapping)
                        if (methodReturnType.Equals(returnTypeName, StringComparison.OrdinalIgnoreCase) ||
                            methodReturnType.Contains(returnTypeName, StringComparison.OrdinalIgnoreCase))
                        {
                            var containingType = method.Ancestors()
                                .OfType<TypeDeclarationSyntax>()
                                .FirstOrDefault()?.Identifier.Text ?? "";

                            var location = method.GetLocation();
                            var lineSpan = location.GetLineSpan();

                            results.Add(new
                            {
                                methodName = method.Identifier.Text,
                                containingType,
                                returnType = methodReturnType,
                                parameters = method.ParameterList.Parameters
                                    .Select(p => $"{p.Type} {p.Identifier}").ToList(),
                                filePath = syntaxTree.FilePath,
                                line = lineSpan.StartLinePosition.Line + 1
                            });

                            if (results.Count >= 100) break;
                        }
                    }
                    if (results.Count >= 100) break;
                }
                if (results.Count >= 100) break;
            }

            return new
            {
                returnTypeName,
                totalResults = results.Count,
                wasTruncated = results.Count >= 100,
                results
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find by return type {Type}", returnTypeName);
            return new { error = $"Failed to find by return type: {ex.Message}" };
        }
    }

    // =========================================================================
    // Refactoring Tool Handlers (Phase 5)
    // =========================================================================

    private async Task<object> RenameSymbolAsync(JsonElement? args, CancellationToken ct)
    {
        var symbolName = args?.GetProperty("symbolName").GetString() ?? "";
        var newName = args?.GetProperty("newName").GetString() ?? "";
        var solutionPath = args?.GetProperty("solutionPath").GetString() ?? "";

        string? containingType = null;
        string? filePath = null;
        var preview = false;

        if (args.HasValue)
        {
            if (args.Value.TryGetProperty("containingType", out var ctEl))
                containingType = ctEl.GetString();
            if (args.Value.TryGetProperty("filePath", out var fpEl))
                filePath = fpEl.GetString();
            if (args.Value.TryGetProperty("preview", out var prevEl))
                preview = prevEl.GetBoolean();
        }

        var result = await _refactoringService.RenameSymbolAsync(new RenameSymbolRequest
        {
            SymbolName = symbolName,
            NewName = newName,
            SolutionPath = solutionPath,
            ContainingType = containingType,
            FilePath = filePath,
            Preview = preview
        }, ct);

        return new
        {
            success = result.Success,
            message = result.Message,
            modifiedFiles = result.ModifiedFiles,
            preview = result.Preview?.Select(p => new { p.FilePath, p.NewContent })
        };
    }

    private async Task<object> ChangeSignatureAsync(JsonElement? args, CancellationToken ct)
    {
        var methodName = args?.GetProperty("methodName").GetString() ?? "";
        var containingType = args?.GetProperty("containingType").GetString() ?? "";
        var solutionPath = args?.GetProperty("solutionPath").GetString() ?? "";

        List<RefactoringParameterInfo>? addParams = null;
        List<string>? removeParams = null;
        var preview = false;

        if (args.HasValue)
        {
            if (args.Value.TryGetProperty("addParameters", out var addEl) && addEl.ValueKind == JsonValueKind.Array)
            {
                addParams = addEl.EnumerateArray().Select(p => new RefactoringParameterInfo(
                    p.GetProperty("name").GetString() ?? "",
                    p.GetProperty("type").GetString() ?? "",
                    p.TryGetProperty("defaultValue", out var dv) ? dv.GetString() : null
                )).ToList();
            }

            if (args.Value.TryGetProperty("removeParameters", out var remEl) && remEl.ValueKind == JsonValueKind.Array)
            {
                removeParams = remEl.EnumerateArray().Select(p => p.GetString() ?? "").ToList();
            }

            if (args.Value.TryGetProperty("preview", out var prevEl))
                preview = prevEl.GetBoolean();
        }

        var result = await _refactoringService.ChangeMethodSignatureAsync(new ChangeSignatureRequest
        {
            MethodName = methodName,
            ContainingType = containingType,
            SolutionPath = solutionPath,
            AddParameters = addParams,
            RemoveParameters = removeParams,
            Preview = preview
        }, ct);

        return new
        {
            success = result.Success,
            message = result.Message,
            modifiedFiles = result.ModifiedFiles,
            preview = result.Preview?.Select(p => new { p.FilePath, p.NewContent })
        };
    }

    private async Task<object> ImplementInterfaceAsync(JsonElement? args, CancellationToken ct)
    {
        var className = args?.GetProperty("className").GetString() ?? "";
        var interfaceName = args?.GetProperty("interfaceName").GetString() ?? "";
        var solutionPath = args?.GetProperty("solutionPath").GetString() ?? "";

        var explicitImpl = false;
        var preview = false;

        if (args.HasValue)
        {
            if (args.Value.TryGetProperty("explicitImplementation", out var expEl))
                explicitImpl = expEl.GetBoolean();
            if (args.Value.TryGetProperty("preview", out var prevEl))
                preview = prevEl.GetBoolean();
        }

        var result = await _refactoringService.ImplementInterfaceAsync(new ImplementInterfaceRequest
        {
            ClassName = className,
            InterfaceName = interfaceName,
            SolutionPath = solutionPath,
            ExplicitImplementation = explicitImpl,
            Preview = preview
        }, ct);

        return new
        {
            success = result.Success,
            message = result.Message,
            modifiedFiles = result.ModifiedFiles,
            preview = result.Preview?.Select(p => new { p.FilePath, p.NewContent })
        };
    }

    private async Task<object> GenerateConstructorAsync(JsonElement? args, CancellationToken ct)
    {
        var className = args?.GetProperty("className").GetString() ?? "";
        var solutionPath = args?.GetProperty("solutionPath").GetString() ?? "";

        List<string>? members = null;
        var preview = false;

        if (args.HasValue)
        {
            if (args.Value.TryGetProperty("members", out var memEl) && memEl.ValueKind == JsonValueKind.Array)
            {
                members = memEl.EnumerateArray().Select(m => m.GetString() ?? "").ToList();
            }

            if (args.Value.TryGetProperty("preview", out var prevEl))
                preview = prevEl.GetBoolean();
        }

        var result = await _refactoringService.GenerateConstructorAsync(new GenerateConstructorRequest
        {
            ClassName = className,
            SolutionPath = solutionPath,
            Members = members,
            Preview = preview
        }, ct);

        return new
        {
            success = result.Success,
            message = result.Message,
            modifiedFiles = result.ModifiedFiles,
            preview = result.Preview?.Select(p => new { p.FilePath, p.NewContent })
        };
    }

    private async Task<object> ExtractInterfaceAsync(JsonElement? args, CancellationToken ct)
    {
        var className = args?.GetProperty("className").GetString() ?? "";
        var interfaceName = args?.GetProperty("interfaceName").GetString() ?? "";
        var solutionPath = args?.GetProperty("solutionPath").GetString() ?? "";

        List<string>? members = null;
        var preview = false;

        if (args.HasValue)
        {
            if (args.Value.TryGetProperty("members", out var memEl) && memEl.ValueKind == JsonValueKind.Array)
            {
                members = memEl.EnumerateArray().Select(m => m.GetString() ?? "").ToList();
            }

            if (args.Value.TryGetProperty("preview", out var prevEl))
                preview = prevEl.GetBoolean();
        }

        var result = await _refactoringService.ExtractInterfaceAsync(new ExtractInterfaceRequest
        {
            ClassName = className,
            InterfaceName = interfaceName,
            SolutionPath = solutionPath,
            Members = members,
            Preview = preview
        }, ct);

        return new
        {
            success = result.Success,
            message = result.Message,
            modifiedFiles = result.ModifiedFiles,
            createdFiles = result.CreatedFiles,
            preview = result.Preview?.Select(p => new { p.FilePath, p.NewContent })
        };
    }

    private async Task<object> SafeDeleteAsync(JsonElement? args, CancellationToken ct)
    {
        var symbolName = args?.GetProperty("symbolName").GetString() ?? "";
        var solutionPath = args?.GetProperty("solutionPath").GetString() ?? "";

        string? containingType = null;
        var preview = false;

        if (args.HasValue)
        {
            if (args.Value.TryGetProperty("containingType", out var ctEl))
                containingType = ctEl.GetString();
            if (args.Value.TryGetProperty("preview", out var prevEl))
                preview = prevEl.GetBoolean();
        }

        var result = await _refactoringService.SafeDeleteAsync(new SafeDeleteRequest
        {
            SymbolName = symbolName,
            SolutionPath = solutionPath,
            ContainingType = containingType,
            Preview = preview
        }, ct);

        if (!result.Success && result.RemainingReferences?.Count > 0)
        {
            return new
            {
                success = false,
                message = result.Message,
                remainingReferences = result.RemainingReferences.Select(r => new
                {
                    r.FilePath,
                    r.Line,
                    r.CodeSnippet
                })
            };
        }

        return new
        {
            success = result.Success,
            message = result.Message,
            modifiedFiles = result.ModifiedFiles
        };
    }

    private async Task<object> AddPropertyAsync(JsonElement? args, CancellationToken ct)
    {
        var className = args?.GetProperty("className").GetString() ?? "";
        var propertyName = args?.GetProperty("propertyName").GetString() ?? "";
        var propertyType = args?.GetProperty("propertyType").GetString() ?? "";
        var solutionPath = args?.GetProperty("solutionPath").GetString() ?? "";

        var hasGetter = true;
        var hasSetter = true;
        string? initialValue = null;
        var preview = false;

        if (args.HasValue)
        {
            if (args.Value.TryGetProperty("hasGetter", out var gEl))
                hasGetter = gEl.GetBoolean();
            if (args.Value.TryGetProperty("hasSetter", out var sEl))
                hasSetter = sEl.GetBoolean();
            if (args.Value.TryGetProperty("initialValue", out var ivEl))
                initialValue = ivEl.GetString();
            if (args.Value.TryGetProperty("preview", out var prevEl))
                preview = prevEl.GetBoolean();
        }

        var result = await _refactoringService.AddPropertyAsync(new AddPropertyRequest
        {
            ClassName = className,
            PropertyName = propertyName,
            PropertyType = propertyType,
            SolutionPath = solutionPath,
            HasGetter = hasGetter,
            HasSetter = hasSetter,
            InitialValue = initialValue,
            Preview = preview
        }, ct);

        return new
        {
            success = result.Success,
            message = result.Message,
            modifiedFiles = result.ModifiedFiles,
            preview = result.Preview?.Select(p => new { p.FilePath, p.NewContent })
        };
    }

    private async Task<object> AddMethodAsync(JsonElement? args, CancellationToken ct)
    {
        var className = args?.GetProperty("className").GetString() ?? "";
        var methodName = args?.GetProperty("methodName").GetString() ?? "";
        var returnType = args?.GetProperty("returnType").GetString() ?? "";
        var solutionPath = args?.GetProperty("solutionPath").GetString() ?? "";

        List<RefactoringParameterInfo>? parameters = null;
        var accessModifier = "public";
        var isAsync = false;
        string? body = null;
        var preview = false;

        if (args.HasValue)
        {
            if (args.Value.TryGetProperty("parameters", out var paramsEl) && paramsEl.ValueKind == JsonValueKind.Array)
            {
                parameters = paramsEl.EnumerateArray().Select(p => new RefactoringParameterInfo(
                    p.GetProperty("name").GetString() ?? "",
                    p.GetProperty("type").GetString() ?? "",
                    p.TryGetProperty("defaultValue", out var dv) ? dv.GetString() : null
                )).ToList();
            }

            if (args.Value.TryGetProperty("accessModifier", out var amEl))
                accessModifier = amEl.GetString() ?? "public";
            if (args.Value.TryGetProperty("isAsync", out var asyncEl))
                isAsync = asyncEl.GetBoolean();
            if (args.Value.TryGetProperty("body", out var bodyEl))
                body = bodyEl.GetString();
            if (args.Value.TryGetProperty("preview", out var prevEl))
                preview = prevEl.GetBoolean();
        }

        var result = await _refactoringService.AddMethodAsync(new AddMethodRequest
        {
            ClassName = className,
            MethodName = methodName,
            ReturnType = returnType,
            SolutionPath = solutionPath,
            Parameters = parameters,
            AccessModifier = accessModifier,
            IsAsync = isAsync,
            Body = body,
            Preview = preview
        }, ct);

        return new
        {
            success = result.Success,
            message = result.Message,
            modifiedFiles = result.ModifiedFiles,
            preview = result.Preview?.Select(p => new { p.FilePath, p.NewContent })
        };
    }

    // =========================================================================
    // Python Refactoring Tool Handlers (Phase 6)
    // =========================================================================

    private async Task<object> PythonRenameAsync(JsonElement? args, CancellationToken ct)
    {
        var projectPath = args?.GetProperty("projectPath").GetString() ?? "";
        var filePath = args?.GetProperty("filePath").GetString() ?? "";
        var offset = args?.GetProperty("offset").GetInt32() ?? 0;
        var newName = args?.GetProperty("newName").GetString() ?? "";
        var preview = args.HasValue && args.Value.TryGetProperty("preview", out var prevEl) && prevEl.GetBoolean();

        var result = await _pythonRefactoringService.RenameSymbolAsync(new PythonRenameRequest
        {
            ProjectPath = projectPath,
            FilePath = filePath,
            Offset = offset,
            NewName = newName,
            Preview = preview
        }, ct);

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
        var projectPath = args?.GetProperty("projectPath").GetString() ?? "";
        var filePath = args?.GetProperty("filePath").GetString() ?? "";
        var startOffset = args?.GetProperty("startOffset").GetInt32() ?? 0;
        var endOffset = args?.GetProperty("endOffset").GetInt32() ?? 0;
        var newName = args?.GetProperty("newName").GetString() ?? "";
        var preview = args.HasValue && args.Value.TryGetProperty("preview", out var prevEl) && prevEl.GetBoolean();

        var result = await _pythonRefactoringService.ExtractMethodAsync(new PythonExtractMethodRequest
        {
            ProjectPath = projectPath,
            FilePath = filePath,
            StartOffset = startOffset,
            EndOffset = endOffset,
            NewName = newName,
            Preview = preview
        }, ct);

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
        var projectPath = args?.GetProperty("projectPath").GetString() ?? "";
        var filePath = args?.GetProperty("filePath").GetString() ?? "";
        var startOffset = args?.GetProperty("startOffset").GetInt32() ?? 0;
        var endOffset = args?.GetProperty("endOffset").GetInt32() ?? 0;
        var newName = args?.GetProperty("newName").GetString() ?? "";
        var preview = args.HasValue && args.Value.TryGetProperty("preview", out var prevEl) && prevEl.GetBoolean();

        var result = await _pythonRefactoringService.ExtractVariableAsync(new PythonExtractVariableRequest
        {
            ProjectPath = projectPath,
            FilePath = filePath,
            StartOffset = startOffset,
            EndOffset = endOffset,
            NewName = newName,
            Preview = preview
        }, ct);

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
        var projectPath = args?.GetProperty("projectPath").GetString() ?? "";
        var filePath = args?.GetProperty("filePath").GetString() ?? "";
        var offset = args?.GetProperty("offset").GetInt32() ?? 0;

        var result = await _pythonRefactoringService.FindReferencesAsync(new PythonFindReferencesRequest
        {
            ProjectPath = projectPath,
            FilePath = filePath,
            Offset = offset
        }, ct);

        return new
        {
            success = result.Success,
            error = result.Error,
            count = result.Count,
            references = result.References.Select(r => new
            {
                filePath = r.FilePath,
                offset = r.Offset,
                isDefinition = r.IsDefinition,
                isWrite = r.IsWrite
            })
        };
    }

    private async Task<object> PythonFindDefinitionAsync(JsonElement? args, CancellationToken ct)
    {
        var projectPath = args?.GetProperty("projectPath").GetString() ?? "";
        var filePath = args?.GetProperty("filePath").GetString() ?? "";
        var offset = args?.GetProperty("offset").GetInt32() ?? 0;

        var result = await _pythonRefactoringService.FindDefinitionAsync(new PythonFindDefinitionRequest
        {
            ProjectPath = projectPath,
            FilePath = filePath,
            Offset = offset
        }, ct);

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
