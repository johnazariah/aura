// <copyright file="HealthEndpoints.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Endpoints;

using Aura.Foundation.Agents;
using Aura.Foundation.Data;
using Aura.Foundation.Llm;
using Aura.Foundation.Rag;
using Aura.Foundation.Tools;

/// <summary>
/// Health check endpoints for monitoring service status.
/// </summary>
public static class HealthEndpoints
{
    /// <summary>
    /// Maps all health endpoints to the application.
    /// </summary>
    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health", GetHealth);
        app.MapGet("/health/db", GetDatabaseHealth);
        app.MapGet("/health/rag", GetRagHealth);
        app.MapGet("/health/ollama", GetLlmHealth);
        app.MapGet("/health/agents", GetAgentHealth);
        app.MapGet("/health/mcp", GetMcpHealth);

        return app;
    }

    private static object GetHealth() => new
    {
        status = "healthy",
        healthy = true,
        version = "0.1.0",
        timestamp = DateTime.UtcNow
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

    private static async Task<IResult> GetLlmHealth(ILlmProviderRegistry registry)
    {
        var provider = registry.GetProvider(LlmProviders.Ollama) ?? registry.GetDefaultProvider();
        if (provider is null)
        {
            return Results.Ok(new { healthy = false, details = "No LLM provider configured" });
        }

        try
        {
            var models = await provider.ListModelsAsync();
            return Results.Ok(new
            {
                healthy = true,
                details = models.Count + " models available",
                models = models.Select(m => m.Name).ToList(),
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

    private static IResult GetAgentHealth(IAgentRegistry registry)
    {
        // Check foundation capabilities (always required)
        var foundationResults = Capabilities.Foundation.Select(cap =>
        {
            var agent = registry.GetBestForCapability(cap);
            return new
            {
                capability = cap,
                category = "foundation",
                available = agent is not null,
                agentId = agent?.AgentId
            };
        }).ToList();

        // Check for ingest:* capability (required for RAG)
        var ingestAgent = registry.GetBestForCapability(Capabilities.IngestWildcard);
        foundationResults.Add(new
        {
            capability = Capabilities.IngestWildcard,
            category = "foundation",
            available = ingestAgent is not null,
            agentId = ingestAgent?.AgentId
        });

        // Check developer module capabilities
        var developerResults = Capabilities.Developer.Select(cap =>
        {
            var agent = registry.GetBestForCapability(cap);
            return new
            {
                capability = cap,
                category = "developer",
                available = agent is not null,
                agentId = agent?.AgentId
            };
        }).ToList();

        var allResults = foundationResults.Concat(developerResults).ToList();
        var foundationHealthy = foundationResults.All(r => r.available);
        var developerHealthy = developerResults.All(r => r.available);
        var allHealthy = foundationHealthy && developerHealthy;

        var missingFoundation = foundationResults.Where(r => !r.available).Select(r => r.capability).ToList();
        var missingDeveloper = developerResults.Where(r => !r.available).Select(r => r.capability).ToList();

        string details;
        if (allHealthy)
        {
            details = "All critical agents available";
        }
        else if (!foundationHealthy)
        {
            details = $"Missing foundation: {string.Join(", ", missingFoundation)}";
        }
        else
        {
            details = $"Missing developer: {string.Join(", ", missingDeveloper)}";
        }

        return Results.Ok(new
        {
            healthy = allHealthy,
            foundationHealthy,
            developerHealthy,
            details,
            agents = allResults,
            timestamp = DateTime.UtcNow
        });
    }

    private static IResult GetMcpHealth(Aura.Api.Mcp.McpHandler mcpHandler, IToolRegistry toolRegistry)
    {
        var mcpTools = mcpHandler.GetToolNames();
        var agentTools = toolRegistry.GetAllTools();

        return Results.Ok(new
        {
            healthy = true,
            details = $"MCP server ready with {mcpTools.Count} tools, {agentTools.Count} agent tools registered",
            mcpTools = mcpTools,
            agentToolCount = agentTools.Count,
            agentTools = agentTools.Select(t => new { t.ToolId, t.Description }).ToList(),
            timestamp = DateTime.UtcNow
        });
    }
}
