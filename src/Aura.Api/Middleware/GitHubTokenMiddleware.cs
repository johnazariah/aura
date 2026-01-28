// <copyright file="GitHubTokenMiddleware.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Middleware;

using Aura.Api.Services;

/// <summary>
/// Middleware that extracts the GitHub token from the X-GitHub-Token header
/// and makes it available via IGitHubTokenAccessor for the duration of the request.
/// </summary>
public class GitHubTokenMiddleware
{
    private readonly RequestDelegate _next;

    public GitHubTokenMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IGitHubTokenAccessor tokenAccessor)
    {
        // Extract token from header
        var token = context.Request.Headers["X-GitHub-Token"].FirstOrDefault();
        if (!string.IsNullOrEmpty(token))
        {
            tokenAccessor.SetToken(token);
        }

        await _next(context);
    }
}

/// <summary>
/// Extension methods for registering the GitHub token middleware.
/// </summary>
public static class GitHubTokenMiddlewareExtensions
{
    /// <summary>
    /// Adds middleware that extracts the GitHub token from request headers.
    /// </summary>
    public static IApplicationBuilder UseGitHubToken(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GitHubTokenMiddleware>();
    }
}
