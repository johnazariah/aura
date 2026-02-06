using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Anvil.Cli.Exceptions;
using Anvil.Cli.Models;
using Microsoft.Extensions.Logging;

namespace Anvil.Cli.Adapters;

/// <summary>
/// HTTP client for communicating with the Aura API.
/// </summary>
public sealed class AuraClient(
    HttpClient httpClient,
    ILogger<AuraClient> logger) : IAuraClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <inheritdoc />
    public async Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        try
        {
            logger.LogDebug("Checking Aura health at {BaseAddress}", httpClient.BaseAddress);
            var response = await httpClient.GetAsync("/health", ct);
            var isHealthy = response.IsSuccessStatusCode;
            logger.LogDebug("Aura health check: {Status}", isHealthy ? "Healthy" : "Unhealthy");
            return isHealthy;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Aura health check failed");
            throw new AuraUnavailableException(httpClient.BaseAddress?.ToString() ?? "unknown", ex);
        }
    }

    /// <inheritdoc />
    public async Task<StoryResponse> CreateStoryAsync(CreateStoryRequest request, CancellationToken ct = default)
    {
        logger.LogDebug("Creating story: {Title}", request.Title);
        var response = await SendAsync<StoryResponse>(
            HttpMethod.Post,
            "/api/developer/stories",
            request,
            ct);
        logger.LogInformation("Created story {Id}: {Title}", response.Id, response.Title);
        return response;
    }

    /// <inheritdoc />
    public async Task<StoryResponse> GetStoryAsync(Guid id, CancellationToken ct = default)
    {
        logger.LogDebug("Getting story: {Id}", id);
        return await SendAsync<StoryResponse>(
            HttpMethod.Get,
            $"/api/developer/stories/{id}",
            null,
            ct,
            id);
    }

    /// <inheritdoc />
    public async Task<StoryResponse> AnalyzeStoryAsync(Guid id, CancellationToken ct = default)
    {
        logger.LogDebug("Analyzing story: {Id}", id);
        return await SendAsync<StoryResponse>(
            HttpMethod.Post,
            $"/api/developer/stories/{id}/analyze",
            null,
            ct,
            id);
    }

    /// <inheritdoc />
    public async Task<StoryResponse> PlanStoryAsync(Guid id, CancellationToken ct = default)
    {
        logger.LogDebug("Planning story: {Id}", id);
        // Use /decompose endpoint which accepts parameters for test generation and parallelism
        // maxParallelism: 1 = sequential steps (simpler, more reliable)
        // includeTests: false = no test generation steps (faster for calibration)
        var request = new { maxParallelism = 1, includeTests = false };
        // Don't parse response - /decompose returns a different DTO than /plan
        // We'll fetch the story status after running anyway
        await SendRequestAsync(
            HttpMethod.Post,
            $"/api/developer/stories/{id}/decompose",
            request,
            ct,
            id);
        // Return a placeholder - caller doesn't use the return value
        return new StoryResponse { Id = id, Status = "Decomposed" };
    }

    /// <inheritdoc />
    public async Task RunStoryAsync(Guid id, CancellationToken ct = default)
    {
        logger.LogDebug("Running story: {Id}", id);
        // Run endpoint returns a progress response, not a StoryResponse.
        // We ignore the response and poll GetStoryAsync for status.
        await SendAsync(
            HttpMethod.Post,
            $"/api/developer/stories/{id}/run",
            ct,
            id);
    }

    /// <inheritdoc />
    public async Task DeleteStoryAsync(Guid id, CancellationToken ct = default)
    {
        logger.LogDebug("Deleting story: {Id}", id);
        await SendAsync(
            HttpMethod.Delete,
            $"/api/developer/stories/{id}",
            ct,
            id);
        logger.LogInformation("Deleted story: {Id}", id);
    }

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string path,
        object? content,
        CancellationToken ct,
        Guid? storyId = null)
    {
        var response = await SendRequestAsync(method, path, content, ct, storyId);
        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        return result ?? throw new AuraApiException((int)response.StatusCode, "Empty response body");
    }

    private async Task SendAsync(
        HttpMethod method,
        string path,
        CancellationToken ct,
        Guid? storyId = null)
    {
        await SendRequestAsync(method, path, null, ct, storyId);
    }

    private async Task<HttpResponseMessage> SendRequestAsync(
        HttpMethod method,
        string path,
        object? content,
        CancellationToken ct,
        Guid? storyId)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path);

            if (content is not null)
            {
                request.Content = JsonContent.Create(content, options: JsonOptions);
            }

            var response = await httpClient.SendAsync(request, ct);

            if (response.StatusCode == HttpStatusCode.NotFound && storyId.HasValue)
            {
                throw new StoryNotFoundException(storyId.Value);
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                throw new AuraApiException((int)response.StatusCode, response.ReasonPhrase ?? "Unknown error", body);
            }

            return response;
        }
        catch (HttpRequestException ex)
        {
            throw new AuraUnavailableException(httpClient.BaseAddress?.ToString() ?? "unknown", ex);
        }
    }
}
