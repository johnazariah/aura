// <copyright file="TypeScriptIngesterAgent.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Agents.Ingesters;

using System.Text.RegularExpressions;
using Aura.Foundation.Agents;
using Aura.Foundation.Rag;
using Microsoft.Extensions.Logging;

/// <summary>
/// TypeScript/JavaScript ingester agent using regex-based parsing.
/// Extracts classes, interfaces, functions, types, and exports.
/// </summary>
public sealed partial class TypeScriptIngesterAgent : RegexIngesterBase
{
    private readonly bool _isTypeScript;

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeScriptIngesterAgent"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="isTypeScript">Whether this is for TypeScript (true) or JavaScript (false).</param>
    public TypeScriptIngesterAgent(ILogger<TypeScriptIngesterAgent> logger, bool isTypeScript = true)
        : base(logger)
    {
        _isTypeScript = isTypeScript;
    }

    /// <inheritdoc/>
    public override string AgentId => _isTypeScript ? "typescript-ingester" : "javascript-ingester";

    /// <inheritdoc/>
    public override AgentMetadata Metadata => _isTypeScript
        ? new(
            Name: "TypeScript Ingester",
            Description: "Parses TypeScript files using regex patterns. Extracts classes, interfaces, functions, types, and exports.",
            Capabilities: ["ingest:ts", "ingest:tsx", "ingest:mts", "ingest:cts"],
            Priority: 10,
            Languages: ["typescript"],
            Provider: "native",
            Model: "regex",
            Temperature: 0,
            Tools: [],
            Tags: ["ingester", "typescript", "native", "regex"])
        : new(
            Name: "JavaScript Ingester",
            Description: "Parses JavaScript files using regex patterns. Extracts classes, functions, and exports.",
            Capabilities: ["ingest:js", "ingest:jsx", "ingest:mjs", "ingest:cjs"],
            Priority: 10,
            Languages: ["javascript"],
            Provider: "native",
            Model: "regex",
            Temperature: 0,
            Tools: [],
            Tags: ["ingester", "javascript", "native", "regex"]);

    /// <inheritdoc/>
    protected override string Language => _isTypeScript ? "typescript" : "javascript";

    /// <inheritdoc/>
    protected override IEnumerable<DeclarationPattern> GetPatterns()
    {
        // These are not used directly - we override ParseContent for better control
        yield break;
    }

    /// <inheritdoc/>
    protected override List<SemanticChunk> ParseContent(string content, string filePath)
    {
        var chunks = new List<SemanticChunk>();

        // Extract interfaces (TypeScript only)
        if (_isTypeScript)
        {
            ExtractInterfaces(content, filePath, chunks);
            ExtractTypeAliases(content, filePath, chunks);
            ExtractEnums(content, filePath, chunks);
        }

        // Extract classes
        ExtractClasses(content, filePath, chunks);

        // Extract top-level functions
        ExtractFunctions(content, filePath, chunks);

        // Extract arrow function constants
        ExtractArrowFunctions(content, filePath, chunks);

        // Sort by start line
        chunks.Sort((a, b) => a.StartLine.CompareTo(b.StartLine));

        return chunks;
    }

    private void ExtractInterfaces(string content, string filePath, List<SemanticChunk> chunks)
    {
        var matches = InterfaceRegex().Matches(content);
        foreach (Match match in matches)
        {
            var name = match.Groups["name"].Value;
            var startLine = GetLineNumber(content, match.Index);
            var endLine = FindBraceBlockEnd(content, match.Index + match.Length);

            var text = GetBlockText(content, startLine, endLine);
            var isExported = match.Groups["export"].Success;

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
    }

    private void ExtractTypeAliases(string content, string filePath, List<SemanticChunk> chunks)
    {
        var matches = TypeAliasRegex().Matches(content);
        foreach (Match match in matches)
        {
            var name = match.Groups["name"].Value;
            var startLine = GetLineNumber(content, match.Index);
            var isExported = match.Groups["export"].Success;

            // Type aliases can span multiple lines if they use union/intersection types
            var endLine = startLine;
            var remaining = content[(match.Index + match.Length)..];
            var parenDepth = 0;
            var braceDepth = 0;
            var foundEnd = false;

            for (var i = 0; i < remaining.Length && !foundEnd; i++)
            {
                var c = remaining[i];
                switch (c)
                {
                    case '(':
                        parenDepth++;
                        break;
                    case ')':
                        parenDepth--;
                        break;
                    case '{':
                        braceDepth++;
                        break;
                    case '}':
                        braceDepth--;
                        break;
                    case ';':
                    case '\n' when parenDepth == 0 && braceDepth == 0:
                        foundEnd = true;
                        endLine = GetLineNumber(content, match.Index + match.Length + i);
                        break;
                }
            }

            var text = GetBlockText(content, startLine, endLine);

            chunks.Add(new SemanticChunk
            {
                Text = text,
                FilePath = filePath,
                ChunkType = ChunkTypes.TypeAlias,
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
            var isExported = match.Groups["export"].Success;
            var isConst = match.Groups["const"].Success;

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
                Metadata = new Dictionary<string, string>
                {
                    ["isExported"] = isExported.ToString().ToLowerInvariant(),
                    ["isConst"] = isConst.ToString().ToLowerInvariant(),
                },
            });
        }
    }

    private void ExtractClasses(string content, string filePath, List<SemanticChunk> chunks)
    {
        var matches = ClassRegex().Matches(content);
        foreach (Match match in matches)
        {
            var className = match.Groups["name"].Value;
            var startLine = GetLineNumber(content, match.Index);
            var endLine = FindBraceBlockEnd(content, match.Index + match.Length);

            var classText = GetBlockText(content, startLine, endLine);
            var isExported = match.Groups["export"].Success;
            var isAbstract = match.Groups["abstract"].Success;
            var isDefault = match.Groups["default"].Success;

            var metadata = new Dictionary<string, string>
            {
                ["isExported"] = isExported.ToString().ToLowerInvariant(),
                ["isAbstract"] = isAbstract.ToString().ToLowerInvariant(),
            };

            if (match.Groups["extends"].Success)
            {
                metadata["extends"] = match.Groups["extends"].Value.Trim();
            }

            if (match.Groups["implements"].Success)
            {
                metadata["implements"] = match.Groups["implements"].Value.Trim();
            }

            chunks.Add(new SemanticChunk
            {
                Text = classText,
                FilePath = filePath,
                ChunkType = ChunkTypes.Class,
                SymbolName = className,
                FullyQualifiedName = className,
                StartLine = startLine,
                EndLine = endLine,
                Language = Language,
                Signature = match.Groups["signature"].Value.Trim(),
                Metadata = metadata,
            });

            // Extract methods from class body
            ExtractClassMembers(content, filePath, chunks, className, match.Index + match.Length, endLine);
        }
    }

    private void ExtractClassMembers(string content, string filePath, List<SemanticChunk> chunks, string className, int startIndex, int classEndLine)
    {
        var classEndIndex = GetCharIndex(content, classEndLine);
        var classBody = content[startIndex..Math.Min(classEndIndex, content.Length)];

        // Extract methods
        var methodMatches = MethodRegex().Matches(classBody);
        foreach (Match match in methodMatches)
        {
            var methodName = match.Groups["name"].Value;
            var methodStartLine = GetLineNumber(content, startIndex) + GetLineNumber(classBody, match.Index) - 1;
            var methodEndLine = FindBraceBlockEnd(classBody, match.Index + match.Length) + GetLineNumber(content, startIndex) - 1;

            var methodText = GetBlockText(content, methodStartLine, Math.Min(methodEndLine, classEndLine));

            var isAsync = match.Groups["async"].Success;
            var isStatic = match.Groups["static"].Success;
            var isPrivate = match.Groups["private"].Success;
            var isProtected = match.Groups["protected"].Success;
            var isPublic = match.Groups["public"].Success;
            var isAbstract = match.Groups["abstract"].Success;

            var accessibility = isPrivate ? "private" : isProtected ? "protected" : "public";

            var metadata = new Dictionary<string, string>
            {
                ["isAsync"] = isAsync.ToString().ToLowerInvariant(),
                ["isStatic"] = isStatic.ToString().ToLowerInvariant(),
                ["accessibility"] = accessibility,
            };

            if (match.Groups["returntype"].Success)
            {
                metadata["returnType"] = match.Groups["returntype"].Value.Trim();
            }

            // Check for constructor
            var chunkType = methodName == "constructor" ? ChunkTypes.Constructor : ChunkTypes.Method;

            chunks.Add(new SemanticChunk
            {
                Text = methodText,
                FilePath = filePath,
                ChunkType = chunkType,
                SymbolName = methodName,
                ParentSymbol = className,
                FullyQualifiedName = $"{className}.{methodName}",
                StartLine = methodStartLine,
                EndLine = Math.Min(methodEndLine, classEndLine),
                Language = Language,
                Signature = match.Groups["signature"].Value.Trim(),
                Metadata = metadata,
            });
        }

        // Extract properties
        var propertyMatches = PropertyRegex().Matches(classBody);
        foreach (Match match in propertyMatches)
        {
            var propName = match.Groups["name"].Value;
            var propStartLine = GetLineNumber(content, startIndex) + GetLineNumber(classBody, match.Index) - 1;

            chunks.Add(new SemanticChunk
            {
                Text = match.Value.Trim(),
                FilePath = filePath,
                ChunkType = ChunkTypes.Property,
                SymbolName = propName,
                ParentSymbol = className,
                FullyQualifiedName = $"{className}.{propName}",
                StartLine = propStartLine,
                EndLine = propStartLine,
                Language = Language,
                Signature = match.Value.Trim(),
            });
        }
    }

    private void ExtractFunctions(string content, string filePath, List<SemanticChunk> chunks)
    {
        var matches = FunctionRegex().Matches(content);
        foreach (Match match in matches)
        {
            var funcName = match.Groups["name"].Value;
            var startLine = GetLineNumber(content, match.Index);
            var endLine = FindBraceBlockEnd(content, match.Index + match.Length);

            var funcText = GetBlockText(content, startLine, endLine);
            var isExported = match.Groups["export"].Success;
            var isAsync = match.Groups["async"].Success;
            var isDefault = match.Groups["default"].Success;

            var metadata = new Dictionary<string, string>
            {
                ["isExported"] = isExported.ToString().ToLowerInvariant(),
                ["isAsync"] = isAsync.ToString().ToLowerInvariant(),
            };

            if (match.Groups["returntype"].Success)
            {
                metadata["returnType"] = match.Groups["returntype"].Value.Trim();
            }

            chunks.Add(new SemanticChunk
            {
                Text = funcText,
                FilePath = filePath,
                ChunkType = ChunkTypes.Function,
                SymbolName = funcName,
                FullyQualifiedName = funcName,
                StartLine = startLine,
                EndLine = endLine,
                Language = Language,
                Signature = match.Groups["signature"].Value.Trim(),
                Metadata = metadata,
            });
        }
    }

    private void ExtractArrowFunctions(string content, string filePath, List<SemanticChunk> chunks)
    {
        var matches = ArrowFunctionRegex().Matches(content);
        foreach (Match match in matches)
        {
            var funcName = match.Groups["name"].Value;
            var startLine = GetLineNumber(content, match.Index);

            // Arrow functions can be single expression or block
            int endLine;
            var afterArrow = content[(match.Index + match.Length)..];
            if (afterArrow.TrimStart().StartsWith('{'))
            {
                endLine = FindBraceBlockEnd(content, match.Index + match.Length);
            }
            else
            {
                // Single expression - find the semicolon or newline
                var semiIndex = afterArrow.IndexOf(';');
                var newlineIndex = afterArrow.IndexOf('\n');
                var endIndex = semiIndex >= 0 && (newlineIndex < 0 || semiIndex < newlineIndex) ? semiIndex : newlineIndex;
                endLine = endIndex >= 0 ? GetLineNumber(content, match.Index + match.Length + endIndex) : startLine;
            }

            var funcText = GetBlockText(content, startLine, endLine);
            var isExported = match.Groups["export"].Success;

            chunks.Add(new SemanticChunk
            {
                Text = funcText,
                FilePath = filePath,
                ChunkType = ChunkTypes.Function,
                SymbolName = funcName,
                FullyQualifiedName = funcName,
                StartLine = startLine,
                EndLine = endLine,
                Language = Language,
                Signature = match.Groups["signature"].Value.Trim(),
                Metadata = new Dictionary<string, string>
                {
                    ["isExported"] = isExported.ToString().ToLowerInvariant(),
                    ["isArrowFunction"] = "true",
                },
            });
        }
    }

    private static int FindBraceBlockEnd(string content, int startIndex)
    {
        var braceDepth = 0;
        var inString = false;
        var stringChar = '\0';
        var startLine = GetLineNumber(content, startIndex);

        for (var i = startIndex; i < content.Length; i++)
        {
            var c = content[i];
            var prev = i > 0 ? content[i - 1] : '\0';

            // Handle string literals
            if ((c == '"' || c == '\'' || c == '`') && prev != '\\')
            {
                if (!inString)
                {
                    inString = true;
                    stringChar = c;
                }
                else if (c == stringChar)
                {
                    inString = false;
                }

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

    // Interface definitions
    [GeneratedRegex(
        @"(?<export>export\s+)?(?<signature>interface\s+(?<name>\w+)(?:<[^>]+>)?(?:\s+extends\s+[^{]+)?\s*\{)",
        RegexOptions.Multiline)]
    private static partial Regex InterfaceRegex();

    // Type alias definitions
    [GeneratedRegex(
        @"(?<export>export\s+)?(?<signature>type\s+(?<name>\w+)(?:<[^>]+>)?\s*=)",
        RegexOptions.Multiline)]
    private static partial Regex TypeAliasRegex();

    // Enum definitions
    [GeneratedRegex(
        @"(?<export>export\s+)?(?<const>const\s+)?(?<signature>enum\s+(?<name>\w+)\s*\{)",
        RegexOptions.Multiline)]
    private static partial Regex EnumRegex();

    // Class definitions
    [GeneratedRegex(
        @"(?<export>export\s+)?(?<default>default\s+)?(?<abstract>abstract\s+)?(?<signature>class\s+(?<name>\w+)(?:<[^>]+>)?(?:\s+extends\s+(?<extends>[\w.<>,\s]+))?(?:\s+implements\s+(?<implements>[\w.<>,\s]+))?\s*\{)",
        RegexOptions.Multiline)]
    private static partial Regex ClassRegex();

    // Function definitions
    [GeneratedRegex(
        @"(?<export>export\s+)?(?<default>default\s+)?(?<async>async\s+)?(?<signature>function\s+(?<name>\w+)(?:<[^>]+>)?\s*\([^)]*\)(?:\s*:\s*(?<returntype>[^{]+))?\s*\{)",
        RegexOptions.Multiline)]
    private static partial Regex FunctionRegex();

    // Arrow function constants
    [GeneratedRegex(
        @"(?<export>export\s+)?(?<signature>(?:const|let|var)\s+(?<name>\w+)(?:\s*:\s*[^=]+)?\s*=\s*(?:async\s+)?(?:\([^)]*\)|[a-zA-Z_]\w*)\s*(?::\s*[^=]+)?\s*=>)",
        RegexOptions.Multiline)]
    private static partial Regex ArrowFunctionRegex();

    // Method definitions
    [GeneratedRegex(
        @"(?<private>private\s+)?(?<protected>protected\s+)?(?<public>public\s+)?(?<static>static\s+)?(?<abstract>abstract\s+)?(?<async>async\s+)?(?<signature>(?<name>\w+)(?:<[^>]+>)?\s*\([^)]*\)(?:\s*:\s*(?<returntype>[^{;]+))?\s*\{)",
        RegexOptions.Multiline)]
    private static partial Regex MethodRegex();

    // Property definitions
    [GeneratedRegex(
        @"^\s*(?:private\s+|protected\s+|public\s+)?(?:static\s+)?(?:readonly\s+)?(?<name>\w+)(?:\?)?(?:\s*:\s*[^;=]+)?(?:\s*=\s*[^;]+)?;",
        RegexOptions.Multiline)]
    private static partial Regex PropertyRegex();
}
