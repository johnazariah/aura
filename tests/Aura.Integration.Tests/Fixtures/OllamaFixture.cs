// <copyright file="OllamaFixture.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Integration.Tests.Fixtures;

using System.Net.Http.Json;

/// <summary>
/// Shared fixture that checks Ollama availability and provides a configured HttpClient.
/// Tests using this fixture will be skipped if Ollama is not running.
/// </summary>
public sealed class OllamaFixture : IAsyncLifetime
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaFixture"/> class.
    /// </summary>
    public OllamaFixture()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(120),
        };
    }

    /// <summary>
    /// Gets the base URL for Ollama API.
    /// </summary>
    public static string BaseUrl =>
        Environment.GetEnvironmentVariable("OLLAMA_BASE_URL") ?? "http://localhost:11434";

    /// <summary>
    /// Gets a value indicating whether Ollama is available.
    /// </summary>
    public bool IsAvailable { get; private set; }

    /// <summary>
    /// Gets the list of available model names.
    /// </summary>
    public IReadOnlyList<string> AvailableModels { get; private set; } = [];

    /// <summary>
    /// Gets the skip reason if Ollama is not available.
    /// </summary>
    public string? SkipReason { get; private set; }

    /// <summary>
    /// Gets the HttpClient configured for Ollama.
    /// </summary>
    public HttpClient HttpClient => _httpClient;

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        try
        {
            // Check if Ollama is running by fetching tags
            var response = await _httpClient.GetAsync("/api/tags").ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                SkipReason = $"Ollama returned status {response.StatusCode}";
                return;
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>().ConfigureAwait(false);
            AvailableModels = result?.Models?.Select(m => m.Name).ToList() ?? [];

            if (AvailableModels.Count == 0)
            {
                SkipReason = "No models available in Ollama. Run 'ollama pull llama3.2:3b' to install a model.";
                return;
            }

            IsAvailable = true;
        }
        catch (HttpRequestException ex)
        {
            SkipReason = $"Ollama not reachable at {BaseUrl}: {ex.Message}";
        }
        catch (TaskCanceledException)
        {
            SkipReason = $"Ollama connection timed out at {BaseUrl}";
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _httpClient.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Checks if a specific model is available.
    /// </summary>
    /// <param name="modelName">The model name to check.</param>
    /// <returns>True if the model is available.</returns>
    public bool HasModel(string modelName)
    {
        return AvailableModels.Any(m =>
            m.Equals(modelName, StringComparison.OrdinalIgnoreCase) ||
            m.StartsWith(modelName + ":", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets the full model name including tag if available.
    /// </summary>
    /// <param name="baseName">The base model name (e.g., "llama3.2").</param>
    /// <returns>The full model name with tag, or null if not found.</returns>
    public string? GetModelName(string baseName)
    {
        return AvailableModels.FirstOrDefault(m =>
            m.Equals(baseName, StringComparison.OrdinalIgnoreCase) ||
            m.StartsWith(baseName + ":", StringComparison.OrdinalIgnoreCase));
    }

    private sealed record OllamaTagsResponse(List<OllamaModel>? Models);

    private sealed record OllamaModel(string Name, string? ModifiedAt, long? Size);
}

/// <summary>
/// Collection definition for Ollama fixture sharing.
/// </summary>
[CollectionDefinition("Ollama")]
public sealed class OllamaCollection : ICollectionFixture<OllamaFixture>
{
}
