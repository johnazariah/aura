namespace Aura.Foundation.Llm;

/// <summary>
/// Known LLM provider identifiers used for configuration and routing. Use these constants instead of magic strings when configuring LLM providers.
/// </summary>
public static class LlmProviders
{
    /// <summary>
    /// Ollama local LLM provider. Default for development.
    /// </summary>
    public const string Ollama = "ollama";

    /// <summary>
    /// OpenAI API provider.
    /// </summary>
    public const string OpenAI = "openai";

    /// <summary>
    /// Azure OpenAI Service provider.
    /// </summary>
    public const string AzureOpenAI = "azureopenai";

    /// <summary>
    /// Stub provider for testing. Returns predefined responses.
    /// </summary>
    public const string Stub = "stub";
}
