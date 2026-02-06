namespace Anvil.Cli.Services;

using Anvil.Cli.Models;

/// <summary>
/// Analyzes tool call traces to measure how effectively agents use semantic indexing.
/// </summary>
public sealed class IndexEffectivenessAnalyzer : IIndexEffectivenessAnalyzer
{
    // Aura semantic tools - use code understanding/indexing
    private static readonly HashSet<string> SemanticTools = new(StringComparer.OrdinalIgnoreCase)
    {
        // Aura MCP tools (discovery)
        "aura_search",
        "aura_navigate",
        "aura_inspect",
        "mcp_aura_codebase_search",
        "mcp_aura_codebase_navigate",
        "mcp_aura_codebase_inspect",
        // Aura code generation/manipulation
        "aura.generate",
        "aura.refactor",
        "aura.validate",
        // Roslyn-based tools (semantic understanding)
        "roslyn.list_projects",
        "roslyn.list_classes",
        "roslyn.get_class_info",
        "roslyn.validate_compilation",
        "roslyn.find_usages",
        "roslyn.get_project_references",
        // Roslyn deterministic agent operations
        "roslyn.add_method",
        "roslyn.add_property",
        "roslyn.create_type",
        "roslyn.implement_interface",
        "roslyn.generate_constructor",
        "roslyn.generate_tests",
        "roslyn.rename",
        // Graph-based tools (code index)
        "graph.index_code",
        "graph.find_implementations",
        "graph.find_callers",
        "graph.get_type_members",
    };

    // File-level tools - bypass the index
    private static readonly HashSet<string> FileLevelTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "read_file",
        "file.read",
        "file.list",
        "grep_search",
        "list_dir",
        "semantic_search", // Copilot's semantic search, not Aura's
        "file_search",
    };

    /// <inheritdoc />
    public IndexEffectivenessMetrics Analyze(IReadOnlyList<ToolCallRecord> toolTrace)
    {
        if (toolTrace.Count == 0)
        {
            return new IndexEffectivenessMetrics
            {
                TotalToolCalls = 0,
                AuraSemanticToolCalls = 0,
                FileLevelToolCalls = 0,
                AuraToolRatio = 0,
                StepsToFirstRelevantCode = 0,
                BacktrackingEvents = 0,
                DetectedPatterns = [],
            };
        }

        var semanticCount = toolTrace.Count(tc => SemanticTools.Contains(tc.ToolName));
        var fileLevelCount = toolTrace.Count(tc => FileLevelTools.Contains(tc.ToolName));
        var total = toolTrace.Count;

        // Calculate Aura tool ratio (semantic / total discovery tools)
        var discoveryTotal = semanticCount + fileLevelCount;
        var auraRatio = discoveryTotal > 0 ? (double)semanticCount / discoveryTotal : 0;

        var patterns = DetectPatterns(toolTrace);
        var stepsToRelevant = CalculateStepsToFirstRelevantCode(toolTrace);
        var backtracking = CountBacktrackingEvents(toolTrace);

        return new IndexEffectivenessMetrics
        {
            TotalToolCalls = total,
            AuraSemanticToolCalls = semanticCount,
            FileLevelToolCalls = fileLevelCount,
            AuraToolRatio = Math.Round(auraRatio, 3),
            StepsToFirstRelevantCode = stepsToRelevant,
            BacktrackingEvents = backtracking,
            DetectedPatterns = patterns,
        };
    }

    private static IReadOnlyList<string> DetectPatterns(IReadOnlyList<ToolCallRecord> toolTrace)
    {
        var patterns = new List<string>();

        // Detect "fishing" pattern: 5+ consecutive file reads without semantic tool
        var consecutiveFileReads = 0;
        foreach (var tc in toolTrace)
        {
            if (FileLevelTools.Contains(tc.ToolName))
            {
                consecutiveFileReads++;
                if (consecutiveFileReads >= 5 && !patterns.Contains("fishing"))
                {
                    patterns.Add("fishing");
                }
            }
            else if (SemanticTools.Contains(tc.ToolName))
            {
                consecutiveFileReads = 0;
            }
        }

        // Detect "guessing" pattern: grep for terms the index should know
        // (e.g., class names, method names that appear in function signatures)
        var grepCalls = toolTrace
            .Where(tc => tc.ToolName.Equals("grep_search", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (grepCalls.Count > 0)
        {
            var hasSemanticAlternative = toolTrace.Any(tc => SemanticTools.Contains(tc.ToolName));
            if (!hasSemanticAlternative && !patterns.Contains("guessing"))
            {
                patterns.Add("guessing");
            }
        }

        // Detect "direct" pattern: semantic tool → immediate file edit (ideal behavior)
        for (var i = 0; i < toolTrace.Count - 1; i++)
        {
            var current = toolTrace[i];
            var next = toolTrace[i + 1];

            if (SemanticTools.Contains(current.ToolName) &&
                IsEditTool(next.ToolName) &&
                !patterns.Contains("direct"))
            {
                patterns.Add("direct");
                break;
            }
        }

        return patterns;
    }

    private static int CalculateStepsToFirstRelevantCode(IReadOnlyList<ToolCallRecord> toolTrace)
    {
        // Count tool calls until first edit or first successful semantic search
        for (var i = 0; i < toolTrace.Count; i++)
        {
            var tc = toolTrace[i];

            // If we hit an edit tool, that's when we found relevant code
            if (IsEditTool(tc.ToolName))
            {
                return i + 1;
            }

            // If semantic tool returns useful results (non-empty, non-error)
            if (SemanticTools.Contains(tc.ToolName) && HasSuccessfulResult(tc))
            {
                return i + 1;
            }
        }

        return toolTrace.Count;
    }

    private static int CountBacktrackingEvents(IReadOnlyList<ToolCallRecord> toolTrace)
    {
        // Count files that were read but not subsequently edited
        var filesRead = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var filesEdited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tc in toolTrace)
        {
            var filePath = ExtractFilePathFromArgs(tc);
            if (string.IsNullOrEmpty(filePath))
            {
                continue;
            }

            if (IsReadTool(tc.ToolName))
            {
                filesRead.Add(filePath);
            }
            else if (IsEditTool(tc.ToolName))
            {
                filesEdited.Add(filePath);
            }
        }

        // Backtracking = files read but never edited
        return filesRead.Count(f => !filesEdited.Contains(f));
    }

    private static bool IsEditTool(string toolName)
    {
        return toolName.Equals("replace_string_in_file", StringComparison.OrdinalIgnoreCase) ||
               toolName.Equals("create_file", StringComparison.OrdinalIgnoreCase) ||
               toolName.Equals("edit_file", StringComparison.OrdinalIgnoreCase) ||
               toolName.Equals("multi_replace_string_in_file", StringComparison.OrdinalIgnoreCase) ||
               toolName.StartsWith("aura_generate", StringComparison.OrdinalIgnoreCase) ||
               toolName.StartsWith("aura_refactor", StringComparison.OrdinalIgnoreCase) ||
               toolName.StartsWith("mcp_aura_codebase_generate", StringComparison.OrdinalIgnoreCase) ||
               toolName.StartsWith("mcp_aura_codebase_refactor", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReadTool(string toolName)
    {
        return toolName.Equals("read_file", StringComparison.OrdinalIgnoreCase) ||
               toolName.Equals("file.read", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasSuccessfulResult(ToolCallRecord tc)
    {
        // Consider result successful if non-empty and doesn't look like an error
        if (string.IsNullOrWhiteSpace(tc.Result))
        {
            return false;
        }

        var result = tc.Result.Trim();
        return !result.StartsWith("error", StringComparison.OrdinalIgnoreCase) &&
               !result.Contains("not found", StringComparison.OrdinalIgnoreCase) &&
               result.Length > 10; // Arbitrary minimum for "useful" result
    }

    private static string? ExtractFilePathFromArgs(ToolCallRecord tc)
    {
        if (string.IsNullOrEmpty(tc.Arguments))
        {
            return null;
        }

        try
        {
            // Arguments are typically JSON
            using var doc = System.Text.Json.JsonDocument.Parse(tc.Arguments);
            var root = doc.RootElement;

            // Try common property names for file paths
            if (root.TryGetProperty("filePath", out var fp))
            {
                return fp.GetString();
            }

            if (root.TryGetProperty("path", out var p))
            {
                return p.GetString();
            }

            if (root.TryGetProperty("file", out var f))
            {
                return f.GetString();
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
