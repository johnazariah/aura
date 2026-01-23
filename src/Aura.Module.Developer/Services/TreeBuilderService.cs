// <copyright file="TreeBuilderService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services;

using Aura.Foundation.Rag;
using Microsoft.Extensions.Logging;

/// <summary>
/// Builds hierarchical tree views from RAG chunks.
/// </summary>
public sealed class TreeBuilderService : ITreeBuilderService
{
    private readonly ILogger<TreeBuilderService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TreeBuilderService"/> class.
    /// </summary>
    public TreeBuilderService(ILogger<TreeBuilderService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public TreeResult BuildTree(
        IReadOnlyList<TreeChunk> chunks,
        string? pattern = null,
        int maxDepth = 2,
        TreeDetail detail = TreeDetail.Min)
    {
        _logger.LogDebug("Building tree from {Count} chunks, maxDepth={MaxDepth}, detail={Detail}", chunks.Count, maxDepth, detail);

        // Group chunks by file path
        var chunksByFile = chunks
            .GroupBy(c => c.SourcePath)
            .ToDictionary(g => g.Key, g => g.ToList());

        var rootNodes = new List<TreeNode>();
        var totalNodes = 0;

        // Build folder structure first, then add files with their symbols
        var folderStructure = BuildFolderStructure(chunksByFile.Keys.ToList());

        foreach (var (folderPath, files) in folderStructure.OrderBy(kv => kv.Key))
        {
            // At depth 0, we show top-level folders
            // At depth 1, we show files in those folders
            // At depth 2, we show types/functions in those files
            // At depth 3+, we show members in those types

            foreach (var filePath in files.OrderBy(f => f))
            {
                var fileChunks = chunksByFile.GetValueOrDefault(filePath) ?? [];

                var fileNode = BuildFileNode(filePath, fileChunks, maxDepth, detail, currentDepth: 1);
                if (fileNode is not null)
                {
                    rootNodes.Add(fileNode);
                    totalNodes += CountNodes(fileNode);
                }
            }
        }

        _logger.LogDebug("Built tree with {TotalNodes} nodes", totalNodes);

        return new TreeResult
        {
            RootPath = ".",
            Nodes = rootNodes,
            TotalNodes = totalNodes,
            Truncated = false,
        };
    }

    /// <inheritdoc/>
    public TreeNodeContent? GetNode(IReadOnlyList<TreeChunk> chunks, string nodeId)
    {
        // Parse node ID: "{type}:{path}:{symbol}"
        var parts = nodeId.Split(':', 3);
        if (parts.Length < 2)
        {
            _logger.LogWarning("Invalid node ID format: {NodeId}", nodeId);
            return null;
        }

        var nodeType = parts[0];
        var path = parts[1];
        var symbol = parts.Length > 2 && !string.IsNullOrEmpty(parts[2]) ? parts[2] : null;

        // Find matching chunk
        TreeChunk? matchingChunk = null;

        if (nodeType == "file")
        {
            // For file nodes, find any chunk from that file and return combined content
            var fileChunks = chunks.Where(c => c.SourcePath == path).ToList();
            if (fileChunks.Count == 0)
            {
                return null;
            }

            // Return the first chunk's content (or could combine all)
            matchingChunk = fileChunks.FirstOrDefault(c => c.ChunkType == "header") ?? fileChunks[0];
        }
        else if (!string.IsNullOrEmpty(symbol))
        {
            // Find by type and symbol name
            matchingChunk = chunks.FirstOrDefault(c =>
                c.SourcePath == path &&
                (c.SymbolName == symbol || c.Title == symbol || c.Title?.EndsWith("." + symbol) == true));
        }

        if (matchingChunk is null)
        {
            _logger.LogWarning("Node not found: {NodeId}", nodeId);
            return null;
        }

        return new TreeNodeContent
        {
            NodeId = nodeId,
            Name = symbol ?? Path.GetFileName(path),
            Type = nodeType,
            Path = path,
            LineStart = matchingChunk.StartLine,
            LineEnd = matchingChunk.EndLine,
            Content = matchingChunk.Content,
            Metadata = new TreeNodeMetadata
            {
                Signature = matchingChunk.Signature,
                Language = matchingChunk.Language,
            },
        };
    }

    private static Dictionary<string, List<string>> BuildFolderStructure(IReadOnlyList<string> filePaths)
    {
        var structure = new Dictionary<string, List<string>>();

        foreach (var path in filePaths)
        {
            var folder = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? ".";
            if (!structure.TryGetValue(folder, out var files))
            {
                files = [];
                structure[folder] = files;
            }

            files.Add(path);
        }

        return structure;
    }

    private static TreeNode? BuildFileNode(
        string filePath,
        IReadOnlyList<TreeChunk> chunks,
        int maxDepth,
        TreeDetail detail,
        int currentDepth)
    {
        var fileName = Path.GetFileName(filePath);

        // Separate chunks by type
        var typeChunks = chunks.Where(c => c.ChunkType is "type" or "class" or "interface" or "struct" or "enum").ToList();
        var methodChunks = chunks.Where(c => c.ChunkType is "method" or "function").ToList();
        var otherChunks = chunks.Where(c => c.ChunkType is not ("type" or "class" or "interface" or "struct" or "enum" or "method" or "function" or "header")).ToList();

        List<TreeNode>? children = null;

        if (currentDepth < maxDepth)
        {
            children = [];

            // Add type nodes with their methods as children
            foreach (var typeChunk in typeChunks)
            {
                var typeName = typeChunk.SymbolName ?? typeChunk.Title ?? "Unknown";
                var typeNode = new TreeNode
                {
                    NodeId = $"{typeChunk.ChunkType}:{filePath}:{typeName}",
                    Name = typeName,
                    Type = typeChunk.ChunkType,
                    Path = filePath,
                    Signature = detail == TreeDetail.Max ? typeChunk.Signature : null,
                    LineStart = typeChunk.StartLine,
                    LineEnd = typeChunk.EndLine,
                    Children = currentDepth + 1 < maxDepth ? GetMethodChildren(filePath, typeName, methodChunks, detail) : null,
                };

                children.Add(typeNode);
            }

            // Add standalone functions (not part of a type)
            foreach (var funcChunk in methodChunks.Where(m => string.IsNullOrEmpty(m.ParentSymbol)))
            {
                var funcName = funcChunk.SymbolName ?? funcChunk.Title ?? "Unknown";
                children.Add(new TreeNode
                {
                    NodeId = $"function:{filePath}:{funcName}",
                    Name = funcName,
                    Type = "function",
                    Path = filePath,
                    Signature = detail == TreeDetail.Max ? funcChunk.Signature : null,
                    LineStart = funcChunk.StartLine,
                    LineEnd = funcChunk.EndLine,
                });
            }
        }

        return new TreeNode
        {
            NodeId = $"file:{filePath}:",
            Name = fileName,
            Type = "file",
            Path = filePath,
            Children = children?.Count > 0 ? children : null,
        };
    }

    private static List<TreeNode>? GetMethodChildren(
        string filePath,
        string parentTypeName,
        IReadOnlyList<TreeChunk> methodChunks,
        TreeDetail detail)
    {
        // Find methods that belong to this type
        var children = new List<TreeNode>();

        foreach (var method in methodChunks)
        {
            // Check if this method belongs to the parent type
            var belongsToType =
                method.ParentSymbol == parentTypeName ||
                method.Title?.StartsWith(parentTypeName + ".") == true;

            if (!belongsToType)
            {
                continue;
            }

            var methodName = method.SymbolName ?? method.Title?.Split('.').LastOrDefault() ?? "Unknown";

            children.Add(new TreeNode
            {
                NodeId = $"method:{filePath}:{methodName}",
                Name = methodName,
                Type = "method",
                Path = filePath,
                Signature = detail == TreeDetail.Max ? method.Signature : null,
                LineStart = method.StartLine,
                LineEnd = method.EndLine,
            });
        }

        return children.Count > 0 ? children : null;
    }

    private static int CountNodes(TreeNode node)
    {
        var count = 1;
        if (node.Children is not null)
        {
            foreach (var child in node.Children)
            {
                count += CountNodes(child);
            }
        }

        return count;
    }
}
