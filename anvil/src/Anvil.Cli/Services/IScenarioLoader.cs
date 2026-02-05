using Anvil.Cli.Models;

namespace Anvil.Cli.Services;

/// <summary>
/// Contract for loading test scenarios from YAML files.
/// </summary>
public interface IScenarioLoader
{
    /// <summary>
    /// Loads all scenarios from a directory.
    /// </summary>
    /// <param name="scenariosPath">Path to the scenarios directory.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of loaded scenarios.</returns>
    Task<IReadOnlyList<Scenario>> LoadAllAsync(string scenariosPath, CancellationToken ct = default);

    /// <summary>
    /// Loads a single scenario from a YAML file.
    /// </summary>
    /// <param name="filePath">Path to the scenario YAML file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The loaded scenario.</returns>
    Task<Scenario> LoadAsync(string filePath, CancellationToken ct = default);
}
