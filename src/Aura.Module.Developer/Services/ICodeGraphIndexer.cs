// <copyright file="ICodeGraphIndexer.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services;

/// <summary>
/// Service for indexing a codebase into the code graph.
/// </summary>
public interface ICodeGraphIndexer
{
    /// <summary>
    /// Indexes a solution or project into the code graph.
    /// </summary>
    /// <param name="solutionOrProjectPath">Path to a .sln or .csproj file.</param>
    /// <param name="repositoryPath">The repository path for node isolation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The indexing result with statistics.</returns>
    Task<CodeGraphIndexResult> IndexAsync(
        string solutionOrProjectPath,
        string repositoryPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-indexes a repository (clears existing graph, then indexes).
    /// </summary>
    /// <param name="solutionOrProjectPath">Path to a .sln or .csproj file.</param>
    /// <param name="repositoryPath">The repository path for node isolation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The indexing result with statistics.</returns>
    Task<CodeGraphIndexResult> ReindexAsync(
        string solutionOrProjectPath,
        string repositoryPath,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a code graph indexing operation.
/// </summary>
public record CodeGraphIndexResult
{
    /// <summary>Gets whether the indexing was successful.</summary>
    public required bool Success { get; init; }

    /// <summary>Gets the number of nodes created.</summary>
    public int NodesCreated { get; init; }

    /// <summary>Gets the number of edges created.</summary>
    public int EdgesCreated { get; init; }

    /// <summary>Gets the number of projects indexed.</summary>
    public int ProjectsIndexed { get; init; }

    /// <summary>Gets the number of files indexed.</summary>
    public int FilesIndexed { get; init; }

    /// <summary>Gets the number of types indexed.</summary>
    public int TypesIndexed { get; init; }

    /// <summary>Gets the indexing duration.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Gets any error message if indexing failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets warnings encountered during indexing.</summary>
    public List<string> Warnings { get; init; } = [];
}
