// <copyright file="AgentContext.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Agents;

using Aura.Foundation.Rag;

/// <summary>
/// Context passed to an agent during execution.
/// </summary>
/// <param name="Prompt">The user prompt or task description.</param>
/// <param name="ConversationHistory">Previous messages in the conversation.</param>
/// <param name="WorkspacePath">Optional workspace path for file operations (working directory).</param>
/// <param name="Properties">Additional properties for agent-specific data.</param>
public sealed record AgentContext(
    string Prompt,
    IReadOnlyList<ChatMessage>? ConversationHistory = null,
    string? WorkspacePath = null,
    IReadOnlyDictionary<string, object>? Properties = null)
{
    /// <summary>
    /// Gets the conversation history.
    /// </summary>
    public IReadOnlyList<ChatMessage> ConversationHistory { get; } = ConversationHistory ?? [];

    /// <summary>
    /// Gets additional properties.
    /// </summary>
    public IReadOnlyDictionary<string, object> Properties { get; } = Properties ?? new Dictionary<string, object>();

    /// <summary>
    /// Gets the RAG context - relevant content retrieved from the indexed knowledge base.
    /// </summary>
    public string? RagContext { get; init; }

    /// <summary>
    /// Gets the individual RAG results with metadata (source, score, etc.).
    /// </summary>
    public IReadOnlyList<RagResult>? RagResults { get; init; }

    /// <summary>
    /// Gets the Code Graph context - structural code information from the semantic index.
    /// </summary>
    public string? CodeGraphContext { get; init; }

    /// <summary>
    /// Gets the relevant code nodes from the Code Graph.
    /// </summary>
    public IReadOnlyList<Data.Entities.CodeNode>? RelevantNodes { get; init; }

    /// <summary>
    /// Gets the relevant code edges (relationships) from the Code Graph.
    /// </summary>
    public IReadOnlyList<Data.Entities.CodeEdge>? RelevantEdges { get; init; }

    /// <summary>
    /// Creates a context with just a prompt.
    /// </summary>
    /// <param name="prompt">The prompt text.</param>
    /// <returns>A new context instance.</returns>
    public static AgentContext FromPrompt(string prompt) => new(prompt);

    /// <summary>
    /// Creates a context with a prompt and workspace.
    /// </summary>
    /// <param name="prompt">The prompt text.</param>
    /// <param name="workspacePath">The workspace path.</param>
    /// <returns>A new context instance.</returns>
    public static AgentContext FromPromptAndWorkspace(string prompt, string workspacePath) =>
        new(prompt, WorkspacePath: workspacePath);
}

/// <summary>
/// A message in a conversation.
/// </summary>
/// <param name="Role">The role of the message sender.</param>
/// <param name="Content">The message content.</param>
public sealed record ChatMessage(ChatRole Role, string Content);

/// <summary>
/// Roles for chat messages.
/// </summary>
public enum ChatRole
{
    /// <summary>System message (instructions).</summary>
    System,

    /// <summary>User message (input).</summary>
    User,

    /// <summary>Assistant message (LLM response).</summary>
    Assistant,

    /// <summary>Tool result message.</summary>
    Tool,
}
