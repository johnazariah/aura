using System.Text.Json;
using Aura.Api.Mcp.Tools;
using Aura.Api.Services;
using Aura.Foundation.Data.Entities;
using Aura.Foundation.Git;
using Aura.Foundation.Rag;
using Aura.Module.Developer.Data.Entities;
using Aura.Module.Developer.GitHub;
using Aura.Module.Developer.Services;
using Aura.Module.Developer.Services.Testing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.Extensions.Logging;
using RefactoringParameterInfo = Aura.Module.Developer.Services.ParameterInfo;

namespace Aura.Api.Mcp;
public sealed partial class McpHandler
{
    // =========================================================================
    // Phase 7: Meta-Tool Routers (8 consolidated tools)
    // =========================================================================
    /// <summary>
        /// aura_search - Semantic and structural code search (was aura_search_code).
        /// </summary>
        private async Task<object> SearchAsync(JsonElement? args, CancellationToken ct)
    {
        // Delegates to existing SearchCodeAsync logic
        return await SearchCodeAsync(args, ct);
    }

    // =========================================================================
    // Existing Tool Implementations (used by meta-tool routers)
    // =========================================================================
    /// <summary>
        /// Resolves a workspacePath (which may be a worktree) to the main repository path.
        /// This ensures RAG queries use the indexed base repository, not the worktree.
        /// </summary>
        private async Task<string?> ResolveToMainRepositoryAsync(string? workspacePath, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(workspacePath))
            return null;
        var result = await _worktreeService.GetMainRepositoryPathAsync(workspacePath, ct);
        if (result.Success && result.Value is not null)
        {
            if (!result.Value.Equals(workspacePath, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Resolved worktree {WorktreePath} to main repository {MainRepoPath}", workspacePath, result.Value);
            }

            return result.Value;
        }

        // Not a git repo or failed to resolve - return original path
        _logger.LogDebug("Could not resolve {WorkspacePath} to main repository, using as-is", workspacePath);
        return workspacePath;
    }

    private async Task<object> SearchCodeAsync(JsonElement? args, CancellationToken ct)
    {
        var query = args?.GetProperty("query").GetString() ?? "";
        var limit = 10;
        if (args.HasValue && args.Value.TryGetProperty("limit", out var limitEl))
        {
            limit = limitEl.GetInt32();
        }

        // Parse workspacePath and detect if it's a worktree
        string? sourcePathPrefix = null;
        DetectedWorktree? worktreeInfo = null;
        if (args.HasValue && args.Value.TryGetProperty("workspacePath", out var workspaceEl))
        {
            var workspacePath = workspaceEl.GetString();
            // Detect worktree synchronously - we need this for path translation
            if (!string.IsNullOrEmpty(workspacePath))
            {
                worktreeInfo = GitWorktreeDetector.Detect(workspacePath);
            }

            if (worktreeInfo?.IsWorktree == true)
            {
                // Use main repo path for index lookup
                sourcePathPrefix = worktreeInfo.Value.MainRepoPath;
                _logger.LogDebug("Search from worktree {WorktreePath} -> querying index at {MainRepoPath}", worktreeInfo.Value.WorktreePath, worktreeInfo.Value.MainRepoPath);
            }
            else
            {
                // Fallback to async resolution for non-worktree cases
                sourcePathPrefix = await ResolveToMainRepositoryAsync(workspacePath, ct);
            }
        }

        // Parse contentType filter
        string? contentTypeFilter = null;
        if (args.HasValue && args.Value.TryGetProperty("contentType", out var contentTypeEl))
        {
            contentTypeFilter = contentTypeEl.GetString();
        }

        // Map contentType string to RagContentType list
        var contentTypes = contentTypeFilter switch
        {
            "code" => new[]
            {
                RagContentType.Code
            },
            "docs" => new[]
            {
                RagContentType.Markdown,
                RagContentType.PlainText
            },
            "config" => new[]
            {
                RagContentType.PlainText
            }, // JSON/YAML indexed as PlainText
            _ => null // "all" or unspecified
        };
        var options = new RagQueryOptions
        {
            TopK = limit,
            ContentTypes = contentTypes,
            SourcePathPrefix = sourcePathPrefix
        };
        // Extract potential symbol names from query (words that look like identifiers)
        // Handles multi-word queries like "IGitWorktreeService CreateAsync WorktreeResult"
        var symbolCandidates = ExtractSymbolCandidates(query);
        // Search for each symbol candidate in the code graph
        var allExactMatches = new List<CodeNode>();
        foreach (var symbol in symbolCandidates)
        {
            var matches = await _graphService.FindNodesAsync(symbol, cancellationToken: ct);
            allExactMatches.AddRange(matches);
        }

        // Deduplicate by full name and prioritize: interfaces, classes, enums first
        var exactMatchResults = allExactMatches.DistinctBy(n => n.FullName).OrderByDescending(n => n.NodeType switch
        {
            CodeNodeType.Interface => 100,
            CodeNodeType.Class => 90,
            CodeNodeType.Enum => 85,
            CodeNodeType.Record => 80,
            CodeNodeType.Struct => 75,
            CodeNodeType.Method => 50,
            CodeNodeType.Property => 40,
            _ => 0
        }).Take(5) // Limit to top 5 exact matches
        .Select(n => new
        {
            content = $"[EXACT MATCH] {n.NodeType}: {n.FullName}",
            filePath = TranslatePathIfWorktree(n.FilePath, worktreeInfo),
            line = n.LineNumber,
            score = 1.0, // Perfect match
            contentType = "Code",
            isExactMatch = true
        }).ToList();
        var ragResults = await _ragService.QueryAsync(query, options, ct);
        var semanticResults = ragResults.Select(r => new { content = r.Text, filePath = TranslatePathIfWorktree(r.SourcePath, worktreeInfo), line = (int?)null, score = r.Score, contentType = r.ContentType.ToString(), isExactMatch = false });
        // Combine: exact matches first, then semantic results (deduplicated)
        var exactFilePaths = exactMatchResults.Select(e => e.filePath).ToHashSet();
        var combinedResults = exactMatchResults.Concat(semanticResults.Where(s => !exactFilePaths.Contains(s.filePath))).Take(limit);
        return combinedResults;
    }

    /// <summary>
        /// Extracts potential symbol names from a search query.
        /// Identifies words that look like code identifiers (PascalCase, camelCase, contain underscores, etc.)
        /// </summary>
        private static List<string> ExtractSymbolCandidates(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<string>();
        // Split on whitespace and common separators
        var tokens = query.Split(new[] { ' ', '\t', '\n', '\r', ',', ';', ':', '(', ')', '[', ']', '{', '}' }, StringSplitOptions.RemoveEmptyEntries);
        var candidates = new List<string>();
        foreach (var token in tokens)
        {
            // Skip very short tokens (likely noise) unless they look like acronyms
            if (token.Length < 2)
                continue;
            // Skip common English words that aren't likely symbol names
            var lower = token.ToLowerInvariant();
            if (IsCommonWord(lower))
                continue;
            // Keep tokens that look like identifiers:
            // - Start with letter or underscore
            // - Contain only alphanumeric and underscores
            // - PascalCase or camelCase patterns
            if (LooksLikeIdentifier(token))
            {
                candidates.Add(token);
            }
        }

        return candidates.Distinct().ToList();
    }

    /// <summary>
        /// Checks if a token looks like a code identifier.
        /// </summary>
        private static bool LooksLikeIdentifier(string token)
    {
        if (string.IsNullOrEmpty(token))
            return false;
        // Must start with letter or underscore
        var first = token[0];
        if (!char.IsLetter(first) && first != '_')
            return false;
        // All characters must be alphanumeric or underscore
        foreach (var c in token)
        {
            if (!char.IsLetterOrDigit(c) && c != '_')
                return false;
        }

        return true;
    }

    /// <summary>
        /// Checks if a word is a common English word that's unlikely to be a symbol name.
        /// </summary>
        private static bool IsCommonWord(string word)
    {
        // Common words to filter out
        return word switch
        {
            "the" or "a" or "an" or "and" or "or" or "but" or "in" or "on" or "at" or "to" or "for" or "of" or "with" or "by" or "from" or "as" or "is" or "was" or "are" or "were" or "be" or "been" or "being" or "have" or "has" or "had" or "do" or "does" or "did" or "will" or "would" or "could" or "should" or "may" or "might" or "must" or "can" or "this" or "that" or "these" or "those" or "it" or "its" or "not" or "no" or "yes" or "all" or "any" or "some" or "find" or "get" or "set" or "how" or "what" or "where" or "when" or "why" or "which" => true,
            _ => false
        };
    }
}
