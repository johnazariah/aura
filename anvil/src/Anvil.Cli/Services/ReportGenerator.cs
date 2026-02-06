using System.IO.Abstractions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Anvil.Cli.Models;
using Spectre.Console;

namespace Anvil.Cli.Services;

/// <summary>
/// Generates console and JSON reports from test results.
/// </summary>
public sealed class ReportGenerator(
    IAnsiConsole console,
    IFileSystem fileSystem) : IReportGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <inheritdoc />
    public Task WriteConsoleReportAsync(SuiteResult result, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        console.WriteLine();
        console.Write(new Rule("[bold]Test Results[/]").RuleStyle("blue"));
        console.WriteLine();

        // Results table
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Status")
            .AddColumn("Scenario")
            .AddColumn("Duration")
            .AddColumn("Details");

        foreach (var storyResult in result.Results)
        {
            var status = storyResult.Success
                ? "[green]✓ PASSED[/]"
                : "[red]✗ FAILED[/]";

            var details = storyResult.Success
                ? "-"
                : GetFailureDetails(storyResult);

            table.AddRow(
                status,
                Markup.Escape(storyResult.Scenario.Name),
                $"{storyResult.Duration.TotalSeconds:F1}s",
                Markup.Escape(details));
        }

        console.Write(table);

        // Summary
        console.WriteLine();
        WriteSummary(result);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task WriteJsonReportAsync(SuiteResult result, string outputPath, CancellationToken ct = default)
    {
        var directory = fileSystem.Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !fileSystem.Directory.Exists(directory))
        {
            fileSystem.Directory.CreateDirectory(directory);
        }

        var report = new JsonReport
        {
            StartedAt = result.StartedAt,
            CompletedAt = result.CompletedAt,
            TotalDurationSeconds = result.TotalDuration.TotalSeconds,
            Passed = result.Passed,
            Failed = result.Failed,
            Total = result.Total,
            Results = result.Results.Select(r => new JsonStoryResult
            {
                ScenarioName = r.Scenario.Name,
                ScenarioPath = r.Scenario.FilePath,
                Success = r.Success,
                DurationSeconds = r.Duration.TotalSeconds,
                StoryId = r.StoryId,
                WorktreePath = r.WorktreePath,
                Error = r.Error,
                Expectations = r.ExpectationResults.Select(e => new JsonExpectationResult
                {
                    Type = e.Expectation.Type,
                    Description = e.Expectation.Description,
                    Passed = e.Passed,
                    Message = e.Message
                }).ToList()
            }).ToList()
        };

        var json = JsonSerializer.Serialize(report, JsonOptions);
        await fileSystem.File.WriteAllTextAsync(outputPath, json, ct);
    }

    private void WriteSummary(SuiteResult result)
    {
        var summaryParts = new List<string>();

        if (result.Passed > 0)
        {
            summaryParts.Add($"[green]{result.Passed} passed[/]");
        }

        if (result.Failed > 0)
        {
            summaryParts.Add($"[red]{result.Failed} failed[/]");
        }

        summaryParts.Add($"{result.Total} total");
        summaryParts.Add($"{result.TotalDuration.TotalSeconds:F1}s");

        var summary = string.Join(" | ", summaryParts);
        console.MarkupLine($"[bold]Summary:[/] {summary}");
    }

    private static string GetFailureDetails(StoryResult result)
    {
        if (!string.IsNullOrEmpty(result.Error))
        {
            return TruncateMessage(result.Error, 50);
        }

        var failedExpectation = result.ExpectationResults.FirstOrDefault(e => !e.Passed);
        if (failedExpectation is not null && !string.IsNullOrEmpty(failedExpectation.Message))
        {
            return TruncateMessage(failedExpectation.Message, 50);
        }

        return "Unknown failure";
    }

    private static string TruncateMessage(string message, int maxLength)
    {
        if (message.Length <= maxLength)
        {
            return message;
        }

        return message[..(maxLength - 3)] + "...";
    }

    // JSON report DTOs
    private sealed class JsonReport
    {
        public DateTimeOffset StartedAt { get; init; }
        public DateTimeOffset CompletedAt { get; init; }
        public double TotalDurationSeconds { get; init; }
        public int Passed { get; init; }
        public int Failed { get; init; }
        public int Total { get; init; }
        public List<JsonStoryResult> Results { get; init; } = [];
    }

    private sealed class JsonStoryResult
    {
        public string ScenarioName { get; init; } = "";
        public string? ScenarioPath { get; init; }
        public bool Success { get; init; }
        public double DurationSeconds { get; init; }
        public Guid? StoryId { get; init; }
        public string? WorktreePath { get; init; }
        public string? Error { get; init; }
        public List<JsonExpectationResult> Expectations { get; init; } = [];
    }

    private sealed class JsonExpectationResult
    {
        public string Type { get; init; } = "";
        public string Description { get; init; } = "";
        public bool Passed { get; init; }
        public string? Message { get; init; }
    }
}
