// <copyright file="GitHubOptions.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.GitHub;

/// <summary>
/// Configuration options for GitHub integration.
/// </summary>
public sealed class GitHubOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "GitHub";

    /// <summary>
    /// Gets or sets the personal access token with repo scope.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Gets or sets the GitHub API base URL.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.github.com";
}
