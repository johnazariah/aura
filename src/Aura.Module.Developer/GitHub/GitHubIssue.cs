// <copyright file="GitHubIssue.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.GitHub;

using System.Text.Json.Serialization;

/// <summary>
/// Represents a GitHub issue.
/// </summary>
public sealed record GitHubIssue
{
    /// <summary>Gets the issue number.</summary>
    [JsonPropertyName("number")]
    public int Number { get; init; }

    /// <summary>Gets the issue title.</summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>Gets the issue body (markdown).</summary>
    [JsonPropertyName("body")]
    public string? Body { get; init; }

    /// <summary>Gets the issue state (open/closed).</summary>
    [JsonPropertyName("state")]
    public required string State { get; init; }

    /// <summary>Gets the issue labels.</summary>
    [JsonPropertyName("labels")]
    public IReadOnlyList<GitHubLabel> Labels { get; init; } = [];

    /// <summary>Gets when the issue was created.</summary>
    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets when the issue was last updated.</summary>
    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Gets the HTML URL to the issue.</summary>
    [JsonPropertyName("html_url")]
    public required string HtmlUrl { get; init; }

    /// <summary>Gets the user who created the issue.</summary>
    [JsonPropertyName("user")]
    public GitHubUser? User { get; init; }
}

/// <summary>
/// Represents a GitHub label.
/// </summary>
public sealed record GitHubLabel
{
    /// <summary>Gets the label name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Gets the label color.</summary>
    [JsonPropertyName("color")]
    public string? Color { get; init; }
}

/// <summary>
/// Represents a GitHub user.
/// </summary>
public sealed record GitHubUser
{
    /// <summary>Gets the username.</summary>
    [JsonPropertyName("login")]
    public required string Login { get; init; }

    /// <summary>Gets the avatar URL.</summary>
    [JsonPropertyName("avatar_url")]
    public string? AvatarUrl { get; init; }
}
