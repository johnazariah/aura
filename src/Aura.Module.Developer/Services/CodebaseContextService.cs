// <copyright file="CodebaseContextService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services;

using System.Text;
using Aura.Foundation.Data.Entities;
using Aura.Foundation.Rag;
using Microsoft.Extensions.Logging;

/// <summary>
/// Implementation of <see cref="ICodebaseContextService"/> that combines
/// code graph structure with RAG semantic search.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="CodebaseContextService"/> class.
/// </remarks>
public class CodebaseContextService(
    ICodeGraphService graphService,
    IRagService ragService,
    ILogger<CodebaseContextService> logger) : ICodebaseContextService
{
    private readonly ICodeGraphService _graphService = graphService;
    private readonly IRagService _ragService = ragService;
    private readonly ILogger<CodebaseContextService> _logger = logger;

    /// <inheritdoc/>
    public async Task<CodebaseContext> GetContextAsync(
        string workspacePath,
        CodebaseContextOptions options,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Getting codebase context for {WorkspacePath} with options: ProjectStructure={IncludeProjectStructure}, Namespaces={IncludeNamespaces}, RagQueries={RagQueryCount}",
            workspacePath,
            options.IncludeProjectStructure,
            options.IncludeNamespaces,
            options.RagQueries?.Count ?? 0);

        string projectStructure = string.Empty;
        string? semanticContext = null;
        string? typeContext = null;

        // Get project structure from graph
        if (options.IncludeProjectStructure)
        {
            projectStructure = await GetProjectStructureAsync(
                workspacePath,
                options.IncludeDependencies,
                options.IncludeNamespaces,
                ct);
        }

        // Get semantic context from RAG
        if (options.RagQueries?.Count > 0)
        {
            semanticContext = await GetSemanticContextAsync(
                workspacePath,
                options.RagQueries,
                options.MaxRagResults,
                options.PrioritizeFiles,
                options.PreferCodeContent,
                ct);
        }

        // Get type details if requested
        if (options.FocusTypes?.Count > 0)
        {
            typeContext = await GetTypeContextAsync(
                workspacePath,
                options.FocusTypes,
                ct);
        }

        var context = new CodebaseContext
        {
            ProjectStructure = projectStructure,
            SemanticContext = semanticContext,
            TypeContext = typeContext,
        };

        _logger.LogInformation(
            "Built codebase context: ProjectStructure={StructureLength} chars, Semantic={SemanticLength} chars, Types={TypeLength} chars",
            projectStructure.Length,
            semanticContext?.Length ?? 0,
            typeContext?.Length ?? 0);

        return context;
    }

    private async Task<string> GetProjectStructureAsync(
        string workspacePath,
        bool includeDependencies,
        bool includeNamespaces,
        CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Workspace Structure");
        sb.AppendLine();

        // Find solution node
        var solutions = await _graphService.FindNodesAsync(
            string.Empty, // Empty name to match all
            CodeNodeType.Solution,
            workspacePath,
            ct);

        // If no solution found, try to find projects directly
        if (solutions.Count == 0)
        {
            _logger.LogDebug("No solution found, looking for projects directly");
        }

        // Get all projects
        var projects = await _graphService.FindNodesAsync(
            string.Empty,
            CodeNodeType.Project,
            workspacePath,
            ct);

        if (projects.Count == 0)
        {
            sb.AppendLine("*No project structure indexed. Run semantic indexing first.*");
            return sb.ToString();
        }

        // Format solution info
        if (solutions.Count > 0)
        {
            var solution = solutions[0];
            sb.AppendLine($"### Solution: {solution.Name}");
            sb.AppendLine();
        }

        // List projects
        sb.AppendLine("### Projects");
        sb.AppendLine();

        foreach (var project in projects.OrderBy(p => p.Name))
        {
            var projectPath = project.FilePath != null
                ? $" - `{GetRelativePath(workspacePath, Path.GetDirectoryName(project.FilePath) ?? project.FilePath)}`"
                : string.Empty;
            sb.AppendLine($"- **{project.Name}**{projectPath}");
        }

        sb.AppendLine();

        // Include dependencies if requested
        if (includeDependencies && projects.Count > 0)
        {
            sb.AppendLine("### Project Dependencies");
            sb.AppendLine();

            var hasDependencies = false;
            foreach (var project in projects.OrderBy(p => p.Name))
            {
                var refs = await _graphService.GetProjectReferencesAsync(
                    project.Name,
                    workspacePath,
                    ct);

                if (refs.Count > 0)
                {
                    hasDependencies = true;
                    var refNames = string.Join(", ", refs.Select(r => r.Name));
                    sb.AppendLine($"- {project.Name} → {refNames}");
                }
            }

            if (!hasDependencies)
            {
                sb.AppendLine("*No project dependencies found.*");
            }

            sb.AppendLine();
        }

        // Include namespaces if requested
        if (includeNamespaces)
        {
            var namespaces = await _graphService.FindNodesAsync(
                string.Empty,
                CodeNodeType.Namespace,
                workspacePath,
                ct);

            if (namespaces.Count > 0)
            {
                sb.AppendLine("### Namespaces");
                sb.AppendLine();

                foreach (var ns in namespaces.OrderBy(n => n.FullName ?? n.Name).Take(30))
                {
                    sb.AppendLine($"- `{ns.FullName ?? ns.Name}`");
                }

                if (namespaces.Count > 30)
                {
                    sb.AppendLine($"- *... and {namespaces.Count - 30} more*");
                }

                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private async Task<string?> GetSemanticContextAsync(
        string workspacePath,
        IReadOnlyList<string> queries,
        int maxResults,
        IReadOnlyList<string>? prioritizeFiles,
        bool preferCodeContent,
        CancellationToken ct)
    {
        var allResults = new List<(string FilePath, string Content, double Score)>();

        // Run all queries and collect results
        foreach (var query in queries)
        {
            try
            {
                var queryOptions = new RagQueryOptions
                {
                    TopK = Math.Max(1, maxResults / queries.Count + 1),
                    MinScore = 0.35f,
                    SourcePathPrefix = workspacePath,
                    PrioritizeFiles = prioritizeFiles,
                    ContentTypes = preferCodeContent
                        ? [Aura.Foundation.Rag.RagContentType.Code]
                        : null,
                };

                var results = await _ragService.QueryAsync(query, queryOptions, ct);

                foreach (var result in results)
                {
                    allResults.Add((result.SourcePath ?? "unknown", result.Text, result.Score));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RAG query failed for: {Query}", query);
            }
        }

        if (allResults.Count == 0)
        {
            return null;
        }

        // Deduplicate and sort by score
        var uniqueResults = allResults
            .GroupBy(r => r.Content)
            .Select(g => g.OrderByDescending(r => r.Score).First())
            .OrderByDescending(r => r.Score)
            .Take(maxResults)
            .ToList();

        var sb = new StringBuilder();

        foreach (var result in uniqueResults)
        {
            var relativePath = GetRelativePath(workspacePath, result.FilePath);
            sb.AppendLine($"### From `{relativePath}`");
            sb.AppendLine();
            sb.AppendLine(result.Content.Trim());
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private async Task<string?> GetTypeContextAsync(
        string workspacePath,
        IReadOnlyList<string> focusTypes,
        CancellationToken ct)
    {
        var sb = new StringBuilder();

        foreach (var typeName in focusTypes)
        {
            // Find the type
            var types = await _graphService.FindNodesAsync(
                typeName,
                null, // Any type (class, interface, etc.)
                workspacePath,
                ct);

            var type = types.FirstOrDefault(t =>
                t.NodeType == CodeNodeType.Class ||
                t.NodeType == CodeNodeType.Interface ||
                t.NodeType == CodeNodeType.Record ||
                t.NodeType == CodeNodeType.Struct);

            if (type == null)
            {
                continue;
            }

            sb.AppendLine($"### {type.NodeType}: {type.FullName ?? type.Name}");
            sb.AppendLine();

            if (type.FilePath != null)
            {
                var relativePath = GetRelativePath(workspacePath, type.FilePath);
                sb.AppendLine($"Defined in: `{relativePath}`");
                if (type.LineNumber.HasValue)
                {
                    sb.AppendLine($"Line: {type.LineNumber}");
                }

                sb.AppendLine();
            }

            // Get members
            var members = await _graphService.GetTypeMembersAsync(
                type.FullName ?? type.Name,
                workspacePath,
                ct);

            if (members.Count > 0)
            {
                sb.AppendLine("**Members:**");
                foreach (var member in members.Take(20))
                {
                    var memberType = member.NodeType.ToString().ToLowerInvariant();
                    var signature = member.Signature ?? member.Name;
                    sb.AppendLine($"- {memberType}: `{signature}`");
                }

                if (members.Count > 20)
                {
                    sb.AppendLine($"- *... and {members.Count - 20} more*");
                }

                sb.AppendLine();
            }

            // Get implementations (for interfaces)
            if (type.NodeType == CodeNodeType.Interface)
            {
                var implementations = await _graphService.FindImplementationsAsync(
                    type.FullName ?? type.Name,
                    workspacePath,
                    ct);

                if (implementations.Count > 0)
                {
                    sb.AppendLine("**Implemented by:**");
                    foreach (var impl in implementations.Take(10))
                    {
                        sb.AppendLine($"- `{impl.FullName ?? impl.Name}`");
                    }

                    sb.AppendLine();
                }
            }

            // Get derived types (for classes)
            if (type.NodeType == CodeNodeType.Class)
            {
                var derived = await _graphService.FindDerivedTypesAsync(
                    type.FullName ?? type.Name,
                    workspacePath,
                    ct);

                if (derived.Count > 0)
                {
                    sb.AppendLine("**Derived types:**");
                    foreach (var d in derived.Take(10))
                    {
                        sb.AppendLine($"- `{d.FullName ?? d.Name}`");
                    }

                    sb.AppendLine();
                }
            }
        }

        return sb.Length > 0 ? sb.ToString() : null;
    }

    private static string GetRelativePath(string workspacePath, string filePath)
    {
        try
        {
            var normalizedWorkspace = Path.GetFullPath(workspacePath).TrimEnd(Path.DirectorySeparatorChar);
            var normalizedFile = Path.GetFullPath(filePath);

            if (normalizedFile.StartsWith(normalizedWorkspace, StringComparison.OrdinalIgnoreCase))
            {
                return normalizedFile.Substring(normalizedWorkspace.Length + 1);
            }

            return filePath;
        }
        catch
        {
            return filePath;
        }
    }
}
