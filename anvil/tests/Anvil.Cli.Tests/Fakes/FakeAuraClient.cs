using Anvil.Cli.Adapters;
using Anvil.Cli.Exceptions;
using Anvil.Cli.Models;

namespace Anvil.Cli.Tests.Fakes;

/// <summary>
/// Fake implementation of IAuraClient for testing.
/// </summary>
public sealed class FakeAuraClient : IAuraClient
{
    private readonly Dictionary<Guid, StoryResponse> _stories = new();
    private readonly Dictionary<Guid, string> _finalStatuses = new();
    private readonly Dictionary<Guid, string?> _finalErrors = new();
    private readonly Queue<StoryResponse> _storyResponses = new();

    public bool IsHealthy { get; set; } = true;
    public bool ShouldThrowUnavailable { get; set; }
    public string AuraUrl { get; set; } = "http://localhost:5300";

    public List<string> CallLog { get; } = [];

    /// <summary>
    /// Enqueues a response to return for the next CreateStoryAsync call.
    /// </summary>
    public void EnqueueStoryResponse(StoryResponse response)
    {
        _storyResponses.Enqueue(response);
        _stories[response.Id] = response;
    }

    /// <summary>
    /// Sets the final status the story should have after RunStoryAsync completes.
    /// This will be returned on the first GetStoryAsync call after RunStoryAsync.
    /// </summary>
    public void SetStoryStatus(Guid id, string status, string? error = null)
    {
        _finalStatuses[id] = status;
        _finalErrors[id] = error;
    }

    public Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        CallLog.Add("HealthCheck");

        if (ShouldThrowUnavailable)
        {
            throw new AuraUnavailableException(AuraUrl);
        }

        return Task.FromResult(IsHealthy);
    }

    public Task<StoryResponse> CreateStoryAsync(CreateStoryRequest request, CancellationToken ct = default)
    {
        CallLog.Add($"CreateStory:{request.Title}");

        if (ShouldThrowUnavailable)
        {
            throw new AuraUnavailableException(AuraUrl);
        }

        if (_storyResponses.Count > 0)
        {
            return Task.FromResult(_storyResponses.Dequeue());
        }

        var response = new StoryResponse
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            Status = "Created"
        };
        _stories[response.Id] = response;
        return Task.FromResult(response);
    }

    public Task<StoryResponse> GetStoryAsync(Guid id, CancellationToken ct = default)
    {
        CallLog.Add($"GetStory:{id}");

        if (ShouldThrowUnavailable)
        {
            throw new AuraUnavailableException(AuraUrl);
        }

        if (!_stories.TryGetValue(id, out var story))
        {
            throw new StoryNotFoundException(id);
        }

        return Task.FromResult(story);
    }

    public Task<StoryResponse> AnalyzeStoryAsync(Guid id, CancellationToken ct = default)
    {
        CallLog.Add($"AnalyzeStory:{id}");

        if (ShouldThrowUnavailable)
        {
            throw new AuraUnavailableException(AuraUrl);
        }

        if (!_stories.TryGetValue(id, out var story))
        {
            throw new StoryNotFoundException(id);
        }

        var updated = story with { Status = "Analyzing" };
        _stories[id] = updated;
        return Task.FromResult(updated);
    }

    public Task<StoryResponse> PlanStoryAsync(Guid id, CancellationToken ct = default)
    {
        CallLog.Add($"PlanStory:{id}");

        if (ShouldThrowUnavailable)
        {
            throw new AuraUnavailableException(AuraUrl);
        }

        if (!_stories.TryGetValue(id, out var story))
        {
            throw new StoryNotFoundException(id);
        }

        var updated = story with { Status = "Planning" };
        _stories[id] = updated;
        return Task.FromResult(updated);
    }

    public Task RunStoryAsync(Guid id, CancellationToken ct = default)
    {
        CallLog.Add($"RunStory:{id}");

        if (ShouldThrowUnavailable)
        {
            throw new AuraUnavailableException(AuraUrl);
        }

        if (!_stories.TryGetValue(id, out var story))
        {
            throw new StoryNotFoundException(id);
        }

        // If a final status was configured, apply it immediately (simulates completion)
        if (_finalStatuses.TryGetValue(id, out var finalStatus))
        {
            _finalErrors.TryGetValue(id, out var finalError);
            var completed = story with { Status = finalStatus, Error = finalError };
            _stories[id] = completed;
            return Task.CompletedTask;
        }

        var updated = story with { Status = "Running" };
        _stories[id] = updated;
        return Task.CompletedTask;
    }

    public Task DeleteStoryAsync(Guid id, CancellationToken ct = default)
    {
        CallLog.Add($"DeleteStory:{id}");

        if (ShouldThrowUnavailable)
        {
            throw new AuraUnavailableException(AuraUrl);
        }

        _stories.Remove(id);
        return Task.CompletedTask;
    }
}
