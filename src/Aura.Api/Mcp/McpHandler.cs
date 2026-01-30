// <copyright file="McpHandler.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Mcp;

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

/// <summary>
/// Handles MCP (Model Context Protocol) JSON-RPC requests.
/// Exposes Aura's RAG and Code Graph capabilities to GitHub Copilot.
/// </summary>
public sealed partial class McpHandler
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
    private readonly IStoryService _storyService;
    private readonly IGitHubService _gitHubService;
    private readonly IRoslynWorkspaceService _roslynService;
    private readonly IRoslynRefactoringService _refactoringService;
    private readonly IPythonRefactoringService _pythonRefactoringService;
    private readonly ITypeScriptRefactoringService _typeScriptRefactoringService;
    private readonly ITestGenerationService _testGenerationService;
    private readonly IGitWorktreeService _worktreeService;
    private readonly ITreeBuilderService _treeBuilderService;
    private readonly IAuraDocsTool _auraDocsTool;
    private readonly IDocsService _docsService;
    private readonly IWorkspaceRegistryService _workspaceRegistryService;
    private readonly ILogger<McpHandler> _logger;

    private readonly Dictionary<string, Func<JsonElement?, CancellationToken, Task<object>>> _tools;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpHandler"/> class.
    /// </summary>
    public McpHandler(
        IRagService ragService,
        ICodeGraphService graphService,
        IStoryService workflowService,
        IGitHubService gitHubService,
        IRoslynWorkspaceService roslynService,
        IRoslynRefactoringService refactoringService,
        IPythonRefactoringService pythonRefactoringService,
        ITypeScriptRefactoringService typeScriptRefactoringService,
        ITestGenerationService testGenerationService,
        IGitWorktreeService worktreeService,
        ITreeBuilderService treeBuilderService,
        IAuraDocsTool auraDocsTool,
        IDocsService docsService,
        IWorkspaceRegistryService workspaceRegistryService,
        ILogger<McpHandler> logger)
    {
        _ragService = ragService;
        _graphService = graphService;
        _storyService = workflowService;
        _gitHubService = gitHubService;
        _roslynService = roslynService;
        _refactoringService = refactoringService;
        _pythonRefactoringService = pythonRefactoringService;
        _typeScriptRefactoringService = typeScriptRefactoringService;
        _testGenerationService = testGenerationService;
        _worktreeService = worktreeService;
        _treeBuilderService = treeBuilderService;
        _auraDocsTool = auraDocsTool;
        _docsService = docsService;
        _workspaceRegistryService = workspaceRegistryService;
        _logger = logger;

        // Phase 7: Consolidated meta-tools (28 tools → 11 tools)
        _tools = new Dictionary<string, Func<JsonElement?, CancellationToken, Task<object>>>
        {
            ["aura_architect"] = ArchitectAsync,
            ["aura_docs"] = DocsAsync,
            ["aura_edit"] = EditAsync,
            ["aura_generate"] = GenerateAsync,
            ["aura_inspect"] = InspectAsync,
            ["aura_navigate"] = NavigateAsync,
            ["aura_pattern"] = PatternAsync,
            ["aura_refactor"] = RefactorAsync,
            ["aura_search"] = SearchAsync,
            ["aura_tree"] = TreeAsync,
            ["aura_validate"] = ValidateAsync,
            ["aura_workflow"] = WorkflowAsync,
            ["aura_workspace"] = WorkspaceAsync,
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
                        workspaces = new
                        {
                            type = "array",
                            items = new { type = "string" },
                            description = "Workspace IDs or aliases to search. Use ['*'] for all registered workspaces. If not specified, uses workspacePath."
                        },
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
                            @enum = new[] { "rename", "change_signature", "extract_interface", "extract_method", "extract_variable", "safe_delete", "move_type_to_file", "move_members_to_partial" }
                        },
                        symbolName = new { type = "string", description = "Symbol to refactor" },
                        newName = new { type = "string", description = "New name for rename, extract_method, extract_variable, extract_interface" },
                        containingType = new { type = "string", description = "Type containing the symbol (for C# disambiguation)" },
                        solutionPath = new { type = "string", description = "Path to solution file (.sln) - for C# operations" },
                        filePath = new { type = "string", description = "Path to file containing the code" },
                        className = new { type = "string", description = "Class name - for move_members_to_partial" },
                        memberNames = new
                        {
                            type = "array",
                            items = new { type = "string" },
                            description = "Member names to move - for move_members_to_partial"
                        },
                        targetFileName = new { type = "string", description = "Target filename for partial file (e.g., 'MyClass.Methods.cs') - for move_members_to_partial" },
                        targetDirectory = new { type = "string", description = "Target directory for move_type_to_file or move_members_to_partial (default: same as source)" },
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
                        isExtension = new { type = "boolean", description = "Generate as extension method (first parameter gets 'this' modifier). Requires isStatic: true and containing class to be static." },
                        documentation = new { type = "string", description = "XML documentation summary for the member." },
                        body = new { type = "string", description = "Optional method body code" },
                        testAttribute = new { type = "string", description = "Test attribute to add: Fact (xunit), Test (nunit), TestMethod (mstest). Auto-detects if omitted for test classes." },
                        attributes = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    name = new { type = "string", description = "Attribute name (e.g., 'JsonPropertyName', 'HttpGet')" },
                                    arguments = new { type = "array", items = new { type = "string" }, description = "Attribute arguments as strings (e.g., '\"value\"', 'typeof(User)')" }
                                }
                            },
                            description = "Attributes to apply to the member (e.g., [JsonPropertyName(\"user_name\")], [HttpGet(\"{id}\")])"
                        },
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
            // aura_workspace - Unified workspace management
            // =================================================================
            new McpToolDefinition
            {
                Name = "aura_workspace",
                Description = "Manage workspaces: registry CRUD, worktree detection, cache invalidation. (Read/Write)",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        operation = new
                        {
                            type = "string",
                            description = "Workspace operation type",
                            @enum = new[] { "list", "add", "remove", "set_default", "detect_worktree", "invalidate_cache", "status" }
                        },
                        path = new { type = "string", description = "Workspace path (for add, detect_worktree, invalidate_cache, status)" },
                        id = new { type = "string", description = "Workspace ID (for remove, set_default)" },
                        alias = new { type = "string", description = "Short alias (for add)" },
                        tags = new
                        {
                            type = "array",
                            items = new { type = "string" },
                            description = "Tags for categorization (for add)"
                        }
                    },
                    required = new[] { "operation" }
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

            // =================================================================
            // aura_tree - Hierarchical code exploration
            // =================================================================
            new McpToolDefinition
            {
                Name = "aura_tree",
                Description = "Explore codebase hierarchically: list files/types/members, or get full source for a node. (Read)",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        operation = new
                        {
                            type = "string",
                            description = "Tree operation: 'explore' for hierarchical view, 'get_node' for source retrieval",
                            @enum = new[] { "explore", "get_node" }
                        },
                        workspacePath = new { type = "string", description = "Path to the workspace root" },
                        pattern = new { type = "string", description = "Filter pattern for file paths or symbol names (for explore, default: '.')" },
                        maxDepth = new { type = "integer", description = "Maximum tree depth: 1=files, 2=+types, 3=+members (for explore, default: 2)" },
                        detail = new
                        {
                            type = "string",
                            description = "Level of detail in output (for explore)",
                            @enum = new[] { "min", "max" }
                        },
                        nodeId = new { type = "string", description = "Node ID from explore results (for get_node)" }
                    },
                    required = new[] { "workspacePath" }
                }
            },

            // =================================================================
            // aura_docs - Documentation operations
            // =================================================================
            new McpToolDefinition
            {
                Name = "aura_docs",
                Description = "Search documentation with semantic retrieval. Returns relevant documentation chunks with scores and metadata. (Read)",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        operation = new
                        {
                            type = "string",
                            description = "Documentation operation type: 'search' (default), 'list', or 'get'",
                            @enum = new[] { "search", "list", "get" }
                        },
                        query = new { type = "string", description = "The documentation search query (required for search operation)" },
                        id = new { type = "string", description = "Document ID to retrieve (required for get operation)" },
                        category = new { type = "string", description = "Filter by category (for list operation)" },
                        tags = new
                        {
                            type = "array",
                            items = new { type = "string" },
                            description = "Filter by tags (for list operation)"
                        }
                    },
                    required = Array.Empty<string>() // No required fields - depends on operation
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
    /// aura_workspace - Unified workspace management.
    /// Supports: list, add, remove, set_default, detect_worktree, invalidate_cache, status.
    /// </summary>
    private Task<object> WorkspaceAsync(JsonElement? args, CancellationToken ct)
    {
        var operation = args?.GetProperty("operation").GetString()
            ?? throw new ArgumentException("operation is required");

        return operation switch
        {
            // Registry operations (from former aura_workspaces)
            "list" => Task.FromResult(ListWorkspacesOperation()),
            "add" => Task.FromResult(AddWorkspaceOperation(args)),
            "remove" => Task.FromResult(RemoveWorkspaceOperation(args)),
            "set_default" => Task.FromResult(SetDefaultWorkspaceOperation(args)),
            // Worktree operations (require path)
            "detect_worktree" => Task.FromResult(DetectWorktreeOperation(GetRequiredPath(args))),
            "invalidate_cache" => Task.FromResult(InvalidateCacheOperation(GetRequiredPath(args))),
            "status" => Task.FromResult(WorkspaceStatusOperation(GetRequiredPath(args))),
            _ => throw new ArgumentException($"Unknown workspace operation: {operation}")
        };
    }

    private static string GetRequiredPath(JsonElement? args)
    {
        return args?.GetProperty("path").GetString()
            ?? throw new ArgumentException("path is required for this operation");
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

    // =========================================================================
    // aura_tree - Hierarchical code exploration with get_node
    // =========================================================================

    private async Task<object> TreeAsync(JsonElement? args, CancellationToken ct)
    {
        var workspacePath = args?.GetProperty("workspacePath").GetString()
            ?? throw new ArgumentException("workspacePath is required");

        var operation = args?.TryGetProperty("operation", out var opEl) == true
            ? opEl.GetString() ?? "explore"
            : "explore";

        return operation switch
        {
            "explore" => await TreeExploreAsync(args, workspacePath, ct),
            "get_node" => await TreeGetNodeAsync(args, workspacePath, ct),
            _ => throw new ArgumentException($"Unknown tree operation: {operation}")
        };
    }

    private async Task<object> TreeExploreAsync(JsonElement? args, string workspacePath, CancellationToken ct)
    {
        var pattern = args?.TryGetProperty("pattern", out var p) == true ? p.GetString() : ".";
        var maxDepth = args?.TryGetProperty("maxDepth", out var d) == true ? d.GetInt32() : 2;
        var detailStr = args?.TryGetProperty("detail", out var det) == true ? det.GetString() : "min";
        var detail = detailStr == "max" ? TreeDetail.Max : TreeDetail.Min;

        _logger.LogDebug("aura_tree(explore): workspacePath={Path}, pattern={Pattern}, maxDepth={MaxDepth}, detail={Detail}",
            workspacePath, pattern, maxDepth, detailStr);

        var chunks = await _ragService.GetChunksForTreeAsync(workspacePath, pattern, ct);

        var tree = _treeBuilderService.BuildTree(chunks, pattern, maxDepth, detail);

        return new
        {
            root_path = tree.RootPath,
            total_nodes = tree.TotalNodes,
            truncated = tree.Truncated,
            nodes = tree.Nodes.Select(n => SerializeTreeNode(n)).ToList()
        };
    }

    private async Task<object> TreeGetNodeAsync(JsonElement? args, string workspacePath, CancellationToken ct)
    {
        var nodeId = args?.GetProperty("nodeId").GetString()
            ?? throw new ArgumentException("nodeId is required for get_node operation");

        _logger.LogDebug("aura_tree(get_node): workspacePath={Path}, nodeId={NodeId}", workspacePath, nodeId);

        var chunks = await _ragService.GetChunksForTreeAsync(workspacePath, null, ct);

        var node = _treeBuilderService.GetNode(chunks, nodeId);

        if (node is null)
        {
            return new { error = $"Node not found: {nodeId}" };
        }

        return new
        {
            node_id = node.NodeId,
            name = node.Name,
            type = node.Type,
            path = node.Path,
            line_start = node.LineStart,
            line_end = node.LineEnd,
            content = node.Content,
            metadata = node.Metadata is null ? null : new
            {
                signature = node.Metadata.Signature,
                docstring = node.Metadata.Docstring,
                language = node.Metadata.Language
            }
        };
    }

    /// <summary>
    /// aura_docs - Documentation operations: search, list, get.
    /// </summary>
    private async Task<object> DocsAsync(JsonElement? args, CancellationToken ct)
    {
        var operation = args?.TryGetProperty("operation", out var opEl) == true
            ? opEl.GetString() ?? "search"
            : "search";

        return operation switch
        {
            "search" => await DocsSearchAsync(args, ct),
            "list" => DocsListInternal(args),
            "get" => DocsGetInternal(args),
            _ => throw new ArgumentException($"Unknown docs operation: {operation}")
        };
    }

    private async Task<object> DocsSearchAsync(JsonElement? args, CancellationToken ct)
    {
        var query = args?.GetProperty("query").GetString()
            ?? throw new ArgumentException("query is required for search operation");

        _logger.LogDebug("aura_docs(search): query={Query}", query);

        return await _auraDocsTool.SearchDocumentationAsync(query, ct);
    }

    private object DocsListInternal(JsonElement? args)
    {
        string? category = null;
        IReadOnlyList<string>? tags = null;

        if (args.HasValue)
        {
            if (args.Value.TryGetProperty("category", out var categoryEl))
            {
                category = categoryEl.GetString();
            }

            if (args.Value.TryGetProperty("tags", out var tagsEl))
            {
                tags = tagsEl.EnumerateArray()
                    .Select(e => e.GetString() ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
            }
        }

        _logger.LogDebug("aura_docs(list): category={Category}, tags={Tags}", category, tags);

        var docs = _docsService.ListDocuments(category, tags);

        return new
        {
            count = docs.Count,
            docs = docs.Select(d => new
            {
                id = d.Id,
                title = d.Title,
                summary = d.Summary,
                category = d.Category,
                tags = d.Tags
            })
        };
    }

    private object DocsGetInternal(JsonElement? args)
    {
        var id = args?.GetProperty("id").GetString()
            ?? throw new ArgumentException("id is required for get operation");

        _logger.LogDebug("aura_docs(get): id={Id}", id);

        var doc = _docsService.GetDocument(id);

        if (doc is null)
        {
            throw new KeyNotFoundException($"Document with ID '{id}' not found");
        }

        return new
        {
            id = doc.Id,
            title = doc.Title,
            category = doc.Category,
            tags = doc.Tags,
            content = doc.Content,
            last_updated = doc.LastUpdated
        };
    }

    private static object SerializeTreeNode(TreeNode node)
    {
        return new
        {
            node_id = node.NodeId,
            name = node.Name,
            type = node.Type,
            path = node.Path,
            signature = node.Signature,
            line_start = node.LineStart,
            line_end = node.LineEnd,
            children = node.Children?.Select(c => SerializeTreeNode(c)).ToList()
        };
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static List<AttributeInfo> ParseAttributeInfoList(JsonElement attrEl)
    {
        return attrEl.EnumerateArray()
            .Select(attr =>
            {
                var name = attr.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                List<string>? arguments = null;
                if (attr.TryGetProperty("arguments", out var argsEl) && argsEl.ValueKind == JsonValueKind.Array)
                {
                    arguments = argsEl.EnumerateArray()
                        .Select(a => a.GetString())
                        .Where(s => s != null)
                        .Cast<string>()
                        .ToList();
                }
                return new AttributeInfo(name, arguments);
            })
            .ToList();
    }

    private static JsonRpcResponse ErrorResponse(object? id, int code, string message) =>
        new()
        {
            Id = id,
            Error = new JsonRpcError { Code = code, Message = message }
        };

    private static string SerializeResponse(JsonRpcResponse response) =>
        JsonSerializer.Serialize(response, JsonOptions);
}
