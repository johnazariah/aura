using System.Collections.Concurrent;
using System.Diagnostics;
using Aura.Module.Developer.Services.Testing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.Extensions.Logging;

namespace Aura.Module.Developer.Services;

public sealed partial class RoslynRefactoringService
{
    /// <inheritdoc/>
    public async Task<BlastRadiusResult> AnalyzeRenameAsync(RenameSymbolRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Analyzing blast radius for renaming {Symbol} to {NewName} in {Solution}", request.SymbolName, request.NewName, request.SolutionPath);
        try
        {
            var solution = await _workspaceService.GetSolutionAsync(request.SolutionPath, ct);
            // Find the primary symbol
            var primarySymbol = await FindSymbolAsync(solution, request.SymbolName, request.ContainingType, request.FilePath, ct);
            if (primarySymbol is null)
            {
                return new BlastRadiusResult
                {
                    Operation = "rename",
                    Symbol = request.SymbolName,
                    NewName = request.NewName,
                    RelatedSymbols = [],
                    SuggestedPlan = [],
                    Error = $"Symbol '{request.SymbolName}' not found"
                };
            }

            // Collect related symbols by naming convention
            var relatedSymbols = await DiscoverRelatedSymbolsAsync(solution, request.SymbolName, primarySymbol, ct);
            // Build suggested plan
            var suggestedPlan = BuildSuggestedPlan(request.SymbolName, request.NewName, relatedSymbols);
            // Calculate totals (only count symbols where we actually counted references)
            var totalReferences = relatedSymbols.Where(s => s.ReferenceCount > 0).Sum(s => s.ReferenceCount);
            var filesAffected = relatedSymbols.Select(s => s.FilePath).Distinct().Count();
            var filesToRename = relatedSymbols.Count(s => Path.GetFileNameWithoutExtension(s.FilePath).Equals(s.Name, StringComparison.OrdinalIgnoreCase));
            return new BlastRadiusResult
            {
                Operation = "rename",
                Symbol = request.SymbolName,
                NewName = request.NewName,
                RelatedSymbols = relatedSymbols,
                TotalReferences = totalReferences,
                FilesAffected = filesAffected,
                FilesToRename = filesToRename,
                SuggestedPlan = suggestedPlan,
                AwaitsConfirmation = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze blast radius for {Symbol}", request.SymbolName);
            return new BlastRadiusResult
            {
                Operation = "rename",
                Symbol = request.SymbolName,
                NewName = request.NewName,
                RelatedSymbols = [],
                SuggestedPlan = [],
                Error = ex.Message
            };
        }
    }

    private async Task<IReadOnlyList<RelatedSymbol>> DiscoverRelatedSymbolsAsync(Solution solution, string primaryName, ISymbol primarySymbol, CancellationToken ct)
    {
        const int MaxRelatedSymbols = 50;
        var related = new List<RelatedSymbol>();
        var seenSymbols = new HashSet<string>();
        // Add the primary symbol first - only count references for this one
        _logger.LogDebug("Counting references for primary symbol {Symbol}...", primaryName);
        var primaryRefs = await SymbolFinder.FindReferencesAsync(primarySymbol, solution, ct);
        var primaryRefCount = primaryRefs.Sum(r => r.Locations.Count());
        related.Add(new RelatedSymbol { Name = primaryName, Kind = primarySymbol.Kind.ToString(), FilePath = primarySymbol.Locations.FirstOrDefault()?.SourceTree?.FilePath ?? "", ReferenceCount = primaryRefCount });
        seenSymbols.Add(primarySymbol.ToDisplayString());
        _logger.LogDebug("Primary symbol has {Count} references. Scanning for related symbols...", primaryRefCount);
        // Find related symbols by naming convention (prefix matching)
        // E.g., if renaming "Workflow", also find "WorkflowStep", "IWorkflowService", etc.
        // NOTE: We skip reference counting for related symbols to avoid O(n) FindReferencesAsync calls
        var relatedCount = 0;
        foreach (var project in solution.Projects)
        {
            if (relatedCount >= MaxRelatedSymbols)
            {
                _logger.LogInformation("Reached maximum of {Max} related symbols, stopping scan", MaxRelatedSymbols);
                break;
            }

            var compilation = await project.GetCompilationAsync(ct);
            if (compilation is null)
                continue;
            foreach (var typeSymbol in GetAllTypes(compilation))
            {
                if (relatedCount >= MaxRelatedSymbols)
                    break;
                if (seenSymbols.Contains(typeSymbol.ToDisplayString()))
                    continue;
                var typeName = typeSymbol.Name;
                // Check if this type is related by naming convention
                // Rules:
                // 1. Starts with primaryName: WorkflowStep, WorkflowStatus → related
                // 2. Interface starting with I + primaryName: IWorkflow, IWorkflowService → related
                // 3. EndsWith is too broad (GitHubWorkflow is NOT related to Workflow)
                bool isRelated = typeName.StartsWith(primaryName, StringComparison.Ordinal) || // WorkflowStep
 (typeName.StartsWith("I") && typeName.Length > 1 && char.IsUpper(typeName[1]) && typeName[1..].StartsWith(primaryName, StringComparison.Ordinal)); // IWorkflow, IWorkflowService
                if (!isRelated)
                    continue;
                seenSymbols.Add(typeSymbol.ToDisplayString());
                relatedCount++;
                _logger.LogDebug("Found related symbol: {TypeName} ({Kind})", typeName, typeSymbol.TypeKind);
                // Don't count references for related symbols - too expensive
                // Just record that they exist and would need renaming
                // ReferenceCount = 0 means "not counted" (we only count the primary symbol)
                related.Add(new RelatedSymbol { Name = typeName, Kind = typeSymbol.TypeKind.ToString(), FilePath = typeSymbol.Locations.FirstOrDefault()?.SourceTree?.FilePath ?? "", ReferenceCount = 0 });
            }
        }

        _logger.LogInformation("Found {Count} related symbols for {Primary}", related.Count - 1, primaryName);
        return related.OrderByDescending(s => s.ReferenceCount).ToList();
    }

    /// <inheritdoc/>
    public async Task<RefactoringResult> RenameSymbolAsync(RenameSymbolRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Renaming symbol {OldName} to {NewName} in {Solution}", request.SymbolName, request.NewName, request.SolutionPath);
        if (!File.Exists(request.SolutionPath))
        {
            return RefactoringResult.Failed($"Solution file not found: {request.SolutionPath}");
        }

        try
        {
            _logger.LogDebug("Loading solution...");
            var solution = await _workspaceService.GetSolutionAsync(request.SolutionPath, ct);
            _logger.LogDebug("Solution loaded with {Count} projects", solution.ProjectIds.Count);
            // Find the symbol
            _logger.LogDebug("Finding symbol {Symbol}...", request.SymbolName);
            var symbol = await FindSymbolAsync(solution, request.SymbolName, request.ContainingType, request.FilePath, ct);
            if (symbol is null)
            {
                return RefactoringResult.Failed($"Symbol '{request.SymbolName}' not found" + (request.ContainingType != null ? $" in type '{request.ContainingType}'" : ""));
            }

            _logger.LogDebug("Found symbol: {Symbol} ({Kind})", symbol.ToDisplayString(), symbol.Kind);
            // Perform the rename
            _logger.LogInformation("Performing Roslyn rename (this may take a moment)...");
            var newSolution = await Renamer.RenameSymbolAsync(solution, symbol, new SymbolRenameOptions(), request.NewName, ct);
            _logger.LogDebug("Roslyn rename complete, computing changed documents...");
            // Get changed documents
            var changedDocs = GetChangedDocuments(solution, newSolution);
            _logger.LogInformation("Rename affects {Count} documents", changedDocs.Count);
            if (request.Preview)
            {
                // Return preview without applying
                var preview = new List<FileChange>();
                foreach (var doc in changedDocs)
                {
                    var originalDoc = solution.GetDocument(doc.Id);
                    if (originalDoc is null)
                        continue;
                    var originalText = (await originalDoc.GetTextAsync(ct)).ToString();
                    var newText = (await doc.GetTextAsync(ct)).ToString();
                    preview.Add(new FileChange(doc.FilePath ?? "", originalText, newText));
                }

                return new RefactoringResult
                {
                    Success = true,
                    Message = $"Preview: Would rename '{request.SymbolName}' to '{request.NewName}' in {changedDocs.Count} files",
                    Preview = preview
                };
            }

            // Apply changes to disk
            var modifiedFiles = new List<string>();
            foreach (var doc in changedDocs)
            {
                if (doc.FilePath is null)
                    continue;
                var text = await doc.GetTextAsync(ct);
                using (await AcquireFileLockAsync(doc.FilePath, ct))
                {
                    await File.WriteAllTextAsync(doc.FilePath, text.ToString(), ct);
                }

                modifiedFiles.Add(doc.FilePath);
                _logger.LogDebug("Updated file: {Path}", doc.FilePath);
            }

            // Clear workspace cache so subsequent operations see the updated files
            _workspaceService.ClearCache();
            _logger.LogDebug("Cleared workspace cache after rename");
            // Optionally validate with build and grep for residuals
            ValidationResult? validation = null;
            if (request.Validate)
            {
                validation = await ValidateRefactoringAsync(request.SolutionPath, request.SymbolName, ct);
            }

            return new RefactoringResult
            {
                Success = true,
                Message = $"Renamed '{request.SymbolName}' to '{request.NewName}' in {modifiedFiles.Count} files",
                ModifiedFiles = modifiedFiles,
                Validation = validation
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rename symbol {Symbol}", request.SymbolName);
            return RefactoringResult.Failed($"Failed to rename: {ex.Message}");
        }
    }
}
