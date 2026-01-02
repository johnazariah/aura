using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Aura.Foundation.Tools;

/// <summary>
/// Default implementation of the tool registry.
/// </summary>
public class ToolRegistry(ILogger<ToolRegistry> logger) : IToolRegistry
{
    private readonly ConcurrentDictionary<string, ToolDefinition> _tools = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<ToolRegistry> _logger = logger;

    public IReadOnlyList<ToolDefinition> GetAllTools() =>
        _tools.Values.ToList();

    public ToolDefinition? GetTool(string toolId) =>
        _tools.TryGetValue(toolId, out var tool) ? tool : null;

    public IReadOnlyList<ToolDefinition> GetByCategory(string category) =>
        _tools.Values
            .Where(t => t.Categories.Contains(category, StringComparer.OrdinalIgnoreCase))
            .ToList();

    public async Task<ToolResult> ExecuteAsync(ToolInput input, CancellationToken ct = default)
    {
        if (!_tools.TryGetValue(input.ToolId, out var tool))
        {
            _logger.LogWarning("Tool not found: {ToolId}", input.ToolId);
            return ToolResult.Fail($"Tool '{input.ToolId}' not found");
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogDebug("Executing tool {ToolId}", input.ToolId);
            var result = await tool.Handler(input, ct);
            stopwatch.Stop();

            _logger.LogDebug("Tool {ToolId} completed in {Duration}ms, success={Success}",
                input.ToolId, stopwatch.ElapsedMilliseconds, result.Success);

            return result with { Duration = stopwatch.Elapsed };
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogInformation("Tool {ToolId} cancelled", input.ToolId);
            return ToolResult.Fail("Operation cancelled", stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Tool {ToolId} failed", input.ToolId);
            return ToolResult.Fail(ex.Message, stopwatch.Elapsed);
        }
    }

    public void RegisterTool(ToolDefinition tool)
    {
        if (_tools.TryAdd(tool.ToolId, tool))
        {
            _logger.LogInformation("Registered tool: {ToolId}", tool.ToolId);
        }
        else
        {
            _tools[tool.ToolId] = tool;
            _logger.LogInformation("Updated tool: {ToolId}", tool.ToolId);
        }
    }

    public void RegisterTool<TInput, TOutput>(ITool<TInput, TOutput> tool)
        where TInput : class
        where TOutput : class
    {
        // Convert typed tool to ToolDefinition
        if (tool is TypedToolBase<TInput, TOutput> typedTool)
        {
            RegisterTool(typedTool.ToToolDefinition());
        }
        else
        {
            // Manual conversion for non-base implementations
            var definition = new ToolDefinition
            {
                ToolId = tool.ToolId,
                Name = tool.Name,
                Description = tool.Description,
                Categories = tool.Categories,
                RequiresConfirmation = tool.RequiresConfirmation,
                Handler = async (input, ct) =>
                {
                    // Deserialize to typed input
                    var json = System.Text.Json.JsonSerializer.Serialize(input.Parameters);
                    var typedInput = System.Text.Json.JsonSerializer.Deserialize<TInput>(json,
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (typedInput is null)
                    {
                        return ToolResult.Fail($"Failed to deserialize input to {typeof(TInput).Name}");
                    }

                    var result = await tool.ExecuteAsync(typedInput, ct);
                    return result.ToUntyped();
                }
            };

            RegisterTool(definition);
        }
    }

    public bool UnregisterTool(string toolId)
    {
        var removed = _tools.TryRemove(toolId, out _);
        if (removed)
        {
            _logger.LogInformation("Unregistered tool: {ToolId}", toolId);
        }
        return removed;
    }

    public bool HasTool(string toolId) => _tools.ContainsKey(toolId);

    public string GetToolDescriptionsForPrompt()
    {
        var sb = new StringBuilder();

        foreach (var tool in _tools.Values.OrderBy(t => t.ToolId))
        {
            sb.AppendLine($"- **{tool.ToolId}**: {tool.Description}");

            if (tool.InputSchema is not null)
            {
                sb.AppendLine($"  Input schema: {tool.InputSchema}");
            }

            if (tool.Categories.Count > 0)
            {
                sb.AppendLine($"  Categories: {string.Join(", ", tool.Categories)}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}
