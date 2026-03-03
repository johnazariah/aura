namespace Aura.Foundation.Agents;

/// <summary>
/// Minimal chat role enum used by LLM interfaces.
/// </summary>
public enum ChatRole
{
    System,
    User,
    Assistant,
    Tool,
}

/// <summary>
/// Minimal chat message contract used by LLM providers.
/// </summary>
/// <param name="Role">Message role.</param>
/// <param name="Content">Message content.</param>
public sealed record ChatMessage(ChatRole Role, string Content);
