// <copyright file="GoIngesterAgent.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Agents.Ingesters;

using System.Text.RegularExpressions;
using Aura.Foundation.Agents;
using Aura.Foundation.Rag;
using Microsoft.Extensions.Logging;

/// <summary>
/// Go ingester agent using regex-based parsing.
/// Extracts structs, interfaces, functions, methods, and type definitions.
/// </summary>
public sealed partial class GoIngesterAgent : RegexIngesterBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GoIngesterAgent"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public GoIngesterAgent(ILogger<GoIngesterAgent> logger)
        : base(logger)
    {
    }

    /// <inheritdoc/>
    public override string AgentId => "go-ingester";

    /// <inheritdoc/>
    public override AgentMetadata Metadata { get; } = new(
        Name: "Go Ingester",
        Description: "Parses Go files using regex patterns. Extracts structs, interfaces, functions, methods, and type definitions.",
        Capabilities: ["ingest:go"],
        Priority: 10,
        Languages: ["go"],
        Provider: "native",
        Model: "regex",
        Temperature: 0,
        Tools: [],
        Tags: ["ingester", "go", "golang", "native", "regex"]);

    /// <inheritdoc/>
    protected override string Language => "go";

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

        // Extract package declaration
        ExtractPackage(content, filePath, chunks);

        // Extract type definitions (structs and interfaces)
        ExtractTypes(content, filePath, chunks);

        // Extract top-level functions
        ExtractFunctions(content, filePath, chunks);

        // Extract methods (functions with receivers)
        ExtractMethods(content, filePath, chunks);

        // Extract constants
        ExtractConstants(content, filePath, chunks);

        // Extract variables
        ExtractVariables(content, filePath, chunks);

        // Sort by start line
        chunks.Sort((a, b) => a.StartLine.CompareTo(b.StartLine));

        return chunks;
    }

    private void ExtractPackage(string content, string filePath, List<SemanticChunk> chunks)
    {
        var match = PackageRegex().Match(content);
        if (match.Success)
        {
            var startLine = GetLineNumber(content, match.Index);
            chunks.Add(new SemanticChunk
            {
                Text = match.Value.Trim(),
                FilePath = filePath,
                ChunkType = ChunkTypes.Namespace,
                SymbolName = match.Groups["name"].Value,
                StartLine = startLine,
                EndLine = startLine,
                Language = Language,
                Signature = match.Value.Trim(),
            });
        }
    }

    private void ExtractTypes(string content, string filePath, List<SemanticChunk> chunks)
    {
        // Extract struct types
        var structMatches = StructRegex().Matches(content);
        foreach (Match match in structMatches)
        {
            var name = match.Groups["name"].Value;
            var startLine = GetLineNumber(content, match.Index);
            var endLine = FindBraceBlockEnd(content, match.Index + match.Length);

            var text = GetBlockText(content, startLine, endLine);
            var isExported = char.IsUpper(name[0]);

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
                Metadata = new Dictionary<string, string>
                {
                    ["isExported"] = isExported.ToString().ToLowerInvariant(),
                },
            });
        }

        // Extract interface types
        var interfaceMatches = InterfaceRegex().Matches(content);
        foreach (Match match in interfaceMatches)
        {
            var name = match.Groups["name"].Value;
            var startLine = GetLineNumber(content, match.Index);
            var endLine = FindBraceBlockEnd(content, match.Index + match.Length);

            var text = GetBlockText(content, startLine, endLine);
            var isExported = char.IsUpper(name[0]);

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
                    ["isExported"] = isExported.ToString().ToLowerInvariant(),
                },
            });
        }

        // Extract type aliases
        var aliasMatches = TypeAliasRegex().Matches(content);
        foreach (Match match in aliasMatches)
        {
            var name = match.Groups["name"].Value;
            var startLine = GetLineNumber(content, match.Index);
            var isExported = char.IsUpper(name[0]);

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
                    ["isExported"] = isExported.ToString().ToLowerInvariant(),
                },
            });
        }
    }

    private void ExtractFunctions(string content, string filePath, List<SemanticChunk> chunks)
    {
        var matches = FunctionRegex().Matches(content);
        foreach (Match match in matches)
        {
            // Skip methods (they have receivers)
            if (match.Groups["receiver"].Success)
            {
                continue;
            }

            var name = match.Groups["name"].Value;
            var startLine = GetLineNumber(content, match.Index);
            var endLine = FindBraceBlockEnd(content, match.Index + match.Length);

            var text = GetBlockText(content, startLine, endLine);
            var isExported = char.IsUpper(name[0]);

            var metadata = new Dictionary<string, string>
            {
                ["isExported"] = isExported.ToString().ToLowerInvariant(),
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

    private void ExtractMethods(string content, string filePath, List<SemanticChunk> chunks)
    {
        var matches = MethodRegex().Matches(content);
        foreach (Match match in matches)
        {
            var name = match.Groups["name"].Value;
            var receiver = match.Groups["receiver"].Value;
            var receiverType = match.Groups["receivertype"].Value;

            var startLine = GetLineNumber(content, match.Index);
            var endLine = FindBraceBlockEnd(content, match.Index + match.Length);

            var text = GetBlockText(content, startLine, endLine);
            var isExported = char.IsUpper(name[0]);
            var isPointerReceiver = receiver.Contains('*');

            var metadata = new Dictionary<string, string>
            {
                ["isExported"] = isExported.ToString().ToLowerInvariant(),
                ["receiver"] = receiverType,
                ["isPointerReceiver"] = isPointerReceiver.ToString().ToLowerInvariant(),
            };

            if (match.Groups["returntype"].Success)
            {
                metadata["returnType"] = match.Groups["returntype"].Value.Trim();
            }

            chunks.Add(new SemanticChunk
            {
                Text = text,
                FilePath = filePath,
                ChunkType = ChunkTypes.Method,
                SymbolName = name,
                ParentSymbol = receiverType,
                FullyQualifiedName = $"{receiverType}.{name}",
                StartLine = startLine,
                EndLine = endLine,
                Language = Language,
                Signature = match.Groups["signature"].Value.Trim(),
                Metadata = metadata,
            });
        }
    }

    private void ExtractConstants(string content, string filePath, List<SemanticChunk> chunks)
    {
        // Single constant
        var singleMatches = SingleConstRegex().Matches(content);
        foreach (Match match in singleMatches)
        {
            var name = match.Groups["name"].Value;
            var startLine = GetLineNumber(content, match.Index);
            var isExported = char.IsUpper(name[0]);

            chunks.Add(new SemanticChunk
            {
                Text = match.Value.Trim(),
                FilePath = filePath,
                ChunkType = ChunkTypes.Field,
                SymbolName = name,
                StartLine = startLine,
                EndLine = startLine,
                Language = Language,
                Signature = match.Value.Trim(),
                Metadata = new Dictionary<string, string>
                {
                    ["isExported"] = isExported.ToString().ToLowerInvariant(),
                    ["isConstant"] = "true",
                },
            });
        }

        // Const block
        var blockMatches = ConstBlockRegex().Matches(content);
        foreach (Match match in blockMatches)
        {
            var startLine = GetLineNumber(content, match.Index);
            var endLine = FindParenBlockEnd(content, match.Index + match.Length);
            var text = GetBlockText(content, startLine, endLine);

            chunks.Add(new SemanticChunk
            {
                Text = text,
                FilePath = filePath,
                ChunkType = ChunkTypes.Field,
                SymbolName = "const",
                StartLine = startLine,
                EndLine = endLine,
                Language = Language,
                Signature = "const (...)",
                Metadata = new Dictionary<string, string>
                {
                    ["isConstant"] = "true",
                    ["isBlock"] = "true",
                },
            });
        }
    }

    private void ExtractVariables(string content, string filePath, List<SemanticChunk> chunks)
    {
        // Single var
        var singleMatches = SingleVarRegex().Matches(content);
        foreach (Match match in singleMatches)
        {
            var name = match.Groups["name"].Value;
            var startLine = GetLineNumber(content, match.Index);
            var isExported = char.IsUpper(name[0]);

            chunks.Add(new SemanticChunk
            {
                Text = match.Value.Trim(),
                FilePath = filePath,
                ChunkType = ChunkTypes.Field,
                SymbolName = name,
                StartLine = startLine,
                EndLine = startLine,
                Language = Language,
                Signature = match.Value.Trim(),
                Metadata = new Dictionary<string, string>
                {
                    ["isExported"] = isExported.ToString().ToLowerInvariant(),
                },
            });
        }

        // Var block
        var blockMatches = VarBlockRegex().Matches(content);
        foreach (Match match in blockMatches)
        {
            var startLine = GetLineNumber(content, match.Index);
            var endLine = FindParenBlockEnd(content, match.Index + match.Length);
            var text = GetBlockText(content, startLine, endLine);

            chunks.Add(new SemanticChunk
            {
                Text = text,
                FilePath = filePath,
                ChunkType = ChunkTypes.Field,
                SymbolName = "var",
                StartLine = startLine,
                EndLine = endLine,
                Language = Language,
                Signature = "var (...)",
                Metadata = new Dictionary<string, string>
                {
                    ["isBlock"] = "true",
                },
            });
        }
    }

    private static int FindBraceBlockEnd(string content, int startIndex)
    {
        var braceDepth = 0;
        var inString = false;
        var inRawString = false;
        var startLine = GetLineNumber(content, startIndex);

        for (var i = startIndex; i < content.Length; i++)
        {
            var c = content[i];
            var prev = i > 0 ? content[i - 1] : '\0';

            // Handle raw strings
            if (c == '`')
            {
                inRawString = !inRawString;
                continue;
            }

            if (inRawString)
            {
                continue;
            }

            // Handle regular strings
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

    private static int FindParenBlockEnd(string content, int startIndex)
    {
        var parenDepth = 0;
        var startLine = GetLineNumber(content, startIndex);

        for (var i = startIndex; i < content.Length; i++)
        {
            var c = content[i];

            switch (c)
            {
                case '(':
                    parenDepth++;
                    break;
                case ')':
                    parenDepth--;
                    if (parenDepth == 0)
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

    // Package declaration
    [GeneratedRegex(@"^package\s+(?<name>\w+)", RegexOptions.Multiline)]
    private static partial Regex PackageRegex();

    // Struct type
    [GeneratedRegex(
        @"(?<signature>type\s+(?<name>\w+)\s+struct\s*\{)",
        RegexOptions.Multiline)]
    private static partial Regex StructRegex();

    // Interface type
    [GeneratedRegex(
        @"(?<signature>type\s+(?<name>\w+)\s+interface\s*\{)",
        RegexOptions.Multiline)]
    private static partial Regex InterfaceRegex();

    // Type alias
    [GeneratedRegex(
        @"^type\s+(?<name>\w+)\s+(?!struct|interface)[^\n{]+$",
        RegexOptions.Multiline)]
    private static partial Regex TypeAliasRegex();

    // Function (without receiver)
    [GeneratedRegex(
        @"(?<signature>func\s+(?<name>\w+)\s*(?<generics>\[[^\]]+\])?\s*\([^)]*\)\s*(?<returntype>[^{]+)?\s*\{)",
        RegexOptions.Multiline)]
    private static partial Regex FunctionRegex();

    // Method (with receiver)
    [GeneratedRegex(
        @"(?<signature>func\s*\((?<receiver>\w+\s+(?<receivertype>\*?\w+))\)\s+(?<name>\w+)\s*(?<generics>\[[^\]]+\])?\s*\([^)]*\)\s*(?<returntype>[^{]+)?\s*\{)",
        RegexOptions.Multiline)]
    private static partial Regex MethodRegex();

    // Single const
    [GeneratedRegex(
        @"^const\s+(?<name>\w+)\s*(?:\w+)?\s*=",
        RegexOptions.Multiline)]
    private static partial Regex SingleConstRegex();

    // Const block
    [GeneratedRegex(@"^const\s*\(", RegexOptions.Multiline)]
    private static partial Regex ConstBlockRegex();

    // Single var
    [GeneratedRegex(
        @"^var\s+(?<name>\w+)\s+",
        RegexOptions.Multiline)]
    private static partial Regex SingleVarRegex();

    // Var block
    [GeneratedRegex(@"^var\s*\(", RegexOptions.Multiline)]
    private static partial Regex VarBlockRegex();
}
