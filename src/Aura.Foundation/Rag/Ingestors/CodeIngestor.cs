// <copyright file="CodeIngestor.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Rag.Ingestors;

using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Ingestor for source code files. Splits content by logical units (classes, functions).
/// Uses regex-based detection (no Roslyn dependency for cross-language support).
/// </summary>
public sealed partial class CodeIngestor : IContentIngestor
{
    private static readonly Dictionary<string, string> ExtensionToLanguage = new(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"] = "csharp",
        [".ts"] = "typescript",
        [".tsx"] = "typescript",
        [".js"] = "javascript",
        [".jsx"] = "javascript",
        [".py"] = "python",
        [".rs"] = "rust",
        [".go"] = "go",
        [".java"] = "java",
        [".cpp"] = "cpp",
        [".c"] = "c",
        [".h"] = "c",
        [".hpp"] = "cpp",
        [".fs"] = "fsharp",
        [".fsx"] = "fsharp",
    };

    /// <inheritdoc/>
    public string IngestorId => "code";

    /// <inheritdoc/>
    public IReadOnlyList<string> SupportedExtensions { get; } = ExtensionToLanguage.Keys.ToList();

    /// <inheritdoc/>
    public RagContentType ContentType => RagContentType.Code;

    /// <inheritdoc/>
    public bool CanIngest(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ExtensionToLanguage.ContainsKey(ext);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<IngestedChunk>> IngestAsync(
        string filePath,
        string content,
        CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(filePath);
        var language = ExtensionToLanguage.GetValueOrDefault(ext, "text");

        var chunks = language switch
        {
            "csharp" => ChunkCSharp(content, filePath),
            "typescript" or "javascript" => ChunkTypeScript(content, filePath),
            "python" => ChunkPython(content, filePath),
            _ => ChunkGeneric(content, filePath, language),
        };

        return Task.FromResult(chunks);
    }

    private static IReadOnlyList<IngestedChunk> ChunkCSharp(string content, string filePath)
    {
        var chunks = new List<IngestedChunk>();
        var lines = content.Split('\n');

        // First, add the file header (usings, namespace declaration)
        var headerBuilder = new StringBuilder();
        int headerEndLine = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            var trimmed = line.TrimStart();

            if (trimmed.StartsWith("using ") || trimmed.StartsWith("namespace ") ||
                trimmed.StartsWith("//") || trimmed.StartsWith("/*") ||
                trimmed.StartsWith("* ") || trimmed.StartsWith("*/") ||
                string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
            {
                headerBuilder.AppendLine(line);
                headerEndLine = i + 1;
            }
            else if (trimmed.StartsWith("[")) // Attributes
            {
                headerBuilder.AppendLine(line);
                headerEndLine = i + 1;
            }
            else
            {
                break;
            }
        }

        var header = headerBuilder.ToString().Trim();
        if (header.Length > 0)
        {
            chunks.Add(new IngestedChunk(header, "header")
            {
                Title = "File Header",
                Language = "csharp",
                StartLine = 1,
                EndLine = headerEndLine,
            });
        }

        // Find type declarations (class, interface, record, struct, enum)
        var typePattern = TypeDeclarationRegex();
        var methodPattern = MethodDeclarationRegex();

        var typeMatches = typePattern.Matches(content);

        foreach (Match match in typeMatches)
        {
            var typeKind = match.Groups[1].Value;
            var typeName = match.Groups[2].Value;
            var startIndex = match.Index;
            var startLine = content[..startIndex].Count(c => c == '\n') + 1;

            // Find the end of this type (matching braces)
            var typeBody = ExtractBracedBlock(content, startIndex + match.Length);
            if (typeBody is not null)
            {
                var endLine = startLine + typeBody.Count(c => c == '\n');
                var fullType = match.Value + typeBody;

                // If the type is small enough, chunk it whole
                if (fullType.Length < 2000)
                {
                    chunks.Add(new IngestedChunk(fullType, "type")
                    {
                        Title = $"{typeKind} {typeName}",
                        Language = "csharp",
                        StartLine = startLine,
                        EndLine = endLine,
                    });
                }
                else
                {
                    // Chunk individual methods within the type
                    var methodMatches = methodPattern.Matches(fullType);
                    foreach (Match methodMatch in methodMatches)
                    {
                        var methodName = methodMatch.Groups[1].Value;
                        var methodStart = startLine + fullType[..methodMatch.Index].Count(c => c == '\n');
                        var methodBody = ExtractBracedBlock(fullType, methodMatch.Index + methodMatch.Length);

                        if (methodBody is not null)
                        {
                            var fullMethod = methodMatch.Value + methodBody;
                            var methodEnd = methodStart + fullMethod.Count(c => c == '\n');

                            chunks.Add(new IngestedChunk(fullMethod, "method")
                            {
                                Title = $"{typeName}.{methodName}",
                                Language = "csharp",
                                StartLine = methodStart,
                                EndLine = methodEnd,
                            });
                        }
                    }
                }
            }
        }

        // If no chunks found, fall back to generic chunking
        if (chunks.Count <= 1)
        {
            return ChunkGeneric(content, filePath, "csharp");
        }

        return chunks;
    }

    private static IReadOnlyList<IngestedChunk> ChunkTypeScript(string content, string filePath)
    {
        var chunks = new List<IngestedChunk>();
        var lines = content.Split('\n');

        // Find imports section
        var importBuilder = new StringBuilder();
        int importEndLine = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            var trimmed = line.TrimStart();

            if (trimmed.StartsWith("import ") || trimmed.StartsWith("export ") && trimmed.Contains(" from ") ||
                trimmed.StartsWith("//") || trimmed.StartsWith("/*") ||
                trimmed.StartsWith("* ") || trimmed.StartsWith("*/") ||
                string.IsNullOrWhiteSpace(trimmed))
            {
                importBuilder.AppendLine(line);
                importEndLine = i + 1;
            }
            else if (!trimmed.StartsWith("export "))
            {
                break;
            }
        }

        var imports = importBuilder.ToString().Trim();
        if (imports.Length > 0)
        {
            chunks.Add(new IngestedChunk(imports, "imports")
            {
                Title = "Imports",
                Language = "typescript",
                StartLine = 1,
                EndLine = importEndLine,
            });
        }

        // Find functions and classes
        var functionPattern = TsFunctionRegex();
        var classPattern = TsClassRegex();

        var functionMatches = functionPattern.Matches(content);
        foreach (Match match in functionMatches)
        {
            var funcName = match.Groups[1].Value;
            var startIndex = match.Index;
            var startLine = content[..startIndex].Count(c => c == '\n') + 1;

            var body = ExtractBracedBlock(content, startIndex + match.Length);
            if (body is not null)
            {
                var fullFunc = match.Value + body;
                var endLine = startLine + fullFunc.Count(c => c == '\n');

                chunks.Add(new IngestedChunk(fullFunc, "function")
                {
                    Title = funcName,
                    Language = "typescript",
                    StartLine = startLine,
                    EndLine = endLine,
                });
            }
        }

        var classMatches = classPattern.Matches(content);
        foreach (Match match in classMatches)
        {
            var className = match.Groups[1].Value;
            var startIndex = match.Index;
            var startLine = content[..startIndex].Count(c => c == '\n') + 1;

            var body = ExtractBracedBlock(content, startIndex + match.Length);
            if (body is not null)
            {
                var fullClass = match.Value + body;
                var endLine = startLine + fullClass.Count(c => c == '\n');

                chunks.Add(new IngestedChunk(fullClass, "class")
                {
                    Title = className,
                    Language = "typescript",
                    StartLine = startLine,
                    EndLine = endLine,
                });
            }
        }

        if (chunks.Count <= 1)
        {
            return ChunkGeneric(content, filePath, "typescript");
        }

        return chunks;
    }

    private static IReadOnlyList<IngestedChunk> ChunkPython(string content, string filePath)
    {
        var chunks = new List<IngestedChunk>();
        var lines = content.Split('\n');

        // Find imports
        var importBuilder = new StringBuilder();
        int importEndLine = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            var trimmed = line.TrimStart();

            if (trimmed.StartsWith("import ") || trimmed.StartsWith("from ") ||
                trimmed.StartsWith("#") || string.IsNullOrWhiteSpace(trimmed) ||
                trimmed.StartsWith("\"\"\"") || trimmed.StartsWith("'''"))
            {
                importBuilder.AppendLine(line);
                importEndLine = i + 1;
            }
            else
            {
                break;
            }
        }

        var imports = importBuilder.ToString().Trim();
        if (imports.Length > 0)
        {
            chunks.Add(new IngestedChunk(imports, "imports")
            {
                Title = "Imports",
                Language = "python",
                StartLine = 1,
                EndLine = importEndLine,
            });
        }

        // Find class and function definitions using indentation
        var defPattern = PyDefRegex();
        var classPattern = PyClassRegex();

        // Simple approach: find def/class and capture until next def/class at same indent level
        var matches = new List<(int Index, string Type, string Name)>();

        foreach (Match m in defPattern.Matches(content))
        {
            matches.Add((m.Index, "function", m.Groups[1].Value));
        }

        foreach (Match m in classPattern.Matches(content))
        {
            matches.Add((m.Index, "class", m.Groups[1].Value));
        }

        matches = matches.OrderBy(m => m.Index).ToList();

        for (int i = 0; i < matches.Count; i++)
        {
            var (startIndex, type, name) = matches[i];
            var endIndex = i + 1 < matches.Count ? matches[i + 1].Index : content.Length;

            var block = content[startIndex..endIndex].TrimEnd();
            var startLine = content[..startIndex].Count(c => c == '\n') + 1;
            var endLine = startLine + block.Count(c => c == '\n');

            chunks.Add(new IngestedChunk(block, type)
            {
                Title = name,
                Language = "python",
                StartLine = startLine,
                EndLine = endLine,
            });
        }

        if (chunks.Count <= 1)
        {
            return ChunkGeneric(content, filePath, "python");
        }

        return chunks;
    }

    private static IReadOnlyList<IngestedChunk> ChunkGeneric(string content, string filePath, string language)
    {
        // Fall back to line-based chunking
        const int targetChunkSize = 1500;
        const int overlapLines = 5;

        var chunks = new List<IngestedChunk>();
        var lines = content.Split('\n');

        var currentChunk = new StringBuilder();
        int chunkStartLine = 1;
        int currentLine = 0;

        foreach (var rawLine in lines)
        {
            currentLine++;
            var line = rawLine.TrimEnd('\r');
            currentChunk.AppendLine(line);

            if (currentChunk.Length >= targetChunkSize)
            {
                chunks.Add(new IngestedChunk(currentChunk.ToString().Trim(), "block")
                {
                    Title = Path.GetFileName(filePath),
                    Language = language,
                    StartLine = chunkStartLine,
                    EndLine = currentLine,
                });

                // Start new chunk with overlap
                currentChunk.Clear();
                var overlapStart = Math.Max(0, currentLine - overlapLines);
                for (int i = overlapStart; i < currentLine && i < lines.Length; i++)
                {
                    currentChunk.AppendLine(lines[i].TrimEnd('\r'));
                }

                chunkStartLine = overlapStart + 1;
            }
        }

        // Add remaining content
        var remaining = currentChunk.ToString().Trim();
        if (remaining.Length > 0)
        {
            chunks.Add(new IngestedChunk(remaining, "block")
            {
                Title = Path.GetFileName(filePath),
                Language = language,
                StartLine = chunkStartLine,
                EndLine = currentLine,
            });
        }

        return chunks;
    }

    private static string? ExtractBracedBlock(string content, int startIndex)
    {
        // Find the opening brace
        var braceIndex = content.IndexOf('{', startIndex);
        if (braceIndex == -1)
        {
            return null;
        }

        // Count braces to find matching close
        var depth = 1;
        var sb = new StringBuilder();
        sb.Append(content[startIndex..braceIndex]);
        sb.Append('{');

        for (int i = braceIndex + 1; i < content.Length && depth > 0; i++)
        {
            var c = content[i];
            sb.Append(c);

            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth--;
            }
        }

        return depth == 0 ? sb.ToString() : null;
    }

    [GeneratedRegex(@"(?:public|private|protected|internal)?\s*(?:static|sealed|abstract|partial)?\s*(?:class|interface|record|struct|enum)\s+(\w+)", RegexOptions.Compiled)]
    private static partial Regex TypeDeclarationRegex();

    [GeneratedRegex(@"(?:public|private|protected|internal)?\s*(?:static|async|virtual|override|sealed)?\s*(?:\w+(?:<[^>]+>)?)\s+(\w+)\s*\([^)]*\)\s*(?:where\s+\w+\s*:\s*[^{]+)?", RegexOptions.Compiled)]
    private static partial Regex MethodDeclarationRegex();

    [GeneratedRegex(@"(?:export\s+)?(?:async\s+)?function\s+(\w+)\s*\([^)]*\)\s*(?::\s*[^{]+)?", RegexOptions.Compiled)]
    private static partial Regex TsFunctionRegex();

    [GeneratedRegex(@"(?:export\s+)?class\s+(\w+)(?:\s+extends\s+\w+)?(?:\s+implements\s+[^{]+)?", RegexOptions.Compiled)]
    private static partial Regex TsClassRegex();

    [GeneratedRegex(@"^def\s+(\w+)\s*\([^)]*\)\s*(?:->\s*[^:]+)?:", RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex PyDefRegex();

    [GeneratedRegex(@"^class\s+(\w+)(?:\([^)]*\))?:", RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex PyClassRegex();
}
