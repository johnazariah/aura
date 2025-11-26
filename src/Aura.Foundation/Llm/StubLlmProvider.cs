// <copyright file="StubLlmProvider.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Llm;

using Aura.Foundation.Agents;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Stub LLM provider for testing and development.
/// Returns predefined responses without calling an actual LLM.
/// </summary>
public sealed class StubLlmProvider : ILlmProvider
{
    private readonly ILogger<StubLlmProvider> _logger;
    private readonly List<ModelInfo> _models;

    /// <summary>
    /// Initializes a new instance of the <see cref="StubLlmProvider"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public StubLlmProvider(ILogger<StubLlmProvider> logger)
    {
        _logger = logger;
        _models =
        [
            new ModelInfo("stub-model", 1_000_000, DateTimeOffset.UtcNow),
            new ModelInfo("qwen2.5-coder:7b", 4_000_000_000, DateTimeOffset.UtcNow),
        ];
    }

    /// <inheritdoc/>
    public string ProviderId => "stub";

    /// <inheritdoc/>
    public Task<Result<LlmResponse, LlmError>> GenerateAsync(
        string model,
        string prompt,
        double temperature = 0.7,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Stub generate: model={Model}, prompt_length={PromptLength}, temp={Temperature}",
            model, prompt.Length, temperature);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result.Failure<LlmResponse, LlmError>(LlmError.Cancelled()));
        }

        var response = new LlmResponse(
            Content: $"[Stub response to: {TruncatePrompt(prompt)}]",
            TokensUsed: prompt.Length / 4, // Rough token estimate
            Model: model,
            FinishReason: "stop");

        return Task.FromResult(Result.Success<LlmResponse, LlmError>(response));
    }

    /// <inheritdoc/>
    public Task<Result<LlmResponse, LlmError>> ChatAsync(
        string model,
        IReadOnlyList<ChatMessage> messages,
        double temperature = 0.7,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Stub chat: model={Model}, messages={MessageCount}, temp={Temperature}",
            model, messages.Count, temperature);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result.Failure<LlmResponse, LlmError>(LlmError.Cancelled()));
        }

        var lastUserMessage = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Content ?? "empty";
        var tokenCount = messages.Sum(m => m.Content.Length) / 4;

        var response = new LlmResponse(
            Content: $"[Stub chat response to: {TruncatePrompt(lastUserMessage)}]",
            TokensUsed: tokenCount,
            Model: model,
            FinishReason: "stop");

        return Task.FromResult(Result.Success<LlmResponse, LlmError>(response));
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
