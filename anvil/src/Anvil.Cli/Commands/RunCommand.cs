using System.ComponentModel;
using System.Diagnostics;
using Anvil.Cli.Exceptions;
using Anvil.Cli.Models;
using Anvil.Cli.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Anvil.Cli.Commands;

/// <summary>
/// Run test scenarios against Aura.
/// </summary>
[Description("Run test scenarios against Aura")]
public sealed class RunCommand(
    IScenarioLoader scenarioLoader,
    IStoryRunner storyRunner,
    IReportGenerator reportGenerator,
    IAnsiConsole console) : AsyncCommand<RunCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[path]")]
        [Description("Scenario file or directory (default: ./scenarios/)")]
        public string? Path { get; init; }

        [CommandOption("-o|--output")]
        [Description("JSON report output path (default: ./reports/anvil-{timestamp}.json)")]
        public string? OutputPath { get; init; }

        [CommandOption("--url")]
        [Description("Aura API URL (default: $AURA_URL or http://localhost:5300)")]
        public string? AuraUrl { get; init; }

        [CommandOption("-t|--timeout")]
        [Description("Story timeout in seconds (default: 300)")]
        [DefaultValue(300)]
        public int Timeout { get; init; } = 300;

        [CommandOption("-q|--quiet")]
        [Description("Quiet mode: show summary only")]
        public bool Quiet { get; init; }

        [CommandOption("--silent")]
        [Description("Silent mode: exit code only (implies -q)")]
        public bool Silent { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var path = settings.Path ?? "./scenarios";
        var isQuiet = settings.Quiet || settings.Silent;
        var isSilent = settings.Silent;

        if (!isSilent)
        {
            console.MarkupLine($"[blue]Loading scenarios from {Markup.Escape(path)}...[/]");
        }

        // Load scenarios
        IReadOnlyList<Scenario> scenarios;
        try
        {
            scenarios = await scenarioLoader.LoadAllAsync(path);
        }
        catch (ScenarioNotFoundException ex)
        {
            if (!isSilent)
            {
                console.MarkupLine($"[red]✗ {Markup.Escape(ex.Message)}[/]");
            }
            return 1;
        }
        catch (ScenarioParseException ex)
        {
            if (!isSilent)
            {
                console.MarkupLine($"[red]✗ {Markup.Escape(ex.Message)}[/]");
            }
            return 1;
        }

        if (scenarios.Count == 0)
        {
            if (!isSilent)
            {
                console.MarkupLine("[yellow]No scenarios found[/]");
            }
            return 0;
        }

        if (!isSilent)
        {
            console.MarkupLine($"[blue]Running {scenarios.Count} scenario(s)...[/]");
        }

        // Run scenarios
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var runOptions = new RunOptions(
            TimeSpan.FromSeconds(settings.Timeout),
            TimeSpan.FromSeconds(2));

        var results = new List<StoryResult>();
        foreach (var scenario in scenarios)
        {
            if (!isQuiet)
            {
                console.MarkupLine($"[blue]→ Running: {Markup.Escape(scenario.Name)}[/]");
            }

            try
            {
                var result = await storyRunner.RunAsync(scenario, runOptions);
                results.Add(result);

                if (!isQuiet)
                {
                    var status = result.Success ? "[green]✓ PASSED[/]" : "[red]✗ FAILED[/]";
                    console.MarkupLine($"  {status} ({result.Duration.TotalSeconds:F1}s)");
                }
            }
            catch (AuraUnavailableException ex)
            {
                if (!isSilent)
                {
                    console.MarkupLine($"[red]✗ Aura unavailable: {Markup.Escape(ex.Message)}[/]");
                }
                return 1;
            }
            catch (StoryTimeoutException ex)
            {
                results.Add(new StoryResult
                {
                    Scenario = scenario,
                    Success = false,
                    Duration = ex.Timeout,
                    Error = ex.Message
                });

                if (!isQuiet)
                {
                    console.MarkupLine($"  [red]✗ TIMEOUT ({ex.Timeout.TotalSeconds:F0}s)[/]");
                }
            }
        }

        stopwatch.Stop();
        var completedAt = DateTimeOffset.UtcNow;

        var suiteResult = new SuiteResult
        {
            Results = results,
            TotalDuration = stopwatch.Elapsed,
            StartedAt = startedAt,
            CompletedAt = completedAt
        };

        // Generate reports
        if (!isSilent)
        {
            await reportGenerator.WriteConsoleReportAsync(suiteResult);
        }

        var outputPath = settings.OutputPath ?? $"./reports/anvil-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
        await reportGenerator.WriteJsonReportAsync(suiteResult, outputPath);

        if (!isSilent)
        {
            console.MarkupLine($"[blue]Report written to: {Markup.Escape(outputPath)}[/]");
        }

        return suiteResult.Failed > 0 ? 1 : 0;
    }
}
