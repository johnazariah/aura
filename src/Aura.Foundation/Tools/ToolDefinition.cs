namespace Aura.Foundation.Tools;

/// <summary>
/// Defines a tool that agents can invoke.
/// </summary>
public record ToolDefinition
{
    /// <summary>Unique identifier (e.g., "file.read", "shell.execute")</summary>
    public required string ToolId { get; init; }

    /// <summary>Human-readable name</summary>
    public required string Name { get; init; }

    /// <summary>Description of what the tool does</summary>
    public required string Description { get; init; }

    /// <summary>JSON schema for input parameters (for LLM function calling)</summary>
    public string? InputSchema { get; init; }

    /// <summary>Categories/tags for discovery</summary>
    public IReadOnlyList<string> Categories { get; init; } = [];

    /// <summary>Whether the tool requires user confirmation before execution</summary>
    public bool RequiresConfirmation { get; init; }

    /// <summary>The handler that executes the tool</summary>
    public required Func<ToolInput, CancellationToken, Task<ToolResult>> Handler { get; init; }
}

/// <summary>
/// Input for tool execution.
/// </summary>
public record ToolInput
{
    /// <summary>The tool being invoked</summary>
    public required string ToolId { get; init; }

    /// <summary>Working directory for the tool</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>Named parameters for the tool</summary>
    public IReadOnlyDictionary<string, object?> Parameters { get; init; } =
        new Dictionary<string, object?>();

    /// <summary>Get a parameter value with type conversion</summary>
    public T? GetParameter<T>(string name, T? defaultValue = default)
    {
        if (!Parameters.TryGetValue(name, out var value) || value is null)
            return defaultValue;

        if (value is T typed)
            return typed;

        // Handle JsonElement from JSON deserialization
        if (value is System.Text.Json.JsonElement jsonElement)
        {
            return ConvertJsonElement<T>(jsonElement, defaultValue);
        }

        // Handle numeric conversions (e.g., long to int?) - Convert.ChangeType doesn't work with Nullable types
        var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        if (targetType.IsPrimitive && value is IConvertible)
        {
            try
            {
                var converted = Convert.ChangeType(value, targetType);
                return (T)converted;
            }
            catch
            {
                return defaultValue;
            }
        }

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>Get a required parameter, throwing if missing</summary>
    public T GetRequiredParameter<T>(string name)
    {
        if (!Parameters.TryGetValue(name, out var value) || value is null)
            throw new ArgumentException($"Required parameter '{name}' is missing");

        if (value is T typed)
            return typed;

        // Handle JsonElement from JSON deserialization
        if (value is System.Text.Json.JsonElement jsonElement)
        {
            var result = ConvertJsonElement<T>(jsonElement, default);
            if (result is null)
                throw new ArgumentException($"Cannot convert parameter '{name}' to {typeof(T).Name}");
            return result;
        }

        return (T)Convert.ChangeType(value, typeof(T));
    }

    private static T? ConvertJsonElement<T>(System.Text.Json.JsonElement element, T? defaultValue)
    {
        try
        {
            // Use System.Text.Json to deserialize the JsonElement to the target type
            return System.Text.Json.JsonSerializer.Deserialize<T>(element.GetRawText());
        }
        catch
        {
            return defaultValue;
        }
    }
}

/// <summary>
/// Result of tool execution.
/// </summary>
public record ToolResult
{
    /// <summary>Whether the tool executed successfully</summary>
    public required bool Success { get; init; }

    /// <summary>Output data from the tool</summary>
    public object? Output { get; init; }

    /// <summary>Error message if failed</summary>
    public string? Error { get; init; }

    /// <summary>Execution duration</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Create a success result</summary>
    public static ToolResult Ok(object? output = null, TimeSpan duration = default) =>
        new() { Success = true, Output = output, Duration = duration };

    /// <summary>Create a failure result</summary>
    public static ToolResult Fail(string error, TimeSpan duration = default) =>
        new() { Success = false, Error = error, Duration = duration };
}
