// <copyright file="CSharpIngesterAgent.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Agents;

using System.Text.Json;
using Aura.Foundation.Agents;
using Aura.Foundation.Rag;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

/// <summary>
/// C# ingester agent using Roslyn for semantic code analysis.
/// Extracts classes, interfaces, methods, properties as semantic chunks.
/// </summary>
public sealed class CSharpIngesterAgent : IAgent
{
    private readonly ILogger<CSharpIngesterAgent> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CSharpIngesterAgent"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public CSharpIngesterAgent(ILogger<CSharpIngesterAgent> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public string AgentId => "csharp-ingester";

    /// <inheritdoc/>
    public AgentMetadata Metadata { get; } = new(
        Name: "C# Roslyn Ingester",
        Description: "Parses C# files using Roslyn for full semantic analysis. Extracts classes, interfaces, methods, and properties as semantic chunks.",
        Capabilities: ["ingest:cs", "ingest:csx"],
        Priority: 10,  // Specialized - user can override with 1-9
        Languages: ["csharp"],
        Provider: "native",
        Model: "roslyn",
        Temperature: 0,
        Tools: [],
        Tags: ["ingester", "roslyn", "csharp", "native", "semantic"]);

    /// <inheritdoc/>
    public Task<AgentOutput> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        var filePath = context.Properties.GetValueOrDefault("filePath") as string
            ?? throw new ArgumentException("filePath is required");
        var content = context.Properties.GetValueOrDefault("content") as string
            ?? throw new ArgumentException("content is required");

        _logger.LogDebug("Parsing C# file with Roslyn: {FilePath}", filePath);

        try
        {
            var chunks = ParseCSharp(content, filePath);

            _logger.LogDebug("Extracted {ChunkCount} chunks from {FilePath}", chunks.Count, filePath);

            var output = new AgentOutput(
                Content: $"Extracted {chunks.Count} semantic chunks from {Path.GetFileName(filePath)}",
                Artifacts: new Dictionary<string, string>
                {
                    ["chunks"] = JsonSerializer.Serialize(chunks),
                    ["language"] = "csharp",
                    ["parser"] = "roslyn",
                });

            return Task.FromResult(output);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse C# file: {FilePath}", filePath);
            throw;
        }
    }

    private List<SemanticChunk> ParseCSharp(string content, string filePath)
    {
        var chunks = new List<SemanticChunk>();

        var tree = CSharpSyntaxTree.ParseText(content, path: filePath);
        var root = tree.GetCompilationUnitRoot();

        // Get line positions for accurate line numbers
        var text = tree.GetText();

        // Process all type declarations
        foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            ProcessTypeDeclaration(typeDecl, filePath, text, chunks);
        }

        // Process enum declarations separately (they don't inherit from TypeDeclarationSyntax)
        foreach (var enumDecl in root.DescendantNodes().OfType<EnumDeclarationSyntax>())
        {
            ProcessEnumDeclaration(enumDecl, filePath, text, chunks);
        }

        // Process top-level statements (C# 9+)
        var topLevelStatements = root.Members.OfType<GlobalStatementSyntax>().ToList();
        if (topLevelStatements.Count > 0)
        {
            var firstStatement = topLevelStatements.First();
            var lastStatement = topLevelStatements.Last();
            var startLine = text.Lines.GetLinePosition(firstStatement.SpanStart).Line + 1;
            var endLine = text.Lines.GetLinePosition(lastStatement.Span.End).Line + 1;

            chunks.Add(new SemanticChunk
            {
                Text = string.Join("\n", topLevelStatements.Select(s => s.ToFullString())),
                FilePath = filePath,
                ChunkType = ChunkTypes.Function,
                SymbolName = "<Program>$",
                StartLine = startLine,
                EndLine = endLine,
                Language = "csharp",
                Context = "Top-level statements",
                Signature = "top-level",
            });
        }

        return chunks;
    }

    private void ProcessTypeDeclaration(
        TypeDeclarationSyntax typeDecl,
        string filePath,
        Microsoft.CodeAnalysis.Text.SourceText text,
        List<SemanticChunk> chunks)
    {
        var startLine = text.Lines.GetLinePosition(typeDecl.SpanStart).Line + 1;
        var endLine = text.Lines.GetLinePosition(typeDecl.Span.End).Line + 1;

        var chunkType = typeDecl switch
        {
            ClassDeclarationSyntax => ChunkTypes.Class,
            InterfaceDeclarationSyntax => ChunkTypes.Interface,
            StructDeclarationSyntax => ChunkTypes.Struct,
            RecordDeclarationSyntax => ChunkTypes.Record,
            _ => ChunkTypes.Class,
        };

        var namespaceName = GetNamespace(typeDecl);
        var fullName = string.IsNullOrEmpty(namespaceName)
            ? typeDecl.Identifier.Text
            : $"{namespaceName}.{typeDecl.Identifier.Text}";

        // Add the type declaration itself (with summary doc if present)
        var typeChunk = new SemanticChunk
        {
            Text = GetTypeSignatureWithDoc(typeDecl),
            FilePath = filePath,
            ChunkType = chunkType,
            SymbolName = typeDecl.Identifier.Text,
            FullyQualifiedName = fullName,
            StartLine = startLine,
            EndLine = endLine,
            Language = "csharp",
            Context = namespaceName,
            Signature = GetTypeSignature(typeDecl),
            Metadata = GetTypeMetadata(typeDecl),
        };
        chunks.Add(typeChunk);

        // Process members
        foreach (var member in typeDecl.Members)
        {
            ProcessMember(member, typeDecl.Identifier.Text, fullName, filePath, text, chunks);
        }
    }

    private void ProcessEnumDeclaration(
        EnumDeclarationSyntax enumDecl,
        string filePath,
        Microsoft.CodeAnalysis.Text.SourceText text,
        List<SemanticChunk> chunks)
    {
        var startLine = text.Lines.GetLinePosition(enumDecl.SpanStart).Line + 1;
        var endLine = text.Lines.GetLinePosition(enumDecl.Span.End).Line + 1;

        var namespaceName = GetNamespace(enumDecl);
        var fullName = string.IsNullOrEmpty(namespaceName)
            ? enumDecl.Identifier.Text
            : $"{namespaceName}.{enumDecl.Identifier.Text}";

        chunks.Add(new SemanticChunk
        {
            Text = enumDecl.ToFullString(),
            FilePath = filePath,
            ChunkType = ChunkTypes.Enum,
            SymbolName = enumDecl.Identifier.Text,
            FullyQualifiedName = fullName,
            StartLine = startLine,
            EndLine = endLine,
            Language = "csharp",
            Context = namespaceName,
            Signature = $"{enumDecl.Modifiers} enum {enumDecl.Identifier.Text}",
            Metadata = new Dictionary<string, string>
            {
                ["accessibility"] = GetAccessibility(enumDecl.Modifiers),
                ["memberCount"] = enumDecl.Members.Count.ToString(),
            },
        });
    }

    private void ProcessMember(
        MemberDeclarationSyntax member,
        string parentName,
        string parentFullName,
        string filePath,
        Microsoft.CodeAnalysis.Text.SourceText text,
        List<SemanticChunk> chunks)
    {
        var startLine = text.Lines.GetLinePosition(member.SpanStart).Line + 1;
        var endLine = text.Lines.GetLinePosition(member.Span.End).Line + 1;

        switch (member)
        {
            case MethodDeclarationSyntax method:
                chunks.Add(new SemanticChunk
                {
                    Text = method.ToFullString(),
                    FilePath = filePath,
                    ChunkType = ChunkTypes.Method,
                    SymbolName = method.Identifier.Text,
                    ParentSymbol = parentName,
                    FullyQualifiedName = $"{parentFullName}.{method.Identifier.Text}",
                    StartLine = startLine,
                    EndLine = endLine,
                    Language = "csharp",
                    Signature = GetMethodSignature(method),
                    Metadata = GetMethodMetadata(method),
                });
                break;

            case PropertyDeclarationSyntax property:
                chunks.Add(new SemanticChunk
                {
                    Text = property.ToFullString(),
                    FilePath = filePath,
                    ChunkType = ChunkTypes.Property,
                    SymbolName = property.Identifier.Text,
                    ParentSymbol = parentName,
                    FullyQualifiedName = $"{parentFullName}.{property.Identifier.Text}",
                    StartLine = startLine,
                    EndLine = endLine,
                    Language = "csharp",
                    Signature = GetPropertySignature(property),
                });
                break;

            case ConstructorDeclarationSyntax ctor:
                chunks.Add(new SemanticChunk
                {
                    Text = ctor.ToFullString(),
                    FilePath = filePath,
                    ChunkType = ChunkTypes.Constructor,
                    SymbolName = ctor.Identifier.Text,
                    ParentSymbol = parentName,
                    FullyQualifiedName = $"{parentFullName}.{ctor.Identifier.Text}",
                    StartLine = startLine,
                    EndLine = endLine,
                    Language = "csharp",
                    Signature = GetConstructorSignature(ctor),
                });
                break;

            case FieldDeclarationSyntax field:
                foreach (var variable in field.Declaration.Variables)
                {
                    chunks.Add(new SemanticChunk
                    {
                        Text = field.ToFullString(),
                        FilePath = filePath,
                        ChunkType = ChunkTypes.Field,
                        SymbolName = variable.Identifier.Text,
                        ParentSymbol = parentName,
                        FullyQualifiedName = $"{parentFullName}.{variable.Identifier.Text}",
                        StartLine = startLine,
                        EndLine = endLine,
                        Language = "csharp",
                        Signature = $"{field.Declaration.Type} {variable.Identifier.Text}",
                    });
                }
                break;

            case EventDeclarationSyntax eventDecl:
                chunks.Add(new SemanticChunk
                {
                    Text = eventDecl.ToFullString(),
                    FilePath = filePath,
                    ChunkType = ChunkTypes.Event,
                    SymbolName = eventDecl.Identifier.Text,
                    ParentSymbol = parentName,
                    FullyQualifiedName = $"{parentFullName}.{eventDecl.Identifier.Text}",
                    StartLine = startLine,
                    EndLine = endLine,
                    Language = "csharp",
                    Signature = $"event {eventDecl.Type} {eventDecl.Identifier.Text}",
                });
                break;

            case DelegateDeclarationSyntax delegateDecl:
                chunks.Add(new SemanticChunk
                {
                    Text = delegateDecl.ToFullString(),
                    FilePath = filePath,
                    ChunkType = ChunkTypes.Delegate,
                    SymbolName = delegateDecl.Identifier.Text,
                    ParentSymbol = parentName,
                    FullyQualifiedName = $"{parentFullName}.{delegateDecl.Identifier.Text}",
                    StartLine = startLine,
                    EndLine = endLine,
                    Language = "csharp",
                    Signature = GetDelegateSignature(delegateDecl),
                });
                break;

            // Nested types are handled recursively by ProcessTypeDeclaration
            case TypeDeclarationSyntax nestedType:
                ProcessTypeDeclaration(nestedType, filePath, text, chunks);
                break;
        }
    }

    private static string? GetNamespace(SyntaxNode node)
    {
        var namespaceDecl = node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
        return namespaceDecl?.Name.ToString();
    }

    private static string GetTypeSignature(TypeDeclarationSyntax typeDecl)
    {
        var modifiers = typeDecl.Modifiers.ToString();
        var keyword = typeDecl.Keyword.Text;
        var name = typeDecl.Identifier.Text;
        var typeParams = typeDecl.TypeParameterList?.ToString() ?? string.Empty;
        var baseList = typeDecl.BaseList?.ToString() ?? string.Empty;

        return $"{modifiers} {keyword} {name}{typeParams} {baseList}".Trim();
    }

    private static string GetTypeSignatureWithDoc(TypeDeclarationSyntax typeDecl)
    {
        var trivia = typeDecl.GetLeadingTrivia();
        var xmlDoc = trivia
            .Where(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                        t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
            .Select(t => t.ToFullString())
            .FirstOrDefault() ?? string.Empty;

        // Get just the type declaration line (without members)
        var signature = GetTypeSignature(typeDecl);

        return string.IsNullOrEmpty(xmlDoc)
            ? signature
            : $"{xmlDoc.Trim()}\n{signature}";
    }

    private static Dictionary<string, string> GetTypeMetadata(TypeDeclarationSyntax typeDecl)
    {
        var metadata = new Dictionary<string, string>
        {
            ["accessibility"] = GetAccessibility(typeDecl.Modifiers),
            ["isAbstract"] = typeDecl.Modifiers.Any(SyntaxKind.AbstractKeyword).ToString(),
            ["isSealed"] = typeDecl.Modifiers.Any(SyntaxKind.SealedKeyword).ToString(),
            ["isStatic"] = typeDecl.Modifiers.Any(SyntaxKind.StaticKeyword).ToString(),
            ["isPartial"] = typeDecl.Modifiers.Any(SyntaxKind.PartialKeyword).ToString(),
        };

        if (typeDecl.BaseList is not null)
        {
            var baseTypes = typeDecl.BaseList.Types.Select(t => t.Type.ToString()).ToList();
            if (baseTypes.Count > 0)
            {
                metadata["baseTypes"] = string.Join(", ", baseTypes);
            }
        }

        return metadata;
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

    private static Dictionary<string, string> GetMethodMetadata(MethodDeclarationSyntax method)
    {
        return new Dictionary<string, string>
        {
            ["accessibility"] = GetAccessibility(method.Modifiers),
            ["isAsync"] = method.Modifiers.Any(SyntaxKind.AsyncKeyword).ToString(),
            ["isStatic"] = method.Modifiers.Any(SyntaxKind.StaticKeyword).ToString(),
            ["isVirtual"] = method.Modifiers.Any(SyntaxKind.VirtualKeyword).ToString(),
            ["isOverride"] = method.Modifiers.Any(SyntaxKind.OverrideKeyword).ToString(),
            ["isAbstract"] = method.Modifiers.Any(SyntaxKind.AbstractKeyword).ToString(),
            ["returnType"] = method.ReturnType.ToString(),
            ["parameterCount"] = method.ParameterList.Parameters.Count.ToString(),
        };
    }

    private static string GetPropertySignature(PropertyDeclarationSyntax property)
    {
        var modifiers = property.Modifiers.ToString();
        var type = property.Type.ToString();
        var name = property.Identifier.Text;
        var accessors = property.AccessorList?.Accessors
            .Select(a => a.Keyword.Text)
            .ToList() ?? [];

        var accessorStr = accessors.Count > 0 ? $" {{ {string.Join("; ", accessors)}; }}" : string.Empty;

        return $"{modifiers} {type} {name}{accessorStr}".Trim();
    }

    private static string GetConstructorSignature(ConstructorDeclarationSyntax ctor)
    {
        var modifiers = ctor.Modifiers.ToString();
        var name = ctor.Identifier.Text;
        var parameters = ctor.ParameterList.ToString();

        return $"{modifiers} {name}{parameters}".Trim();
    }

    private static string GetDelegateSignature(DelegateDeclarationSyntax delegateDecl)
    {
        var modifiers = delegateDecl.Modifiers.ToString();
        var returnType = delegateDecl.ReturnType.ToString();
        var name = delegateDecl.Identifier.Text;
        var typeParams = delegateDecl.TypeParameterList?.ToString() ?? string.Empty;
        var parameters = delegateDecl.ParameterList.ToString();

        return $"{modifiers} delegate {returnType} {name}{typeParams}{parameters}".Trim();
    }

    private static string GetAccessibility(SyntaxTokenList modifiers)
    {
        if (modifiers.Any(SyntaxKind.PublicKeyword))
        {
            return "public";
        }

        if (modifiers.Any(SyntaxKind.PrivateKeyword) && modifiers.Any(SyntaxKind.ProtectedKeyword))
        {
            return "private protected";
        }

        if (modifiers.Any(SyntaxKind.ProtectedKeyword) && modifiers.Any(SyntaxKind.InternalKeyword))
        {
            return "protected internal";
        }

        if (modifiers.Any(SyntaxKind.ProtectedKeyword))
        {
            return "protected";
        }

        if (modifiers.Any(SyntaxKind.InternalKeyword))
        {
            return "internal";
        }

        if (modifiers.Any(SyntaxKind.PrivateKeyword))
        {
            return "private";
        }

        return "private"; // Default for class members
    }
}
