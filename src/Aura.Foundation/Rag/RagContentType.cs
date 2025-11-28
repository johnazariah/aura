// <copyright file="RagContentType.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Rag;

/// <summary>
/// Types of content that can be indexed in RAG.
/// </summary>
public enum RagContentType
{
    /// <summary>Unknown or generic text.</summary>
    Unknown = 0,

    /// <summary>Source code.</summary>
    Code,

    /// <summary>Markdown documentation.</summary>
    Markdown,

    /// <summary>Plain text.</summary>
    PlainText,

    /// <summary>Technical documentation.</summary>
    Documentation,

    /// <summary>PDF document (extracted text).</summary>
    Pdf,

    /// <summary>Receipt or invoice (for financial vertical).</summary>
    Receipt,

    /// <summary>Research paper (for research vertical).</summary>
    Paper,
}
