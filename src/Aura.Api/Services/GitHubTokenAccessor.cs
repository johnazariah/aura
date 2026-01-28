// <copyright file="GitHubTokenAccessor.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Services;

/// <summary>
/// Provides access to the GitHub token from the current HTTP request.
/// Registered as scoped so each request gets its own instance.
/// </summary>
public interface IGitHubTokenAccessor
{
    /// <summary>
    /// Gets the GitHub token from the current request, if provided.
    /// </summary>
    string? Token { get; }

    /// <summary>
    /// Sets the token (called by middleware).
    /// </summary>
    void SetToken(string? token);
}

/// <summary>
/// Scoped service that holds the GitHub token for the current request.
/// </summary>
public class GitHubTokenAccessor : IGitHubTokenAccessor
{
    /// <inheritdoc/>
    public string? Token { get; private set; }

    /// <inheritdoc/>
    public void SetToken(string? token)
    {
        Token = token;
    }
}
