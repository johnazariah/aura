// <copyright file="ICodebaseContextService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services;

/// <summary>
/// Service for building comprehensive codebase context for agents.
/// Combines structural information from the code graph with semantic
/// search results from RAG to provide agents with full understanding
/// of a codebase.
/// </summary>
public interface ICodebaseContextService
{
    /// <summary>
    /// Gets comprehensive codebase context for an agent.
    /// </summary>
    /// <param name="workspacePath">The workspace path to get context for.</param>
    /// <param name="options">Options controlling what context to include.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The codebase context.</returns>
    Task<CodebaseContext> GetContextAsync(
        string workspacePath,
        CodebaseContextOptions options,
        CancellationToken ct = default);
}

/// <summary>
/// Options for controlling what codebase context to retrieve.
/// </summary>
public record CodebaseContextOptions
{
    /// <summary>
    /// Gets RAG queries for semantic search.
    /// If provided, semantic search results will be included in context.
    /// </summary>
    public IReadOnlyList<string>? RagQueries { get; init; }

    /// <summary>
    /// Gets whether to include project structure from the code graph.
    /// Default is true.
    /// </summary>
    public bool IncludeProjectStructure { get; init; } = true;

    /// <summary>
    /// Gets whether to include namespace hierarchy.
    /// Default is false (can be verbose for large codebases).
    /// </summary>
    public bool IncludeNamespaces { get; init; }

    /// <summary>
    /// Gets whether to include project dependencies/references.
    /// Default is true.
    /// </summary>
    public bool IncludeDependencies { get; init; } = true;

    /// <summary>
    /// Gets specific types to retrieve detailed information for.
    /// When provided, includes inheritance and member information.
    /// </summary>
    public IReadOnlyList<string>? FocusTypes { get; init; }

    /// <summary>
    /// Gets the maximum number of RAG results to include.
    /// Default is 10.
    /// </summary>
    public int MaxRagResults { get; init; } = 10;

    /// <summary>
    /// Gets file names to prioritize in RAG results.
    /// Results from files matching these names will be boosted in ranking.
    /// </summary>
    public IReadOnlyList<string>? PrioritizeFiles { get; init; }

    /// <summary>
    /// Gets whether to prefer code content over documentation in RAG results.
    /// When true, filters to Code content type. Default is false (all types).
    /// </summary>
    public bool PreferCodeContent { get; init; }

    /// <summary>
    /// Gets the default options (project structure + dependencies, no RAG).
    /// </summary>
    public static CodebaseContextOptions Default => new();

    /// <summary>
    /// Creates options for documentation tasks (structure + RAG, all content types).
    /// </summary>
    public static CodebaseContextOptions ForDocumentation(
        IReadOnlyList<string> ragQueries,
        IReadOnlyList<string>? prioritizeFiles = null) =>
        new()
        {
            RagQueries = ragQueries,
            IncludeProjectStructure = true,
            IncludeDependencies = true,
            MaxRagResults = 20,
            PrioritizeFiles = prioritizeFiles,
            PreferCodeContent = false, // Include docs for documentation tasks
        };

    /// <summary>
    /// Creates options for coding tasks (structure + types + RAG, code content preferred).
    /// </summary>
    public static CodebaseContextOptions ForCoding(
        IReadOnlyList<string> ragQueries,
        IReadOnlyList<string>? focusTypes = null,
        IReadOnlyList<string>? prioritizeFiles = null) =>
        new()
        {
            RagQueries = ragQueries,
            IncludeProjectStructure = true,
            IncludeDependencies = true,
            IncludeNamespaces = true,
            FocusTypes = focusTypes,
            MaxRagResults = 20,
            PrioritizeFiles = prioritizeFiles,
            PreferCodeContent = true, // Prefer actual code for coding tasks
        };
}

/// <summary>
/// Comprehensive codebase context for agent consumption.
/// </summary>
public record CodebaseContext
{
    /// <summary>
    /// Gets the project structure summary from the code graph.
    /// Always present if requested.
    /// </summary>
    public required string ProjectStructure { get; init; }

    /// <summary>
    /// Gets the semantic context from RAG search.
    /// Present only if RAG queries were provided.
    /// </summary>
    public string? SemanticContext { get; init; }

    /// <summary>
    /// Gets detailed type information for focus types.
    /// Present only if FocusTypes were requested.
    /// </summary>
    public string? TypeContext { get; init; }

    /// <summary>
    /// Gets whether any context was retrieved.
    /// </summary>
    public bool HasContent =>
        !string.IsNullOrEmpty(ProjectStructure) ||
        !string.IsNullOrEmpty(SemanticContext) ||
        !string.IsNullOrEmpty(TypeContext);

    /// <summary>
    /// Formats all context sections into a single string for LLM consumption.
    /// </summary>
    /// <returns>Formatted context string.</returns>
    public string ToPromptContext()
    {
        var sections = new List<string>();

        if (!string.IsNullOrEmpty(ProjectStructure))
        {
            sections.Add(ProjectStructure);
        }

        if (!string.IsNullOrEmpty(TypeContext))
        {
            sections.Add("## Type Information\n\n" + TypeContext);
        }

        if (!string.IsNullOrEmpty(SemanticContext))
        {
            sections.Add("## Relevant Code and Documentation\n\n" + SemanticContext);
        }

        return string.Join("\n\n---\n\n", sections);
    }
}
