namespace Aura.Foundation;

using CSharpFunctionalExtensions;

/// <summary>
/// Contract for agent execution.
/// </summary>
public interface IAgent
{
    /// <summary>
    /// Unique identifier for this agent.
    /// </summary>
    string AgentId { get; }
    
    /// <summary>
    /// Execute the agent with the given context.
    /// </summary>
    Task<Result<AgentResult, AgentError>> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Context passed to agent execution.
/// </summary>
public record AgentContext
{
    /// <summary>
    /// The prompt or instruction for the agent.
    /// </summary>
    public required string Prompt { get; init; }
    
    /// <summary>
    /// Optional system prompt override.
    /// </summary>
    public string? SystemPrompt { get; init; }
    
    /// <summary>
    /// Additional context data.
    /// </summary>
    public IReadOnlyDictionary<string, object> Data { get; init; } = 
        new Dictionary<string, object>();
    
    /// <summary>
    /// Conversation history for chat-based agents.
    /// </summary>
    public IReadOnlyList<ChatMessage> History { get; init; } = [];
}

/// <summary>
/// Result from agent execution.
/// </summary>
public record AgentResult
{
    /// <summary>
    /// The primary output from the agent.
    /// </summary>
    public required string Output { get; init; }
    
    /// <summary>
    /// Structured data extracted from the output (if any).
    /// </summary>
    public IReadOnlyDictionary<string, object>? StructuredOutput { get; init; }
    
    /// <summary>
    /// Token usage information.
    /// </summary>
    public TokenUsage? Usage { get; init; }
}

/// <summary>
/// Error from agent execution.
/// </summary>
public record AgentError
{
    public required string Message { get; init; }
    public AgentErrorType Type { get; init; } = AgentErrorType.Unknown;
    public Exception? Exception { get; init; }
    
    public static AgentError NotFound(string agentId) => new()
    {
        Message = $"Agent '{agentId}' not found",
        Type = AgentErrorType.NotFound
    };
    
    public static AgentError LlmError(string message, Exception? ex = null) => new()
    {
        Message = message,
        Type = AgentErrorType.LlmError,
        Exception = ex
    };
    
    public static AgentError Cancelled() => new()
    {
        Message = "Operation was cancelled",
        Type = AgentErrorType.Cancelled
    };
}

public enum AgentErrorType
{
    Unknown,
    NotFound,
    LlmError,
    Timeout,
    Cancelled,
    InvalidInput
}

/// <summary>
/// Token usage information.
/// </summary>
public record TokenUsage(int PromptTokens, int CompletionTokens)
{
    public int TotalTokens => PromptTokens + CompletionTokens;
}

/// <summary>
/// A message in a conversation.
/// </summary>
public record ChatMessage(ChatRole Role, string Content);

public enum ChatRole
{
    System,
    User,
    Assistant
}
