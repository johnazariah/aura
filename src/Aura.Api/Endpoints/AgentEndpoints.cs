// <copyright file="AgentEndpoints.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Endpoints;

using Aura.Api.Problems;
using Aura.Foundation.Agents;

/// <summary>
/// Agent-related endpoints for listing agents.
/// </summary>
public static class AgentEndpoints
{
    /// <summary>
    /// Maps all agent endpoints to the application.
    /// </summary>
    public static WebApplication MapAgentEndpoints(this WebApplication app)
    {
        app.MapGet("/api/agents", ListAgents);
        app.MapGet("/api/agents/best", GetBestAgent);
        app.MapGet("/api/agents/{agentId}", GetAgent);

        return app;
    }

    private static object ListAgents(IAgentRegistry registry, string? capability, string? language)
    {
        IEnumerable<IAgent> agents = capability is not null
            ? registry.GetByCapability(capability, language)
            : registry.Agents.OrderBy(a => a.Metadata.Priority);

        return agents.Select(a => new
        {
            id = a.AgentId,
            name = a.Metadata.Name,
            description = a.Metadata.Description,
            capabilities = a.Metadata.Capabilities,
            priority = a.Metadata.Priority,
            languages = a.Metadata.Languages,
            provider = a.Metadata.Provider,
            model = a.Metadata.Model,
            tags = a.Metadata.Tags
        });
    }

    private static IResult GetBestAgent(HttpContext context, IAgentRegistry registry, string capability, string? language)
    {
        var agent = registry.GetBestForCapability(capability, language);
        if (agent is null)
        {
            return Problem.AgentNotFoundForCapability(capability, language, context);
        }

        return Results.Ok(new
        {
            id = agent.AgentId,
            name = agent.Metadata.Name,
            description = agent.Metadata.Description,
            capabilities = agent.Metadata.Capabilities,
            priority = agent.Metadata.Priority,
            languages = agent.Metadata.Languages,
            provider = agent.Metadata.Provider,
            model = agent.Metadata.Model,
            tags = agent.Metadata.Tags
        });
    }

    private static IResult GetAgent(string agentId, HttpContext context, IAgentRegistry registry)
    {
        var agent = registry.GetAgent(agentId);
        if (agent is null)
        {
            return Problem.AgentNotFound(agentId, context);
        }

        return Results.Ok(new
        {
            id = agent.AgentId,
            name = agent.Metadata.Name,
            description = agent.Metadata.Description,
            capabilities = agent.Metadata.Capabilities,
            priority = agent.Metadata.Priority,
            languages = agent.Metadata.Languages,
            provider = agent.Metadata.Provider,
            model = agent.Metadata.Model,
            temperature = agent.Metadata.Temperature,
            tools = agent.Metadata.Tools,
            tags = agent.Metadata.Tags
        });
    }
}
