using System.ComponentModel;
using Anvil.Cli.Adapters;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Anvil.Cli.Commands;

/// <summary>
/// Check Aura API health.
/// </summary>
[Description("Check Aura API health")]
public sealed class HealthCommand(IAuraClient auraClient, IAnsiConsole console) : AsyncCommand<HealthCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--url")]
        [Description("Aura API URL (default: $AURA_URL or http://localhost:5300)")]
        public string? AuraUrl { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        console.MarkupLine("[blue]Checking Aura health...[/]");

        try
        {
            var isHealthy = await auraClient.HealthCheckAsync();

            if (isHealthy)
            {
                console.MarkupLine("[green]✓ Aura is healthy[/]");
                return 0;
            }
            else
            {
                console.MarkupLine("[red]✗ Aura is unhealthy[/]");
                return 1;
            }
        }
        catch (Exception ex)
        {
            console.MarkupLine($"[red]✗ Failed to connect to Aura: {Markup.Escape(ex.Message)}[/]");
            return 1;
        }
    }
}
