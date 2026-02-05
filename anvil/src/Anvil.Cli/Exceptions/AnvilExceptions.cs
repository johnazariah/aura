namespace Anvil.Cli.Exceptions;

/// <summary>
/// Base exception for all Anvil-specific errors.
/// </summary>
public abstract class AnvilException : Exception
{
    protected AnvilException(string message) : base(message) { }
    protected AnvilException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when Aura API is not available.
/// </summary>
public sealed class AuraUnavailableException : AnvilException
{
    public string Url { get; }

    public AuraUnavailableException(string url)
        : base($"Aura API is not available at {url}")
    {
        Url = url;
    }

    public AuraUnavailableException(string url, Exception innerException)
        : base($"Aura API is not available at {url}", innerException)
    {
        Url = url;
    }
}

/// <summary>
/// Thrown when an Aura API call fails.
/// </summary>
public sealed class AuraApiException : AnvilException
{
    public int StatusCode { get; }
    public string? ResponseBody { get; }

    public AuraApiException(int statusCode, string message)
        : base($"Aura API error ({statusCode}): {message}")
    {
        StatusCode = statusCode;
    }

    public AuraApiException(int statusCode, string message, string? responseBody)
        : base($"Aura API error ({statusCode}): {message}")
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}

/// <summary>
/// Thrown when a story is not found in Aura.
/// </summary>
public sealed class StoryNotFoundException : AnvilException
{
    public Guid StoryId { get; }

    public StoryNotFoundException(Guid storyId)
        : base($"Story not found: {storyId}")
    {
        StoryId = storyId;
    }
}

/// <summary>
/// Thrown when a story execution times out.
/// </summary>
public sealed class StoryTimeoutException : AnvilException
{
    public Guid StoryId { get; }
    public TimeSpan Timeout { get; }

    public StoryTimeoutException(Guid storyId, TimeSpan timeout)
        : base($"Story {storyId} timed out after {timeout.TotalSeconds:F0} seconds")
    {
        StoryId = storyId;
        Timeout = timeout;
    }
}

/// <summary>
/// Thrown when a scenario file is not found.
/// </summary>
public sealed class ScenarioNotFoundException : AnvilException
{
    public string Path { get; }

    public ScenarioNotFoundException(string path)
        : base($"Scenario file not found: {path}")
    {
        Path = path;
    }
}

/// <summary>
/// Thrown when a scenario YAML file cannot be parsed.
/// </summary>
public sealed class ScenarioParseException : AnvilException
{
    public string Path { get; }

    public ScenarioParseException(string path, string details)
        : base($"Failed to parse scenario file '{path}': {details}")
    {
        Path = path;
    }

    public ScenarioParseException(string path, Exception innerException)
        : base($"Failed to parse scenario file '{path}': {innerException.Message}", innerException)
    {
        Path = path;
    }
}

/// <summary>
/// Thrown when a scenario fails validation (missing required fields, invalid values).
/// </summary>
public sealed class ScenarioValidationException : AnvilException
{
    public string Path { get; }
    public IReadOnlyList<string> Errors { get; }

    public ScenarioValidationException(string path, IReadOnlyList<string> errors)
        : base($"Scenario validation failed for '{path}': {string.Join("; ", errors)}")
    {
        Path = path;
        Errors = errors;
    }

    public ScenarioValidationException(string path, string error)
        : this(path, new[] { error })
    {
    }
}
