// <copyright file="PythonIngesterAgent.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Agents.Ingesters;

using System.Text.RegularExpressions;
using Aura.Foundation.Agents;
using Aura.Foundation.Rag;
using Microsoft.Extensions.Logging;

/// <summary>
/// Python ingester agent using regex-based parsing.
/// Extracts classes, functions, methods, and decorators.
/// </summary>
public sealed partial class PythonIngesterAgent : RegexIngesterBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PythonIngesterAgent"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public PythonIngesterAgent(ILogger<PythonIngesterAgent> logger)
        : base(logger)
    {
    }

    /// <inheritdoc/>
    public override string AgentId => "python-ingester";

    /// <inheritdoc/>
    public override AgentMetadata Metadata { get; } = new(
        Name: "Python Ingester",
        Description: "Parses Python files using regex patterns. Extracts classes, functions, methods, and decorated symbols.",
        Capabilities: ["ingest:py", "ingest:pyw"],
        Priority: 10,
        Languages: ["python"],
        Provider: "native",
        Model: "regex",
        Temperature: 0,
        Tools: [],
        Tags: ["ingester", "python", "native", "regex"]);

    /// <inheritdoc/>
    protected override string Language => "python";

    /// <inheritdoc/>
    protected override IEnumerable<DeclarationPattern> GetPatterns()
    {
        // Class definitions (with optional decorators and docstrings)
        yield return new DeclarationPattern
        {
            Regex = ClassRegex(),
            ChunkType = ChunkTypes.Class,
            NameGroup = "name",
            SignatureGroup = "signature",
            MetadataGroups = new Dictionary<string, string>
            {
                ["decorators"] = "decorators",
                ["bases"] = "bases",
            },
        };

        // Top-level function definitions (with optional decorators)
        yield return new DeclarationPattern
        {
            Regex = FunctionRegex(),
            ChunkType = ChunkTypes.Function,
            NameGroup = "name",
            SignatureGroup = "signature",
            MetadataGroups = new Dictionary<string, string>
            {
                ["decorators"] = "decorators",
                ["async"] = "async",
                ["returnType"] = "returntype",
            },
        };

        // Type aliases (Python 3.12+ style: type X = ...)
        yield return new DeclarationPattern
        {
            Regex = TypeAliasRegex(),
            ChunkType = ChunkTypes.TypeAlias,
            NameGroup = "name",
            SignatureGroup = "signature",
        };

        // Module-level constants (UPPER_CASE assignments)
        yield return new DeclarationPattern
        {
            Regex = ConstantRegex(),
            ChunkType = ChunkTypes.Field,
            NameGroup = "name",
            SignatureGroup = "signature",
        };
    }

    /// <inheritdoc/>
    protected override List<SemanticChunk> ParseContent(string content, string filePath)
    {
        var chunks = new List<SemanticChunk>();

        // First pass: extract classes with their methods
        var classMatches = ClassRegex().Matches(content);
        foreach (Match classMatch in classMatches)
        {
            var className = classMatch.Groups["name"].Value;
            var classStartLine = GetLineNumber(content, classMatch.Index);
            var classEndLine = FindBlockEnd(content, classMatch.Index + classMatch.Length);

            // Extract class body for method parsing
            var classBodyStart = content.IndexOf(':', classMatch.Index) + 1;
            var classBodyEnd = GetCharIndex(content, classEndLine);
            var classBody = content[classBodyStart..Math.Min(classBodyEnd, content.Length)];

            // Get decorators
            var decorators = classMatch.Groups["decorators"].Success
                ? classMatch.Groups["decorators"].Value.Trim()
                : null;

            // Add the class chunk
            var classText = GetBlockText(content, classMatch.Index, classEndLine);
            chunks.Add(new SemanticChunk
            {
                Text = classText,
                FilePath = filePath,
                ChunkType = ChunkTypes.Class,
                SymbolName = className,
                FullyQualifiedName = className,
                StartLine = classStartLine,
                EndLine = classEndLine,
                Language = Language,
                Signature = classMatch.Groups["signature"].Value.Trim(),
                Metadata = decorators is not null
                    ? new Dictionary<string, string> { ["decorators"] = decorators }
                    : [],
            });

            // Extract methods within the class
            var methodMatches = MethodRegex().Matches(classBody);
            foreach (Match methodMatch in methodMatches)
            {
                var methodName = methodMatch.Groups["name"].Value;
                var methodStartLine = classStartLine + GetLineNumber(classBody, methodMatch.Index) - 1;
                var methodEndLine = FindBlockEnd(classBody, methodMatch.Index + methodMatch.Length) + classStartLine - 1;

                var isAsync = methodMatch.Groups["async"].Success;
                var returnType = methodMatch.Groups["returntype"].Success
                    ? methodMatch.Groups["returntype"].Value.Trim()
                    : null;
                var methodDecorators = methodMatch.Groups["decorators"].Success
                    ? methodMatch.Groups["decorators"].Value.Trim()
                    : null;

                var methodText = GetBlockText(classBody, methodMatch.Index, methodEndLine - classStartLine + 1);

                var metadata = new Dictionary<string, string>();
                if (isAsync)
                {
                    metadata["isAsync"] = "true";
                }

                if (returnType is not null)
                {
                    metadata["returnType"] = returnType;
                }

                if (methodDecorators is not null)
                {
                    metadata["decorators"] = methodDecorators;
                }

                // Check if it's a special method
                if (methodName.StartsWith("__") && methodName.EndsWith("__"))
                {
                    metadata["isSpecial"] = "true";
                }

                // Check for property decorator
                if (methodDecorators?.Contains("@property") == true)
                {
                    chunks.Add(new SemanticChunk
                    {
                        Text = methodText,
                        FilePath = filePath,
                        ChunkType = ChunkTypes.Property,
                        SymbolName = methodName,
                        ParentSymbol = className,
                        FullyQualifiedName = $"{className}.{methodName}",
                        StartLine = methodStartLine,
                        EndLine = methodEndLine,
                        Language = Language,
                        Signature = methodMatch.Groups["signature"].Value.Trim(),
                        Metadata = metadata,
                    });
                }
                else
                {
                    chunks.Add(new SemanticChunk
                    {
                        Text = methodText,
                        FilePath = filePath,
                        ChunkType = ChunkTypes.Method,
                        SymbolName = methodName,
                        ParentSymbol = className,
                        FullyQualifiedName = $"{className}.{methodName}",
                        StartLine = methodStartLine,
                        EndLine = methodEndLine,
                        Language = Language,
                        Signature = methodMatch.Groups["signature"].Value.Trim(),
                        Metadata = metadata,
                    });
                }
            }
        }

        // Second pass: extract top-level functions (not inside classes)
        var functionMatches = TopLevelFunctionRegex().Matches(content);
        foreach (Match funcMatch in functionMatches)
        {
            // Skip if this function is inside a class
            if (IsInsideClass(content, funcMatch.Index, classMatches))
            {
                continue;
            }

            var funcName = funcMatch.Groups["name"].Value;
            var funcStartLine = GetLineNumber(content, funcMatch.Index);
            var funcEndLine = FindBlockEnd(content, funcMatch.Index + funcMatch.Length);

            var isAsync = funcMatch.Groups["async"].Success;
            var returnType = funcMatch.Groups["returntype"].Success
                ? funcMatch.Groups["returntype"].Value.Trim()
                : null;
            var decorators = funcMatch.Groups["decorators"].Success
                ? funcMatch.Groups["decorators"].Value.Trim()
                : null;

            var funcText = GetBlockText(content, funcMatch.Index, funcEndLine);

            var metadata = new Dictionary<string, string>();
            if (isAsync)
            {
                metadata["isAsync"] = "true";
            }

            if (returnType is not null)
            {
                metadata["returnType"] = returnType;
            }

            if (decorators is not null)
            {
                metadata["decorators"] = decorators;
            }

            chunks.Add(new SemanticChunk
            {
                Text = funcText,
                FilePath = filePath,
                ChunkType = ChunkTypes.Function,
                SymbolName = funcName,
                FullyQualifiedName = funcName,
                StartLine = funcStartLine,
                EndLine = funcEndLine,
                Language = Language,
                Signature = funcMatch.Groups["signature"].Value.Trim(),
                Metadata = metadata,
            });
        }

        // Third pass: extract type aliases
        var typeAliasMatches = TypeAliasRegex().Matches(content);
        foreach (Match match in typeAliasMatches)
        {
            if (IsInsideClass(content, match.Index, classMatches))
            {
                continue;
            }

            var startLine = GetLineNumber(content, match.Index);
            chunks.Add(new SemanticChunk
            {
                Text = match.Value.Trim(),
                FilePath = filePath,
                ChunkType = ChunkTypes.TypeAlias,
                SymbolName = match.Groups["name"].Value,
                StartLine = startLine,
                EndLine = startLine,
                Language = Language,
                Signature = match.Value.Trim(),
            });
        }

        // Fourth pass: extract module-level constants
        var constantMatches = ConstantRegex().Matches(content);
        foreach (Match match in constantMatches)
        {
            if (IsInsideClass(content, match.Index, classMatches) ||
                IsInsideFunction(content, match.Index, functionMatches))
            {
                continue;
            }

            var startLine = GetLineNumber(content, match.Index);
            chunks.Add(new SemanticChunk
            {
                Text = match.Value.Trim(),
                FilePath = filePath,
                ChunkType = ChunkTypes.Field,
                SymbolName = match.Groups["name"].Value,
                StartLine = startLine,
                EndLine = startLine,
                Language = Language,
                Signature = match.Value.Trim(),
                Metadata = new Dictionary<string, string> { ["isConstant"] = "true" },
            });
        }

        // Sort by start line
        chunks.Sort((a, b) => a.StartLine.CompareTo(b.StartLine));

        return chunks;
    }

    private static bool IsInsideClass(string content, int index, MatchCollection classMatches)
    {
        foreach (Match classMatch in classMatches)
        {
            if (index > classMatch.Index && index < classMatch.Index + classMatch.Length + 1000)
            {
                // Check if we're actually inside the class block by looking at indentation
                var lineStart = content.LastIndexOf('\n', Math.Max(0, index - 1)) + 1;
                var indent = index - lineStart;

                // Class body has at least 4 spaces of indentation typically
                if (indent >= 4)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsInsideFunction(string content, int index, MatchCollection funcMatches)
    {
        foreach (Match funcMatch in funcMatches)
        {
            if (index > funcMatch.Index && index < funcMatch.Index + funcMatch.Length + 500)
            {
                var lineStart = content.LastIndexOf('\n', Math.Max(0, index - 1)) + 1;
                var indent = index - lineStart;
                if (indent >= 4)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int FindBlockEnd(string content, int startIndex)
    {
        // Find the indentation level of the first line after the colon
        var lines = content[startIndex..].Split('\n');
        var baseIndent = -1;
        var lineCount = 0;

        foreach (var line in lines)
        {
            lineCount++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var indent = line.TakeWhile(char.IsWhiteSpace).Count();

            if (baseIndent < 0)
            {
                baseIndent = indent;
                continue;
            }

            // If we hit a line with less or equal indentation (and it's not empty), block ends
            if (indent <= baseIndent - 4 && !string.IsNullOrWhiteSpace(line))
            {
                return GetLineNumber(content, startIndex) + lineCount - 2;
            }
        }

        return GetLineNumber(content, startIndex) + lineCount - 1;
    }

    private static string GetBlockText(string content, int startIndex, int endLine)
    {
        var startLine = GetLineNumber(content, startIndex);
        var lines = content.Split('\n');
        var blockLines = lines.Skip(startLine - 1).Take(endLine - startLine + 1);
        return string.Join('\n', blockLines);
    }

    private static int GetCharIndex(string content, int lineNumber)
    {
        var lines = content.Split('\n');
        var index = 0;
        for (var i = 0; i < Math.Min(lineNumber, lines.Length); i++)
        {
            index += lines[i].Length + 1;
        }

        return index;
    }

    // Matches class definitions with optional decorators
    [GeneratedRegex(
        @"(?<decorators>(?:@[\w.]+(?:\([^)]*\))?\s*\n\s*)*)" +
        @"(?<signature>class\s+(?<name>\w+)(?:\s*\((?<bases>[^)]*)\))?\s*:)",
        RegexOptions.Multiline)]
    private static partial Regex ClassRegex();

    // Matches top-level function definitions with optional decorators
    [GeneratedRegex(
        @"^(?<decorators>(?:@[\w.]+(?:\([^)]*\))?\s*\n)*)" +
        @"(?<signature>(?<async>async\s+)?def\s+(?<name>\w+)\s*\([^)]*\)(?:\s*->\s*(?<returntype>[^:]+))?\s*:)",
        RegexOptions.Multiline)]
    private static partial Regex TopLevelFunctionRegex();

    // Matches any function/method definition (used for general parsing)
    [GeneratedRegex(
        @"(?<decorators>(?:@[\w.]+(?:\([^)]*\))?\s*\n\s*)*)" +
        @"(?<signature>(?<async>async\s+)?def\s+(?<name>\w+)\s*\([^)]*\)(?:\s*->\s*(?<returntype>[^:]+))?\s*:)",
        RegexOptions.Multiline)]
    private static partial Regex FunctionRegex();

    // Matches method definitions (indented)
    [GeneratedRegex(
        @"(?<decorators>(?:\s*@[\w.]+(?:\([^)]*\))?\s*\n)*)" +
        @"\s+(?<signature>(?<async>async\s+)?def\s+(?<name>\w+)\s*\([^)]*\)(?:\s*->\s*(?<returntype>[^:]+))?\s*:)",
        RegexOptions.Multiline)]
    private static partial Regex MethodRegex();

    // Matches Python 3.12+ type aliases
    [GeneratedRegex(
        @"^(?<signature>type\s+(?<name>\w+)\s*=\s*.+)$",
        RegexOptions.Multiline)]
    private static partial Regex TypeAliasRegex();

    // Matches module-level constants (UPPER_CASE = value)
    [GeneratedRegex(
        @"^(?<signature>(?<name>[A-Z][A-Z0-9_]*)\s*(?::\s*\w+)?\s*=\s*.+)$",
        RegexOptions.Multiline)]
    private static partial Regex ConstantRegex();
}
