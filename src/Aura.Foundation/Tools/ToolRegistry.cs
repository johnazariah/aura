using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Aura.Foundation.Tools;

/// <summary>
/// Default implementation of the tool registry.
/// </summary>
public class ToolRegistry : IToolRegistry
{
    private readonly ConcurrentDictionary<string, ToolDefinition> _tools = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<ToolRegistry> _logger;

    public ToolRegistry(ILogger<ToolRegistry> logger)
    {
        _logger = logger;
    }

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
}
