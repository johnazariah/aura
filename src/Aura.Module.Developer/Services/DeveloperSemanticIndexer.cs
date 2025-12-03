// <copyright file="DeveloperSemanticIndexer.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services;

using System.Diagnostics;
using Aura.Foundation.Rag;
using Microsoft.Extensions.Logging;

/// <summary>
/// Developer module's semantic indexer that combines code graph and RAG indexing.
/// - For C# code: Uses Roslyn-based CodeGraphIndexer (fast structural indexing)
/// - For text/docs: Uses RAG service with text chunking (selective embeddings)
/// </summary>
public sealed class DeveloperSemanticIndexer : ISemanticIndexer
{
    private static readonly string[] DocumentPatterns = ["*.md", "*.txt"];
    private static readonly string[] ExcludedDirectories = ["bin", "obj", "node_modules", ".git", ".vs", "packages"];

    private readonly ICodeGraphIndexer _graphIndexer;
    private readonly IRagService _ragService;
    private readonly ILogger<DeveloperSemanticIndexer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeveloperSemanticIndexer"/> class.
    /// </summary>
    public DeveloperSemanticIndexer(
        ICodeGraphIndexer graphIndexer,
        IRagService ragService,
        ILogger<DeveloperSemanticIndexer> logger)
    {
        _graphIndexer = graphIndexer;
        _ragService = ragService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SemanticIndexResult> IndexDirectoryAsync(
        string directoryPath,
        SemanticIndexOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new SemanticIndexOptions();
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Starting semantic indexing of {DirectoryPath}", directoryPath);

        if (!Directory.Exists(directoryPath))
        {
            return new SemanticIndexResult
            {
                Success = false,
                Errors = [$"Directory not found: {directoryPath}"],
                Duration = stopwatch.Elapsed,
            };
        }

        var errors = new List<string>();
        var warnings = new List<string>();
        var filesByLanguage = new Dictionary<string, int>();
        var totalChunks = 0;
        var totalFiles = 0;

        try
        {
            // Phase 1: Find and index C# solution/project using Roslyn
            var graphResult = await IndexCSharpCodeAsync(directoryPath, cancellationToken);
            filesByLanguage["csharp"] = graphResult.FilesIndexed;
            totalChunks += graphResult.NodesCreated; // Graph nodes count as "chunks"
            totalFiles += graphResult.FilesIndexed;
            if (!graphResult.Success && !string.IsNullOrEmpty(graphResult.ErrorMessage))
            {
                warnings.Add($"C# indexing: {graphResult.ErrorMessage}");
            }

            // Phase 2: Index documentation files using RAG embeddings
            var docResult = await IndexDocumentFilesAsync(directoryPath, options.Recursive, cancellationToken);
            filesByLanguage["markdown"] = docResult.FilesIndexed;
            totalChunks += docResult.ChunksCreated;
            totalFiles += docResult.FilesIndexed;
            warnings.AddRange(docResult.Warnings);

            stopwatch.Stop();

            _logger.LogInformation(
                "Semantic indexing complete: {FilesIndexed} files, {ChunksCreated} chunks in {Duration:N2}s",
                totalFiles,
                totalChunks,
                stopwatch.Elapsed.TotalSeconds);

            return new SemanticIndexResult
            {
                Success = true,
                FilesIndexed = totalFiles,
                ChunksCreated = totalChunks,
                FilesByLanguage = filesByLanguage,
                Duration = stopwatch.Elapsed,
                Errors = errors,
                Warnings = warnings,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Semantic indexing failed for {DirectoryPath}", directoryPath);
            errors.Add(ex.Message);
            return new SemanticIndexResult
            {
                Success = false,
                FilesIndexed = totalFiles,
                ChunksCreated = totalChunks,
                FilesByLanguage = filesByLanguage,
                Duration = stopwatch.Elapsed,
                Errors = errors,
                Warnings = warnings,
            };
        }
    }

    private async Task<CodeGraphIndexResult> IndexCSharpCodeAsync(
        string directoryPath,
        CancellationToken cancellationToken)
    {
        // Find solution or project file
        var solutionPath = FindSolutionOrProject(directoryPath);
        if (solutionPath == null)
        {
            _logger.LogDebug("No C# solution or project found in {DirectoryPath}", directoryPath);
            return new CodeGraphIndexResult { Success = true, FilesIndexed = 0 };
        }

        _logger.LogInformation("Found C# solution/project: {SolutionPath}", solutionPath);

        try
        {
            return await _graphIndexer.IndexAsync(solutionPath, directoryPath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to index C# code from {SolutionPath}", solutionPath);
            return new CodeGraphIndexResult
            {
                Success = false,
                ErrorMessage = ex.Message,
            };
        }
    }

    private async Task<(int FilesIndexed, int ChunksCreated, List<string> Warnings)> IndexDocumentFilesAsync(
        string directoryPath,
        bool recursive,
        CancellationToken cancellationToken)
    {
        var filesIndexed = 0;
        var chunksCreated = 0;
        var warnings = new List<string>();

        // Find documentation files
        var docFiles = new List<string>();
        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        foreach (var pattern in DocumentPatterns)
        {
            var matchingFiles = Directory.GetFiles(directoryPath, pattern, searchOption)
                .Where(f => !IsExcludedPath(f));
            docFiles.AddRange(matchingFiles);
        }

        if (docFiles.Count == 0)
        {
            _logger.LogDebug("No documentation files found in {DirectoryPath}", directoryPath);
            return (0, 0, warnings);
        }

        _logger.LogInformation("Indexing {FileCount} documentation files", docFiles.Count);

        // Index each file using RAG service
        foreach (var file in docFiles)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var content = await File.ReadAllTextAsync(file, cancellationToken);
                if (string.IsNullOrWhiteSpace(content))
                {
                    continue;
                }

                var ragContent = RagContent.FromFile(file, content);
                await _ragService.IndexAsync(ragContent, cancellationToken);
                filesIndexed++;
                chunksCreated++; // Approximate - actual chunking happens inside RagService
            }
            catch (Exception ex)
            {
                warnings.Add($"Error indexing {file}: {ex.Message}");
            }
        }

        return (filesIndexed, chunksCreated, warnings);
    }

    private static string? FindSolutionOrProject(string directoryPath)
    {
        // First, look for solution files
        var solutions = Directory.GetFiles(directoryPath, "*.sln", SearchOption.TopDirectoryOnly);
        if (solutions.Length > 0)
        {
            return solutions[0];
        }

        // Then look for project files in the directory
        var projects = Directory.GetFiles(directoryPath, "*.csproj", SearchOption.TopDirectoryOnly);
        if (projects.Length > 0)
        {
            return projects[0];
        }

        // Look one level up for solution
        var parent = Directory.GetParent(directoryPath);
        if (parent != null)
        {
            solutions = Directory.GetFiles(parent.FullName, "*.sln", SearchOption.TopDirectoryOnly);
            if (solutions.Length > 0)
            {
                return solutions[0];
            }
        }

        // Look in src/ directory for solution
        var srcPath = Path.Combine(directoryPath, "src");
        if (Directory.Exists(srcPath))
        {
            solutions = Directory.GetFiles(srcPath, "*.sln", SearchOption.TopDirectoryOnly);
            if (solutions.Length > 0)
            {
                return solutions[0];
            }
        }

        return null;
    }

    private static bool IsExcludedPath(string path)
    {
        var pathParts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return pathParts.Any(part => ExcludedDirectories.Contains(part, StringComparer.OrdinalIgnoreCase));
    }
}
