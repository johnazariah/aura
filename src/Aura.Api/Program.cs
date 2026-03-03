// <copyright file="Program.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

using System.Globalization;
using Serilog;
using Serilog.Events;
using Aura.Api.Endpoints;
using Aura.Api.Mcp;
using Aura.Foundation;
using Aura.Foundation.Data;
using Aura.Module.Developer.Services;
using Aura.Module.Developer.Services.Testing;
using Aura.Module.Researcher;
using Aura.Module.Researcher.Data;
using Microsoft.EntityFrameworkCore;

// Server metadata for health endpoint
var ServerStartTime = DateTime.UtcNow;
var DeploymentTag = Environment.GetEnvironmentVariable("DEPLOY_TAG") ?? "unknown";

var builder = WebApplication.CreateBuilder(args);

// Configure as Windows Service when installed as service
builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "AuraService";
});

// Configure Serilog for file logging
// Use platform-appropriate log directory:
// - Windows: C:\ProgramData\Aura\logs
// - macOS/Linux: ~/.local/share/Aura/logs (or $AURA_LOG_DIR if set)
string logDir;
if (OperatingSystem.IsWindows())
{
    logDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Aura", "logs");
}
else
{
    // On macOS/Linux, prefer XDG_DATA_HOME or fall back to ~/.local/share
    var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
    logDir = Environment.GetEnvironmentVariable("AURA_LOG_DIR")
        ?? Path.Combine(dataHome, "Aura", "logs");
}

var logPath = Path.Combine(logDir, "aura-.log");
if (!Directory.Exists(logDir))
{
    Directory.CreateDirectory(logDir);
}

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.Extensions.Http", LogEventLevel.Warning)
        .MinimumLevel.Override("Polly", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
        .WriteTo.File(
            logPath,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7,
            formatProvider: CultureInfo.InvariantCulture);

    // On Windows, also log warnings and errors to Windows Event Log
    if (OperatingSystem.IsWindows())
    {
        configuration.WriteTo.EventLog(
            source: "Aura",
            logName: "Application",
            restrictedToMinimumLevel: LogEventLevel.Warning,
            formatProvider: CultureInfo.InvariantCulture);
    }
});

// Add Aspire service defaults (telemetry, health checks, resilience)
builder.AddServiceDefaults();

// Add PostgreSQL with EF Core
var connectionString = builder.Configuration.GetConnectionString(ResourceNames.AuraDb);
builder.Services.AddDbContext<AuraDbContext>(options =>
    options.UseNpgsql(connectionString, o => o.UseVector())
           .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

// Add DbContextFactory for singleton services that need database access
builder.Services.AddDbContextFactory<AuraDbContext>(options =>
    options.UseNpgsql(connectionString, o => o.UseVector())
           .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

// Add Aura Foundation services
builder.Services.AddAuraFoundation(builder.Configuration);

// Add Developer services (MCP code intelligence)
builder.Services.AddSingleton<IRoslynWorkspaceService, RoslynWorkspaceService>();
builder.Services.AddScoped<IRoslynRefactoringService, RoslynRefactoringService>();
builder.Services.AddScoped<IPythonRefactoringService, PythonRefactoringService>();
builder.Services.AddScoped<ITypeScriptLanguageService, TypeScriptLanguageService>();
builder.Services.AddScoped<ITestGenerationService, RoslynTestGenerator>();
builder.Services.AddScoped<ICodeGraphIndexer, CodeGraphIndexer>();
builder.Services.AddSingleton<ITreeBuilderService, TreeBuilderService>();

// Add Researcher Module
var researcherModule = new ResearcherModule();
researcherModule.ConfigureServices(builder.Services, builder.Configuration);

// Add MCP handler for GitHub Copilot integration
builder.Services.AddScoped<McpHandler>();

// Add CORS for the VS Code extension
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Apply EF Core migrations on startup (required for pgvector)
// Skip database operations in Testing environment (unit tests use stubs without a real DB)
// Integration tests use Testcontainers which handle their own migrations
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Program>>();

    // Apply Foundation migrations first
    var foundationDb = scope.ServiceProvider.GetRequiredService<AuraDbContext>();
    await ApplyMigrationsAsync(foundationDb, "Foundation", logger);

    // Apply Researcher module migrations
    var researcherDb = scope.ServiceProvider.GetRequiredService<ResearcherDbContext>();
    await ApplyMigrationsAsync(researcherDb, "Researcher", logger);
}

// Map Aspire default endpoints (health, alive)
app.MapDefaultEndpoints();

// Enable CORS
app.UseCors();

// Map all endpoint groups
app.MapHealthEndpoints(ServerStartTime, DeploymentTag);
app.MapMcpEndpoints();
app.MapWorkspaceEndpoints();
app.MapWorkspaceIndexEndpoints();
app.MapWorkspaceGraphEndpoints();
app.MapWorkspaceSearchEndpoints();

await app.RunAsync();

/// <summary>
/// Applies database migrations on startup.
/// For v1, we use simple migrations - clean installs are required.
/// </summary>
static async Task ApplyMigrationsAsync(DbContext db, string moduleName, Microsoft.Extensions.Logging.ILogger logger)
{
    var pendingMigrations = (await db.Database.GetPendingMigrationsAsync()).ToList();
    if (pendingMigrations.Count == 0)
    {
        logger.LogInformation("{Module} database is up to date", moduleName);
        return;
    }

    logger.LogInformation("Applying {Count} {Module} migrations: {Migrations}",
        pendingMigrations.Count, moduleName, string.Join(", ", pendingMigrations));

    try
    {
        await db.Database.MigrateAsync();
        logger.LogInformation("{Module} migrations complete", moduleName);
    }
    catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P07" || ex.SqlState == "42701")
    {
        // Table or column already exists - database is in inconsistent state
        logger.LogError(ex,
            "{Module} migration failed: {Message}. Database may need to be reset. " +
            "Stop services, drop database with: psql -h 127.0.0.1 -p 5433 -U postgres -c \"DROP DATABASE auradb; CREATE DATABASE auradb;\" " +
            "then: psql -h 127.0.0.1 -p 5433 -U postgres -d auradb -c \"CREATE EXTENSION vector;\" and restart.",
            moduleName, ex.MessageText);
        throw;
    }
}

/// <summary>
/// Partial class for Program to support WebApplicationFactory in integration tests.
/// </summary>
public partial class Program { }
