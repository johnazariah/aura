// <copyright file="GetProjectReferencesTool.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Tools;

using Aura.Foundation.Tools;
using Aura.Module.Developer.Services;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

/// <summary>
/// Input for the get_project_references tool.
/// </summary>
public record GetProjectReferencesInput
{
    /// <summary>Project name to get references for</summary>
    public required string ProjectName { get; init; }

    /// <summary>Include transitive (indirect) dependencies</summary>
    public bool IncludeTransitive { get; init; }

    /// <summary>Include projects that reference this project (reverse dependencies)</summary>
    public bool IncludeReferencedBy { get; init; }
}

/// <summary>
/// Information about a project dependency.
/// </summary>
public record ProjectDependency
{
    /// <summary>Project name</summary>
    public required string Name { get; init; }

    /// <summary>Project file path</summary>
    public required string Path { get; init; }

    /// <summary>Whether this is a direct or transitive dependency</summary>
    public bool IsDirect { get; init; }

    /// <summary>Depth in dependency tree (1 = direct)</summary>
    public int Depth { get; init; }
}

/// <summary>
/// Output from the get_project_references tool.
/// </summary>
public record GetProjectReferencesOutput
{
    /// <summary>The project analyzed</summary>
    public required string ProjectName { get; init; }

    /// <summary>Projects this project depends on</summary>
    public required IReadOnlyList<ProjectDependency> Dependencies { get; init; }

    /// <summary>Projects that depend on this project</summary>
    public IReadOnlyList<ProjectDependency> ReferencedBy { get; init; } = [];

    /// <summary>Total direct dependencies</summary>
    public int DirectDependencyCount => Dependencies.Count(d => d.IsDirect);

    /// <summary>Total transitive dependencies</summary>
    public int TransitiveDependencyCount => Dependencies.Count(d => !d.IsDirect);
}

/// <summary>
/// Gets the project reference graph for a project.
/// Use to understand project dependencies before making architectural changes.
/// </summary>
public class GetProjectReferencesTool(
    IRoslynWorkspaceService workspace,
    ILogger<GetProjectReferencesTool> logger) : TypedToolBase<GetProjectReferencesInput, GetProjectReferencesOutput>
{
    private readonly IRoslynWorkspaceService _workspace = workspace;
    private readonly ILogger<GetProjectReferencesTool> _logger = logger;

    /// <inheritdoc/>
    public override string ToolId => "roslyn.get_project_references";

    /// <inheritdoc/>
    public override string Name => "Get Project References";

    /// <inheritdoc/>
    public override string Description =>
        "Gets the project dependency graph showing what a project depends on and optionally " +
        "what depends on it. Use to understand project relationships before making changes " +
        "that might affect multiple projects.";

    /// <inheritdoc/>
    public override IReadOnlyList<string> Categories => ["roslyn", "analysis"];

    /// <inheritdoc/>
    public override bool RequiresConfirmation => false;

    /// <inheritdoc/>
    public override async Task<ToolResult<GetProjectReferencesOutput>> ExecuteAsync(
        GetProjectReferencesInput input,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Getting project references for: {ProjectName}", input.ProjectName);

        try
        {
            var solutionPath = _workspace.FindSolutionFile(Environment.CurrentDirectory);
            if (solutionPath is null)
            {
                return ToolResult<GetProjectReferencesOutput>.Fail("No solution file found in current directory");
            }

            var solution = await _workspace.GetSolutionAsync(solutionPath, ct);
            var project = solution.Projects.FirstOrDefault(p =>
                p.Name.Equals(input.ProjectName, StringComparison.OrdinalIgnoreCase));

            if (project is null)
            {
                return ToolResult<GetProjectReferencesOutput>.Fail(
                    $"Project '{input.ProjectName}' not found. Use list_projects to see available projects.");
            }

            // Get dependencies
            var dependencies = new List<ProjectDependency>();
            var visited = new HashSet<ProjectId>();

            await CollectDependenciesAsync(
                project,
                solution,
                dependencies,
                visited,
                depth: 1,
                input.IncludeTransitive,
                ct);

            // Get projects that reference this project
            var referencedBy = new List<ProjectDependency>();
            if (input.IncludeReferencedBy)
            {
                foreach (var otherProject in solution.Projects)
                {
                    if (otherProject.Id == project.Id) continue;

                    var references = otherProject.ProjectReferences
                        .Any(r => r.ProjectId == project.Id);

                    if (references)
                    {
                        referencedBy.Add(new ProjectDependency
                        {
                            Name = otherProject.Name,
                            Path = otherProject.FilePath ?? "",
                            IsDirect = true,
                            Depth = 1,
                        });
                    }
                }
            }

            var output = new GetProjectReferencesOutput
            {
                ProjectName = project.Name,
                Dependencies = dependencies.OrderBy(d => d.Depth).ThenBy(d => d.Name).ToList(),
                ReferencedBy = referencedBy.OrderBy(r => r.Name).ToList(),
            };

            _logger.LogInformation(
                "Found {DirectCount} direct and {TransitiveCount} transitive dependencies for {ProjectName}",
                output.DirectDependencyCount,
                output.TransitiveDependencyCount,
                input.ProjectName);

            return ToolResult<GetProjectReferencesOutput>.Ok(output);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get project references for {ProjectName}", input.ProjectName);
            return ToolResult<GetProjectReferencesOutput>.Fail($"Failed to get project references: {ex.Message}");
        }
    }

    private static async Task CollectDependenciesAsync(
        Project project,
        Solution solution,
        List<ProjectDependency> dependencies,
        HashSet<ProjectId> visited,
        int depth,
        bool includeTransitive,
        CancellationToken ct)
    {
        foreach (var reference in project.ProjectReferences)
        {
            if (visited.Contains(reference.ProjectId))
                continue;

            visited.Add(reference.ProjectId);

            var referencedProject = solution.GetProject(reference.ProjectId);
            if (referencedProject is null) continue;

            dependencies.Add(new ProjectDependency
            {
                Name = referencedProject.Name,
                Path = referencedProject.FilePath ?? "",
                IsDirect = depth == 1,
                Depth = depth,
            });

            if (includeTransitive)
            {
                await CollectDependenciesAsync(
                    referencedProject,
                    solution,
                    dependencies,
                    visited,
                    depth + 1,
                    includeTransitive,
                    ct);
            }
        }
    }
}
