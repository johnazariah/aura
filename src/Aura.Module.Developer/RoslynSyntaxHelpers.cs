// <copyright file="RoslynSyntaxHelpers.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer;

using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Shared Roslyn syntax tree helpers to avoid duplication across ingestors, agents, and services.
/// </summary>
internal static class RoslynSyntaxHelpers
{
    /// <summary>
    /// Walks the syntax tree upward from the given node to find the enclosing namespace declaration.
    /// Returns the namespace name, or <see langword="null"/> if the node is not inside a namespace.
    /// </summary>
    internal static string? GetContainingNamespace(Microsoft.CodeAnalysis.SyntaxNode node)
    {
        return node.Ancestors()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .FirstOrDefault()
            ?.Name.ToString();
    }
}
