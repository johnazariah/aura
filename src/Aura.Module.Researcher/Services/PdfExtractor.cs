// <copyright file="PdfExtractor.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Researcher.Services;

using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Extracts text from PDFs using pdftotext (poppler-utils).
/// </summary>
public partial class PdfExtractor : IPdfExtractor
{
    private readonly ILogger<PdfExtractor> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfExtractor"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public PdfExtractor(ILogger<PdfExtractor> logger)
    {
        this.logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> IsPdfToTextAvailableAsync()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pdftotext",
                    Arguments = "-v",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode == 0 || process.ExitCode == 99; // -v returns 99
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<RawPdfContent> ExtractRawAsync(
        string pdfPath,
        bool preserveLayout = true,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(pdfPath))
        {
            throw new FileNotFoundException("PDF file not found", pdfPath);
        }

        var tempFile = Path.GetTempFileName();

        try
        {
            // Build pdftotext arguments
            var args = preserveLayout ? "-layout" : string.Empty;
            args += $" \"{pdfPath}\" \"{tempFile}\"";

            this.logger.LogDebug("Running pdftotext {Args}", args);

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pdftotext",
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            process.Start();

            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                var error = await errorTask;
                throw new InvalidOperationException($"pdftotext failed: {error}");
            }

            var text = await File.ReadAllTextAsync(tempFile, cancellationToken);

            // Get metadata and page count using pdfinfo
            var metadata = await this.ExtractMetadataAsync(pdfPath, cancellationToken);
            var pageCount = this.EstimatePageCount(text);

            if (metadata.TryGetValue("Pages", out var pages) && int.TryParse(pages, out var parsedPages))
            {
                pageCount = parsedPages;
            }

            return new RawPdfContent(text, pageCount, metadata);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private async Task<Dictionary<string, string>> ExtractMetadataAsync(
        string pdfPath,
        CancellationToken cancellationToken)
    {
        var metadata = new Dictionary<string, string>();

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pdfinfo",
                    Arguments = $"\"{pdfPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0)
            {
                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var colonIndex = line.IndexOf(':');
                    if (colonIndex > 0)
                    {
                        var key = line[..colonIndex].Trim();
                        var value = line[(colonIndex + 1)..].Trim();
                        metadata[key] = value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            this.logger.LogDebug(ex, "Failed to extract PDF metadata");
        }

        return metadata;
    }

    private int EstimatePageCount(string text)
    {
        // Count form feed characters (page breaks)
        return FormFeedPattern().Matches(text).Count + 1;
    }

    [GeneratedRegex(@"\f")]
    private static partial Regex FormFeedPattern();
}
