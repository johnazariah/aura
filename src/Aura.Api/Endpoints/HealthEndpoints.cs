// <copyright file="HealthEndpoints.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Endpoints;

using Aura.Foundation.Data;
using Aura.Foundation.Rag;

/// <summary>
/// Health check endpoints for monitoring service status.
/// </summary>
public static class HealthEndpoints
{
    /// <summary>
    /// Maps all health endpoints to the application.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <param name="serverStartTime">The server start time.</param>
    /// <param name="deploymentTag">The deployment tag.</param>
    public static WebApplication MapHealthEndpoints(
        this WebApplication app,
        DateTime serverStartTime,
        string deploymentTag)
    {
        app.MapGet("/health", () => GetHealth(serverStartTime, deploymentTag));
        app.MapGet("/health/db", GetDatabaseHealth);
        app.MapGet("/health/rag", GetRagHealth);
        app.MapGet("/health/mcp", GetMcpHealth);

        return app;
    }

    private static object GetHealth(DateTime serverStartTime, string deploymentTag) => new
    {
        status = "healthy",
        startedAt = serverStartTime.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
        deployTag = deploymentTag
    };

    private static async Task<IResult> GetDatabaseHealth(AuraDbContext db)
    {
        try
        {
            var canConnect = await db.Database.CanConnectAsync();
            return Results.Ok(new
            {
                healthy = canConnect,
                details = canConnect ? "Database connection successful" : "Cannot connect to database",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return Results.Ok(new
            {
                healthy = false,
                details = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    private static async Task<IResult> GetRagHealth(IRagService ragService)
    {
        try
        {
            var healthy = await ragService.IsHealthyAsync();
            var stats = healthy ? await ragService.GetStatsAsync() : null;
            return Results.Ok(new
            {
                healthy,
                details = healthy
                    ? "RAG service operational - " + (stats?.TotalChunks ?? 0) + " chunks indexed"
                    : "RAG service unavailable",
                totalDocuments = stats?.TotalDocuments ?? 0,
                totalChunks = stats?.TotalChunks ?? 0,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return Results.Ok(new
            {
                healthy = false,
                details = ex.Message,
                totalDocuments = 0,
                totalChunks = 0,
                timestamp = DateTime.UtcNow
            });
        }
    }

    private static IResult GetMcpHealth(Aura.Api.Mcp.McpHandler mcpHandler)
    {
        var mcpTools = mcpHandler.GetToolNames();

        return Results.Ok(new
        {
            healthy = true,
            details = $"MCP server ready with {mcpTools.Count} tools",
            mcpTools = mcpTools,
            timestamp = DateTime.UtcNow
        });
    }
}
