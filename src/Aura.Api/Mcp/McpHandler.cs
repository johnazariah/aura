// <copyright file="McpHandler.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Mcp;

using System.Text.Json;
using Aura.Foundation.Rag;
using Aura.Module.Developer.Data.Entities;
using Aura.Module.Developer.Services;
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
    private readonly ILogger<McpHandler> _logger;

    private readonly Dictionary<string, Func<JsonElement?, CancellationToken, Task<object>>> _tools;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpHandler"/> class.
    /// </summary>
    public McpHandler(
        IRagService ragService,
        ICodeGraphService graphService,
        IWorkflowService workflowService,
        ILogger<McpHandler> logger)
    {
        _ragService = ragService;
        _graphService = graphService;
        _workflowService = workflowService;
        _logger = logger;

        _tools = new Dictionary<string, Func<JsonElement?, CancellationToken, Task<object>>>
        {
            ["aura_search_code"] = SearchCodeAsync,
            ["aura_find_implementations"] = FindImplementationsAsync,
            ["aura_find_callers"] = FindCallersAsync,
            ["aura_get_type_members"] = GetTypeMembersAsync,
            ["aura_list_stories"] = ListStoriesAsync,
            ["aura_get_story_context"] = GetStoryContextAsync,
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
                repositoryPath = w.RepositoryPath,
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
