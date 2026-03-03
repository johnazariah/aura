// <copyright file="TreeSitterCodeIngestor.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Ingestors;

using Aura.Foundation.Data.Entities;
using Aura.Foundation.Rag;
using Aura.Foundation.Rag.Ingestors;
using Microsoft.Extensions.Logging;
using TreeSitter;

/// <summary>
/// TreeSitter-based code ingestor for non-C# languages.
/// Produces both RAG chunks and code graph nodes in a single parse pass.
/// </summary>
public sealed class TreeSitterCodeIngestor(ILogger<TreeSitterCodeIngestor> logger) : ICodeIngestor
{
    private static readonly Dictionary<string, string> ExtensionToLanguage = new(StringComparer.OrdinalIgnoreCase)
    {
        [".py"] = "Python",
        [".ts"] = "TypeScript",
        [".tsx"] = "Tsx",
        [".js"] = "JavaScript",
        [".jsx"] = "JavaScript",
        [".go"] = "Go",
        [".rs"] = "Rust",
        [".java"] = "Java",
        [".cpp"] = "Cpp",
        [".c"] = "C",
        [".h"] = "C",
        [".rb"] = "Ruby",
        [".swift"] = "Swift",
        [".kt"] = "Kotlin",
    };

    private static readonly Dictionary<string, string> LanguageToDisplayName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Python"] = "python",
        ["TypeScript"] = "typescript",
        ["Tsx"] = "typescript",
        ["JavaScript"] = "javascript",
        ["Go"] = "go",
        ["Rust"] = "rust",
        ["Java"] = "java",
        ["Cpp"] = "cpp",
        ["C"] = "c",
        ["Ruby"] = "ruby",
        ["Swift"] = "swift",
        ["Kotlin"] = "kotlin",
    };

    // Node types considered significant per language family
    private static readonly HashSet<string> PythonNodeTypes =
        ["function_definition", "class_definition", "decorated_definition"];

    private static readonly HashSet<string> JsTsNodeTypes =
        ["function_declaration", "class_declaration", "method_definition", "arrow_function",
         "interface_declaration", "type_alias_declaration", "export_statement"];

    private static readonly HashSet<string> GoNodeTypes =
        ["function_declaration", "method_declaration", "type_declaration"];

    private static readonly HashSet<string> RustNodeTypes =
        ["function_item", "struct_item", "enum_item", "impl_item", "trait_item"];

    private static readonly HashSet<string> JavaNodeTypes =
        ["class_declaration", "method_declaration", "interface_declaration"];

    private static readonly HashSet<string> RubyNodeTypes =
        ["method", "class", "module", "singleton_method"];

    private static readonly HashSet<string> SwiftNodeTypes =
        ["function_declaration", "class_declaration", "struct_declaration",
         "protocol_declaration", "enum_declaration"];

    private static readonly HashSet<string> KotlinNodeTypes =
        ["function_declaration", "class_declaration", "object_declaration",
         "interface_declaration"];

    private static readonly HashSet<string> CNodeTypes =
        ["function_definition", "struct_specifier", "enum_specifier"];

    /// <inheritdoc/>
    public string IngestorId => "treesitter-code";

    /// <inheritdoc/>
    public IReadOnlyList<string> SupportedExtensions { get; } =
        [".py", ".ts", ".tsx", ".js", ".jsx", ".go", ".rs", ".java", ".cpp", ".c", ".h", ".rb", ".swift", ".kt"];

    /// <inheritdoc/>
    public RagContentType ContentType => RagContentType.Code;

    /// <inheritdoc/>
    public bool CanIngest(string filePath)
    {
        var extension = Path.GetExtension(filePath);

        // Roslyn handles C# files
        if (extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".csx", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return ExtensionToLanguage.ContainsKey(extension);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IngestedChunk>> IngestAsync(
        string filePath,
        string content,
        CancellationToken cancellationToken = default)
    {
        var result = await IngestCodeAsync(filePath, content, string.Empty, cancellationToken);
        return result.Chunks;
    }

    /// <inheritdoc/>
    public Task<CodeIngestionResult> IngestCodeAsync(
        string filePath,
        string content,
        string workspacePath,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(filePath);
        if (!ExtensionToLanguage.TryGetValue(extension, out var languageId))
        {
            logger.LogWarning("Unsupported extension for TreeSitter: {FilePath}", filePath);
            return Task.FromResult(new CodeIngestionResult([], [], []));
        }

        logger.LogDebug("Parsing {Language} file with TreeSitter: {FilePath}", languageId, filePath);

        var chunks = new List<IngestedChunk>();
        var nodes = new List<CodeNode>();
        var edges = new List<CodeEdge>();

        try
        {
            using var language = new Language(languageId);
            using var parser = new Parser(language);
            using var tree = parser.Parse(content);

            if (tree?.RootNode.Type is null)
            {
                logger.LogWarning("TreeSitter returned null tree for: {FilePath}", filePath);
                return Task.FromResult(new CodeIngestionResult([], [], []));
            }

            var normalizedWorkspace = PathNormalizer.Normalize(workspacePath);
            var displayLang = LanguageToDisplayName.GetValueOrDefault(languageId, languageId.ToLowerInvariant());
            var significantTypes = GetSignificantNodeTypes(languageId);

            WalkTree(
                tree.RootNode,
                filePath,
                content,
                normalizedWorkspace,
                displayLang,
                significantTypes,
                parentNodeId: null,
                chunks,
                nodes,
                edges);

            // Fallback: if no significant declarations found, chunk the entire file as a module
            if (chunks.Count == 0)
            {
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                chunks.Add(new IngestedChunk(content, "module")
                {
                    SymbolName = fileName,
                    FullyQualifiedName = fileName,
                    StartLine = 1,
                    EndLine = content.Split('\n').Length,
                    Language = displayLang,
                });

                nodes.Add(new CodeNode
                {
                    Id = Guid.NewGuid(),
                    NodeType = CodeNodeType.File,
                    Name = fileName,
                    FullName = fileName,
                    FilePath = PathNormalizer.Normalize(filePath),
                    LineNumber = 1,
                    RepositoryPath = normalizedWorkspace,
                });
            }

            logger.LogDebug(
                "Extracted {ChunkCount} chunks and {NodeCount} nodes from {FilePath}",
                chunks.Count,
                nodes.Count,
                filePath);

            return Task.FromResult(new CodeIngestionResult(chunks, nodes, edges));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "TreeSitter failed to parse {FilePath}, returning empty result", filePath);
            return Task.FromResult(new CodeIngestionResult([], [], []));
        }
    }

    private static void WalkTree(
        Node node,
        string filePath,
        string content,
        string workspacePath,
        string displayLang,
        HashSet<string> significantTypes,
        Guid? parentNodeId,
        List<IngestedChunk> chunks,
        List<CodeNode> nodes,
        List<CodeEdge> edges)
    {
        if (significantTypes.Contains(node.Type))
        {
            var processedNodeId = ProcessSignificantNode(
                node, filePath, content, workspacePath, displayLang,
                parentNodeId, chunks, nodes, edges);

            // Recurse into children with this node as parent
            foreach (var child in node.Children)
            {
                WalkTree(child, filePath, content, workspacePath, displayLang,
                    significantTypes, processedNodeId, chunks, nodes, edges);
            }
        }
        else
        {
            // Not a significant node — recurse into children with same parent
            foreach (var child in node.Children)
            {
                WalkTree(child, filePath, content, workspacePath, displayLang,
                    significantTypes, parentNodeId, chunks, nodes, edges);
            }
        }
    }

    private static Guid ProcessSignificantNode(
        Node node,
        string filePath,
        string content,
        string workspacePath,
        string displayLang,
        Guid? parentNodeId,
        List<IngestedChunk> chunks,
        List<CodeNode> nodes,
        List<CodeEdge> edges)
    {
        var startLine = node.StartPosition.Row + 1;
        var endLine = node.EndPosition.Row + 1;
        var symbolName = ExtractSymbolName(node);
        var (chunkType, nodeType) = MapNodeType(node.Type);
        var nodeText = node.Text;

        // For export_statement, dig into the actual declaration
        if (node.Type == "export_statement")
        {
            var innerDecl = FindInnerDeclaration(node);
            if (innerDecl is not null)
            {
                symbolName = ExtractSymbolName(innerDecl);
                var (innerChunkType, innerNodeType) = MapNodeType(innerDecl.Type);
                chunkType = innerChunkType;
                nodeType = innerNodeType;
            }
        }

        // Use filename as fallback symbol name
        if (string.IsNullOrEmpty(symbolName))
        {
            symbolName = $"<anonymous:{node.Type}>";
        }

        chunks.Add(new IngestedChunk(nodeText, chunkType)
        {
            SymbolName = symbolName,
            FullyQualifiedName = symbolName,
            StartLine = startLine,
            EndLine = endLine,
            Language = displayLang,
            Metadata = new Dictionary<string, string>
            {
                ["astNodeType"] = node.Type,
            },
        });

        var codeNode = new CodeNode
        {
            Id = Guid.NewGuid(),
            NodeType = nodeType,
            Name = symbolName,
            FullName = symbolName,
            FilePath = PathNormalizer.Normalize(filePath),
            LineNumber = startLine,
            RepositoryPath = workspacePath,
        };
        nodes.Add(codeNode);

        // Create "Contains" edge from parent to this node
        if (parentNodeId.HasValue)
        {
            edges.Add(new CodeEdge
            {
                Id = Guid.NewGuid(),
                EdgeType = CodeEdgeType.Contains,
                SourceId = parentNodeId.Value,
                TargetId = codeNode.Id,
            });
        }

        return codeNode.Id;
    }

    private static string? ExtractSymbolName(Node node)
    {
        var nameNode = node.GetChildForField("name");
        if (nameNode?.Type is not null)
        {
            return nameNode.Text;
        }

        var declarator = node.GetChildForField("declarator");
        if (declarator?.Type is not null)
        {
            var innerName = declarator.GetChildForField("declarator");
            if (innerName?.Type is not null)
            {
                return innerName.Text;
            }

            return declarator.Text;
        }

        return null;
    }

    private static Node? FindInnerDeclaration(Node exportNode)
    {
        var declaration = exportNode.GetChildForField("declaration");
        if (declaration?.Type is not null)
        {
            return declaration;
        }

        // Try named children
        foreach (var child in exportNode.NamedChildren)
        {
            if (child.Type.EndsWith("_declaration", StringComparison.Ordinal)
                || child.Type == "class_declaration"
                || child.Type == "function_declaration"
                || child.Type == "type_alias_declaration"
                || child.Type == "interface_declaration")
            {
                return child;
            }
        }

        return null;
    }

    private static (string ChunkType, CodeNodeType NodeType) MapNodeType(string astNodeType) =>
        astNodeType switch
        {
            // Functions (Python function_definition, C/C++ function_definition, Go/TS/JS/Swift/Kotlin function_declaration, Rust function_item)
            "function_definition" or "function_declaration" or "function_item" => ("function", CodeNodeType.Method),

            // Classes (Python class_definition, TS/JS/Java/Swift/Kotlin class_declaration, Ruby class)
            "class_definition" or "class_declaration" or "class" => ("class", CodeNodeType.Class),

            // Methods (TS/JS method_definition, Go/Java method_declaration, Ruby method/singleton_method)
            "method_definition" or "method_declaration" or "method" or "singleton_method" => ("method", CodeNodeType.Method),

            // Interfaces (TS/Java interface_declaration, Rust trait_item, Swift protocol_declaration)
            "interface_declaration" or "trait_item" or "protocol_declaration" => ("interface", CodeNodeType.Interface),

            // Structs (Rust struct_item, C struct_specifier, Go type_declaration, Swift struct_declaration)
            "struct_item" or "struct_specifier" or "type_declaration" or "struct_declaration" => ("struct", CodeNodeType.Struct),

            // Enums (Rust enum_item, C enum_specifier, Swift enum_declaration)
            "enum_item" or "enum_specifier" or "enum_declaration" => ("enum", CodeNodeType.Enum),

            // Python decorators, TS/JS arrow functions, export wrappers
            "decorated_definition" or "arrow_function" or "export_statement" => ("function", CodeNodeType.Method),

            // Type aliases
            "type_alias_declaration" => ("type", CodeNodeType.Class),

            // Rust impl blocks
            "impl_item" => ("class", CodeNodeType.Class),

            // Kotlin objects
            "object_declaration" => ("class", CodeNodeType.Class),

            // Ruby modules
            "module" => ("module", CodeNodeType.Namespace),

            _ => ("code", CodeNodeType.Method),
        };

    private static HashSet<string> GetSignificantNodeTypes(string languageId) =>
        languageId switch
        {
            "Python" => PythonNodeTypes,
            "TypeScript" or "Tsx" => JsTsNodeTypes,
            "JavaScript" => JsTsNodeTypes,
            "Go" => GoNodeTypes,
            "Rust" => RustNodeTypes,
            "Java" => JavaNodeTypes,
            "Ruby" => RubyNodeTypes,
            "Swift" => SwiftNodeTypes,
            "Kotlin" => KotlinNodeTypes,
            "C" or "Cpp" => CNodeTypes,
            _ => [],
        };
}
