// <copyright file="ResearcherModuleOptions.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Researcher;

/// <summary>
/// Configuration options for the Researcher module.
/// </summary>
public class ResearcherModuleOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Researcher";

    /// <summary>
    /// Gets or sets the base path for storing research files.
    /// </summary>
    public string StoragePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".aura",
        "research");

    /// <summary>
    /// Gets or sets the path for cached PDFs.
    /// </summary>
    public string PapersPath => Path.Combine(this.StoragePath, "papers");

    /// <summary>
    /// Gets or sets whether to auto-download PDFs when importing.
    /// </summary>
    public bool AutoDownloadPdfs { get; set; } = true;

    /// <summary>
    /// Gets or sets the default enhancement level for PDF conversion.
    /// </summary>
    public string DefaultEnhancementLevel { get; set; } = "Basic";

    /// <summary>
    /// Gets or sets the Semantic Scholar API key (optional, increases rate limits).
    /// </summary>
    public string? SemanticScholarApiKey { get; set; }
}
