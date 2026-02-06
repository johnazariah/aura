using Anvil.Cli.Models;

namespace Anvil.Cli.Adapters;

/// <summary>
/// Contract for Aura API communication.
/// </summary>
public interface IAuraClient
{
    /// <summary>
    /// Checks if the Aura API is healthy and reachable.
    /// </summary>
    Task<bool> HealthCheckAsync(CancellationToken ct = default);

    /// <summary>
    /// Creates a new story in Aura.
    /// </summary>
    Task<StoryResponse> CreateStoryAsync(CreateStoryRequest request, CancellationToken ct = default);

    /// <summary>
    /// Gets a story by ID.
    /// </summary>
    Task<StoryResponse> GetStoryAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Triggers story analysis (enrich phase).
    /// </summary>
    Task<StoryResponse> AnalyzeStoryAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Triggers story planning.
    /// </summary>
    Task<StoryResponse> PlanStoryAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Triggers story execution. Response is ignored; poll GetStoryAsync for status.
    /// </summary>
    Task RunStoryAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Deletes a story.
    /// </summary>
    Task DeleteStoryAsync(Guid id, CancellationToken ct = default);
}
