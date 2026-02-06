using Anvil.Cli.Models;

namespace Anvil.Cli.Services;

/// <summary>
/// Contract for validating story results against scenario expectations.
/// </summary>
public interface IExpectationValidator
{
    /// <summary>
    /// Validates a story's results against the scenario's expectations.
    /// </summary>
    /// <param name="scenario">The scenario containing expectations.</param>
    /// <param name="story">The story response from Aura.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of expectation validation results.</returns>
    Task<IReadOnlyList<ExpectationResult>> ValidateAsync(
        Scenario scenario,
        StoryResponse story,
        CancellationToken ct = default);
}
