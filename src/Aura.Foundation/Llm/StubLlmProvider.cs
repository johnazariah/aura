// <copyright file="StubLlmProvider.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Llm;

using Aura.Foundation.Agents;
using Microsoft.Extensions.Logging;

/// <summary>
/// Stub LLM provider for testing and development.
/// Returns predefined responses without calling an actual LLM.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="StubLlmProvider"/> class.
/// </remarks>
/// <param name="logger">Logger instance.</param>
public sealed class StubLlmProvider(ILogger<StubLlmProvider> logger) : ILlmProvider
{
    private readonly ILogger<StubLlmProvider> _logger = logger;
    private readonly List<ModelInfo> _models =
        [
            new ModelInfo("stub-model", 1_000_000, DateTimeOffset.UtcNow),
            new ModelInfo("qwen2.5-coder:7b", 4_000_000_000, DateTimeOffset.UtcNow),
        ];

    /// <inheritdoc/>
    public string ProviderId => "stub";

    /// <inheritdoc/>
    public Task<LlmResponse> GenerateAsync(
        string? model,
        string prompt,
        double temperature = 0.7,
        CancellationToken cancellationToken = default)
    {
        var effectiveModel = model ?? "stub-model";
        _logger.LogDebug(
            "Stub generate: model={Model}, prompt_length={PromptLength}, temp={Temperature}",
            effectiveModel, prompt.Length, temperature);

        cancellationToken.ThrowIfCancellationRequested();

        var response = new LlmResponse(
            Content: $"[Stub response to: {TruncatePrompt(prompt)}]",
            TokensUsed: prompt.Length / 4, // Rough token estimate
            Model: effectiveModel,
            FinishReason: "stop");

        return Task.FromResult(response);
    }

    /// <inheritdoc/>
    public Task<LlmResponse> ChatAsync(
        string? model,
        IReadOnlyList<ChatMessage> messages,
        double temperature = 0.7,
        CancellationToken cancellationToken = default)
    {
        var effectiveModel = model ?? "stub-model";
        _logger.LogDebug(
            "Stub chat: model={Model}, messages={MessageCount}, temp={Temperature}",
            effectiveModel, messages.Count, temperature);

        cancellationToken.ThrowIfCancellationRequested();

        var lastUserMessage = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Content ?? "empty";
        var tokenCount = messages.Sum(m => m.Content.Length) / 4;

        var response = new LlmResponse(
            Content: $"[Stub chat response to: {TruncatePrompt(lastUserMessage)}]",
            TokensUsed: tokenCount,
            Model: effectiveModel,
            FinishReason: "stop");

        return Task.FromResult(response);
    }

    /// <inheritdoc/>
    public Task<bool> IsModelAvailableAsync(string model, CancellationToken cancellationToken = default)
    {
        // Stub provider accepts any model
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ModelInfo>>(_models);
    }

    private static string TruncatePrompt(string prompt, int maxLength = 50)
    {
        if (prompt.Length <= maxLength)
        {
            return prompt;
        }

        return prompt[..maxLength] + "...";
    }
}
