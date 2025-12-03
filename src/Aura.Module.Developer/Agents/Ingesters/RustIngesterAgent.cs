// <copyright file="RustIngesterAgent.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Agents.Ingesters;

using System.Text.RegularExpressions;
using Aura.Foundation.Agents;
using Aura.Foundation.Rag;
using Microsoft.Extensions.Logging;

/// <summary>
/// Rust ingester agent using regex-based parsing.
/// Extracts structs, enums, traits, impls, functions, and modules.
/// </summary>
public sealed partial class RustIngesterAgent : RegexIngesterBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RustIngesterAgent"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public RustIngesterAgent(ILogger<RustIngesterAgent> logger)
        : base(logger)
    {
    }

    /// <inheritdoc/>
    public override string AgentId => "rust-ingester";

    /// <inheritdoc/>
    public override AgentMetadata Metadata { get; } = new(
        Name: "Rust Ingester",
        Description: "Parses Rust files using regex patterns. Extracts structs, enums, traits, impls, functions, and modules.",
        Capabilities: ["ingest:rs"],
        Priority: 10,
        Languages: ["rust"],
        Provider: "native",
        Model: "regex",
        Temperature: 0,
        Tools: [],
        Tags: ["ingester", "rust", "native", "regex"]);

    /// <inheritdoc/>
    protected override string Language => "rust";

    /// <inheritdoc/>
    protected override IEnumerable<DeclarationPattern> GetPatterns()
    {
        // Not used - we override ParseContent
        yield break;
    }

    /// <inheritdoc/>
    protected override List<SemanticChunk> ParseContent(string content, string filePath)
    {
        var chunks = new List<SemanticChunk>();

        // Extract modules
        ExtractModules(content, filePath, chunks);

        // Extract structs
        ExtractStructs(content, filePath, chunks);

        // Extract enums
        ExtractEnums(content, filePath, chunks);

        // Extract traits
        ExtractTraits(content, filePath, chunks);

        // Extract impl blocks
        ExtractImpls(content, filePath, chunks);

        // Extract functions
        ExtractFunctions(content, filePath, chunks);

        // Extract type aliases
        ExtractTypeAliases(content, filePath, chunks);

        // Extract constants
        ExtractConstants(content, filePath, chunks);

        // Extract statics
        ExtractStatics(content, filePath, chunks);

        // Sort by start line
        chunks.Sort((a, b) => a.StartLine.CompareTo(b.StartLine));

        return chunks;
    }

    private void ExtractModules(string content, string filePath, List<SemanticChunk> chunks)
    {
        var matches = ModuleRegex().Matches(content);
        foreach (Match match in matches)
        {
            var name = match.Groups["name"].Value;
            var startLine = GetLineNumber(content, match.Index);

            var isPublic = match.Groups["pub"].Success;
            var hasBody = match.Value.TrimEnd().EndsWith('{');

            int endLine;
            string text;

            if (hasBody)
            {
                endLine = FindBraceBlockEnd(content, match.Index + match.Length);
                text = GetBlockText(content, startLine, endLine);
            }
            else
            {
                endLine = startLine;
                text = match.Value.Trim();
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
                Signature = match.Groups["signature"].Value.Trim(),
                Metadata = new Dictionary<string, string>
                {
                    ["isPublic"] = isPublic.ToString().ToLowerInvariant(),
                    ["hasBody"] = hasBody.ToString().ToLowerInvariant(),
                },
            });
        }
    }

    private void ExtractStructs(string content, string filePath, List<SemanticChunk> chunks)
    {
        var matches = StructRegex().Matches(content);
        foreach (Match match in matches)
        {
            var name = match.Groups["name"].Value;
            var startLine = GetLineNumber(content, match.Index);

            var isPublic = match.Groups["pub"].Success;
            var hasBody = match.Value.Contains('{');

            int endLine;
            if (hasBody)
            {
                endLine = FindBraceBlockEnd(content, match.Index + match.Length);
            }
            else
            {
                // Tuple struct or unit struct
                var semiIndex = content.IndexOf(';', match.Index);
                endLine = semiIndex >= 0 ? GetLineNumber(content, semiIndex) : startLine;
            }

            var text = GetBlockText(content, startLine, endLine);

            var metadata = new Dictionary<string, string>
            {
                ["isPublic"] = isPublic.ToString().ToLowerInvariant(),
            };

            if (match.Groups["derive"].Success)
            {
                metadata["derive"] = match.Groups["derive"].Value;
            }

            chunks.Add(new SemanticChunk
            {
                Text = text,
                FilePath = filePath,
                ChunkType = ChunkTypes.Struct,
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

    private void ExtractEnums(string content, string filePath, List<SemanticChunk> chunks)
    {
        var matches = EnumRegex().Matches(content);
        foreach (Match match in matches)
        {
            var name = match.Groups["name"].Value;
            var startLine = GetLineNumber(content, match.Index);
            var endLine = FindBraceBlockEnd(content, match.Index + match.Length);

            var text = GetBlockText(content, startLine, endLine);
            var isPublic = match.Groups["pub"].Success;

            var metadata = new Dictionary<string, string>
            {
                ["isPublic"] = isPublic.ToString().ToLowerInvariant(),
            };

            if (match.Groups["derive"].Success)
            {
                metadata["derive"] = match.Groups["derive"].Value;
            }

            chunks.Add(new SemanticChunk
            {
                Text = text,
                FilePath = filePath,
                ChunkType = ChunkTypes.Enum,
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

    private void ExtractTraits(string content, string filePath, List<SemanticChunk> chunks)
    {
        var matches = TraitRegex().Matches(content);
        foreach (Match match in matches)
        {
            var name = match.Groups["name"].Value;
            var startLine = GetLineNumber(content, match.Index);
            var endLine = FindBraceBlockEnd(content, match.Index + match.Length);

            var text = GetBlockText(content, startLine, endLine);
            var isPublic = match.Groups["pub"].Success;
            var isUnsafe = match.Groups["unsafe"].Success;

            chunks.Add(new SemanticChunk
            {
                Text = text,
                FilePath = filePath,
                ChunkType = ChunkTypes.Interface,
                SymbolName = name,
                FullyQualifiedName = name,
                StartLine = startLine,
                EndLine = endLine,
                Language = Language,
                Signature = match.Groups["signature"].Value.Trim(),
                Metadata = new Dictionary<string, string>
                {
                    ["isPublic"] = isPublic.ToString().ToLowerInvariant(),
                    ["isUnsafe"] = isUnsafe.ToString().ToLowerInvariant(),
                },
            });
        }
    }

    private void ExtractImpls(string content, string filePath, List<SemanticChunk> chunks)
    {
        var matches = ImplRegex().Matches(content);
        foreach (Match match in matches)
        {
            var typeName = match.Groups["type"].Value;
            var traitName = match.Groups["trait"].Success ? match.Groups["trait"].Value : null;

            var startLine = GetLineNumber(content, match.Index);
            var endLine = FindBraceBlockEnd(content, match.Index + match.Length);

            var text = GetBlockText(content, startLine, endLine);
            var isUnsafe = match.Groups["unsafe"].Success;

            var symbolName = traitName is not null ? $"{traitName} for {typeName}" : typeName;

            chunks.Add(new SemanticChunk
            {
                Text = text,
                FilePath = filePath,
                ChunkType = ChunkTypes.Class, // impl block is similar to a class implementation
                SymbolName = symbolName,
                FullyQualifiedName = symbolName,
                StartLine = startLine,
                EndLine = endLine,
                Language = Language,
                Signature = match.Groups["signature"].Value.Trim(),
                Metadata = new Dictionary<string, string>
                {
                    ["isUnsafe"] = isUnsafe.ToString().ToLowerInvariant(),
                    ["implementsType"] = typeName,
                    ["implementsTrait"] = traitName ?? string.Empty,
                },
            });

            // Extract methods from impl block
            ExtractImplMethods(content, filePath, chunks, symbolName, match.Index + match.Length, endLine);
        }
    }

    private void ExtractImplMethods(string content, string filePath, List<SemanticChunk> chunks, string implName, int startIndex, int implEndLine)
    {
        var implEndIndex = GetCharIndex(content, implEndLine);
        var implBody = content[startIndex..Math.Min(implEndIndex, content.Length)];

        var methodMatches = FunctionRegex().Matches(implBody);
        foreach (Match match in methodMatches)
        {
            var methodName = match.Groups["name"].Value;
            var methodStartLine = GetLineNumber(content, startIndex) + GetLineNumber(implBody, match.Index) - 1;
            var methodEndLine = FindBraceBlockEnd(implBody, match.Index + match.Length) + GetLineNumber(content, startIndex) - 1;

            var methodText = GetBlockText(content, methodStartLine, Math.Min(methodEndLine, implEndLine));

            var isPublic = match.Groups["pub"].Success;
            var isAsync = match.Groups["async"].Success;
            var isUnsafe = match.Groups["unsafe"].Success;
            var isConst = match.Groups["const"].Success;

            var metadata = new Dictionary<string, string>
            {
                ["isPublic"] = isPublic.ToString().ToLowerInvariant(),
                ["isAsync"] = isAsync.ToString().ToLowerInvariant(),
                ["isUnsafe"] = isUnsafe.ToString().ToLowerInvariant(),
                ["isConst"] = isConst.ToString().ToLowerInvariant(),
            };

            if (match.Groups["returntype"].Success)
            {
                metadata["returnType"] = match.Groups["returntype"].Value.Trim();
            }

            chunks.Add(new SemanticChunk
            {
                Text = methodText,
                FilePath = filePath,
                ChunkType = ChunkTypes.Method,
                SymbolName = methodName,
                ParentSymbol = implName,
                FullyQualifiedName = $"{implName}::{methodName}",
                StartLine = methodStartLine,
                EndLine = Math.Min(methodEndLine, implEndLine),
                Language = Language,
                Signature = match.Groups["signature"].Value.Trim(),
                Metadata = metadata,
            });
        }
    }

    private void ExtractFunctions(string content, string filePath, List<SemanticChunk> chunks)
    {
        var matches = TopLevelFunctionRegex().Matches(content);
        foreach (Match match in matches)
        {
            // Skip if inside an impl block
            if (IsInsideBlock(content, match.Index, ImplRegex()))
            {
                continue;
            }

            var name = match.Groups["name"].Value;
            var startLine = GetLineNumber(content, match.Index);
            var endLine = FindBraceBlockEnd(content, match.Index + match.Length);

            var text = GetBlockText(content, startLine, endLine);

            var isPublic = match.Groups["pub"].Success;
            var isAsync = match.Groups["async"].Success;
            var isUnsafe = match.Groups["unsafe"].Success;
            var isConst = match.Groups["const"].Success;
            var isExtern = match.Groups["extern"].Success;

            var metadata = new Dictionary<string, string>
            {
                ["isPublic"] = isPublic.ToString().ToLowerInvariant(),
                ["isAsync"] = isAsync.ToString().ToLowerInvariant(),
                ["isUnsafe"] = isUnsafe.ToString().ToLowerInvariant(),
                ["isConst"] = isConst.ToString().ToLowerInvariant(),
                ["isExtern"] = isExtern.ToString().ToLowerInvariant(),
            };

            if (match.Groups["returntype"].Success)
            {
                metadata["returnType"] = match.Groups["returntype"].Value.Trim();
            }

            chunks.Add(new SemanticChunk
            {
                Text = text,
                FilePath = filePath,
                ChunkType = ChunkTypes.Function,
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

    private void ExtractTypeAliases(string content, string filePath, List<SemanticChunk> chunks)
    {
        var matches = TypeAliasRegex().Matches(content);
        foreach (Match match in matches)
        {
            var name = match.Groups["name"].Value;
            var startLine = GetLineNumber(content, match.Index);
            var isPublic = match.Groups["pub"].Success;

            chunks.Add(new SemanticChunk
            {
                Text = match.Value.Trim(),
                FilePath = filePath,
                ChunkType = ChunkTypes.TypeAlias,
                SymbolName = name,
                FullyQualifiedName = name,
                StartLine = startLine,
                EndLine = startLine,
                Language = Language,
                Signature = match.Value.Trim(),
                Metadata = new Dictionary<string, string>
                {
                    ["isPublic"] = isPublic.ToString().ToLowerInvariant(),
                },
            });
        }
    }

    private void ExtractConstants(string content, string filePath, List<SemanticChunk> chunks)
    {
        var matches = ConstRegex().Matches(content);
        foreach (Match match in matches)
        {
            var name = match.Groups["name"].Value;
            var startLine = GetLineNumber(content, match.Index);

            // Find the end of the const (could span multiple lines)
            var semiIndex = content.IndexOf(';', match.Index);
            var endLine = semiIndex >= 0 ? GetLineNumber(content, semiIndex) : startLine;
            var text = GetBlockText(content, startLine, endLine);

            var isPublic = match.Groups["pub"].Success;

            chunks.Add(new SemanticChunk
            {
                Text = text,
                FilePath = filePath,
                ChunkType = ChunkTypes.Field,
                SymbolName = name,
                FullyQualifiedName = name,
                StartLine = startLine,
                EndLine = endLine,
                Language = Language,
                Signature = match.Groups["signature"].Value.Trim(),
                Metadata = new Dictionary<string, string>
                {
                    ["isPublic"] = isPublic.ToString().ToLowerInvariant(),
                    ["isConstant"] = "true",
                },
            });
        }
    }

    private void ExtractStatics(string content, string filePath, List<SemanticChunk> chunks)
    {
        var matches = StaticRegex().Matches(content);
        foreach (Match match in matches)
        {
            var name = match.Groups["name"].Value;
            var startLine = GetLineNumber(content, match.Index);

            var semiIndex = content.IndexOf(';', match.Index);
            var endLine = semiIndex >= 0 ? GetLineNumber(content, semiIndex) : startLine;
            var text = GetBlockText(content, startLine, endLine);

            var isPublic = match.Groups["pub"].Success;
            var isMut = match.Groups["mut"].Success;

            chunks.Add(new SemanticChunk
            {
                Text = text,
                FilePath = filePath,
                ChunkType = ChunkTypes.Field,
                SymbolName = name,
                FullyQualifiedName = name,
                StartLine = startLine,
                EndLine = endLine,
                Language = Language,
                Signature = match.Groups["signature"].Value.Trim(),
                Metadata = new Dictionary<string, string>
                {
                    ["isPublic"] = isPublic.ToString().ToLowerInvariant(),
                    ["isStatic"] = "true",
                    ["isMutable"] = isMut.ToString().ToLowerInvariant(),
                },
            });
        }
    }

    private static bool IsInsideBlock(string content, int index, Regex blockRegex)
    {
        var matches = blockRegex.Matches(content);
        foreach (Match match in matches)
        {
            if (index <= match.Index)
            {
                continue;
            }

            // Check if we're inside this block
            var blockEnd = FindBraceBlockEnd(content, match.Index + match.Length);
            var blockEndIndex = GetCharIndex(content, blockEnd);

            if (index < blockEndIndex)
            {
                return true;
            }
        }

        return false;
    }

    private static int FindBraceBlockEnd(string content, int startIndex)
    {
        var braceDepth = 0;
        var inString = false;
        var inRawString = false;
        var inChar = false;
        var startLine = GetLineNumber(content, startIndex);

        for (var i = startIndex; i < content.Length; i++)
        {
            var c = content[i];
            var prev = i > 0 ? content[i - 1] : '\0';

            // Handle raw strings r#"..."#
            if (c == 'r' && i + 1 < content.Length && content[i + 1] == '#')
            {
                inRawString = true;
                continue;
            }

            if (inRawString && c == '"' && prev == '#')
            {
                inRawString = false;
                continue;
            }

            if (inRawString)
            {
                continue;
            }

            // Handle char literals
            if (c == '\'' && !inString && prev != '\\')
            {
                inChar = !inChar;
                continue;
            }

            if (inChar)
            {
                continue;
            }

            // Handle strings
            if (c == '"' && prev != '\\')
            {
                inString = !inString;
                continue;
            }

            if (inString)
            {
                continue;
            }

            switch (c)
            {
                case '{':
                    braceDepth++;
                    break;
                case '}':
                    braceDepth--;
                    if (braceDepth == 0)
                    {
                        return GetLineNumber(content, i);
                    }

                    break;
            }
        }

        return startLine;
    }

    private static string GetBlockText(string content, int startLine, int endLine)
    {
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

    // Module declaration
    [GeneratedRegex(
        @"(?<signature>(?<pub>pub(?:\s*\([^)]+\))?\s+)?mod\s+(?<name>\w+)\s*[{;])",
        RegexOptions.Multiline)]
    private static partial Regex ModuleRegex();

    // Struct definition
    [GeneratedRegex(
        @"(?<derive>#\[derive\([^\]]+\)\]\s*)?(?<signature>(?<pub>pub(?:\s*\([^)]+\))?\s+)?struct\s+(?<name>\w+)(?:<[^>]+>)?)",
        RegexOptions.Multiline)]
    private static partial Regex StructRegex();

    // Enum definition
    [GeneratedRegex(
        @"(?<derive>#\[derive\([^\]]+\)\]\s*)?(?<signature>(?<pub>pub(?:\s*\([^)]+\))?\s+)?enum\s+(?<name>\w+)(?:<[^>]+>)?\s*\{)",
        RegexOptions.Multiline)]
    private static partial Regex EnumRegex();

    // Trait definition
    [GeneratedRegex(
        @"(?<signature>(?<pub>pub(?:\s*\([^)]+\))?\s+)?(?<unsafe>unsafe\s+)?trait\s+(?<name>\w+)(?:<[^>]+>)?(?:\s*:\s*[^{]+)?\s*\{)",
        RegexOptions.Multiline)]
    private static partial Regex TraitRegex();

    // Impl block
    [GeneratedRegex(
        @"(?<signature>(?<unsafe>unsafe\s+)?impl(?:<[^>]+>)?\s+(?:(?<trait>[\w:]+(?:<[^>]+>)?)\s+for\s+)?(?<type>[\w:]+(?:<[^>]+>)?)\s*\{)",
        RegexOptions.Multiline)]
    private static partial Regex ImplRegex();

    // Function (any)
    [GeneratedRegex(
        @"(?<signature>(?<pub>pub(?:\s*\([^)]+\))?\s+)?(?<const>const\s+)?(?<async>async\s+)?(?<unsafe>unsafe\s+)?(?<extern>extern\s*""[^""]*""\s+)?fn\s+(?<name>\w+)(?:<[^>]+>)?\s*\([^)]*\)(?:\s*->\s*(?<returntype>[^{;]+))?\s*(?:where[^{]+)?\s*\{)",
        RegexOptions.Multiline)]
    private static partial Regex FunctionRegex();

    // Top-level function
    [GeneratedRegex(
        @"^(?<signature>(?<pub>pub(?:\s*\([^)]+\))?\s+)?(?<const>const\s+)?(?<async>async\s+)?(?<unsafe>unsafe\s+)?(?<extern>extern\s*""[^""]*""\s+)?fn\s+(?<name>\w+)(?:<[^>]+>)?\s*\([^)]*\)(?:\s*->\s*(?<returntype>[^{;]+))?\s*(?:where[^{]+)?\s*\{)",
        RegexOptions.Multiline)]
    private static partial Regex TopLevelFunctionRegex();

    // Type alias
    [GeneratedRegex(
        @"^(?<pub>pub(?:\s*\([^)]+\))?\s+)?type\s+(?<name>\w+)(?:<[^>]+>)?\s*=\s*[^;]+;",
        RegexOptions.Multiline)]
    private static partial Regex TypeAliasRegex();

    // Const
    [GeneratedRegex(
        @"(?<signature>(?<pub>pub(?:\s*\([^)]+\))?\s+)?const\s+(?<name>\w+)\s*:\s*[^=]+\s*=)",
        RegexOptions.Multiline)]
    private static partial Regex ConstRegex();

    // Static
    [GeneratedRegex(
        @"(?<signature>(?<pub>pub(?:\s*\([^)]+\))?\s+)?static\s+(?<mut>mut\s+)?(?<name>\w+)\s*:\s*[^=]+\s*=)",
        RegexOptions.Multiline)]
    private static partial Regex StaticRegex();
}
