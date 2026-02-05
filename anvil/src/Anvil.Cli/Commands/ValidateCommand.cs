using System.ComponentModel;
using Anvil.Cli.Exceptions;
using Anvil.Cli.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Anvil.Cli.Commands;

/// <summary>
/// Validate scenario files without running.
/// </summary>
[Description("Validate scenario files without running")]
public sealed class ValidateCommand(
    IScenarioLoader scenarioLoader,
    IAnsiConsole console) : AsyncCommand<ValidateCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[path]")]
        [Description("Scenario file or directory (default: ./scenarios/)")]
        public string? Path { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var path = settings.Path ?? "./scenarios";
        console.MarkupLine($"[blue]Validating scenarios in {Markup.Escape(path)}...[/]");

        try
        {
            var scenarios = await scenarioLoader.LoadAllAsync(path);

            if (scenarios.Count == 0)
            {
                console.MarkupLine("[yellow]No scenarios found[/]");
                return 0;
            }

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Status")
                .AddColumn("Scenario")
                .AddColumn("Language")
                .AddColumn("Expectations");

            foreach (var scenario in scenarios)
            {
                table.AddRow(
                    "[green]✓ Valid[/]",
                    Markup.Escape(scenario.Name),
                    scenario.Language,
                    scenario.Expectations.Count.ToString());
            }

            console.Write(table);
            console.MarkupLine($"[green]✓ {scenarios.Count} scenario(s) validated successfully[/]");
            return 0;
        }
        catch (ScenarioValidationException ex)
        {
            console.MarkupLine($"[red]✗ Validation failed: {Markup.Escape(ex.Message)}[/]");
            foreach (var error in ex.Errors)
            {
                console.MarkupLine($"  [red]• {Markup.Escape(error)}[/]");
            }
            return 1;
        }
        catch (ScenarioParseException ex)
        {
            console.MarkupLine($"[red]✗ Parse error: {Markup.Escape(ex.Message)}[/]");
            return 1;
        }
        catch (ScenarioNotFoundException ex)
        {
            console.MarkupLine($"[red]✗ Not found: {Markup.Escape(ex.Message)}[/]");
            return 1;
        }
    }
}
