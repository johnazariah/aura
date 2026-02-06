using Anvil.Cli.Models;

namespace Anvil.Cli.Services;

/// <summary>
/// Contract for running a story through Aura.
/// </summary>
public interface IStoryRunner
{
    /// <summary>
    /// Runs a scenario through Aura.
    /// </summary>
    /// <param name="scenario">The scenario to run.</param>
    /// <param name="options">Run options (timeout, poll interval).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of running the story.</returns>
    Task<StoryResult> RunAsync(Scenario scenario, RunOptions options, CancellationToken ct = default);
}

/// <summary>
/// Options for running a story.
/// </summary>
/// <param name="Timeout">Maximum time to wait for story completion.</param>
/// <param name="PollInterval">Interval between status checks.</param>
public record RunOptions(
    TimeSpan Timeout,
    TimeSpan PollInterval)
{
    /// <summary>
    /// Default run options: 5 minute timeout, 2 second poll interval.
    /// </summary>
    public static RunOptions Default => new(
        TimeSpan.FromMinutes(5),
        TimeSpan.FromSeconds(2));
}
