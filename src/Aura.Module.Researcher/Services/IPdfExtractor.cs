// <copyright file="IPdfExtractor.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Researcher.Services;

/// <summary>
/// Service for extracting text from PDF files.
/// </summary>
public interface IPdfExtractor
{
    /// <summary>
    /// Extracts raw text from a PDF using pdftotext.
    /// </summary>
    /// <param name="pdfPath">Path to the PDF file.</param>
    /// <param name="preserveLayout">Whether to preserve layout (multi-column).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Extracted text content.</returns>
    Task<RawPdfContent> ExtractRawAsync(
        string pdfPath,
        bool preserveLayout = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if pdftotext is available on the system.
    /// </summary>
    /// <returns>True if pdftotext is installed.</returns>
    Task<bool> IsPdfToTextAvailableAsync();
}

/// <summary>
/// Raw extracted content from a PDF.
/// </summary>
/// <param name="Text">The raw text content.</param>
/// <param name="PageCount">Number of pages.</param>
/// <param name="Metadata">PDF metadata if available.</param>
public record RawPdfContent(
    string Text,
    int PageCount,
    Dictionary<string, string> Metadata);
