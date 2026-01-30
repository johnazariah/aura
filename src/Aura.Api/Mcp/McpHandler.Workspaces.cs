using System.Text.Json;

namespace Aura.Api.Mcp;

public sealed partial class McpHandler
{
    // =========================================================================
    // aura_workspaces - Workspace registry management for multi-workspace queries
    // =========================================================================

    /// <summary>
    /// aura_workspaces - Manage the workspace registry.
    /// Supports: list, add, remove, set_default.
    /// </summary>
    private Task<object> WorkspacesAsync(JsonElement? args, CancellationToken ct)
    {
        var operation = args?.GetProperty("operation").GetString()
            ?? throw new ArgumentException("operation is required");

        return operation switch
        {
            "list" => Task.FromResult(ListWorkspacesOperation()),
            "add" => Task.FromResult(AddWorkspaceOperation(args)),
            "remove" => Task.FromResult(RemoveWorkspaceOperation(args)),
            "set_default" => Task.FromResult(SetDefaultWorkspaceOperation(args)),
            _ => throw new ArgumentException($"Unknown workspaces operation: {operation}")
        };
    }

    private object ListWorkspacesOperation()
    {
        var workspaces = _workspaceRegistryService.ListWorkspaces();
        var defaultWorkspace = _workspaceRegistryService.GetDefaultWorkspace();

        return new
        {
            workspaces = workspaces.Select(w => new
            {
                id = w.Id,
                path = w.Path,
                alias = w.Alias,
                tags = w.Tags,
                indexed = w.Indexed,
                chunkCount = w.ChunkCount,
                lastIndexed = w.LastIndexed?.ToString("o")
            }),
            @default = defaultWorkspace?.Id,
            count = workspaces.Count
        };
    }

    private object AddWorkspaceOperation(JsonElement? args)
    {
        var path = args?.GetProperty("path").GetString()
            ?? throw new ArgumentException("path is required for add operation");

        string? alias = null;
        if (args.Value.TryGetProperty("alias", out var aliasEl))
        {
            alias = aliasEl.GetString();
        }

        List<string>? tags = null;
        if (args.Value.TryGetProperty("tags", out var tagsEl))
        {
            tags = tagsEl.EnumerateArray()
                .Select(e => e.GetString() ?? string.Empty)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }

        try
        {
            var workspace = _workspaceRegistryService.AddWorkspace(path, alias, tags);
            return new
            {
                success = true,
                message = $"Workspace added: {workspace.Id}",
                workspace = new
                {
                    id = workspace.Id,
                    path = workspace.Path,
                    alias = workspace.Alias,
                    tags = workspace.Tags
                }
            };
        }
        catch (InvalidOperationException ex)
        {
            return new
            {
                success = false,
                error = ex.Message
            };
        }
    }

    private object RemoveWorkspaceOperation(JsonElement? args)
    {
        var id = args?.GetProperty("id").GetString()
            ?? throw new ArgumentException("id is required for remove operation");

        var removed = _workspaceRegistryService.RemoveWorkspace(id);

        return new
        {
            success = removed,
            message = removed ? $"Workspace removed: {id}" : $"Workspace not found: {id}"
        };
    }

    private object SetDefaultWorkspaceOperation(JsonElement? args)
    {
        var id = args?.GetProperty("id").GetString()
            ?? throw new ArgumentException("id is required for set_default operation");

        var set = _workspaceRegistryService.SetDefault(id);

        return new
        {
            success = set,
            message = set ? $"Default workspace set: {id}" : $"Workspace not found: {id}"
        };
    }
}
