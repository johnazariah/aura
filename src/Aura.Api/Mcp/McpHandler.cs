// <copyright file="McpHandler.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Mcp;

using System.Text.Json;
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
    private readonly ITestGenerationService _testGenerationService;
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
        ITestGenerationService testGenerationService,
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
        _testGenerationService = testGenerationService;
        _worktreeService = worktreeService;
        _logger = logger;

        // Phase 7: Consolidated meta-tools (28 tools → 11 tools)
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
            ["aura_workspace"] = WorkspaceAsync,
            ["aura_pattern"] = PatternAsync,
            ["aura_edit"] = EditAsync,
        };
    }

    /// <summary>
    /// Gets the list of registered MCP tool names.
    /// </summary>
    public IReadOnlyList<string> GetToolNames() => _tools.Keys.ToList();

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
                        solutionPath = new { type = "string", description = "Path to solution file (.sln) - enables Roslyn fallback when code graph is empty" },
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
                Description = "Transform existing code: rename symbols, change signatures, extract methods/variables/interfaces, safe delete, move type to file. Auto-detects language from filePath. (Write)",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        operation = new
                        {
                            type = "string",
                            description = "Refactoring operation type",
                            @enum = new[] { "rename", "change_signature", "extract_interface", "extract_method", "extract_variable", "safe_delete", "move_type_to_file" }
                        },
                        symbolName = new { type = "string", description = "Symbol to refactor" },
                        newName = new { type = "string", description = "New name for rename, extract_method, extract_variable, extract_interface" },
                        containingType = new { type = "string", description = "Type containing the symbol (for C# disambiguation)" },
                        solutionPath = new { type = "string", description = "Path to solution file (.sln) - for C# operations" },
                        filePath = new { type = "string", description = "Path to file containing the code" },
                        targetDirectory = new { type = "string", description = "Target directory for move_type_to_file (default: same as source)" },
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
                        analyze = new { type = "boolean", description = "If true (default), return blast radius analysis without executing. Set to false to execute immediately." },
                        preview = new { type = "boolean", description = "If true, return changes without applying (default: false)" },
                        validate = new { type = "boolean", description = "If true, run build after refactoring and check for residuals (default: false)" }
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
                Description = "Generate new code: create types with proper namespace, implement interfaces, generate constructors, add properties/methods, generate tests. (Write)",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        operation = new
                        {
                            type = "string",
                            description = "Generation operation type",
                            @enum = new[] { "implement_interface", "constructor", "property", "method", "create_type", "tests" }
                        },
                        className = new { type = "string", description = "Target class name (for existing class operations)" },
                        solutionPath = new { type = "string", description = "Path to solution file (.sln)" },
                        typeName = new { type = "string", description = "Name of type to create (for create_type)" },
                        typeKind = new { type = "string", description = "Kind of type: class, interface, record, struct (for create_type)", @enum = new[] { "class", "interface", "record", "struct" } },
                        targetDirectory = new { type = "string", description = "Target directory for new type file (for create_type)" },

                        // Test generation parameters
                        target = new { type = "string", description = "Target for test generation: class name, method (Class.Method), or namespace (for tests)" },
                        count = new { type = "integer", description = "Explicit test count. If omitted, generates comprehensive tests (for tests)" },
                        maxTests = new { type = "integer", description = "Maximum tests to generate, default: 20 (for tests)" },
                        focus = new { type = "string", description = "Focus area for tests", @enum = new[] { "all", "happy_path", "edge_cases", "error_handling" } },
                        testFramework = new { type = "string", description = "Override framework detection: xunit, nunit, mstest (for tests)" },
                        outputDirectory = new { type = "string", description = "Output directory for test file - relative path under test project (e.g., 'Services/Testing') or absolute path (for tests)" },
                        analyzeOnly = new { type = "boolean", description = "If true, return analysis without generating code (for tests)" },
                        validateCompilation = new { type = "boolean", description = "If true, validate generated code compiles before returning - adds latency (for tests)" },
                        baseClass = new { type = "string", description = "Base class to inherit from (for create_type)" },
                        implements = new { type = "array", items = new { type = "string" }, description = "Interfaces to implement (for create_type)" },
                        isSealed = new { type = "boolean", description = "Whether class is sealed (for create_type)" },
                        isAbstract = new { type = "boolean", description = "Whether class is abstract (for create_type)" },
                        isStatic = new { type = "boolean", description = "Whether type or method is static" },
                        documentationSummary = new { type = "string", description = "XML doc summary for the type (for create_type)" },
                        primaryConstructorParameters = new
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
                            description = "Primary constructor parameters (C# 12 for classes, C# 9 for records). For records, these become positional parameters."
                        },
                        typeParameters = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    name = new { type = "string", description = "Type parameter name (e.g., 'T', 'TEntity')" },
                                    constraints = new { type = "array", items = new { type = "string" }, description = "Constraints (e.g., 'class', 'new()', 'IEntity')" }
                                }
                            },
                            description = "Generic type parameters for types and methods (e.g., Repository<TEntity> where TEntity : class, IEntity)"
                        },
                        interfaceName = new { type = "string", description = "Interface to implement (for implement_interface)" },
                        explicitImplementation = new { type = "boolean", description = "Use explicit interface implementation (default: false)" },
                        members = new
                        {
                            type = "array",
                            items = new { type = "string" },
                            description = "Field/property names for constructor initialization"
                        },
                        propertyName = new { type = "string", description = "Name for new property or field" },
                        propertyType = new { type = "string", description = "Type for new property or field" },
                        hasGetter = new { type = "boolean", description = "Include getter (default: true, for properties only)" },
                        hasSetter = new { type = "boolean", description = "Include setter (default: true, for properties only)" },
                        hasInit = new { type = "boolean", description = "Use init accessor instead of set (C# 9+). Mutually exclusive with hasSetter." },
                        isRequired = new { type = "boolean", description = "Add required modifier (C# 11+)" },
                        initialValue = new { type = "string", description = "Initial value for property or field" },
                        isField = new { type = "boolean", description = "If true, generate a field instead of a property (default: false)" },
                        isReadonly = new { type = "boolean", description = "If true, add readonly modifier (for fields)" },
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
                        accessModifier = new { type = "string", description = "Access modifier for properties, fields, and methods (e.g., 'public', 'private', 'private readonly'). Default: 'public'" },
                        methodModifier = new { type = "string", description = "Method modifier: virtual, override, abstract, sealed, or new", @enum = new[] { "virtual", "override", "abstract", "sealed", "new" } },
                        isAsync = new { type = "boolean", description = "Whether method is async" },
                        body = new { type = "string", description = "Optional method body code" },
                        testAttribute = new { type = "string", description = "Test attribute to add: Fact (xunit), Test (nunit), TestMethod (mstest). Auto-detects if omitted for test classes." },
                        preview = new { type = "boolean", description = "If true, return changes without applying (default: false)" }
                    },
                    required = new[] { "operation", "solutionPath" }
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
                Description = "Manage Aura development workflows/stories: list, get details, get by worktree path, create from GitHub issues, complete with squash merge. Use get_by_path to auto-discover current story context. Use complete to finalize: squash commits, push, create draft PR. Pattern content is auto-included when story has a bound pattern. (CRUD)",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        operation = new
                        {
                            type = "string",
                            description = "Workflow operation type",
                            @enum = new[] { "list", "get", "get_by_path", "create", "enrich", "update_step", "complete" }
                        },
                        storyId = new { type = "string", description = "Story ID (GUID) - for get, enrich operations" },
                        workspacePath = new { type = "string", description = "Workspace/worktree path - for get_by_path to auto-discover current story" },
                        issueUrl = new { type = "string", description = "GitHub issue URL - for create operation" },
                        repositoryPath = new { type = "string", description = "Local repository path for worktree creation" },
                        pattern = new { type = "string", description = "Pattern name to apply - for enrich operation. Binds pattern to story and loads content." },
                        language = new { type = "string", description = "Language for pattern overlay (e.g., 'csharp', 'python') - for enrich operation. Stored with story." },
                        steps = new
                        {
                            type = "array",
                            description = "Steps to add - for enrich operation (optional if pattern is provided)",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    name = new { type = "string", description = "Step name" },
                                    capability = new { type = "string", description = "Required capability/tool (e.g., 'aura_refactor', 'run_in_terminal')" },
                                    description = new { type = "string", description = "Step description with phase prefix like '[Analysis] Examine code structure'" },
                                    input = new { type = "object", description = "Tool arguments as JSON object" }
                                },
                                required = new[] { "name", "capability" }
                            }
                        },
                        stepId = new { type = "string", description = "Step ID (GUID) - for update_step operation" },
                        status = new
                        {
                            type = "string",
                            description = "New step status - for update_step operation",
                            @enum = new[] { "completed", "failed", "skipped", "pending" }
                        },
                        output = new { type = "string", description = "Step output/result - for update_step operation" },
                        error = new { type = "string", description = "Error message - for update_step with status=failed" },
                        skipReason = new { type = "string", description = "Reason for skipping - for update_step with status=skipped" }
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

            // =================================================================
            // aura_workspace - Workspace and worktree management
            // =================================================================
            new McpToolDefinition
            {
                Name = "aura_workspace",
                Description = "Manage workspace state: detect git worktrees, invalidate cached workspaces, check status. (Read/Write)",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        operation = new
                        {
                            type = "string",
                            description = "Workspace operation type",
                            @enum = new[] { "detect_worktree", "invalidate_cache", "status" }
                        },
                        path = new { type = "string", description = "Path to workspace, solution, or worktree" }
                    },
                    required = new[] { "operation", "path" }
                }
            },

            // =================================================================
            // aura_pattern - Load operational patterns for complex tasks
            // =================================================================
            new McpToolDefinition
            {
                Name = "aura_pattern",
                Description = "Load operational patterns (step-by-step playbooks) for complex multi-step tasks. Patterns are dynamically discovered from the patterns/ folder. Supports language overlays for language-specific guidance. (Read)",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        operation = new
                        {
                            type = "string",
                            description = "Pattern operation: 'list' to see available patterns, 'get' to load a specific pattern",
                            @enum = new[] { "list", "get" }
                        },
                        name = new { type = "string", description = "Pattern name (without .md extension), e.g., 'comprehensive-rename'" },
                        language = new { type = "string", description = "Language for overlay (e.g., 'csharp', 'python', 'typescript'). If specified, merges language-specific guidance with base pattern." }
                    },
                    required = new[] { "operation" }
                }
            },

            // =================================================================
            // aura_edit - Surgical text editing (line-based)
            // =================================================================
            new McpToolDefinition
            {
                Name = "aura_edit",
                Description = "Surgical text editing: insert, replace, or delete lines in any file. Use for simple edits where AST manipulation is overkill. Works with any file type. Always normalizes to LF line endings. (Write)",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        operation = new
                        {
                            type = "string",
                            description = "Edit operation type",
                            @enum = new[] { "insert_lines", "replace_lines", "delete_lines", "append", "prepend" }
                        },
                        filePath = new { type = "string", description = "Absolute path to the file to edit" },
                        line = new { type = "integer", description = "Line number (1-based) for insert operations. For insert_lines: inserts AFTER this line. Use 0 to insert at the beginning." },
                        startLine = new { type = "integer", description = "Start line number (1-based, inclusive) for replace_lines and delete_lines" },
                        endLine = new { type = "integer", description = "End line number (1-based, inclusive) for replace_lines and delete_lines" },
                        content = new { type = "string", description = "Content to insert or replace with. Can be multi-line (use \\n for newlines)." },
                        preview = new { type = "boolean", description = "If true, return the result without writing to disk (default: false)" }
                    },
                    required = new[] { "operation", "filePath" }
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
            "references" => await FindReferencesAsync(args, ct),
            "definition" => await FindDefinitionAsync(args, ct),
            _ => throw new ArgumentException($"Unknown navigate operation: {operation}")
        };
    }

    /// <summary>
    /// Find references - auto-detects language from filePath.
    /// </summary>
    private async Task<object> FindReferencesAsync(JsonElement? args, CancellationToken ct)
    {
        // Check if filePath is provided and ends with .py
        if (args.HasValue && args.Value.TryGetProperty("filePath", out var filePathEl))
        {
            var filePath = filePathEl.GetString() ?? "";
            if (filePath.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
            {
                return await PythonFindReferencesAsync(args, ct);
            }
        }

        // For C#, use usages (references is an alias for usages)
        return await FindUsagesAsync(args, ct);
    }

    /// <summary>
    /// Find definition - auto-detects language from filePath.
    /// </summary>
    private async Task<object> FindDefinitionAsync(JsonElement? args, CancellationToken ct)
    {
        // Check if this is a Python request (has filePath ending in .py)
        if (args.HasValue && args.Value.TryGetProperty("filePath", out var filePathEl))
        {
            var filePath = filePathEl.GetString() ?? "";
            if (filePath.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
            {
                // Validate required Python parameters
                if (!args.Value.TryGetProperty("projectPath", out _))
                {
                    return new { error = "projectPath is required for Python definition lookup" };
                }
                if (!args.Value.TryGetProperty("offset", out _))
                {
                    return new { error = "offset is required for Python definition lookup" };
                }
                return await PythonFindDefinitionAsync(args, ct);
            }
        }

        // For C#, find definition in code graph
        return await FindCSharpDefinitionAsync(args, ct);
    }

    /// <summary>
    /// Find C# symbol definition using Roslyn and code graph.
    /// </summary>
    private async Task<object> FindCSharpDefinitionAsync(JsonElement? args, CancellationToken ct)
    {
        string? symbolName = null;
        string? solutionPath = null;
        string? containingType = null;

        if (args.HasValue)
        {
            if (args.Value.TryGetProperty("symbolName", out var symEl))
                symbolName = symEl.GetString();
            if (args.Value.TryGetProperty("solutionPath", out var solEl))
                solutionPath = solEl.GetString();
            if (args.Value.TryGetProperty("containingType", out var typeEl))
                containingType = typeEl.GetString();
        }

        if (string.IsNullOrEmpty(symbolName))
        {
            return new { error = "symbolName is required for C# definition lookup" };
        }

        // First try code graph
        var worktreeInfo = DetectWorktreeFromArgs(args);
        var results = await _graphService.FindNodesAsync(symbolName, cancellationToken: ct);

        if (results.Count > 0)
        {
            // Filter by containing type if specified
            var filtered = containingType is not null
                ? results.Where(n => n.FullName?.Contains(containingType) == true).ToList()
                : results;

            if (filtered.Count > 0)
            {
                var node = filtered.First();
                return new
                {
                    found = true,
                    name = node.Name,
                    fullName = node.FullName,
                    kind = node.NodeType.ToString(),
                    filePath = TranslatePathIfWorktree(node.FilePath, worktreeInfo),
                    line = node.LineNumber,
                    message = $"Found {node.NodeType} {node.Name} at {node.FilePath}:{node.LineNumber}"
                };
            }
        }

        // If we have a solution, try Roslyn
        if (!string.IsNullOrEmpty(solutionPath) && File.Exists(solutionPath))
        {
            try
            {
                var solution = await _roslynService.GetSolutionAsync(solutionPath, ct);

                foreach (var project in solution.Projects)
                {
                    var compilation = await project.GetCompilationAsync(ct);
                    if (compilation is null) continue;

                    // Search all types
                    foreach (var typeSymbol in GetAllTypes(compilation))
                    {
                        // Check if this is the symbol we're looking for
                        if (typeSymbol.Name == symbolName)
                        {
                            var location = typeSymbol.Locations.FirstOrDefault(l => l.IsInSource);
                            if (location is not null)
                            {
                                var lineSpan = location.GetLineSpan();
                                return new
                                {
                                    found = true,
                                    name = typeSymbol.Name,
                                    fullName = typeSymbol.ToDisplayString(),
                                    kind = typeSymbol.TypeKind.ToString(),
                                    filePath = TranslatePathIfWorktree(lineSpan.Path, worktreeInfo),
                                    line = lineSpan.StartLinePosition.Line + 1,
                                    message = $"Found {typeSymbol.TypeKind} {typeSymbol.Name}"
                                };
                            }
                        }

                        // Check members
                        var member = typeSymbol.GetMembers(symbolName).FirstOrDefault();
                        if (member is not null && (containingType is null || typeSymbol.Name == containingType))
                        {
                            var location = member.Locations.FirstOrDefault(l => l.IsInSource);
                            if (location is not null)
                            {
                                var lineSpan = location.GetLineSpan();
                                return new
                                {
                                    found = true,
                                    name = member.Name,
                                    fullName = member.ToDisplayString(),
                                    kind = member.Kind.ToString(),
                                    filePath = TranslatePathIfWorktree(lineSpan.Path, worktreeInfo),
                                    line = lineSpan.StartLinePosition.Line + 1,
                                    message = $"Found {member.Kind} {member.Name} in {typeSymbol.Name}"
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to find definition via Roslyn for {Symbol}", symbolName);
            }
        }

        return new
        {
            found = false,
            message = $"Symbol '{symbolName}' not found. Try specifying solutionPath for Roslyn-based lookup, or ensure the code graph is indexed."
        };
    }

    private static IEnumerable<INamedTypeSymbol> GetAllTypes(Compilation compilation)
    {
        var stack = new Stack<INamespaceSymbol>();
        stack.Push(compilation.GlobalNamespace);

        while (stack.Count > 0)
        {
            var ns = stack.Pop();
            foreach (var member in ns.GetMembers())
            {
                if (member is INamespaceSymbol childNs)
                {
                    stack.Push(childNs);
                }
                else if (member is INamedTypeSymbol type)
                {
                    yield return type;
                }
            }
        }
    }

    // Navigation helpers - adapt from old parameter names to new unified schema
    private async Task<object> FindImplementationsFromNavigate(JsonElement? args, CancellationToken ct)
    {
        var typeName = args?.GetProperty("symbolName").GetString() ?? "";
        var worktreeInfo = DetectWorktreeFromArgs(args);
        var results = await _graphService.FindImplementationsAsync(typeName, cancellationToken: ct);
        return results.Select(n => new
        {
            name = n.Name,
            fullName = n.FullName,
            kind = n.NodeType.ToString(),
            filePath = TranslatePathIfWorktree(n.FilePath, worktreeInfo),
            line = n.LineNumber
        });
    }

    private async Task<object> FindDerivedTypesFromNavigate(JsonElement? args, CancellationToken ct)
    {
        var baseClassName = args?.GetProperty("symbolName").GetString() ?? "";
        var worktreeInfo = DetectWorktreeFromArgs(args);
        var results = await _graphService.FindDerivedTypesAsync(baseClassName, cancellationToken: ct);
        return results.Select(n => new
        {
            name = n.Name,
            fullName = n.FullName,
            kind = n.NodeType.ToString(),
            filePath = TranslatePathIfWorktree(n.FilePath, worktreeInfo),
            line = n.LineNumber
        });
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
            "move_type_to_file" => await MoveTypeToFileAsync(args, ct),
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
    /// Routes to: implement_interface, constructor, property, method, tests.
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
            "create_type" => await CreateTypeAsync(args, ct),
            "tests" => await GenerateTestsAsync(args, ct),
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
    /// Routes to: list, get, create, enrich, update_step, complete.
    /// </summary>
    private async Task<object> WorkflowAsync(JsonElement? args, CancellationToken ct)
    {
        var operation = args?.GetProperty("operation").GetString()
            ?? throw new ArgumentException("operation is required");

        return operation switch
        {
            "list" => await ListStoriesAsync(args, ct),
            "get" => await GetStoryContextAsync(args, ct),
            "get_by_path" => await GetStoryByPathAsync(args, ct),
            "create" => await CreateStoryFromIssueAsync(args, ct),
            "enrich" => await EnrichStoryAsync(args, ct),
            "update_step" => await UpdateStepAsync(args, ct),
            "complete" => await CompleteStoryAsync(args, ct),
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

    /// <summary>
    /// aura_workspace - Workspace and worktree management.
    /// Supports: detect_worktree, invalidate_cache, status.
    /// </summary>
    private Task<object> WorkspaceAsync(JsonElement? args, CancellationToken ct)
    {
        var operation = args?.GetProperty("operation").GetString()
            ?? throw new ArgumentException("operation is required");
        var path = args?.GetProperty("path").GetString()
            ?? throw new ArgumentException("path is required");

        return operation switch
        {
            "detect_worktree" => Task.FromResult(DetectWorktreeOperation(path)),
            "invalidate_cache" => Task.FromResult(InvalidateCacheOperation(path)),
            "status" => Task.FromResult(WorkspaceStatusOperation(path)),
            _ => throw new ArgumentException($"Unknown workspace operation: {operation}")
        };
    }

    private object DetectWorktreeOperation(string path)
    {
        var worktreeInfo = GitWorktreeDetector.Detect(path);

        if (worktreeInfo is null)
        {
            return new
            {
                isGitRepository = false,
                isWorktree = false,
                path = Path.GetFullPath(path),
                message = "Path is not in a git repository"
            };
        }

        if (!worktreeInfo.Value.IsWorktree)
        {
            return new
            {
                isGitRepository = true,
                isWorktree = false,
                path = worktreeInfo.Value.WorktreePath,
                message = "Path is in a main git repository (not a worktree)"
            };
        }

        return new
        {
            isGitRepository = true,
            isWorktree = true,
            worktreePath = worktreeInfo.Value.WorktreePath,
            mainRepoPath = worktreeInfo.Value.MainRepoPath,
            gitDir = worktreeInfo.Value.GitDir,
            message = "Path is in a git worktree"
        };
    }

    private object InvalidateCacheOperation(string path)
    {
        // Try to invalidate both the exact path and any parent .sln files
        var normalizedPath = Path.GetFullPath(path);
        var invalidated = _roslynService.InvalidateCache(normalizedPath);

        // Also try worktree detection to invalidate related paths
        var worktreeInfo = GitWorktreeDetector.Detect(path);
        var additionalInfo = worktreeInfo?.IsWorktree == true
            ? $"Worktree detected. Main repo: {worktreeInfo.Value.MainRepoPath}"
            : null;

        return new
        {
            success = invalidated,
            path = normalizedPath,
            message = invalidated
                ? "Roslyn workspace cache invalidated for this path"
                : "No cached workspace found for this path",
            worktreeInfo = additionalInfo
        };
    }

    private object WorkspaceStatusOperation(string path)
    {
        var worktreeInfo = GitWorktreeDetector.Detect(path);

        return new
        {
            path = Path.GetFullPath(path),
            isGitRepository = worktreeInfo is not null,
            isWorktree = worktreeInfo?.IsWorktree ?? false,
            worktreePath = worktreeInfo?.WorktreePath,
            mainRepoPath = worktreeInfo?.MainRepoPath,
            gitDir = worktreeInfo?.GitDir,
            roslynWorkspaceCached = false, // TODO: Expose cache status from RoslynWorkspaceService
            message = worktreeInfo?.IsWorktree == true
                ? $"Git worktree linked to {worktreeInfo.Value.MainRepoPath}"
                : worktreeInfo is not null
                    ? "Main git repository"
                    : "Not a git repository"
        };
    }

    // =========================================================================
    // aura_pattern - Load operational patterns for complex tasks
    // =========================================================================

    /// <summary>
    /// aura_pattern - Load operational patterns for complex multi-step tasks.
    /// Patterns are dynamically discovered from the patterns/ folder.
    /// </summary>
    private Task<object> PatternAsync(JsonElement? args, CancellationToken ct)
    {
        var operation = args?.GetProperty("operation").GetString()
            ?? throw new ArgumentException("operation is required");

        return operation switch
        {
            "list" => Task.FromResult(ListPatternsOperation()),
            "get" => Task.FromResult(GetPatternOperation(args)),
            _ => throw new ArgumentException($"Unknown pattern operation: {operation}")
        };
    }

    private object ListPatternsOperation()
    {
        var patternsDir = GetPatternsDirectory();
        if (!Directory.Exists(patternsDir))
        {
            return new
            {
                success = false,
                patterns = Array.Empty<object>(),
                languagePatterns = Array.Empty<object>(),
                languages = Array.Empty<string>(),
                message = $"Patterns directory not found: {patternsDir}"
            };
        }

        // Get available languages (subdirectories)
        var languages = Directory.GetDirectories(patternsDir)
            .Select(d => Path.GetFileName(d))
            .Where(n => !n.StartsWith('.'))
            .ToArray();

        // Base patterns (polyglot)
        var patterns = Directory.GetFiles(patternsDir, "*.md")
            .Where(f => !Path.GetFileName(f).Equals("README.md", StringComparison.OrdinalIgnoreCase))
            .Select(f =>
            {
                var name = Path.GetFileNameWithoutExtension(f);
                var content = File.ReadAllText(f);
                var firstLine = content.Split('\n').FirstOrDefault()?.Trim() ?? "";
                var description = firstLine.StartsWith("#")
                    ? firstLine.TrimStart('#', ' ')
                    : name;

                // Check which languages have overlays for this pattern
                var overlays = languages
                    .Where(lang => File.Exists(Path.Combine(patternsDir, lang, $"{name}.md")))
                    .ToArray();

                return new { name, description, overlays };
            })
            .ToArray();

        // Language-specific patterns (no base, only in language folder)
        var languagePatterns = languages
            .SelectMany(lang =>
            {
                var langDir = Path.Combine(patternsDir, lang);
                return Directory.GetFiles(langDir, "*.md")
                    .Where(f =>
                    {
                        var name = Path.GetFileNameWithoutExtension(f);
                        // Exclude patterns that are overlays of base patterns
                        return !File.Exists(Path.Combine(patternsDir, $"{name}.md"));
                    })
                    .Select(f =>
                    {
                        var name = Path.GetFileNameWithoutExtension(f);
                        var content = File.ReadAllText(f);
                        var firstLine = content.Split('\n').FirstOrDefault()?.Trim() ?? "";
                        var description = firstLine.StartsWith("#")
                            ? firstLine.TrimStart('#', ' ')
                            : name;

                        return new { name, language = lang, description };
                    });
            })
            .ToArray();

        return new
        {
            success = true,
            patterns,
            languagePatterns,
            languages,
            message = $"Found {patterns.Length} base patterns, {languagePatterns.Length} language-specific patterns. Use aura_pattern(operation: 'get', name: '...', language: '...') to load."
        };
    }

    private object GetPatternOperation(JsonElement? args)
    {
        var name = args?.TryGetProperty("name", out var nameProp) == true
            ? nameProp.GetString()
            : null;

        var language = args?.TryGetProperty("language", out var langProp) == true
            ? langProp.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("name is required for 'get' operation");
        }

        var patternsDir = GetPatternsDirectory();
        var basePatternPath = Path.Combine(patternsDir, $"{name}.md");
        var hasBasePattern = File.Exists(basePatternPath);

        // Check for language-specific pattern (no base)
        string? langOnlyPatternPath = null;
        if (!string.IsNullOrWhiteSpace(language))
        {
            langOnlyPatternPath = Path.Combine(patternsDir, language, $"{name}.md");
        }

        // Case 1: Base pattern exists
        if (hasBasePattern)
        {
            var baseContent = File.ReadAllText(basePatternPath);
            string? overlayContent = null;
            var hasOverlay = false;

            // Check for language overlay
            if (!string.IsNullOrWhiteSpace(language))
            {
                var overlayPath = Path.Combine(patternsDir, language, $"{name}.md");
                if (File.Exists(overlayPath))
                {
                    overlayContent = File.ReadAllText(overlayPath);
                    hasOverlay = true;
                }
            }

            // Merge base + overlay if overlay exists
            var finalContent = hasOverlay
                ? $"{baseContent}\n\n---\n\n# {language!.ToUpperInvariant()} Language Overlay\n\n{overlayContent}"
                : baseContent;

            var message = hasOverlay
                ? $"Loaded pattern '{name}' with {language} overlay. Follow the steps in this pattern."
                : !string.IsNullOrWhiteSpace(language)
                    ? $"Pattern '{name}' loaded (no {language} overlay found). Follow the steps in this pattern."
                    : "Follow the steps in this pattern. Do not deviate.";

            return new
            {
                success = true,
                name,
                language,
                hasOverlay,
                isLanguageSpecific = false,
                content = finalContent,
                message
            };
        }

        // Case 2: Language-specific pattern (no base)
        if (langOnlyPatternPath != null && File.Exists(langOnlyPatternPath))
        {
            var content = File.ReadAllText(langOnlyPatternPath);
            return new
            {
                success = true,
                name,
                language,
                hasOverlay = false,
                isLanguageSpecific = true,
                content,
                message = $"Loaded {language}-specific pattern '{name}'. Follow the steps in this pattern."
            };
        }

        // Case 3: Not found
        return new
        {
            success = false,
            name,
            language,
            hasOverlay = false,
            isLanguageSpecific = false,
            content = (string?)null,
            message = $"Pattern '{name}' not found. Use aura_pattern(operation: 'list') to see available patterns."
        };
    }

    private static string GetPatternsDirectory()
    {
        // Try relative to the base directory of the executing assembly
        var basePath = AppContext.BaseDirectory;
        var absolutePath = Path.Combine(basePath, "patterns");
        if (Directory.Exists(absolutePath))
        {
            return absolutePath;
        }

        // Try one level up from base directory (installed layout: api\ is sibling to patterns\)
        var parentPath = Path.GetDirectoryName(basePath.TrimEnd(Path.DirectorySeparatorChar));
        if (!string.IsNullOrEmpty(parentPath))
        {
            var siblingPath = Path.Combine(parentPath, "patterns");
            if (Directory.Exists(siblingPath))
            {
                return siblingPath;
            }
        }

        // Default fallback - will fail gracefully with "not found" message
        return Path.Combine(basePath, "patterns");
    }

    /// <summary>
    /// Loads pattern content with optional language overlay.
    /// Returns merged base + overlay if both exist, or just the pattern if no overlay.
    /// </summary>
    private static string? LoadPatternContent(string patternName, string? language)
    {
        var patternsDir = GetPatternsDirectory();
        var basePatternPath = Path.Combine(patternsDir, $"{patternName}.md");
        var hasBasePattern = File.Exists(basePatternPath);

        // Check for language-specific pattern path
        string? langPatternPath = null;
        if (!string.IsNullOrWhiteSpace(language))
        {
            langPatternPath = Path.Combine(patternsDir, language, $"{patternName}.md");
        }

        // Case 1: Base pattern exists
        if (hasBasePattern)
        {
            var baseContent = File.ReadAllText(basePatternPath);

            // Check for language overlay
            if (langPatternPath != null && File.Exists(langPatternPath))
            {
                var overlayContent = File.ReadAllText(langPatternPath);
                return $"{baseContent}\n\n---\n\n# {language!.ToUpperInvariant()} Language Overlay\n\n{overlayContent}";
            }

            return baseContent;
        }

        // Case 2: Language-specific pattern only (no base)
        if (langPatternPath != null && File.Exists(langPatternPath))
        {
            return File.ReadAllText(langPatternPath);
        }

        // Pattern not found
        return null;
    }

    // =========================================================================
    // aura_edit - Surgical text editing
    // =========================================================================

    /// <summary>
    /// Surgical text editing: insert, replace, or delete lines in any file.
    /// Uses 1-based line numbers. All writes normalize to LF line endings.
    /// </summary>
    private async Task<object> EditAsync(JsonElement? args, CancellationToken ct)
    {
        var operation = args?.GetProperty("operation").GetString()
            ?? throw new ArgumentException("operation is required");

        var filePath = args?.GetProperty("filePath").GetString()
            ?? throw new ArgumentException("filePath is required");

        var preview = args?.TryGetProperty("preview", out var previewProp) == true && previewProp.GetBoolean();

        if (!File.Exists(filePath))
        {
            return new
            {
                success = false,
                error = $"File not found: {filePath}",
                operation,
                filePath
            };
        }

        try
        {
            // Read file content preserving original for comparison
            var originalContent = await File.ReadAllTextAsync(filePath, ct);
            var lines = originalContent.Split('\n')
                .Select(l => l.TrimEnd('\r')) // Normalize CRLF to LF
                .ToList();

            string modifiedContent;
            string description;

            switch (operation)
            {
                case "insert_lines":
                    (modifiedContent, description) = InsertLinesOperation(args, lines, filePath);
                    break;

                case "replace_lines":
                    (modifiedContent, description) = ReplaceLinesOperation(args, lines, filePath);
                    break;

                case "delete_lines":
                    (modifiedContent, description) = DeleteLinesOperation(args, lines, filePath);
                    break;

                case "append":
                    (modifiedContent, description) = AppendOperation(args, lines, filePath);
                    break;

                case "prepend":
                    (modifiedContent, description) = PrependOperation(args, lines, filePath);
                    break;

                default:
                    throw new ArgumentException($"Unknown edit operation: {operation}");
            }

            // Normalize to LF and ensure final newline
            modifiedContent = NormalizeLineEndings(modifiedContent);

            if (preview)
            {
                return new
                {
                    success = true,
                    preview = true,
                    operation,
                    filePath,
                    description,
                    originalLineCount = lines.Count,
                    modifiedLineCount = modifiedContent.Split('\n').Length,
                    content = modifiedContent
                };
            }

            // Write the modified content
            await File.WriteAllTextAsync(filePath, modifiedContent, ct);

            return new
            {
                success = true,
                preview = false,
                operation,
                filePath,
                description,
                originalLineCount = lines.Count,
                modifiedLineCount = modifiedContent.Split('\n').Length
            };
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            return new
            {
                success = false,
                error = ex.Message,
                operation,
                filePath
            };
        }
    }

    private static (string content, string description) InsertLinesOperation(
        JsonElement? args, List<string> lines, string filePath)
    {
        var line = args?.TryGetProperty("line", out var lineProp) == true
            ? lineProp.GetInt32()
            : throw new ArgumentException("line is required for insert_lines operation");

        var content = args?.TryGetProperty("content", out var contentProp) == true
            ? contentProp.GetString() ?? ""
            : throw new ArgumentException("content is required for insert_lines operation");

        // Line 0 means insert at the very beginning
        // Line N means insert after line N
        if (line < 0 || line > lines.Count)
        {
            throw new ArgumentException(
                $"line {line} is out of range. File has {lines.Count} lines. Use 0 to insert at beginning, or 1-{lines.Count} to insert after that line.");
        }

        var newLines = content.Split('\n').Select(l => l.TrimEnd('\r')).ToList();

        // Insert the new lines
        lines.InsertRange(line, newLines);

        var description = line == 0
            ? $"Inserted {newLines.Count} line(s) at the beginning"
            : $"Inserted {newLines.Count} line(s) after line {line}";

        return (string.Join("\n", lines), description);
    }

    private static (string content, string description) ReplaceLinesOperation(
        JsonElement? args, List<string> lines, string filePath)
    {
        var startLine = args?.TryGetProperty("startLine", out var startProp) == true
            ? startProp.GetInt32()
            : throw new ArgumentException("startLine is required for replace_lines operation");

        var endLine = args?.TryGetProperty("endLine", out var endProp) == true
            ? endProp.GetInt32()
            : throw new ArgumentException("endLine is required for replace_lines operation");

        var content = args?.TryGetProperty("content", out var contentProp) == true
            ? contentProp.GetString() ?? ""
            : throw new ArgumentException("content is required for replace_lines operation");

        // Validate range (1-based)
        if (startLine < 1 || startLine > lines.Count)
        {
            throw new ArgumentException(
                $"startLine {startLine} is out of range. File has {lines.Count} lines (1-based).");
        }

        if (endLine < startLine || endLine > lines.Count)
        {
            throw new ArgumentException(
                $"endLine {endLine} is invalid. Must be >= startLine ({startLine}) and <= {lines.Count}.");
        }

        var newLines = content.Split('\n').Select(l => l.TrimEnd('\r')).ToList();

        // Convert to 0-based index
        var startIndex = startLine - 1;
        var endIndex = endLine - 1;
        var countToRemove = endIndex - startIndex + 1;

        // Remove the old lines and insert new ones
        lines.RemoveRange(startIndex, countToRemove);
        lines.InsertRange(startIndex, newLines);

        var description = $"Replaced lines {startLine}-{endLine} ({countToRemove} line(s)) with {newLines.Count} line(s)";

        return (string.Join("\n", lines), description);
    }

    private static (string content, string description) DeleteLinesOperation(
        JsonElement? args, List<string> lines, string filePath)
    {
        var startLine = args?.TryGetProperty("startLine", out var startProp) == true
            ? startProp.GetInt32()
            : throw new ArgumentException("startLine is required for delete_lines operation");

        var endLine = args?.TryGetProperty("endLine", out var endProp) == true
            ? endProp.GetInt32()
            : throw new ArgumentException("endLine is required for delete_lines operation");

        // Validate range (1-based)
        if (startLine < 1 || startLine > lines.Count)
        {
            throw new ArgumentException(
                $"startLine {startLine} is out of range. File has {lines.Count} lines (1-based).");
        }

        if (endLine < startLine || endLine > lines.Count)
        {
            throw new ArgumentException(
                $"endLine {endLine} is invalid. Must be >= startLine ({startLine}) and <= {lines.Count}.");
        }

        // Convert to 0-based index
        var startIndex = startLine - 1;
        var endIndex = endLine - 1;
        var countToRemove = endIndex - startIndex + 1;

        lines.RemoveRange(startIndex, countToRemove);

        var description = $"Deleted lines {startLine}-{endLine} ({countToRemove} line(s))";

        return (string.Join("\n", lines), description);
    }

    private static (string content, string description) AppendOperation(
        JsonElement? args, List<string> lines, string filePath)
    {
        var content = args?.TryGetProperty("content", out var contentProp) == true
            ? contentProp.GetString() ?? ""
            : throw new ArgumentException("content is required for append operation");

        var newLines = content.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        lines.AddRange(newLines);

        var description = $"Appended {newLines.Count} line(s) at end of file";

        return (string.Join("\n", lines), description);
    }

    private static (string content, string description) PrependOperation(
        JsonElement? args, List<string> lines, string filePath)
    {
        var content = args?.TryGetProperty("content", out var contentProp) == true
            ? contentProp.GetString() ?? ""
            : throw new ArgumentException("content is required for prepend operation");

        var newLines = content.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        lines.InsertRange(0, newLines);

        var description = $"Prepended {newLines.Count} line(s) at beginning of file";

        return (string.Join("\n", lines), description);
    }

    /// <summary>
    /// Normalizes content to LF line endings and ensures a trailing newline.
    /// </summary>
    private static string NormalizeLineEndings(string content)
    {
        // Replace any CRLF with LF
        content = content.Replace("\r\n", "\n").Replace("\r", "\n");

        // Ensure trailing newline
        if (!content.EndsWith('\n'))
        {
            content += '\n';
        }

        return content;
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

        // Parse workspacePath and detect if it's a worktree
        string? sourcePathPrefix = null;
        DetectedWorktree? worktreeInfo = null;
        if (args.HasValue && args.Value.TryGetProperty("workspacePath", out var workspaceEl))
        {
            var workspacePath = workspaceEl.GetString();

            // Detect worktree synchronously - we need this for path translation
            if (!string.IsNullOrEmpty(workspacePath))
            {
                worktreeInfo = GitWorktreeDetector.Detect(workspacePath);
            }

            if (worktreeInfo?.IsWorktree == true)
            {
                // Use main repo path for index lookup
                sourcePathPrefix = worktreeInfo.Value.MainRepoPath;
                _logger.LogDebug(
                    "Search from worktree {WorktreePath} -> querying index at {MainRepoPath}",
                    worktreeInfo.Value.WorktreePath,
                    worktreeInfo.Value.MainRepoPath);
            }
            else
            {
                // Fallback to async resolution for non-worktree cases
                sourcePathPrefix = await ResolveToMainRepositoryAsync(workspacePath, ct);
            }
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

        // Extract potential symbol names from query (words that look like identifiers)
        // Handles multi-word queries like "IGitWorktreeService CreateAsync WorktreeResult"
        var symbolCandidates = ExtractSymbolCandidates(query);

        // Search for each symbol candidate in the code graph
        var allExactMatches = new List<CodeNode>();
        foreach (var symbol in symbolCandidates)
        {
            var matches = await _graphService.FindNodesAsync(symbol, cancellationToken: ct);
            allExactMatches.AddRange(matches);
        }

        // Deduplicate by full name and prioritize: interfaces, classes, enums first
        var exactMatchResults = allExactMatches
            .DistinctBy(n => n.FullName)
            .OrderByDescending(n => n.NodeType switch
            {
                CodeNodeType.Interface => 100,
                CodeNodeType.Class => 90,
                CodeNodeType.Enum => 85,
                CodeNodeType.Record => 80,
                CodeNodeType.Struct => 75,
                CodeNodeType.Method => 50,
                CodeNodeType.Property => 40,
                _ => 0
            })
            .Take(5) // Limit to top 5 exact matches
            .Select(n => new
            {
                content = $"[EXACT MATCH] {n.NodeType}: {n.FullName}",
                filePath = TranslatePathIfWorktree(n.FilePath, worktreeInfo),
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
            filePath = TranslatePathIfWorktree(r.SourcePath, worktreeInfo),
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

    /// <summary>
    /// Extracts potential symbol names from a search query.
    /// Identifies words that look like code identifiers (PascalCase, camelCase, contain underscores, etc.)
    /// </summary>
    private static List<string> ExtractSymbolCandidates(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<string>();

        // Split on whitespace and common separators
        var tokens = query.Split(new[] { ' ', '\t', '\n', '\r', ',', ';', ':', '(', ')', '[', ']', '{', '}' },
            StringSplitOptions.RemoveEmptyEntries);

        var candidates = new List<string>();
        foreach (var token in tokens)
        {
            // Skip very short tokens (likely noise) unless they look like acronyms
            if (token.Length < 2)
                continue;

            // Skip common English words that aren't likely symbol names
            var lower = token.ToLowerInvariant();
            if (IsCommonWord(lower))
                continue;

            // Keep tokens that look like identifiers:
            // - Start with letter or underscore
            // - Contain only alphanumeric and underscores
            // - PascalCase or camelCase patterns
            if (LooksLikeIdentifier(token))
            {
                candidates.Add(token);
            }
        }

        return candidates.Distinct().ToList();
    }

    /// <summary>
    /// Checks if a token looks like a code identifier.
    /// </summary>
    private static bool LooksLikeIdentifier(string token)
    {
        if (string.IsNullOrEmpty(token))
            return false;

        // Must start with letter or underscore
        var first = token[0];
        if (!char.IsLetter(first) && first != '_')
            return false;

        // All characters must be alphanumeric or underscore
        foreach (var c in token)
        {
            if (!char.IsLetterOrDigit(c) && c != '_')
                return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if a word is a common English word that's unlikely to be a symbol name.
    /// </summary>
    private static bool IsCommonWord(string word)
    {
        // Common words to filter out
        return word switch
        {
            "the" or "a" or "an" or "and" or "or" or "but" or "in" or "on" or "at" or "to" or "for" or
            "of" or "with" or "by" or "from" or "as" or "is" or "was" or "are" or "were" or "be" or
            "been" or "being" or "have" or "has" or "had" or "do" or "does" or "did" or "will" or
            "would" or "could" or "should" or "may" or "might" or "must" or "can" or "this" or "that" or
            "these" or "those" or "it" or "its" or "not" or "no" or "yes" or "all" or "any" or "some" or
            "find" or "get" or "set" or "how" or "what" or "where" or "when" or "why" or "which" => true,
            _ => false
        };
    }

    /// <summary>
    /// Translates a file path from the main repository to the worktree if applicable.
    /// </summary>
    private static string? TranslatePathIfWorktree(string? filePath, DetectedWorktree? worktreeInfo)
    {
        if (filePath is null || worktreeInfo is null || !worktreeInfo.Value.IsWorktree)
        {
            return filePath;
        }

        return GitWorktreeDetector.TranslatePath(filePath, worktreeInfo.Value);
    }

    /// <summary>
    /// Detects worktree info from solutionPath, workspacePath, or filePath in args.
    /// </summary>
    private static DetectedWorktree? DetectWorktreeFromArgs(JsonElement? args)
    {
        if (!args.HasValue) return null;

        // Try solutionPath first (most common for C# operations)
        if (args.Value.TryGetProperty("solutionPath", out var solutionEl))
        {
            var path = solutionEl.GetString();
            if (!string.IsNullOrEmpty(path))
            {
                return GitWorktreeDetector.Detect(path);
            }
        }

        // Try workspacePath (used by search)
        if (args.Value.TryGetProperty("workspacePath", out var workspaceEl))
        {
            var path = workspaceEl.GetString();
            if (!string.IsNullOrEmpty(path))
            {
                return GitWorktreeDetector.Detect(path);
            }
        }

        // Try filePath (used by Python operations)
        if (args.Value.TryGetProperty("filePath", out var fileEl))
        {
            var path = fileEl.GetString();
            if (!string.IsNullOrEmpty(path))
            {
                return GitWorktreeDetector.Detect(path);
            }
        }

        return null;
    }

    private async Task<object> FindImplementationsAsync(JsonElement? args, CancellationToken ct)
    {
        var typeName = args?.GetProperty("typeName").GetString() ?? "";
        var worktreeInfo = DetectWorktreeFromArgs(args);
        var results = await _graphService.FindImplementationsAsync(typeName, cancellationToken: ct);

        return results.Select(n => new
        {
            name = n.Name,
            fullName = n.FullName,
            kind = n.NodeType.ToString(),
            filePath = TranslatePathIfWorktree(n.FilePath, worktreeInfo),
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

        var worktreeInfo = DetectWorktreeFromArgs(args);
        var results = await _graphService.FindCallersAsync(methodName, containingType, cancellationToken: ct);

        return results.Select(n => new
        {
            name = n.Name,
            fullName = n.FullName,
            kind = n.NodeType.ToString(),
            filePath = TranslatePathIfWorktree(n.FilePath, worktreeInfo),
            line = n.LineNumber
        });
    }

    private async Task<object> GetTypeMembersAsync(JsonElement? args, CancellationToken ct)
    {
        var typeName = args?.GetProperty("typeName").GetString() ?? "";
        var worktreeInfo = DetectWorktreeFromArgs(args);

        // Try code graph first
        var results = await _graphService.GetTypeMembersAsync(typeName, cancellationToken: ct);

        if (results.Count > 0)
        {
            return results.Select(n => new
            {
                name = n.Name,
                kind = n.NodeType.ToString(),
                filePath = TranslatePathIfWorktree(n.FilePath, worktreeInfo),
                line = n.LineNumber
            });
        }

        // Fallback to Roslyn if code graph returns empty and solutionPath is provided
        string? solutionPath = null;
        if (args.HasValue && args.Value.TryGetProperty("solutionPath", out var solEl))
        {
            solutionPath = solEl.GetString();
        }

        if (string.IsNullOrEmpty(solutionPath) || !File.Exists(solutionPath))
        {
            return Array.Empty<object>();
        }

        return await GetTypeMembersViaRoslynAsync(solutionPath, typeName, worktreeInfo, ct);
    }

    private async Task<object> GetTypeMembersViaRoslynAsync(
        string solutionPath,
        string typeName,
        DetectedWorktree? worktreeInfo,
        CancellationToken ct)
    {
        try
        {
            var solution = await _roslynService.GetSolutionAsync(solutionPath, ct);
            INamedTypeSymbol? typeSymbol = null;

            foreach (var project in solution.Projects)
            {
                var compilation = await project.GetCompilationAsync(ct);
                if (compilation is null) continue;

                // Try exact match first, then partial match
                typeSymbol = compilation.GetTypeByMetadataName(typeName);
                if (typeSymbol is null)
                {
                    // Try finding by simple name
                    foreach (var tree in compilation.SyntaxTrees)
                    {
                        var semanticModel = compilation.GetSemanticModel(tree);
                        var root = await tree.GetRootAsync(ct);

                        var typeDeclarations = root.DescendantNodes()
                            .OfType<TypeDeclarationSyntax>()
                            .Where(t => t.Identifier.Text == typeName ||
                                         t.Identifier.Text.EndsWith(typeName));

                        foreach (var typeDecl in typeDeclarations)
                        {
                            if (semanticModel.GetDeclaredSymbol(typeDecl) is INamedTypeSymbol found)
                            {
                                typeSymbol = found;
                                break;
                            }
                        }

                        if (typeSymbol != null) break;
                    }
                }

                if (typeSymbol != null) break;
            }

            if (typeSymbol is null)
            {
                return Array.Empty<object>();
            }

            // Get all members
            var members = typeSymbol.GetMembers()
                .Where(m => !m.IsImplicitlyDeclared && m.CanBeReferencedByName)
                .Select(m =>
                {
                    var location = m.Locations.FirstOrDefault();
                    var filePath = location?.SourceTree?.FilePath ?? "";

                    return new
                    {
                        name = m.Name,
                        kind = m.Kind.ToString(),
                        signature = GetMemberSignature(m),
                        filePath = TranslatePathIfWorktree(filePath, worktreeInfo),
                        line = location?.GetLineSpan().StartLinePosition.Line + 1 ?? 0
                    };
                })
                .ToList();

            return members;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Roslyn fallback for type_members failed for {TypeName}", typeName);
            return Array.Empty<object>();
        }
    }

    private static string GetMemberSignature(ISymbol member)
    {
        return member switch
        {
            IMethodSymbol method => $"{method.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} {method.Name}({string.Join(", ", method.Parameters.Select(p => $"{p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} {p.Name}"))})",
            IPropertySymbol prop => $"{prop.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} {prop.Name}",
            IFieldSymbol field => $"{field.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} {field.Name}",
            IEventSymbol evt => $"event {evt.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} {evt.Name}",
            _ => member.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
        };
    }

    private async Task<object> FindDerivedTypesAsync(JsonElement? args, CancellationToken ct)
    {
        var baseClassName = args?.GetProperty("baseClassName").GetString() ?? "";
        var worktreeInfo = DetectWorktreeFromArgs(args);
        var results = await _graphService.FindDerivedTypesAsync(baseClassName, cancellationToken: ct);

        return results.Select(n => new
        {
            name = n.Name,
            fullName = n.FullName,
            kind = n.NodeType.ToString(),
            filePath = TranslatePathIfWorktree(n.FilePath, worktreeInfo),
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
        // Get solutionPath - required
        string? solutionPath = null;
        if (args.HasValue && args.Value.TryGetProperty("solutionPath", out var solEl))
        {
            solutionPath = solEl.GetString();
        }

        // Get projectName - optional (if omitted, validate all projects)
        string? projectName = null;
        if (args.HasValue && args.Value.TryGetProperty("projectName", out var projEl))
        {
            projectName = projEl.GetString();
        }

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

            // If projectName is specified, validate just that project
            if (!string.IsNullOrEmpty(projectName))
            {
                var project = solution.Projects.FirstOrDefault(p =>
                    p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));

                if (project is null)
                {
                    var available = string.Join(", ", solution.Projects.Select(p => p.Name));
                    return new { error = $"Project '{projectName}' not found. Available: {available}" };
                }

                return await ValidateProjectAsync(project, includeWarnings, ct);
            }

            // No project specified - validate all projects in solution
            var results = new List<object>();
            var totalErrors = 0;
            var totalWarnings = 0;

            foreach (var project in solution.Projects.Where(p => !p.Name.Contains(".Tests")))
            {
                var result = await ValidateProjectAsync(project, includeWarnings, ct);
                results.Add(new { project = project.Name, result });

                // Extract counts from dynamic result
                if (result is { } r)
                {
                    var props = r.GetType().GetProperties();
                    var errorProp = props.FirstOrDefault(p => p.Name == "errorCount");
                    var warnProp = props.FirstOrDefault(p => p.Name == "warningCount");
                    if (errorProp?.GetValue(r) is int errors) totalErrors += errors;
                    if (warnProp?.GetValue(r) is int warnings) totalWarnings += warnings;
                }
            }

            return new
            {
                solutionPath,
                success = totalErrors == 0,
                totalErrors,
                totalWarnings,
                projectCount = results.Count,
                projects = results,
                summary = totalErrors == 0
                    ? $"Solution compiles successfully ({results.Count} projects)"
                    : $"Solution has {totalErrors} error(s) across {results.Count} projects"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate compilation for {SolutionPath}", solutionPath);
            return new { error = $"Failed to validate compilation: {ex.Message}" };
        }
    }

    private async Task<object> ValidateProjectAsync(Project project, bool includeWarnings, CancellationToken ct)
    {
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

            // Parse test results from output - handle multiple output formats
            // Format 1: "Passed: 10" (normal)
            // Format 2: "Passed:    10, Failed:     0" (summary line)
            // Format 3: "Total tests: 10"
            var passedMatch = System.Text.RegularExpressions.Regex.Match(outputText, @"Passed[:\s]+(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var failedMatch = System.Text.RegularExpressions.Regex.Match(outputText, @"Failed[:\s]+(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var skippedMatch = System.Text.RegularExpressions.Regex.Match(outputText, @"Skipped[:\s]+(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var totalMatch = System.Text.RegularExpressions.Regex.Match(outputText, @"Total[:\s]+(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Fallback: look for "Total tests: X" format
            if (!totalMatch.Success)
            {
                totalMatch = System.Text.RegularExpressions.Regex.Match(outputText, @"Total tests:\s*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            // Calculate total from passed + failed + skipped if total not found
            var passed = passedMatch.Success ? int.Parse(passedMatch.Groups[1].Value) : 0;
            var failed = failedMatch.Success ? int.Parse(failedMatch.Groups[1].Value) : 0;
            var skipped = skippedMatch.Success ? int.Parse(skippedMatch.Groups[1].Value) : 0;
            var total = totalMatch.Success ? int.Parse(totalMatch.Groups[1].Value) : (passed + failed + skipped);

            return new
            {
                projectPath,
                success,
                exitCode = process.ExitCode,
                passed,
                failed,
                skipped,
                total,
                output = outputText.Length > 10000 ? outputText[..10000] + "\n... (truncated)" : outputText
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

        // Auto-load pattern content if story has a pattern
        string? patternContent = null;
        if (!string.IsNullOrWhiteSpace(workflow.PatternName))
        {
            patternContent = LoadPatternContent(workflow.PatternName, workflow.PatternLanguage);
        }

        return new
        {
            id = workflow.Id,
            title = workflow.Title,
            description = workflow.Description,
            status = workflow.Status.ToString(),
            issueUrl = workflow.IssueUrl,
            issueProvider = workflow.IssueProvider?.ToString(),
            issueNumber = workflow.IssueNumber,
            issueOwner = workflow.IssueOwner,
            issueRepo = workflow.IssueRepo,
            analyzedContext = workflow.AnalyzedContext,
            gitBranch = workflow.GitBranch,
            worktreePath = workflow.WorktreePath,
            repositoryPath = workflow.RepositoryPath,
            patternName = workflow.PatternName,
            patternLanguage = workflow.PatternLanguage,
            patternContent,
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

    private async Task<object> GetStoryByPathAsync(JsonElement? args, CancellationToken ct)
    {
        var workspacePath = args?.GetProperty("workspacePath").GetString() ?? "";
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            return new { hasStory = false, message = "workspacePath is required" };
        }

        var normalizedPath = Path.GetFullPath(workspacePath);

        // First try exact match on worktree path
        var workflow = await _workflowService.GetByWorktreePathAsync(normalizedPath, ct);

        // If not found, check if this is a worktree and try parent repo path
        if (workflow is null)
        {
            var worktreeInfo = GitWorktreeDetector.Detect(normalizedPath);
            if (worktreeInfo?.IsWorktree == true)
            {
                // Try the main repo path instead
                workflow = await _workflowService.GetByWorktreePathAsync(worktreeInfo.Value.MainRepoPath, ct);
            }
        }

        if (workflow is null)
        {
            return new
            {
                hasStory = false,
                message = "No active story found for this workspace",
                checkedPath = normalizedPath
            };
        }

        // Auto-load pattern content if story has a pattern
        string? patternContent = null;
        if (!string.IsNullOrWhiteSpace(workflow.PatternName))
        {
            patternContent = LoadPatternContent(workflow.PatternName, workflow.PatternLanguage);
        }

        // Return full story context
        return new
        {
            hasStory = true,
            id = workflow.Id,
            title = workflow.Title,
            description = workflow.Description,
            status = workflow.Status.ToString(),
            issueUrl = workflow.IssueUrl,
            issueProvider = workflow.IssueProvider?.ToString(),
            issueNumber = workflow.IssueNumber,
            issueOwner = workflow.IssueOwner,
            issueRepo = workflow.IssueRepo,
            analyzedContext = workflow.AnalyzedContext,
            gitBranch = workflow.GitBranch,
            worktreePath = workflow.WorktreePath,
            repositoryPath = workflow.RepositoryPath,
            patternName = workflow.PatternName,
            patternLanguage = workflow.PatternLanguage,
            patternContent,
            currentStep = workflow.Steps
                .Where(s => s.Status == StepStatus.Pending || s.Status == StepStatus.Running)
                .OrderBy(s => s.Order)
                .Select(s => new { id = s.Id, name = s.Name, description = s.Description, order = s.Order })
                .FirstOrDefault(),
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

        try
        {
            // Fetch issue from GitHub
            var issue = await _gitHubService.GetIssueAsync(parsed.Value.Owner, parsed.Value.Repo, parsed.Value.Number, ct);

            // Create workflow/story
            var workflow = await _workflowService.CreateAsync(
                issue.Title,
                issue.Body,
                repositoryPath,
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

    private async Task<object> EnrichStoryAsync(JsonElement? args, CancellationToken ct)
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

        // Check for pattern and language parameters
        string? patternName = null;
        string? patternLanguage = null;
        string? patternContent = null;

        if (args.HasValue && args.Value.TryGetProperty("pattern", out var patternEl))
        {
            patternName = patternEl.GetString();
        }

        if (args.HasValue && args.Value.TryGetProperty("language", out var langEl))
        {
            patternLanguage = langEl.GetString();
        }

        // Load pattern content using tiered loading (base + overlay)
        if (!string.IsNullOrWhiteSpace(patternName))
        {
            patternContent = LoadPatternContent(patternName, patternLanguage);
            if (patternContent is null)
            {
                return new
                {
                    error = $"Pattern '{patternName}' not found. Use aura_pattern(operation: 'list') to see available patterns.",
                    storyId
                };
            }
        }

        // If pattern provided but no steps, return pattern content for agent to parse steps from
        JsonElement stepsEl = default;
        var hasSteps = args.HasValue && args.Value.TryGetProperty("steps", out stepsEl) && stepsEl.ValueKind == JsonValueKind.Array;

        if (!string.IsNullOrEmpty(patternContent) && !hasSteps)
        {
            // Store pattern name and language on the workflow for future reference
            if (workflow.PatternName != patternName || workflow.PatternLanguage != patternLanguage)
            {
                workflow.PatternName = patternName;
                workflow.PatternLanguage = patternLanguage;
                await _workflowService.UpdateAsync(workflow, ct);
            }

            // Return pattern content - agent should parse steps and call enrich again with steps array
            return new
            {
                storyId,
                patternName,
                patternLanguage,
                patternContent,
                message = "Pattern loaded and bound to story. Parse the steps from the pattern content and call enrich again with the steps array.",
                hint = "Look for numbered steps, checkboxes (- [ ]), or ### Step headers in the pattern markdown."
            };
        }

        if (!hasSteps)
        {
            return new { error = "Either 'pattern' or 'steps' array is required for enrich operation" };
        }

        var addedSteps = new List<object>();
        foreach (var stepEl in stepsEl.EnumerateArray())
        {
            var name = stepEl.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";
            var capability = stepEl.TryGetProperty("capability", out var capEl) ? capEl.GetString() ?? "" : "";
            var description = stepEl.TryGetProperty("description", out var descEl) ? descEl.GetString() : null;
            string? input = null;
            if (stepEl.TryGetProperty("input", out var inputEl))
            {
                input = inputEl.ValueKind == JsonValueKind.String
                    ? inputEl.GetString()
                    : inputEl.GetRawText();
            }

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(capability))
            {
                continue; // Skip invalid steps
            }

            var step = await _workflowService.AddStepAsync(
                storyId,
                name,
                capability,
                description,
                input,
                ct: ct);

            addedSteps.Add(new
            {
                id = step.Id,
                name = step.Name,
                capability = step.Capability,
                description = step.Description,
                order = step.Order,
                status = step.Status.ToString()
            });
        }

        // If pattern was provided, save it on the workflow
        if (!string.IsNullOrWhiteSpace(patternName) && workflow.PatternName != patternName)
        {
            workflow.PatternName = patternName;
            await _workflowService.UpdateAsync(workflow, ct);
        }

        return new
        {
            storyId,
            stepsAdded = addedSteps.Count,
            patternName,
            steps = addedSteps,
            message = $"Added {addedSteps.Count} steps to story" + (patternName != null ? $" (pattern: {patternName})" : "")
        };
    }

    private async Task<object> UpdateStepAsync(JsonElement? args, CancellationToken ct)
    {
        var storyIdStr = args?.GetProperty("storyId").GetString() ?? "";
        if (!Guid.TryParse(storyIdStr, out var storyId))
        {
            return new { error = "storyId is required and must be a valid GUID" };
        }

        var stepIdStr = args?.GetProperty("stepId").GetString() ?? "";
        if (!Guid.TryParse(stepIdStr, out var stepId))
        {
            return new { error = "stepId is required and must be a valid GUID" };
        }

        var statusStr = args?.GetProperty("status").GetString()?.ToLowerInvariant() ?? "";
        if (string.IsNullOrEmpty(statusStr))
        {
            return new { error = "status is required" };
        }

        var workflow = await _workflowService.GetByIdWithStepsAsync(storyId, ct);
        if (workflow is null)
        {
            return new { error = $"Story not found: {storyId}" };
        }

        var step = workflow.Steps.FirstOrDefault(s => s.Id == stepId);
        if (step is null)
        {
            return new { error = $"Step not found: {stepId}" };
        }

        string? output = null;
        string? error = null;
        string? skipReason = null;

        if (args.HasValue)
        {
            if (args.Value.TryGetProperty("output", out var outputEl))
                output = outputEl.GetString();
            if (args.Value.TryGetProperty("error", out var errorEl))
                error = errorEl.GetString();
            if (args.Value.TryGetProperty("skipReason", out var skipEl))
                skipReason = skipEl.GetString();
        }

        WorkflowStep updatedStep;
        switch (statusStr)
        {
            case "completed":
                step.Status = StepStatus.Completed;
                step.Output = output;
                step.CompletedAt = DateTimeOffset.UtcNow;
                await _workflowService.UpdateStepAsync(step, ct);
                updatedStep = step;
                break;

            case "failed":
                step.Status = StepStatus.Failed;
                step.Error = error ?? "Step marked as failed";
                await _workflowService.UpdateStepAsync(step, ct);
                updatedStep = step;
                break;

            case "skipped":
                updatedStep = await _workflowService.SkipStepAsync(storyId, stepId, skipReason, ct);
                break;

            case "pending":
                updatedStep = await _workflowService.ResetStepAsync(storyId, stepId, ct);
                break;

            default:
                return new { error = $"Unknown status: {statusStr}. Valid values: completed, failed, skipped, pending" };
        }

        return new
        {
            stepId = updatedStep.Id,
            name = updatedStep.Name,
            status = updatedStep.Status.ToString(),
            output = updatedStep.Output,
            error = updatedStep.Error,
            skipReason = updatedStep.SkipReason,
            message = $"Step status updated to {updatedStep.Status}"
        };
    }

    /// <summary>
    /// Complete a workflow/story: validates all steps are done, squash merges commits, pushes branch, creates draft PR.
    /// </summary>
    private async Task<object> CompleteStoryAsync(JsonElement? args, CancellationToken ct)
    {
        var storyIdStr = args?.GetProperty("storyId").GetString() ?? "";
        if (!Guid.TryParse(storyIdStr, out var storyId))
        {
            return new { error = "storyId is required and must be a valid GUID" };
        }

        try
        {
            var workflow = await _workflowService.CompleteAsync(storyId, ct);

            return new
            {
                storyId = workflow.Id,
                title = workflow.Title,
                status = workflow.Status.ToString(),
                completedAt = workflow.CompletedAt,
                gitBranch = workflow.GitBranch,
                pullRequestUrl = workflow.PullRequestUrl,
                message = "Workflow completed successfully" +
                    (workflow.PullRequestUrl is not null ? $". Draft PR created: {workflow.PullRequestUrl}" : "")
            };
        }
        catch (InvalidOperationException ex)
        {
            return new
            {
                error = ex.Message,
                storyId,
                hint = "Ensure all steps are completed or skipped before completing the workflow."
            };
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
        var validate = false;
        var analyze = true; // Default to analyze mode

        if (args.HasValue)
        {
            if (args.Value.TryGetProperty("containingType", out var ctEl))
                containingType = ctEl.GetString();
            if (args.Value.TryGetProperty("filePath", out var fpEl))
                filePath = fpEl.GetString();
            if (args.Value.TryGetProperty("preview", out var prevEl))
                preview = prevEl.GetBoolean();
            if (args.Value.TryGetProperty("validate", out var valEl))
                validate = valEl.GetBoolean();
            if (args.Value.TryGetProperty("analyze", out var analyzeEl))
                analyze = analyzeEl.GetBoolean();
        }

        // If analyze mode, return blast radius without executing
        if (analyze)
        {
            var blastRadius = await _refactoringService.AnalyzeRenameAsync(new RenameSymbolRequest
            {
                SymbolName = symbolName,
                NewName = newName,
                SolutionPath = solutionPath,
                ContainingType = containingType
            }, ct);

            return new
            {
                operation = blastRadius.Operation,
                symbol = blastRadius.Symbol,
                newName = blastRadius.NewName,
                success = blastRadius.Success,
                error = blastRadius.Error,
                blastRadius = new
                {
                    relatedSymbols = blastRadius.RelatedSymbols.Select(s => new
                    {
                        name = s.Name,
                        kind = s.Kind,
                        filePath = s.FilePath,
                        referenceCount = s.ReferenceCount,
                        suggestedNewName = s.SuggestedNewName
                    }),
                    totalReferences = blastRadius.TotalReferences,
                    filesAffected = blastRadius.FilesAffected,
                    filesToRename = blastRadius.FilesToRename
                },
                suggestedPlan = blastRadius.SuggestedPlan.Select(op => new
                {
                    order = op.Order,
                    operation = op.Operation,
                    target = op.Target,
                    newValue = op.NewValue,
                    referenceCount = op.ReferenceCount
                }),
                awaitsConfirmation = blastRadius.AwaitsConfirmation,
                instructions = $"""
                    STEP-BY-STEP EXECUTION REQUIRED:
                    
                    The suggestedPlan contains {blastRadius.SuggestedPlan.Count} operations. Execute them ONE AT A TIME:
                    
                    1. Present this blast radius to the user and wait for confirmation
                    2. For each step in suggestedPlan:
                       a. State which step you're executing (e.g., "Step 1 of {blastRadius.SuggestedPlan.Count}: Renaming X → Y")
                       b. Explain WHY this rename is needed
                       c. Call aura_refactor(operation: "rename", symbolName: "<target>", newName: "<newValue>", analyze: false)
                       d. Run `dotnet build` to verify
                       e. Report result before proceeding to next step
                    3. For 'rename_file' operations, use aura_refactor(operation: "move_type_to_file", symbolName: "<newValue>")
                    4. After all steps, sweep for residuals with grep_search
                    
                    DO NOT execute multiple steps in one tool call. Each rename is a separate operation.
                    """
            };
        }

        var result = await _refactoringService.RenameSymbolAsync(new RenameSymbolRequest
        {
            SymbolName = symbolName,
            NewName = newName,
            SolutionPath = solutionPath,
            ContainingType = containingType,
            FilePath = filePath,
            Preview = preview,
            Validate = validate
        }, ct);

        return new
        {
            success = result.Success,
            message = result.Message,
            modifiedFiles = result.ModifiedFiles,
            preview = result.Preview?.Select(p => new { p.FilePath, p.NewContent }),
            validation = result.Validation is null ? null : new
            {
                buildSucceeded = result.Validation.BuildSucceeded,
                buildOutput = result.Validation.BuildOutput,
                residuals = result.Validation.Residuals
            }
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

    private async Task<object> MoveTypeToFileAsync(JsonElement? args, CancellationToken ct)
    {
        var typeName = args?.GetProperty("symbolName").GetString()
            ?? throw new ArgumentException("symbolName (type name) is required for move_type_to_file");
        var solutionPath = args?.GetProperty("solutionPath").GetString()
            ?? throw new ArgumentException("solutionPath is required for move_type_to_file");

        string? targetDirectory = null;
        string? targetFileName = null;
        var useGitMove = true;
        var preview = false;

        if (args.HasValue)
        {
            if (args.Value.TryGetProperty("targetDirectory", out var tdEl))
                targetDirectory = tdEl.GetString();
            if (args.Value.TryGetProperty("newName", out var tfEl))
                targetFileName = tfEl.GetString();
            if (args.Value.TryGetProperty("useGitMove", out var gmEl))
                useGitMove = gmEl.GetBoolean();
            if (args.Value.TryGetProperty("preview", out var prevEl))
                preview = prevEl.GetBoolean();
        }

        var result = await _refactoringService.MoveTypeToFileAsync(new MoveTypeToFileRequest
        {
            TypeName = typeName,
            SolutionPath = solutionPath,
            TargetDirectory = targetDirectory,
            TargetFileName = targetFileName,
            UseGitMove = useGitMove,
            Preview = preview
        }, ct);

        return new
        {
            success = result.Success,
            message = result.Message,
            modifiedFiles = result.ModifiedFiles,
            createdFiles = result.CreatedFiles,
            deletedFiles = result.DeletedFiles,
            preview = result.Preview?.Select(p => new { p.FilePath, p.NewContent })
        };
    }

    private async Task<object> AddPropertyAsync(JsonElement? args, CancellationToken ct)
    {
        var className = args?.GetProperty("className").GetString() ?? "";
        var propertyName = args?.GetProperty("propertyName").GetString() ?? "";
        var propertyType = args?.GetProperty("propertyType").GetString() ?? "";
        var solutionPath = args?.GetProperty("solutionPath").GetString() ?? "";

        var accessModifier = "public";
        var hasGetter = true;
        var hasSetter = true;
        var hasInit = false;
        var isRequired = false;
        string? initialValue = null;
        var isField = false;
        var isReadonly = false;
        var isStatic = false;
        var preview = false;

        if (args.HasValue)
        {
            if (args.Value.TryGetProperty("accessModifier", out var amEl))
                accessModifier = amEl.GetString() ?? "public";
            if (args.Value.TryGetProperty("hasGetter", out var gEl))
                hasGetter = gEl.GetBoolean();
            if (args.Value.TryGetProperty("hasSetter", out var sEl))
                hasSetter = sEl.GetBoolean();
            if (args.Value.TryGetProperty("hasInit", out var hiEl))
                hasInit = hiEl.GetBoolean();
            if (args.Value.TryGetProperty("isRequired", out var reqEl))
                isRequired = reqEl.GetBoolean();
            if (args.Value.TryGetProperty("initialValue", out var ivEl))
                initialValue = ivEl.GetString();
            if (args.Value.TryGetProperty("isField", out var ifEl))
                isField = ifEl.GetBoolean();
            if (args.Value.TryGetProperty("isReadonly", out var irEl))
                isReadonly = irEl.GetBoolean();
            if (args.Value.TryGetProperty("isStatic", out var isEl))
                isStatic = isEl.GetBoolean();
            if (args.Value.TryGetProperty("preview", out var prevEl))
                preview = prevEl.GetBoolean();
        }

        var result = await _refactoringService.AddPropertyAsync(new AddPropertyRequest
        {
            ClassName = className,
            PropertyName = propertyName,
            PropertyType = propertyType,
            SolutionPath = solutionPath,
            AccessModifier = accessModifier,
            HasGetter = hasGetter,
            HasSetter = hasSetter,
            HasInit = hasInit,
            IsRequired = isRequired,
            InitialValue = initialValue,
            IsField = isField,
            IsReadonly = isReadonly,
            IsStatic = isStatic,
            Preview = preview
        }, ct);

        var memberKind = isField ? "field" : "property";
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
        var isStatic = false;
        string? methodModifier = null;
        string? body = null;
        string? testAttribute = null;
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
            if (args.Value.TryGetProperty("isStatic", out var staticEl))
                isStatic = staticEl.GetBoolean();
            if (args.Value.TryGetProperty("methodModifier", out var mmEl))
                methodModifier = mmEl.GetString();
            if (args.Value.TryGetProperty("body", out var bodyEl))
                body = bodyEl.GetString();
            if (args.Value.TryGetProperty("testAttribute", out var taEl))
                testAttribute = taEl.GetString();
            if (args.Value.TryGetProperty("preview", out var prevEl))
                preview = prevEl.GetBoolean();
        }

        // Parse generic type parameters for method
        List<TypeParameterInfo>? typeParameters = null;
        if (args?.TryGetProperty("typeParameters", out var tpEl) == true && tpEl.ValueKind == JsonValueKind.Array)
        {
            typeParameters = tpEl.EnumerateArray()
                .Select(tp =>
                {
                    var name = tp.TryGetProperty("name", out var n) ? n.GetString() ?? "T" : "T";
                    List<string>? constraints = null;
                    if (tp.TryGetProperty("constraints", out var cEl) && cEl.ValueKind == JsonValueKind.Array)
                    {
                        constraints = cEl.EnumerateArray()
                            .Select(c => c.GetString())
                            .Where(s => s != null)
                            .Cast<string>()
                            .ToList();
                    }
                    return new TypeParameterInfo(name, constraints);
                })
                .ToList();
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
            IsStatic = isStatic,
            MethodModifier = methodModifier,
            Body = body,
            TestAttribute = testAttribute,
            TypeParameters = typeParameters,
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

    private async Task<object> CreateTypeAsync(JsonElement? args, CancellationToken ct)
    {
        var typeName = args?.GetProperty("typeName").GetString()
            ?? throw new ArgumentException("typeName is required for create_type");
        var typeKind = args?.GetProperty("typeKind").GetString()
            ?? throw new ArgumentException("typeKind is required for create_type");
        var solutionPath = args?.GetProperty("solutionPath").GetString()
            ?? throw new ArgumentException("solutionPath is required for create_type");
        var targetDirectory = args?.GetProperty("targetDirectory").GetString()
            ?? throw new ArgumentException("targetDirectory is required for create_type");

        string? ns = null;
        string? baseClass = null;
        List<string>? interfaces = null;
        var accessModifier = "public";
        var isSealed = false;
        var isAbstract = false;
        var isStatic = false;
        var isRecordStruct = false;
        List<string>? additionalUsings = null;
        string? documentationSummary = null;
        var preview = false;

        if (args.HasValue)
        {
            if (args.Value.TryGetProperty("namespace", out var nsEl))
                ns = nsEl.GetString();
            if (args.Value.TryGetProperty("baseClass", out var bcEl))
                baseClass = bcEl.GetString();
            if (args.Value.TryGetProperty("implements", out var implEl) && implEl.ValueKind == JsonValueKind.Array)
            {
                interfaces = implEl.EnumerateArray()
                    .Select(i => i.GetString())
                    .Where(s => s != null)
                    .Cast<string>()
                    .ToList();
            }
            if (args.Value.TryGetProperty("accessModifier", out var amEl))
                accessModifier = amEl.GetString() ?? "public";
            if (args.Value.TryGetProperty("isSealed", out var sealedEl))
                isSealed = sealedEl.GetBoolean();
            if (args.Value.TryGetProperty("isAbstract", out var abstractEl))
                isAbstract = abstractEl.GetBoolean();
            if (args.Value.TryGetProperty("isStatic", out var staticEl))
                isStatic = staticEl.GetBoolean();
            if (args.Value.TryGetProperty("isRecordStruct", out var rsEl))
                isRecordStruct = rsEl.GetBoolean();
            if (args.Value.TryGetProperty("additionalUsings", out var usingsEl) && usingsEl.ValueKind == JsonValueKind.Array)
            {
                additionalUsings = usingsEl.EnumerateArray()
                    .Select(u => u.GetString())
                    .Where(s => s != null)
                    .Cast<string>()
                    .ToList();
            }
            if (args.Value.TryGetProperty("documentationSummary", out var docEl))
                documentationSummary = docEl.GetString();
            if (args.Value.TryGetProperty("preview", out var prevEl))
                preview = prevEl.GetBoolean();
        }

        // Parse primary constructor parameters
        List<RefactoringParameterInfo>? primaryConstructorParameters = null;
        if (args?.TryGetProperty("primaryConstructorParameters", out var pcpEl) == true && pcpEl.ValueKind == JsonValueKind.Array)
        {
            primaryConstructorParameters = pcpEl.EnumerateArray()
                .Select(p => new RefactoringParameterInfo(
                    Name: p.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                    Type: p.TryGetProperty("type", out var t) ? t.GetString() ?? "object" : "object",
                    DefaultValue: p.TryGetProperty("defaultValue", out var dv) ? dv.GetString() : null))
                .ToList();
        }

        // Parse generic type parameters
        List<TypeParameterInfo>? typeParameters = null;
        if (args?.TryGetProperty("typeParameters", out var tpEl) == true && tpEl.ValueKind == JsonValueKind.Array)
        {
            typeParameters = tpEl.EnumerateArray()
                .Select(tp =>
                {
                    var name = tp.TryGetProperty("name", out var n) ? n.GetString() ?? "T" : "T";
                    List<string>? constraints = null;
                    if (tp.TryGetProperty("constraints", out var cEl) && cEl.ValueKind == JsonValueKind.Array)
                    {
                        constraints = cEl.EnumerateArray()
                            .Select(c => c.GetString())
                            .Where(s => s != null)
                            .Cast<string>()
                            .ToList();
                    }
                    return new TypeParameterInfo(name, constraints);
                })
                .ToList();
        }

        var result = await _refactoringService.CreateTypeAsync(new CreateTypeRequest
        {
            TypeName = typeName,
            TypeKind = typeKind,
            SolutionPath = solutionPath,
            TargetDirectory = targetDirectory,
            Namespace = ns,
            BaseClass = baseClass,
            Interfaces = interfaces,
            AccessModifier = accessModifier,
            IsSealed = isSealed,
            IsAbstract = isAbstract,
            IsStatic = isStatic,
            IsRecordStruct = isRecordStruct,
            AdditionalUsings = additionalUsings,
            DocumentationSummary = documentationSummary,
            PrimaryConstructorParameters = primaryConstructorParameters,
            TypeParameters = typeParameters,
            Preview = preview
        }, ct);

        return new
        {
            success = result.Success,
            message = result.Message,
            createdFiles = result.CreatedFiles,
            preview = result.Preview?.Select(p => new { p.FilePath, p.NewContent })
        };
    }

    /// <summary>
    /// aura_generate(operation: "tests") - Generate tests for a target.
    /// </summary>
    private async Task<object> GenerateTestsAsync(JsonElement? args, CancellationToken ct)
    {
        var target = args?.TryGetProperty("target", out var targetEl) == true
            ? targetEl.GetString() ?? throw new ArgumentException("target is required for tests operation")
            : args?.TryGetProperty("className", out var classEl) == true
                ? classEl.GetString() ?? throw new ArgumentException("target or className is required")
                : throw new ArgumentException("target is required for tests operation");

        var solutionPath = args?.GetProperty("solutionPath").GetString()
            ?? throw new ArgumentException("solutionPath is required");

        int? count = null;
        int maxTests = 20;
        var focus = TestFocus.All;
        string? testFramework = null;
        bool analyzeOnly = false;
        bool validateCompilation = false;

        if (args.HasValue)
        {
            if (args.Value.TryGetProperty("count", out var countEl))
                count = countEl.GetInt32();
            if (args.Value.TryGetProperty("maxTests", out var maxEl))
                maxTests = maxEl.GetInt32();
            if (args.Value.TryGetProperty("focus", out var focusEl))
            {
                focus = focusEl.GetString() switch
                {
                    "happy_path" => TestFocus.HappyPath,
                    "edge_cases" => TestFocus.EdgeCases,
                    "error_handling" => TestFocus.ErrorHandling,
                    _ => TestFocus.All
                };
            }
            if (args.Value.TryGetProperty("testFramework", out var fwEl))
                testFramework = fwEl.GetString();
            if (args.Value.TryGetProperty("analyzeOnly", out var aoEl))
                analyzeOnly = aoEl.GetBoolean();
            if (args.Value.TryGetProperty("validateCompilation", out var vcEl))
                validateCompilation = vcEl.GetBoolean();
        }

        string? outputDirectory = null;
        if (args?.TryGetProperty("outputDirectory", out var odEl) == true)
            outputDirectory = odEl.GetString();

        var result = await _testGenerationService.GenerateTestsAsync(new TestGenerationRequest
        {
            Target = target,
            SolutionPath = solutionPath,
            Count = count,
            MaxTests = maxTests,
            Focus = focus,
            TestFramework = testFramework,
            OutputDirectory = outputDirectory,
            AnalyzeOnly = analyzeOnly,
            ValidateCompilation = validateCompilation
        }, ct);

        return new
        {
            success = result.Success,
            message = result.Message,
            analysis = result.Analysis is not null ? new
            {
                testableMembers = result.Analysis.TestableMembers.Select(m => new
                {
                    m.Name,
                    m.Signature,
                    m.ReturnType,
                    m.IsAsync,
                    m.ContainingType,
                    parameters = m.Parameters.Select(p => new { p.Name, p.Type, p.IsNullable }),
                    throwsExceptions = m.ThrowsExceptions
                }),
                existingTests = result.Analysis.ExistingTests.Select(t => new
                {
                    t.FilePath,
                    t.TestCount,
                    t.TestedMethods
                }),
                gaps = result.Analysis.Gaps.Select(g => new
                {
                    g.MethodName,
                    kind = g.Kind.ToString(),
                    g.Description,
                    priority = g.Priority.ToString()
                }),
                result.Analysis.DetectedFramework,
                result.Analysis.SuggestedTestCount
            } : null,
            generated = result.Generated is not null ? new
            {
                result.Generated.TestFilePath,
                result.Generated.FileCreated,
                result.Generated.TestsAdded,
                tests = result.Generated.Tests.Select(t => new
                {
                    t.TestName,
                    t.Description,
                    t.TargetMethod
                }),
                compilesSuccessfully = result.Generated.CompilesSuccessfully,
                compilationDiagnostics = result.Generated.CompilationDiagnostics
            } : null,
            stoppingReason = result.StoppingReason,
            error = result.Error
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
