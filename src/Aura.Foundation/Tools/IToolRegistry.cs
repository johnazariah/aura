namespace Aura.Foundation.Tools;

/// <summary>
/// Registry for tools that agents can invoke.
/// </summary>
public interface IToolRegistry
{
    /// <summary>Get all registered tools</summary>
    IReadOnlyList<ToolDefinition> GetAllTools();

    /// <summary>Get a tool by ID</summary>
    ToolDefinition? GetTool(string toolId);

    /// <summary>Get tools by category</summary>
    IReadOnlyList<ToolDefinition> GetByCategory(string category);

    /// <summary>Execute a tool</summary>
    Task<ToolResult> ExecuteAsync(
        ToolInput input,
        CancellationToken ct = default);

    /// <summary>Register a tool</summary>
    void RegisterTool(ToolDefinition tool);

    /// <summary>Register a strongly-typed tool</summary>
    void RegisterTool<TInput, TOutput>(ITool<TInput, TOutput> tool)
        where TInput : class
        where TOutput : class;

    /// <summary>Unregister a tool</summary>
    bool UnregisterTool(string toolId);

    /// <summary>Check if a tool exists</summary>
    bool HasTool(string toolId);

    /// <summary>Get tool descriptions formatted for LLM prompts</summary>
    string GetToolDescriptionsForPrompt();
}