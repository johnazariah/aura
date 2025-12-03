// <copyright file="FSharpIngesterAgent.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Agents.Ingesters;

using System.Text.RegularExpressions;
using Aura.Foundation.Agents;
using Aura.Foundation.Rag;
using Microsoft.Extensions.Logging;

/// <summary>
/// F# ingester agent using regex-based parsing.
/// Extracts modules, types, functions, and bindings from F# source files.
/// </summary>
public sealed partial class FSharpIngesterAgent : RegexIngesterBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FSharpIngesterAgent"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public FSharpIngesterAgent(ILogger<FSharpIngesterAgent> logger)
        : base(logger)
    {
    }

    /// <inheritdoc/>
    public override string AgentId => "fsharp-ingester";

    /// <inheritdoc/>
    public override AgentMetadata Metadata { get; } = new(
        Name: "F# Ingester",
        Description: "Parses F# files using regex patterns. Extracts modules, types, functions, and bindings.",
        Capabilities: ["ingest:fs", "ingest:fsi", "ingest:fsx"],
        Priority: 10,
        Languages: ["fsharp"],
        Provider: "native",
        Model: "regex",
        Temperature: 0,
        Tools: [],
        Tags: ["ingester", "fsharp", "native", "regex"]);

    /// <inheritdoc/>
    protected override string Language => "fsharp";

    /// <inheritdoc/>
    protected override IEnumerable<DeclarationPattern> GetPatterns()
    {
        // Not used - we override ParseContent for more control
        yield break;
    }

    /// <inheritdoc/>
    protected override List<SemanticChunk> ParseContent(string content, string filePath)
    {
        var chunks = new List<SemanticChunk>();

        // Extract namespace declarations
        ExtractNamespaces(content, filePath, chunks);

        // Extract module declarations
        ExtractModules(content, filePath, chunks);

        // Extract type definitions (records, discriminated unions, classes)
        ExtractTypes(content, filePath, chunks);

        // Extract let bindings (functions and values)
        ExtractLetBindings(content, filePath, chunks);

        // Extract member declarations
        ExtractMembers(content, filePath, chunks);

        // Sort by start line
        chunks.Sort((a, b) => a.StartLine.CompareTo(b.StartLine));

        return chunks;
    }

    private void ExtractNamespaces(string content, string filePath, List<SemanticChunk> chunks)
    {
        var matches = NamespaceRegex().Matches(content);
        foreach (Match match in matches)
        {
            var name = match.Groups["name"].Value;
            var startLine = GetLineNumber(content, match.Index);

            chunks.Add(new SemanticChunk
            {
                Text = match.Value.Trim(),
                FilePath = filePath,
                ChunkType = ChunkTypes.Namespace,
                SymbolName = name,
                FullyQualifiedName = name,
                StartLine = startLine,
                EndLine = startLine,
                Language = Language,
                Signature = $"namespace {name}",
                Metadata = new Dictionary<string, string>(),
            });
        }
    }

    private void ExtractModules(string content, string filePath, List<SemanticChunk> chunks)
    {
        var matches = ModuleRegex().Matches(content);
        foreach (Match match in matches)
        {
            var name = match.Groups["name"].Value;
            var startLine = GetLineNumber(content, match.Index);

            // Determine if it's a top-level module (ends with newline) or nested module (has = followed by content)
            var isNested = match.Groups["nested"].Success;
            var isPrivate = match.Groups["private"].Success;
            var isInternal = match.Groups["internal"].Success;

            int endLine;
            string text;

            if (isNested)
            {
                // Nested module - find the end by indentation
                endLine = FindIndentationBlockEnd(content, startLine);
                text = GetBlockText(content, startLine, endLine);
            }
            else
            {
                // Top-level module declaration
                endLine = startLine;
                text = match.Value.Trim();
            }

            var metadata = new Dictionary<string, string>
            {
                ["isNested"] = isNested.ToString().ToLowerInvariant(),
            };

            if (isPrivate)
            {
                metadata["accessibility"] = "private";
            }
            else if (isInternal)
            {
                metadata["accessibility"] = "internal";
            }

            chunks.Add(new SemanticChunk
            {
                Text = text,
                FilePath = filePath,
                ChunkType = ChunkTypes.Namespace,
                SymbolName = name,
                FullyQualifiedName = name,
                StartLine = startLine,
                EndLine = endLine,
                Language = Language,
                Signature = $"module {name}",
                Metadata = metadata,
            });
        }
    }

    private void ExtractTypes(string content, string filePath, List<SemanticChunk> chunks)
    {
        var matches = TypeRegex().Matches(content);
        foreach (Match match in matches)
        {
            var name = match.Groups["name"].Value;
            var startLine = GetLineNumber(content, match.Index);
            var endLine = FindTypeBlockEnd(content, startLine);

            var text = GetBlockText(content, startLine, endLine);

            var isPrivate = match.Groups["private"].Success;
            var isInternal = match.Groups["internal"].Success;
            var hasTypeParams = match.Groups["typeparams"].Success;

            // Determine the type kind from the definition
            var chunkType = DetermineTypeKind(content, match.Index);

            var metadata = new Dictionary<string, string>();

            if (isPrivate)
            {
                metadata["accessibility"] = "private";
            }
            else if (isInternal)
            {
                metadata["accessibility"] = "internal";
            }

            if (hasTypeParams)
            {
                metadata["typeParameters"] = match.Groups["typeparams"].Value;
            }

            chunks.Add(new SemanticChunk
            {
                Text = text,
                FilePath = filePath,
                ChunkType = chunkType,
                SymbolName = name,
                FullyQualifiedName = name,
                StartLine = startLine,
                EndLine = endLine,
                Language = Language,
                Signature = $"type {name}",
                Metadata = metadata,
            });
        }
    }

    private void ExtractLetBindings(string content, string filePath, List<SemanticChunk> chunks)
    {
        var matches = LetBindingRegex().Matches(content);
        foreach (Match match in matches)
        {
            // Skip if inside a type definition (member bindings are handled separately)
            if (IsInsideTypeDefinition(content, match.Index))
            {
                continue;
            }

            var name = match.Groups["name"].Value;
            var startLine = GetLineNumber(content, match.Index);
            var endLine = FindLetBindingEnd(content, startLine);

            var text = GetBlockText(content, startLine, endLine);

            var isRec = match.Groups["rec"].Success;
            var isInline = match.Groups["inline"].Success;
            var isPrivate = match.Groups["private"].Success;
            var isInternal = match.Groups["internal"].Success;
            var hasParams = match.Groups["params"].Success;

            // Determine if it's a function or value
            var chunkType = hasParams ? ChunkTypes.Function : ChunkTypes.Field;

            var metadata = new Dictionary<string, string>
            {
                ["isRecursive"] = isRec.ToString().ToLowerInvariant(),
                ["isInline"] = isInline.ToString().ToLowerInvariant(),
            };

            if (isPrivate)
            {
                metadata["accessibility"] = "private";
            }
            else if (isInternal)
            {
                metadata["accessibility"] = "internal";
            }

            if (match.Groups["returntype"].Success)
            {
                metadata["returnType"] = match.Groups["returntype"].Value.Trim();
            }

            chunks.Add(new SemanticChunk
            {
                Text = text,
                FilePath = filePath,
                ChunkType = chunkType,
                SymbolName = name,
                FullyQualifiedName = name,
                StartLine = startLine,
                EndLine = endLine,
                Language = Language,
                Signature = match.Groups["signature"].Value.Trim(),
                Metadata = metadata,
            });
        }
    }

    private void ExtractMembers(string content, string filePath, List<SemanticChunk> chunks)
    {
        var matches = MemberRegex().Matches(content);
        foreach (Match match in matches)
        {
            var name = match.Groups["name"].Value;
            var startLine = GetLineNumber(content, match.Index);
            var endLine = FindLetBindingEnd(content, startLine);

            var text = GetBlockText(content, startLine, endLine);

            var memberKind = match.Groups["kind"].Value;
            var isPrivate = match.Groups["private"].Success;
            var isInternal = match.Groups["internal"].Success;
            var isOverride = match.Groups["override"].Success;
            var isAbstract = match.Groups["abstract"].Success;
            var isStatic = match.Groups["static"].Success;

            var chunkType = memberKind switch
            {
                "val" => ChunkTypes.Property,
                _ => ChunkTypes.Method,
            };

            var metadata = new Dictionary<string, string>
            {
                ["memberKind"] = memberKind,
                ["isOverride"] = isOverride.ToString().ToLowerInvariant(),
                ["isAbstract"] = isAbstract.ToString().ToLowerInvariant(),
                ["isStatic"] = isStatic.ToString().ToLowerInvariant(),
            };

            if (isPrivate)
            {
                metadata["accessibility"] = "private";
            }
            else if (isInternal)
            {
                metadata["accessibility"] = "internal";
            }

            // Find the parent type
            var parentType = FindParentType(content, match.Index);
            string? parentSymbol = null;
            var fullName = name;

            if (!string.IsNullOrEmpty(parentType))
            {
                parentSymbol = parentType;
                fullName = $"{parentType}.{name}";
            }

            chunks.Add(new SemanticChunk
            {
                Text = text,
                FilePath = filePath,
                ChunkType = chunkType,
                SymbolName = name,
                ParentSymbol = parentSymbol,
                FullyQualifiedName = fullName,
                StartLine = startLine,
                EndLine = endLine,
                Language = Language,
                Signature = match.Groups["signature"].Value.Trim(),
                Metadata = metadata,
            });
        }
    }

    private static string DetermineTypeKind(string content, int typeIndex)
    {
        // Look at what follows the type definition to determine its kind
        var afterType = content[(typeIndex + 4)..Math.Min(typeIndex + 500, content.Length)];

        if (afterType.Contains("{|") || afterType.Contains("{ "))
        {
            return ChunkTypes.Struct; // Record type
        }

        if (afterType.Contains(" | ") || afterType.Contains("\n    | "))
        {
            return ChunkTypes.Enum; // Discriminated union
        }

        if (afterType.Contains("interface") || afterType.Contains("abstract"))
        {
            return ChunkTypes.Interface;
        }

        if (afterType.Contains("class") || afterType.Contains("inherit"))
        {
            return ChunkTypes.Class;
        }

        return ChunkTypes.TypeAlias;
    }

    private static bool IsInsideTypeDefinition(string content, int index)
    {
        // Simple heuristic: check if we're in a type block by looking for 'type' before us
        // and checking indentation
        var beforeIndex = content[..index];
        var lastTypeMatch = Regex.Match(beforeIndex, @"^\s*type\s+\w+", RegexOptions.Multiline | RegexOptions.RightToLeft);

        if (!lastTypeMatch.Success)
        {
            return false;
        }

        // Check if the let binding is indented more than the type
        var typeLineStart = beforeIndex.LastIndexOf('\n', lastTypeMatch.Index) + 1;
        var letLineStart = beforeIndex.LastIndexOf('\n') + 1;

        var typeIndent = lastTypeMatch.Index - typeLineStart;
        var letIndent = index - letLineStart;

        return letIndent > typeIndent;
    }

    private static string? FindParentType(string content, int memberIndex)
    {
        var beforeMember = content[..memberIndex];
        var typeMatch = Regex.Match(beforeMember, @"^\s*type\s+(?<name>\w+)", RegexOptions.Multiline | RegexOptions.RightToLeft);

        return typeMatch.Success ? typeMatch.Groups["name"].Value : null;
    }

    private static int FindIndentationBlockEnd(string content, int startLine)
    {
        var lines = content.Split('\n');
        if (startLine < 1 || startLine > lines.Length)
        {
            return startLine;
        }

        var startLineText = lines[startLine - 1];
        var baseIndent = startLineText.Length - startLineText.TrimStart().Length;

        for (var i = startLine; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var currentIndent = line.Length - line.TrimStart().Length;
            if (currentIndent <= baseIndent && i > startLine)
            {
                return i; // Previous line was the end
            }
        }

        return lines.Length;
    }

    private static int FindTypeBlockEnd(string content, int startLine)
    {
        var lines = content.Split('\n');
        if (startLine < 1 || startLine > lines.Length)
        {
            return startLine;
        }

        var startLineText = lines[startLine - 1];
        var baseIndent = startLineText.Length - startLineText.TrimStart().Length;

        for (var i = startLine; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var trimmed = line.TrimStart();

            // Check for new top-level declarations
            if (trimmed.StartsWith("type ") ||
                trimmed.StartsWith("module ") ||
                trimmed.StartsWith("namespace ") ||
                (trimmed.StartsWith("let ") && line.Length - trimmed.Length <= baseIndent) ||
                trimmed.StartsWith("open "))
            {
                var currentIndent = line.Length - trimmed.Length;
                if (currentIndent <= baseIndent && i > startLine)
                {
                    return i; // Previous line was the end
                }
            }
        }

        return lines.Length;
    }

    private static int FindLetBindingEnd(string content, int startLine)
    {
        var lines = content.Split('\n');
        if (startLine < 1 || startLine > lines.Length)
        {
            return startLine;
        }

        var startLineText = lines[startLine - 1];
        var baseIndent = startLineText.Length - startLineText.TrimStart().Length;

        for (var i = startLine; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var trimmed = line.TrimStart();
            var currentIndent = line.Length - trimmed.Length;

            // Check for new declarations at the same or lower indentation level
            if (currentIndent <= baseIndent && i > startLine)
            {
                if (trimmed.StartsWith("let ") ||
                    trimmed.StartsWith("type ") ||
                    trimmed.StartsWith("module ") ||
                    trimmed.StartsWith("member ") ||
                    trimmed.StartsWith("override ") ||
                    trimmed.StartsWith("abstract ") ||
                    trimmed.StartsWith("static ") ||
                    trimmed.StartsWith("and ") ||
                    trimmed.StartsWith("namespace ") ||
                    trimmed.StartsWith("open "))
                {
                    return i; // Previous line was the end
                }
            }
        }

        return lines.Length;
    }

    private static string GetBlockText(string content, int startLine, int endLine)
    {
        var lines = content.Split('\n');
        var blockLines = lines.Skip(startLine - 1).Take(endLine - startLine + 1);
        return string.Join('\n', blockLines);
    }

    // Namespace declaration
    [GeneratedRegex(
        @"^\s*namespace\s+(?<name>[\w.]+)",
        RegexOptions.Multiline)]
    private static partial Regex NamespaceRegex();

    // Module declaration (top-level or nested)
    [GeneratedRegex(
        @"^\s*(?<private>private\s+)?(?<internal>internal\s+)?module\s+(?<name>[\w.]+)(?<nested>\s*=)?",
        RegexOptions.Multiline)]
    private static partial Regex ModuleRegex();

    // Type definition
    [GeneratedRegex(
        @"^\s*(?<private>private\s+)?(?<internal>internal\s+)?type\s+(?<name>\w+)(?<typeparams><[^>]+>)?",
        RegexOptions.Multiline)]
    private static partial Regex TypeRegex();

    // Let binding (function or value)
    [GeneratedRegex(
        @"(?<signature>^\s*(?<private>private\s+)?(?<internal>internal\s+)?let\s+(?<rec>rec\s+)?(?<inline>inline\s+)?(?<name>\w+)(?<params>\s+[\w\(\)]+)*(?:\s*:\s*(?<returntype>[^=]+))?\s*=)",
        RegexOptions.Multiline)]
    private static partial Regex LetBindingRegex();

    // Member declaration
    [GeneratedRegex(
        @"(?<signature>^\s*(?<private>private\s+)?(?<internal>internal\s+)?(?<static>static\s+)?(?<override>override\s+)?(?<abstract>abstract\s+)?(?<kind>member|val)\s+(?:this\.|self\.|x\.)?(?<name>\w+))",
        RegexOptions.Multiline)]
    private static partial Regex MemberRegex();
}
