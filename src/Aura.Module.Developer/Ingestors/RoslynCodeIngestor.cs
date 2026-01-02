// <copyright file="RoslynCodeIngestor.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Ingestors;

using Aura.Foundation.Data.Entities;
using Aura.Foundation.Rag;
using Aura.Foundation.Rag.Ingestors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

/// <summary>
/// Roslyn-based code ingestor for C# files.
/// Produces both RAG chunks and code graph nodes in a single parse pass.
/// </summary>
public sealed class RoslynCodeIngestor : ICodeIngestor
{
    private readonly ILogger<RoslynCodeIngestor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RoslynCodeIngestor"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public RoslynCodeIngestor(ILogger<RoslynCodeIngestor> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public string IngestorId => "roslyn-code";

    /// <inheritdoc/>
    public IReadOnlyList<string> SupportedExtensions { get; } = [".cs", ".csx"];

    /// <inheritdoc/>
    public RagContentType ContentType => RagContentType.Code;

    /// <inheritdoc/>
    public bool CanIngest(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IngestedChunk>> IngestAsync(
        string filePath,
        string content,
        CancellationToken cancellationToken = default)
    {
        // Delegate to IngestCodeAsync but return only chunks
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
        _logger.LogDebug("Parsing C# file with Roslyn: {FilePath}", filePath);

        var chunks = new List<IngestedChunk>();
        var nodes = new List<CodeNode>();
        var edges = new List<CodeEdge>();

        try
        {
            var tree = CSharpSyntaxTree.ParseText(content, path: filePath);
            var root = tree.GetCompilationUnitRoot();
            var text = tree.GetText();

            // Normalize workspace path for consistent storage
            var normalizedWorkspace = PathNormalizer.Normalize(workspacePath);

            // Process all type declarations
            foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                ProcessTypeDeclaration(typeDecl, filePath, text, normalizedWorkspace, chunks, nodes, edges);
            }

            // Process enum declarations
            foreach (var enumDecl in root.DescendantNodes().OfType<EnumDeclarationSyntax>())
            {
                ProcessEnumDeclaration(enumDecl, filePath, text, normalizedWorkspace, chunks, nodes);
            }

            // Process top-level statements (C# 9+)
            var topLevelStatements = root.Members.OfType<GlobalStatementSyntax>().ToList();
            if (topLevelStatements.Count > 0)
            {
                ProcessTopLevelStatements(topLevelStatements, filePath, text, normalizedWorkspace, chunks, nodes);
            }

            _logger.LogDebug(
                "Extracted {ChunkCount} chunks and {NodeCount} nodes from {FilePath}",
                chunks.Count,
                nodes.Count,
                filePath);

            return Task.FromResult(new CodeIngestionResult(chunks, nodes, edges));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse C# file: {FilePath}", filePath);
            throw;
        }
    }

    private void ProcessTypeDeclaration(
        TypeDeclarationSyntax typeDecl,
        string filePath,
        Microsoft.CodeAnalysis.Text.SourceText text,
        string workspacePath,
        List<IngestedChunk> chunks,
        List<CodeNode> nodes,
        List<CodeEdge> edges)
    {
        var startLine = text.Lines.GetLinePosition(typeDecl.SpanStart).Line + 1;
        var endLine = text.Lines.GetLinePosition(typeDecl.Span.End).Line + 1;

        var chunkType = typeDecl switch
        {
            ClassDeclarationSyntax => "class",
            InterfaceDeclarationSyntax => "interface",
            StructDeclarationSyntax => "struct",
            RecordDeclarationSyntax => "record",
            _ => "class",
        };

        var nodeType = typeDecl switch
        {
            ClassDeclarationSyntax => CodeNodeType.Class,
            InterfaceDeclarationSyntax => CodeNodeType.Interface,
            StructDeclarationSyntax => CodeNodeType.Struct,
            RecordDeclarationSyntax => CodeNodeType.Record,
            _ => CodeNodeType.Class,
        };

        var namespaceName = GetNamespace(typeDecl);
        var fullName = string.IsNullOrEmpty(namespaceName)
            ? typeDecl.Identifier.Text
            : $"{namespaceName}.{typeDecl.Identifier.Text}";

        var modifiers = typeDecl.Modifiers.ToString();
        var signature = GetTypeSignature(typeDecl);

        // Add RAG chunk
        chunks.Add(new IngestedChunk(GetTypeSignatureWithDoc(typeDecl), chunkType)
        {
            SymbolName = typeDecl.Identifier.Text,
            FullyQualifiedName = fullName,
            StartLine = startLine,
            EndLine = endLine,
            Language = "csharp",
            Metadata = new Dictionary<string, string>
            {
                ["namespace"] = namespaceName,
                ["accessibility"] = GetAccessibility(typeDecl.Modifiers),
            },
        });

        // Add code graph node
        var typeNode = new CodeNode
        {
            Id = Guid.NewGuid(),
            NodeType = nodeType,
            Name = typeDecl.Identifier.Text,
            FullName = fullName,
            FilePath = PathNormalizer.Normalize(filePath),
            LineNumber = startLine,
            Signature = signature,
            Modifiers = modifiers,
            RepositoryPath = workspacePath,
        };
        nodes.Add(typeNode);

        // Process members
        foreach (var member in typeDecl.Members)
        {
            ProcessMember(member, typeDecl.Identifier.Text, fullName, typeNode.Id, filePath, text, workspacePath, chunks, nodes, edges);
        }
    }

    private void ProcessEnumDeclaration(
        EnumDeclarationSyntax enumDecl,
        string filePath,
        Microsoft.CodeAnalysis.Text.SourceText text,
        string workspacePath,
        List<IngestedChunk> chunks,
        List<CodeNode> nodes)
    {
        var startLine = text.Lines.GetLinePosition(enumDecl.SpanStart).Line + 1;
        var endLine = text.Lines.GetLinePosition(enumDecl.Span.End).Line + 1;

        var namespaceName = GetNamespace(enumDecl);
        var fullName = string.IsNullOrEmpty(namespaceName)
            ? enumDecl.Identifier.Text
            : $"{namespaceName}.{enumDecl.Identifier.Text}";

        // Add RAG chunk
        chunks.Add(new IngestedChunk(enumDecl.ToFullString(), "enum")
        {
            SymbolName = enumDecl.Identifier.Text,
            FullyQualifiedName = fullName,
            StartLine = startLine,
            EndLine = endLine,
            Language = "csharp",
            Metadata = new Dictionary<string, string>
            {
                ["namespace"] = namespaceName,
                ["accessibility"] = GetAccessibility(enumDecl.Modifiers),
                ["memberCount"] = enumDecl.Members.Count.ToString(),
            },
        });

        // Add code graph node
        nodes.Add(new CodeNode
        {
            Id = Guid.NewGuid(),
            NodeType = CodeNodeType.Enum,
            Name = enumDecl.Identifier.Text,
            FullName = fullName,
            FilePath = PathNormalizer.Normalize(filePath),
            LineNumber = startLine,
            Modifiers = enumDecl.Modifiers.ToString(),
            RepositoryPath = workspacePath,
        });
    }

    private void ProcessTopLevelStatements(
        List<GlobalStatementSyntax> statements,
        string filePath,
        Microsoft.CodeAnalysis.Text.SourceText text,
        string workspacePath,
        List<IngestedChunk> chunks,
        List<CodeNode> nodes)
    {
        var firstStatement = statements.First();
        var lastStatement = statements.Last();
        var startLine = text.Lines.GetLinePosition(firstStatement.SpanStart).Line + 1;
        var endLine = text.Lines.GetLinePosition(lastStatement.Span.End).Line + 1;

        var code = string.Join("\n", statements.Select(s => s.ToFullString()));

        // Add RAG chunk
        chunks.Add(new IngestedChunk(code, "function")
        {
            SymbolName = "<Program>$",
            FullyQualifiedName = "<Program>$",
            StartLine = startLine,
            EndLine = endLine,
            Language = "csharp",
        });

        // Add code graph node
        nodes.Add(new CodeNode
        {
            Id = Guid.NewGuid(),
            NodeType = CodeNodeType.Method,
            Name = "<Program>$",
            FullName = "<Program>$",
            FilePath = PathNormalizer.Normalize(filePath),
            LineNumber = startLine,
            Signature = "top-level",
            RepositoryPath = workspacePath,
        });
    }

    private void ProcessMember(
        Microsoft.CodeAnalysis.CSharp.Syntax.MemberDeclarationSyntax member,
        string parentName,
        string parentFullName,
        Guid parentNodeId,
        string filePath,
        Microsoft.CodeAnalysis.Text.SourceText text,
        string workspacePath,
        List<IngestedChunk> chunks,
        List<CodeNode> nodes,
        List<CodeEdge> edges)
    {
        var startLine = text.Lines.GetLinePosition(member.SpanStart).Line + 1;
        var endLine = text.Lines.GetLinePosition(member.Span.End).Line + 1;

        CodeNode? memberNode = null;

        switch (member)
        {
            case MethodDeclarationSyntax method:
                var methodFullName = $"{parentFullName}.{method.Identifier.Text}";
                var methodSignature = GetMethodSignature(method);

                chunks.Add(new IngestedChunk(method.ToFullString(), "method")
                {
                    SymbolName = method.Identifier.Text,
                    FullyQualifiedName = methodFullName,
                    StartLine = startLine,
                    EndLine = endLine,
                    Language = "csharp",
                    Metadata = new Dictionary<string, string>
                    {
                        ["parentType"] = parentName,
                        ["returnType"] = method.ReturnType.ToString(),
                        ["accessibility"] = GetAccessibility(method.Modifiers),
                    },
                });

                memberNode = new CodeNode
                {
                    Id = Guid.NewGuid(),
                    NodeType = CodeNodeType.Method,
                    Name = method.Identifier.Text,
                    FullName = methodFullName,
                    FilePath = PathNormalizer.Normalize(filePath),
                    LineNumber = startLine,
                    Signature = methodSignature,
                    Modifiers = method.Modifiers.ToString(),
                    RepositoryPath = workspacePath,
                };
                break;

            case PropertyDeclarationSyntax property:
                var propFullName = $"{parentFullName}.{property.Identifier.Text}";

                chunks.Add(new IngestedChunk(property.ToFullString(), "property")
                {
                    SymbolName = property.Identifier.Text,
                    FullyQualifiedName = propFullName,
                    StartLine = startLine,
                    EndLine = endLine,
                    Language = "csharp",
                    Metadata = new Dictionary<string, string>
                    {
                        ["parentType"] = parentName,
                        ["type"] = property.Type.ToString(),
                    },
                });

                memberNode = new CodeNode
                {
                    Id = Guid.NewGuid(),
                    NodeType = CodeNodeType.Property,
                    Name = property.Identifier.Text,
                    FullName = propFullName,
                    FilePath = PathNormalizer.Normalize(filePath),
                    LineNumber = startLine,
                    Signature = $"{property.Type} {property.Identifier.Text}",
                    Modifiers = property.Modifiers.ToString(),
                    RepositoryPath = workspacePath,
                };
                break;

            case ConstructorDeclarationSyntax ctor:
                var ctorFullName = $"{parentFullName}.{ctor.Identifier.Text}";
                var ctorSignature = GetConstructorSignature(ctor);

                chunks.Add(new IngestedChunk(ctor.ToFullString(), "constructor")
                {
                    SymbolName = ctor.Identifier.Text,
                    FullyQualifiedName = ctorFullName,
                    StartLine = startLine,
                    EndLine = endLine,
                    Language = "csharp",
                    Metadata = new Dictionary<string, string>
                    {
                        ["parentType"] = parentName,
                    },
                });

                memberNode = new CodeNode
                {
                    Id = Guid.NewGuid(),
                    NodeType = CodeNodeType.Method,
                    Name = ".ctor",
                    FullName = ctorFullName,
                    FilePath = PathNormalizer.Normalize(filePath),
                    LineNumber = startLine,
                    Signature = ctorSignature,
                    Modifiers = ctor.Modifiers.ToString(),
                    RepositoryPath = workspacePath,
                };
                break;

            case FieldDeclarationSyntax field:
                foreach (var variable in field.Declaration.Variables)
                {
                    var fieldFullName = $"{parentFullName}.{variable.Identifier.Text}";

                    chunks.Add(new IngestedChunk(field.ToFullString(), "field")
                    {
                        SymbolName = variable.Identifier.Text,
                        FullyQualifiedName = fieldFullName,
                        StartLine = startLine,
                        EndLine = endLine,
                        Language = "csharp",
                        Metadata = new Dictionary<string, string>
                        {
                            ["parentType"] = parentName,
                            ["type"] = field.Declaration.Type.ToString(),
                        },
                    });

                    memberNode = new CodeNode
                    {
                        Id = Guid.NewGuid(),
                        NodeType = CodeNodeType.Field,
                        Name = variable.Identifier.Text,
                        FullName = fieldFullName,
                        FilePath = PathNormalizer.Normalize(filePath),
                        LineNumber = startLine,
                        Signature = $"{field.Declaration.Type} {variable.Identifier.Text}",
                        Modifiers = field.Modifiers.ToString(),
                        RepositoryPath = workspacePath,
                    };
                }
                break;

            case EventDeclarationSyntax eventDecl:
                var eventFullName = $"{parentFullName}.{eventDecl.Identifier.Text}";

                chunks.Add(new IngestedChunk(eventDecl.ToFullString(), "event")
                {
                    SymbolName = eventDecl.Identifier.Text,
                    FullyQualifiedName = eventFullName,
                    StartLine = startLine,
                    EndLine = endLine,
                    Language = "csharp",
                    Metadata = new Dictionary<string, string>
                    {
                        ["parentType"] = parentName,
                    },
                });

                memberNode = new CodeNode
                {
                    Id = Guid.NewGuid(),
                    NodeType = CodeNodeType.Event,
                    Name = eventDecl.Identifier.Text,
                    FullName = eventFullName,
                    FilePath = PathNormalizer.Normalize(filePath),
                    LineNumber = startLine,
                    Modifiers = eventDecl.Modifiers.ToString(),
                    RepositoryPath = workspacePath,
                };
                break;
        }

        if (memberNode != null)
        {
            nodes.Add(memberNode);

            // Create "Contains" edge from parent type to member
            edges.Add(new CodeEdge
            {
                Id = Guid.NewGuid(),
                EdgeType = CodeEdgeType.Contains,
                SourceId = parentNodeId,
                TargetId = memberNode.Id,
            });
        }
    }

    private static string GetNamespace(Microsoft.CodeAnalysis.SyntaxNode node)
    {
        var current = node.Parent;
        while (current != null)
        {
            if (current is BaseNamespaceDeclarationSyntax ns)
            {
                return ns.Name.ToString();
            }
            current = current.Parent;
        }
        return string.Empty;
    }

    private static string GetAccessibility(SyntaxTokenList modifiers)
    {
        if (modifiers.Any(SyntaxKind.PublicKeyword)) return "public";
        if (modifiers.Any(SyntaxKind.PrivateKeyword)) return "private";
        if (modifiers.Any(SyntaxKind.ProtectedKeyword)) return "protected";
        if (modifiers.Any(SyntaxKind.InternalKeyword)) return "internal";
        return "private"; // Default for C#
    }

    private static string GetTypeSignature(TypeDeclarationSyntax typeDecl)
    {
        var modifiers = typeDecl.Modifiers.ToString();
        var keyword = typeDecl switch
        {
            ClassDeclarationSyntax => "class",
            InterfaceDeclarationSyntax => "interface",
            StructDeclarationSyntax => "struct",
            RecordDeclarationSyntax r => r.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword) ? "record struct" : "record",
            _ => "class",
        };

        var name = typeDecl.Identifier.Text;
        var typeParams = typeDecl.TypeParameterList?.ToString() ?? string.Empty;
        var baseList = typeDecl.BaseList?.ToString() ?? string.Empty;

        return $"{modifiers} {keyword} {name}{typeParams}{(string.IsNullOrEmpty(baseList) ? "" : " " + baseList)}".Trim();
    }

    private static string GetTypeSignatureWithDoc(TypeDeclarationSyntax typeDecl)
    {
        var doc = GetXmlDocumentation(typeDecl);
        var signature = GetTypeSignature(typeDecl);
        return string.IsNullOrEmpty(doc) ? signature : $"{doc}\n{signature}";
    }

    private static string GetMethodSignature(MethodDeclarationSyntax method)
    {
        var modifiers = method.Modifiers.ToString();
        var returnType = method.ReturnType.ToString();
        var name = method.Identifier.Text;
        var typeParams = method.TypeParameterList?.ToString() ?? string.Empty;
        var parameters = method.ParameterList.ToString();

        return $"{modifiers} {returnType} {name}{typeParams}{parameters}".Trim();
    }

    private static string GetConstructorSignature(ConstructorDeclarationSyntax ctor)
    {
        var modifiers = ctor.Modifiers.ToString();
        var name = ctor.Identifier.Text;
        var parameters = ctor.ParameterList.ToString();

        return $"{modifiers} {name}{parameters}".Trim();
    }

    private static string GetXmlDocumentation(SyntaxNode node)
    {
        var trivia = node.GetLeadingTrivia();
        var docComment = trivia
            .Where(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                     || t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
            .Select(t => t.ToFullString().Trim())
            .FirstOrDefault();

        return docComment ?? string.Empty;
    }
}
