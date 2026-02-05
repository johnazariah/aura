// Anvil CLI - Aura Test Harness

using System.IO.Abstractions;
using Anvil.Cli.Adapters;
using Anvil.Cli.Commands;
using Anvil.Cli.Infrastructure;
using Anvil.Cli.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

// Resolve Aura URL: --url flag → AURA_URL env → default
var auraUrl = Environment.GetEnvironmentVariable("AURA_URL") ?? "http://localhost:5300";

// Check for --url in args and extract it
for (var i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--url")
    {
        auraUrl = args[i + 1];
        break;
    }
}

// Configure services
var services = new ServiceCollection();

// Logging
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

// File system abstraction
services.AddSingleton<IFileSystem, FileSystem>();

// Console
services.AddSingleton(AnsiConsole.Console);

// HTTP client for Aura
services.AddHttpClient<IAuraClient, AuraClient>(client =>
{
    client.BaseAddress = new Uri(auraUrl);
    client.Timeout = TimeSpan.FromMinutes(10);
});

// Services
services.AddSingleton<IScenarioLoader, ScenarioLoader>();
services.AddSingleton<IStoryRunner, StoryRunner>();
services.AddSingleton<IIndexEffectivenessAnalyzer, IndexEffectivenessAnalyzer>();
services.AddSingleton<IExpectationValidator, ExpectationValidator>();
services.AddSingleton<IReportGenerator, ReportGenerator>();

// Build CLI app
var registrar = new TypeRegistrar(services);
var app = new CommandApp(registrar);

app.Configure(config =>
{
    config.SetApplicationName("anvil");
    config.SetApplicationVersion("0.1.0");

    config.AddCommand<HealthCommand>("health")
        .WithDescription("Check Aura API health");

    config.AddCommand<ValidateCommand>("validate")
        .WithDescription("Validate scenario files without running");

    config.AddCommand<RunCommand>("run")
        .WithDescription("Run test scenarios against Aura");
});

return app.Run(args);
