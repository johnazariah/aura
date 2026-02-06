using Anvil.Cli.Models;

namespace Anvil.Cli.Services;

/// <summary>
/// Contract for generating reports from test results.
/// </summary>
public interface IReportGenerator
{
    /// <summary>
    /// Writes the report to the console.
    /// </summary>
    Task WriteConsoleReportAsync(SuiteResult result, CancellationToken ct = default);

    /// <summary>
    /// Writes the report as JSON to a file.
    /// </summary>
    Task WriteJsonReportAsync(SuiteResult result, string outputPath, CancellationToken ct = default);
}
