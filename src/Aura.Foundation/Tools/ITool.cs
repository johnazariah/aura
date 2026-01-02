namespace Aura.Foundation.Tools;

/// <summary>
/// Strongly-typed tool interface for compile-time safety.
/// Tools implement this to get type-checked inputs and outputs.
/// </summary>
/// <typeparam name="TInput">The input contract type</typeparam>
/// <typeparam name="TOutput">The output contract type</typeparam>
public interface ITool<TInput, TOutput>
    where TInput : class
    where TOutput : class
{
    /// <summary>Unique tool identifier (e.g., "roslyn.list_classes")</summary>
    string ToolId { get; }

    /// <summary>Human-readable name</summary>
    string Name { get; }

    /// <summary>Description for LLM to understand when to use this tool</summary>
    string Description { get; }

    /// <summary>Categories for discovery</summary>
    IReadOnlyList<string> Categories { get; }

    /// <summary>Whether user confirmation is required before execution</summary>
    bool RequiresConfirmation { get; }

    /// <summary>Execute the tool with strongly-typed input</summary>
    Task<ToolResult<TOutput>> ExecuteAsync(TInput input, CancellationToken ct = default);
}

/// <summary>
/// Result of strongly-typed tool execution.
/// </summary>
/// <typeparam name="T">The output type</typeparam>
public record ToolResult<T> where T : class
{
    /// <summary>Whether the tool executed successfully</summary>
    public required bool Success { get; init; }

    /// <summary>Strongly-typed output data (null if failed)</summary>
    public T? Output { get; init; }

    /// <summary>Error message if failed</summary>
    public string? Error { get; init; }

    /// <summary>Execution duration</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Create a success result</summary>
    public static ToolResult<T> Ok(T output, TimeSpan duration = default) =>
        new() { Success = true, Output = output, Duration = duration };

    /// <summary>Create a failure result</summary>
    public static ToolResult<T> Fail(string error, TimeSpan duration = default) =>
        new() { Success = false, Error = error, Duration = duration };

    /// <summary>Convert to untyped ToolResult for compatibility</summary>
    public ToolResult ToUntyped() =>
        new()
        {
            Success = Success,
            Output = Output,
            Error = Error,
            Duration = Duration
        };
}

/// <summary>
/// Base class for strongly-typed tools with common functionality.
/// </summary>
public abstract class TypedToolBase<TInput, TOutput> : ITool<TInput, TOutput>
    where TInput : class
    where TOutput : class
{
    public abstract string ToolId { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public virtual IReadOnlyList<string> Categories => [];
    public virtual bool RequiresConfirmation => false;

    public abstract Task<ToolResult<TOutput>> ExecuteAsync(TInput input, CancellationToken ct = default);

    /// <summary>
    /// Convert this typed tool to a ToolDefinition for registry compatibility.
    /// </summary>
    public ToolDefinition ToToolDefinition()
    {
        return new ToolDefinition
        {
            ToolId = ToolId,
            Name = Name,
            Description = Description,
            Categories = Categories,
            RequiresConfirmation = RequiresConfirmation,
            InputSchema = GenerateInputSchema(),
            Handler = async (input, ct) =>
            {
                // Inject WorkingDirectory into parameters if not already present
                // Use case-insensitive check since LLM might use different casing
                var parameters = input.Parameters;
                var hasWorkingDir = parameters.Keys.Any(k =>
                    k.Equals("workingDirectory", StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(input.WorkingDirectory) && !hasWorkingDir)
                {
                    var mutableParams = new Dictionary<string, object?>(parameters)
                    {
                        ["workingDirectory"] = input.WorkingDirectory
                    };
                    parameters = mutableParams;
                }

                // Deserialize from dictionary to typed input
                var typedInput = DeserializeInput(parameters);
                var result = await ExecuteAsync(typedInput, ct);
                return result.ToUntyped();
            }
        };
    }

    /// <summary>
    /// Generate JSON schema from TInput type for LLM function calling.
    /// Override for custom schema.
    /// </summary>
    protected virtual string? GenerateInputSchema()
    {
        // Default implementation generates basic schema from properties
        var inputType = typeof(TInput);
        var properties = inputType.GetProperties()
            .Where(p => p.CanRead)
            .Select(p => $"\"{ToCamelCase(p.Name)}\": {{ \"type\": \"{GetJsonType(p.PropertyType)}\" }}")
            .ToList();

        var required = inputType.GetProperties()
            .Where(p => p.CanRead && IsRequired(p))
            .Select(p => $"\"{ToCamelCase(p.Name)}\"")
            .ToList();

        return $$"""
        {
            "type": "object",
            "properties": {
                {{string.Join(",\n            ", properties)}}
            },
            "required": [{{string.Join(", ", required)}}]
        }
        """;
    }

    /// <summary>
    /// Deserialize dictionary parameters to typed input.
    /// Override for custom deserialization.
    /// </summary>
    protected virtual TInput DeserializeInput(IReadOnlyDictionary<string, object?> parameters)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(parameters);
        return System.Text.Json.JsonSerializer.Deserialize<TInput>(json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException($"Failed to deserialize input to {typeof(TInput).Name}");
    }

    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];

    private static string GetJsonType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        return underlying switch
        {
            _ when underlying == typeof(string) => "string",
            _ when underlying == typeof(int) || underlying == typeof(long) => "integer",
            _ when underlying == typeof(float) || underlying == typeof(double) || underlying == typeof(decimal) => "number",
            _ when underlying == typeof(bool) => "boolean",
            _ when underlying.IsArray || typeof(System.Collections.IEnumerable).IsAssignableFrom(underlying) => "array",
            _ => "object"
        };
    }

    private static bool IsRequired(System.Reflection.PropertyInfo prop)
    {
        // Check for required keyword (C# 11+) or RequiredAttribute
        var nullabilityContext = new System.Reflection.NullabilityInfoContext();
        var nullabilityInfo = nullabilityContext.Create(prop);
        return nullabilityInfo.WriteState == System.Reflection.NullabilityState.NotNull;
    }
}
