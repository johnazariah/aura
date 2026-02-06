using System.Diagnostics;
using Anvil.Cli.Adapters;
using Anvil.Cli.Exceptions;
using Anvil.Cli.Models;
using Microsoft.Extensions.Logging;

namespace Anvil.Cli.Services;

/// <summary>
/// Orchestrates story execution through the Aura API.
/// </summary>
public sealed class StoryRunner(
    IAuraClient auraClient,
    IExpectationValidator validator,
    ILogger<StoryRunner> logger) : IStoryRunner
{
    // Terminal statuses - story won't change from these
    private static readonly HashSet<string> TerminalStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Completed",
        "Failed",
        "Cancelled",
        "ReadyToComplete",  // All steps done, waiting for finalization
        "GateFailed"        // Gate failed, needs user intervention
    };

    /// <inheritdoc />
    public async Task<StoryResult> RunAsync(Scenario scenario, RunOptions options, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        Guid? storyId = null;

        try
        {
            logger.LogInformation("Running scenario: {Name}", scenario.Name);

            // Resolve repository path - relative paths are resolved relative to the anvil/ directory
            // (the parent of scenarios/) since fixtures are typically at anvil/fixtures/
            string repositoryPath;
            if (Path.IsPathRooted(scenario.Repository))
            {
                repositoryPath = scenario.Repository;
            }
            else if (scenario.FilePath != null)
            {
                // Find the anvil/ root by going up from the scenario file until we find fixtures/
                var scenarioDir = Path.GetDirectoryName(scenario.FilePath)!;
                var basePath = FindAnvilRoot(scenarioDir) ?? scenarioDir;
                repositoryPath = Path.GetFullPath(Path.Combine(basePath, scenario.Repository));
            }
            else
            {
                repositoryPath = Path.GetFullPath(scenario.Repository);
            }
            logger.LogDebug("Repository path resolved to: {Path}", repositoryPath);

            // Ensure MCP config exists so Copilot CLI can use Aura tools
            EnsureMcpConfig(repositoryPath);

            // Create story
            var createRequest = new CreateStoryRequest
            {
                Title = scenario.Story.Title,
                Description = scenario.Story.Description,
                RepositoryPath = repositoryPath,
                PreferredExecutor = scenario.Executor
            };
            var story = await auraClient.CreateStoryAsync(createRequest, ct);
            storyId = story.Id;
            logger.LogDebug("Created story {Id} with executor: {Executor}", storyId, scenario.Executor ?? "default");

            // Execute story lifecycle: Analyze → Plan → Run
            await auraClient.AnalyzeStoryAsync(storyId.Value, ct);
            logger.LogDebug("Story analyzed");

            await auraClient.PlanStoryAsync(storyId.Value, ct);
            logger.LogDebug("Story planned");

            await auraClient.RunStoryAsync(storyId.Value, ct);
            logger.LogDebug("Story execution started");

            // Poll for completion
            story = await WaitForCompletionAsync(storyId.Value, options, ct);
            logger.LogDebug("Story completed with status: {Status}", story.Status);

            // Validate expectations
            var expectationResults = await validator.ValidateAsync(scenario, story, ct);
            var allPassed = expectationResults.All(r => r.Passed);

            stopwatch.Stop();

            // Success if all expectations pass - the expectations define what we care about
            // Story status may be "Failed" if non-critical steps failed, but if the code compiles
            // and has the expected content, the scenario passes
            var result = new StoryResult
            {
                Scenario = scenario,
                Success = allPassed,
                Duration = stopwatch.Elapsed,
                StoryId = storyId,
                WorktreePath = story.WorktreePath,
                ExpectationResults = expectationResults.ToList(),
                Error = allPassed ? null : story.Error
            };

            logger.LogInformation(
                "Scenario {Name}: {Result} ({Duration:F1}s)",
                scenario.Name,
                result.Success ? "PASSED" : "FAILED",
                result.Duration.TotalSeconds);

            return result;
        }
        catch (StoryTimeoutException)
        {
            throw;
        }
        catch (AuraUnavailableException)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, "Scenario {Name} failed with error", scenario.Name);

            return new StoryResult
            {
                Scenario = scenario,
                Success = false,
                Duration = stopwatch.Elapsed,
                StoryId = storyId,
                Error = ex.Message
            };
        }
        finally
        {
            // Always cleanup the story
            if (storyId.HasValue)
            {
                await TryDeleteStoryAsync(storyId.Value, ct);
            }
        }
    }

    private async Task<StoryResponse> WaitForCompletionAsync(
        Guid storyId,
        RunOptions options,
        CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + options.Timeout;

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            var story = await auraClient.GetStoryAsync(storyId, ct);

            if (TerminalStatuses.Contains(story.Status))
            {
                return story;
            }

            // If status is GatePending and gateMode is AutoProceed, call /run again
            // to trigger the quality gate and proceed to next wave
            if (story.Status.Equals("GatePending", StringComparison.OrdinalIgnoreCase) &&
                story.GateMode?.Equals("AutoProceed", StringComparison.OrdinalIgnoreCase) == true)
            {
                logger.LogDebug("Story {Id} at gate after wave {Wave}, triggering next wave", storyId, story.CurrentWave - 1);
                await auraClient.RunStoryAsync(storyId, ct);
                // Don't poll immediately - give it time to start executing
                await Task.Delay(options.PollInterval, ct);
                continue;
            }

            logger.LogDebug("Story {Id} status: {Status}, waiting...", storyId, story.Status);
            await Task.Delay(options.PollInterval, ct);
        }

        throw new StoryTimeoutException(storyId, options.Timeout);
    }

    private async Task TryDeleteStoryAsync(Guid storyId, CancellationToken ct)
    {
        try
        {
            await auraClient.DeleteStoryAsync(storyId, ct);
            logger.LogDebug("Deleted story {Id}", storyId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete story {Id}", storyId);
        }
    }

    /// <summary>
    /// Creates .vscode/mcp.json in the repository so Copilot CLI can use Aura's MCP tools.
    /// </summary>
    private void EnsureMcpConfig(string repositoryPath)
    {
        var vscodeDir = Path.Combine(repositoryPath, ".vscode");
        var mcpConfigPath = Path.Combine(vscodeDir, "mcp.json");

        if (File.Exists(mcpConfigPath))
        {
            logger.LogDebug("MCP config already exists at {Path}", mcpConfigPath);
            return;
        }

        Directory.CreateDirectory(vscodeDir);

        const string mcpConfig = """
            {
              "servers": {
                "aura-codebase": {
                  "url": "http://localhost:5300/mcp"
                }
              }
            }
            """;

        File.WriteAllText(mcpConfigPath, mcpConfig);
        logger.LogDebug("Created MCP config at {Path}", mcpConfigPath);
    }

    /// <summary>
    /// Find the anvil root directory by looking for fixtures/ or scenarios/ directories.
    /// </summary>
    private static string? FindAnvilRoot(string startDir)
    {
        var dir = startDir;
        while (dir != null)
        {
            var fixturesDir = Path.Combine(dir, "fixtures");
            var scenariosDir = Path.Combine(dir, "scenarios");
            if (Directory.Exists(fixturesDir) || Directory.Exists(scenariosDir))
            {
                return dir;
            }
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }
}
