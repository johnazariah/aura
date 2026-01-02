// <copyright file="ListProjectsTool.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Tools;

using Aura.Foundation.Tools;
using Aura.Module.Developer.Services;
using Microsoft.Extensions.Logging;

/// <summary>
/// Input for the list_projects tool.
/// </summary>
public record ListProjectsInput
{
    /// <summary>Directory to search for projects (defaults to working directory)</summary>
    public string? Directory { get; init; }

    /// <summary>Whether to include detailed dependency information</summary>
    public bool IncludeDependencies { get; init; }
}

/// <summary>
/// Information about a discovered project.
/// </summary>
public record ProjectInfo
{
    /// <summary>Project name</summary>
    public required string Name { get; init; }

    /// <summary>Full path to the project file</summary>
    public required string Path { get; init; }

    /// <summary>Target framework(s)</summary>
    public IReadOnlyList<string> TargetFrameworks { get; init; } = [];

    /// <summary>Project references (other projects)</summary>
    public IReadOnlyList<string> ProjectReferences { get; init; } = [];

    /// <summary>Package references (NuGet packages)</summary>
    public IReadOnlyList<string> PackageReferences { get; init; } = [];

    /// <summary>Document count (source files)</summary>
    public int DocumentCount { get; init; }
}

/// <summary>
/// Output from the list_projects tool.
/// </summary>
public record ListProjectsOutput
{
    /// <summary>Path to solution file if found</summary>
    public string? SolutionPath { get; init; }

    /// <summary>List of discovered projects</summary>
    public required IReadOnlyList<ProjectInfo> Projects { get; init; }

    /// <summary>Total number of projects</summary>
    public int TotalProjects => Projects.Count;
}

/// <summary>
/// Lists all C# projects in a solution or directory.
/// Provides project metadata for understanding codebase structure.
/// </summary>
public class ListProjectsTool(
    IRoslynWorkspaceService workspace,
    ILogger<ListProjectsTool> logger) : TypedToolBase<ListProjectsInput, ListProjectsOutput>
{
    private readonly IRoslynWorkspaceService _workspace = workspace;
    private readonly ILogger<ListProjectsTool> _logger = logger;

    /// <inheritdoc/>
    public override string ToolId => "roslyn.list_projects";

    /// <inheritdoc/>
    public override string Name => "List Projects";

    /// <inheritdoc/>
    public override string Description =>
        "Lists all C# projects in a solution or directory. Returns project names, paths, " +
        "target frameworks, and optionally dependency information. Use this first to understand " +
        "the codebase structure before examining specific classes.";

    /// <inheritdoc/>
    public override IReadOnlyList<string> Categories => ["roslyn", "analysis"];

    /// <inheritdoc/>
    public override bool RequiresConfirmation => false;

    /// <inheritdoc/>
    public override async Task<ToolResult<ListProjectsOutput>> ExecuteAsync(
        ListProjectsInput input,
        CancellationToken ct = default)
    {
        var directory = input.Directory ?? Environment.CurrentDirectory;

        if (!Directory.Exists(directory))
        {
            return ToolResult<ListProjectsOutput>.Fail($"Directory not found: {directory}");
        }

        _logger.LogInformation("Listing projects in: {Directory}", directory);

        try
        {
            // Try to find and load a solution first
            var solutionPath = _workspace.FindSolutionFile(directory);
            var projects = new List<ProjectInfo>();

            if (solutionPath is not null)
            {
                _logger.LogInformation("Found solution: {SolutionPath}", solutionPath);
                var solution = await _workspace.GetSolutionAsync(solutionPath, ct);

                foreach (var project in solution.Projects)
                {
                    var projectInfo = await BuildProjectInfoAsync(project, input.IncludeDependencies, ct);
                    projects.Add(projectInfo);
                }
            }
            else
            {
                // Fallback: Find individual project files
                var projectFiles = _workspace.FindProjectFiles(directory);
                _logger.LogInformation("No solution found, found {Count} project files", projectFiles.Count);

                foreach (var projectFile in projectFiles)
                {
                    try
                    {
                        var project = await _workspace.GetProjectAsync(projectFile, ct);
                        var projectInfo = await BuildProjectInfoAsync(project, input.IncludeDependencies, ct);
                        projects.Add(projectInfo);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load project: {Path}", projectFile);
                        // Add minimal info for failed projects
                        projects.Add(new ProjectInfo
                        {
                            Name = Path.GetFileNameWithoutExtension(projectFile),
                            Path = projectFile,
                        });
                    }
                }
            }

            var output = new ListProjectsOutput
            {
                SolutionPath = solutionPath,
                Projects = projects.OrderBy(p => p.Name).ToList(),
            };

            _logger.LogInformation("Found {Count} projects", projects.Count);
            return ToolResult<ListProjectsOutput>.Ok(output);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list projects in {Directory}", directory);
            return ToolResult<ListProjectsOutput>.Fail($"Failed to list projects: {ex.Message}");
        }
    }

    private static async Task<ProjectInfo> BuildProjectInfoAsync(
        Microsoft.CodeAnalysis.Project project,
        bool includeDependencies,
        CancellationToken ct)
    {
        var projectReferences = new List<string>();
        var packageReferences = new List<string>();

        if (includeDependencies)
        {
            // Get project references
            foreach (var reference in project.ProjectReferences)
            {
                var referencedProject = project.Solution.GetProject(reference.ProjectId);
                if (referencedProject is not null)
                {
                    projectReferences.Add(referencedProject.Name);
                }
            }

            // Get metadata (package) references - simplified for now
            foreach (var reference in project.MetadataReferences)
            {
                var display = reference.Display;
                if (display is not null && !display.Contains("Microsoft.NETCore") && !display.Contains("System."))
                {
                    var name = Path.GetFileNameWithoutExtension(display);
                    if (!packageReferences.Contains(name))
                    {
                        packageReferences.Add(name);
                    }
                }
            }
        }

        // Get target framework from compilation options if available
        var targetFrameworks = new List<string>();
        var compilation = await project.GetCompilationAsync(ct);
        if (compilation?.Options is not null)
        {
            // This is a simplified approach - in real usage you'd parse the csproj
            targetFrameworks.Add("net10.0"); // Placeholder
        }

        return new ProjectInfo
        {
            Name = project.Name,
            Path = project.FilePath ?? "",
            TargetFrameworks = targetFrameworks,
            ProjectReferences = projectReferences,
            PackageReferences = packageReferences.Take(20).ToList(), // Limit for output size
            DocumentCount = project.Documents.Count(),
        };
    }
}
