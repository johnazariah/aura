// <copyright file="McpEndpoints.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Endpoints;

using Aura.Api.Mcp;

/// <summary>
/// MCP (Model Context Protocol) endpoint for GitHub Copilot integration.
/// </summary>
public static class McpEndpoints
{
    /// <summary>
    /// Maps the MCP endpoint to the application.
    /// </summary>
    public static WebApplication MapMcpEndpoints(this WebApplication app)
    {
        app.MapPost("/mcp", HandleMcpRequest);
        return app;
    }

    private static async Task HandleMcpRequest(HttpContext ctx, McpHandler handler, CancellationToken ct)
    {
        using var reader = new StreamReader(ctx.Request.Body);
        var json = await reader.ReadToEndAsync(ct);
        var response = await handler.HandleAsync(json, ct);
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(response, ct);
    }
}
