// <copyright file="ResearcherEndpoints.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Endpoints;

using Aura.Module.Researcher.Data.Entities;
using Aura.Module.Researcher.Fetchers;
using Aura.Module.Researcher.Services;

/// <summary>
/// Researcher module endpoints for library management.
/// </summary>
public static class ResearcherEndpoints
{
    /// <summary>
    /// Maps all researcher endpoints to the application.
    /// </summary>
    public static WebApplication MapResearcherEndpoints(this WebApplication app)
    {
        // Library CRUD
        app.MapGet("/api/researcher/sources", ListSources);
        app.MapGet("/api/researcher/sources/{id:guid}", GetSource);
        app.MapPost("/api/researcher/sources", CreateSource);
        app.MapPost("/api/researcher/sources/import", ImportSource);
        app.MapPatch("/api/researcher/sources/{id:guid}", UpdateSource);
        app.MapDelete("/api/researcher/sources/{id:guid}", DeleteSource);

        // Search
        app.MapPost("/api/researcher/sources/search", SearchLibrary);
        app.MapPost("/api/researcher/papers/search", SearchPapers);

        // Excerpts
        app.MapGet("/api/researcher/sources/{id:guid}/excerpts", GetExcerpts);
        app.MapPost("/api/researcher/sources/{id:guid}/excerpts", AddExcerpt);

        // PDF operations
        app.MapPost("/api/researcher/sources/{id:guid}/convert", ConvertToMarkdown);
        app.MapGet("/api/researcher/sources/{id:guid}/markdown", GetMarkdown);

        return app;
    }

    private static async Task<IResult> ListSources(
        ILibraryService libraryService,
        string? type = null,
        string? status = null,
        string? tags = null,
        CancellationToken ct = default)
    {
        SourceType? sourceType = null;
        if (!string.IsNullOrEmpty(type) && Enum.TryParse<SourceType>(type, true, out var st))
        {
            sourceType = st;
        }

        ReadingStatus? readingStatus = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<ReadingStatus>(status, true, out var rs))
        {
            readingStatus = rs;
        }

        var tagArray = string.IsNullOrEmpty(tags) ? null : tags.Split(',');

        var sources = await libraryService.GetSourcesAsync(sourceType, readingStatus, tagArray, ct);

        return Results.Ok(sources.Select(s => new
        {
            s.Id,
            s.Title,
            s.Authors,
            s.SourceType,
            s.ReadingStatus,
            s.Tags,
            s.ArxivId,
            s.Doi,
            s.PublishedDate,
            s.CitationCount,
            s.CreatedAt,
        }));
    }

    private static async Task<IResult> GetSource(
        Guid id,
        ILibraryService libraryService,
        bool includeExcerpts = false,
        CancellationToken ct = default)
    {
        var source = await libraryService.GetSourceAsync(id, includeExcerpts, ct);
        if (source == null)
        {
            return Results.NotFound(new { error = "Source not found" });
        }

        return Results.Ok(new
        {
            source.Id,
            source.Title,
            source.Authors,
            source.Abstract,
            source.SourceType,
            source.ReadingStatus,
            source.Url,
            source.Doi,
            source.ArxivId,
            source.PublishedDate,
            source.Venue,
            source.CitationCount,
            source.Tags,
            source.Notes,
            source.PdfPath,
            source.MarkdownPath,
            source.CreatedAt,
            source.UpdatedAt,
            Excerpts = includeExcerpts
                ? source.Excerpts.Select(e => new
                {
                    e.Id,
                    e.Content,
                    e.PageNumber,
                    e.Location,
                    e.Annotation,
                    e.CreatedAt,
                })
                : null,
        });
    }

    private static async Task<IResult> CreateSource(
        CreateSourceRequest request,
        ILibraryService libraryService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Results.BadRequest(new { error = "Title is required" });
        }

        var source = new Source
        {
            Title = request.Title,
            Authors = request.Authors ?? [],
            Abstract = request.Abstract,
            Url = request.Url,
            Doi = request.Doi,
            ArxivId = request.ArxivId,
            Tags = request.Tags ?? [],
            SourceType = Enum.TryParse<SourceType>(request.SourceType, true, out var st) ? st : SourceType.Article,
        };

        var created = await libraryService.CreateSourceAsync(source, ct);

        return Results.Created($"/api/researcher/sources/{created.Id}", new
        {
            created.Id,
            created.Title,
        });
    }

    private static async Task<IResult> ImportSource(
        ImportSourceRequest request,
        SourceFetcherService fetcherService,
        ILibraryService libraryService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.UrlOrId))
        {
            return Results.BadRequest(new { error = "URL or identifier is required" });
        }

        var result = await fetcherService.FetchAsync(request.UrlOrId, request.DownloadPdf ?? true, ct);

        if (!result.Success)
        {
            return Results.BadRequest(new { error = result.Error });
        }

        // Add tags if provided
        if (request.Tags is { Length: > 0 })
        {
            result.Source.Tags = request.Tags;
        }

        var created = await libraryService.CreateSourceAsync(result.Source, ct);

        return Results.Created($"/api/researcher/sources/{created.Id}", new
        {
            created.Id,
            created.Title,
            created.Authors,
            created.ArxivId,
            created.Doi,
            created.PdfPath,
        });
    }

    private static async Task<IResult> UpdateSource(
        Guid id,
        UpdateSourceRequest request,
        ILibraryService libraryService,
        CancellationToken ct)
    {
        var source = await libraryService.GetSourceAsync(id, false, ct);
        if (source == null)
        {
            return Results.NotFound(new { error = "Source not found" });
        }

        if (request.Tags != null)
        {
            source.Tags = request.Tags;
        }

        if (request.Notes != null)
        {
            source.Notes = request.Notes;
        }

        if (!string.IsNullOrEmpty(request.ReadingStatus) && Enum.TryParse<ReadingStatus>(request.ReadingStatus, true, out var rs))
        {
            source.ReadingStatus = rs;
        }

        var updated = await libraryService.UpdateSourceAsync(source, ct);

        return Results.Ok(new { updated.Id, updated.Tags, updated.Notes, updated.ReadingStatus });
    }

    private static async Task<IResult> DeleteSource(
        Guid id,
        ILibraryService libraryService,
        CancellationToken ct)
    {
        var deleted = await libraryService.DeleteSourceAsync(id, ct);
        if (!deleted)
        {
            return Results.NotFound(new { error = "Source not found" });
        }

        return Results.NoContent();
    }

    private static async Task<IResult> SearchLibrary(
        SearchRequest request,
        ILibraryService libraryService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return Results.BadRequest(new { error = "Query is required" });
        }

        var results = await libraryService.SearchAsync(request.Query, request.Limit ?? 10, ct);

        return Results.Ok(results.Select(r => new
        {
            r.Source.Id,
            r.Source.Title,
            r.Source.Authors,
            r.Source.SourceType,
            r.Score,
        }));
    }

    private static async Task<IResult> SearchPapers(
        SearchPapersRequest request,
        SourceFetcherService fetcherService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return Results.BadRequest(new { error = "Query is required" });
        }

        var results = await fetcherService.SearchAsync(
            request.Query,
            request.Sources,
            request.Limit ?? 10,
            ct);

        return Results.Ok(results);
    }

    private static async Task<IResult> GetExcerpts(
        Guid id,
        ILibraryService libraryService,
        CancellationToken ct)
    {
        var excerpts = await libraryService.GetExcerptsAsync(id, ct);

        return Results.Ok(excerpts.Select(e => new
        {
            e.Id,
            e.Content,
            e.PageNumber,
            e.Location,
            e.Annotation,
            e.CreatedAt,
        }));
    }

    private static async Task<IResult> AddExcerpt(
        Guid id,
        AddExcerptRequest request,
        ILibraryService libraryService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return Results.BadRequest(new { error = "Content is required" });
        }

        var excerpt = new Excerpt
        {
            SourceId = id,
            Content = request.Content,
            PageNumber = request.PageNumber,
            Location = request.Location,
            Annotation = request.Annotation,
        };

        var created = await libraryService.AddExcerptAsync(excerpt, ct);

        return Results.Created($"/api/researcher/sources/{id}/excerpts/{created.Id}", new
        {
            created.Id,
            created.Content,
            created.PageNumber,
        });
    }

    private static async Task<IResult> ConvertToMarkdown(
        Guid id,
        ILibraryService libraryService,
        IPdfToMarkdownService pdfToMarkdownService,
        CancellationToken ct)
    {
        var source = await libraryService.GetSourceAsync(id, false, ct);
        if (source == null)
        {
            return Results.NotFound(new { error = "Source not found" });
        }

        if (string.IsNullOrEmpty(source.PdfPath) || !File.Exists(source.PdfPath))
        {
            return Results.BadRequest(new { error = "No PDF available for this source" });
        }

        var document = await pdfToMarkdownService.ConvertAsync(source.PdfPath, null, ct);

        // Save markdown to file
        var markdownPath = Path.Combine(
            Path.GetDirectoryName(source.PdfPath)!,
            "content.md");

        await File.WriteAllTextAsync(markdownPath, document.Content, ct);

        source.MarkdownPath = markdownPath;
        await libraryService.UpdateSourceAsync(source, ct);

        return Results.Ok(new
        {
            source.Id,
            source.MarkdownPath,
            document.Title,
            document.Authors,
            SectionCount = document.Sections.Count,
            CitationCount = document.Citations.Count,
        });
    }

    private static async Task<IResult> GetMarkdown(
        Guid id,
        ILibraryService libraryService,
        CancellationToken ct)
    {
        var source = await libraryService.GetSourceAsync(id, false, ct);
        if (source == null)
        {
            return Results.NotFound(new { error = "Source not found" });
        }

        if (string.IsNullOrEmpty(source.MarkdownPath) || !File.Exists(source.MarkdownPath))
        {
            return Results.NotFound(new { error = "Markdown not available. Call /convert first." });
        }

        var content = await File.ReadAllTextAsync(source.MarkdownPath, ct);

        return Results.Content(content, "text/markdown");
    }

    // Request DTOs
    private record CreateSourceRequest(
        string? Title,
        string[]? Authors,
        string? Abstract,
        string? Url,
        string? Doi,
        string? ArxivId,
        string[]? Tags,
        string? SourceType);

    private record ImportSourceRequest(
        string? UrlOrId,
        string[]? Tags,
        bool? DownloadPdf);

    private record UpdateSourceRequest(
        string[]? Tags,
        string? Notes,
        string? ReadingStatus);

    private record SearchRequest(
        string? Query,
        int? Limit);

    private record SearchPapersRequest(
        string? Query,
        string[]? Sources,
        int? Limit);

    private record AddExcerptRequest(
        string? Content,
        int? PageNumber,
        string? Location,
        string? Annotation);
}
