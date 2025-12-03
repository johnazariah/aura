// <copyright file="DeveloperModuleOptions.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer;

/// <summary>
/// Configuration options for the Developer Module.
/// </summary>
public sealed class DeveloperModuleOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Aura:Modules:Developer";

    /// <summary>
    /// Gets or sets the prefix for workflow branches.
    /// Default is "workflow" which creates branches like "workflow/feature-name-abc123".
    /// </summary>
    /// <example>
    /// Set to "aura-workflow" for "aura-workflow/feature-name-abc123"
    /// Set to "feature" for "feature/feature-name-abc123"
    /// Set to "dev/john" for "dev/john/feature-name-abc123"
    /// </example>
    public string BranchPrefix { get; set; } = "workflow";

    /// <summary>
    /// Gets or sets the path for agents specific to the Developer Module.
    /// </summary>
    public string AgentsPath { get; set; } = "./agents/developer";

    /// <summary>
    /// Gets or sets the default directory for git worktrees.
    /// Relative to the repository root.
    /// </summary>
    public string WorktreeDirectory { get; set; } = ".worktrees";
}
