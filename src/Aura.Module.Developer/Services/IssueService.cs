// <copyright file="IssueService.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services;

using Aura.Module.Developer.Data;
using Aura.Module.Developer.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Service for managing local issues.
/// </summary>
public sealed class IssueService : IIssueService
{
    private readonly DeveloperDbContext _db;
    private readonly ILogger<IssueService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IssueService"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="logger">The logger.</param>
    public IssueService(DeveloperDbContext db, ILogger<IssueService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Issue> CreateAsync(
        string title,
        string? description = null,
        string? repositoryPath = null,
        CancellationToken ct = default)
    {
        var issue = new Issue
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description,
            RepositoryPath = repositoryPath,
            Status = IssueStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _db.Issues.Add(issue);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created issue {IssueId}: {Title}", issue.Id, title);
        return issue;
    }

    /// <inheritdoc/>
    public async Task<Issue?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Issues.FindAsync([id], ct);
    }

    /// <inheritdoc/>
    public async Task<Issue?> GetByIdWithWorkflowAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Issues
            .Include(i => i.Workflow)
            .ThenInclude(w => w!.Steps.OrderBy(s => s.Order))
            .FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Issue>> ListAsync(IssueStatus? status = null, CancellationToken ct = default)
    {
        var query = _db.Issues.AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(i => i.Status == status.Value);
        }

        return await query
            .OrderByDescending(i => i.CreatedAt)
            .Include(i => i.Workflow)
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<Issue> UpdateAsync(
        Guid id,
        string? title = null,
        string? description = null,
        CancellationToken ct = default)
    {
        var issue = await _db.Issues.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Issue {id} not found");

        if (title is not null)
        {
            issue.Title = title;
        }

        if (description is not null)
        {
            issue.Description = description;
        }

        issue.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Updated issue {IssueId}", id);
        return issue;
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var issue = await _db.Issues.FindAsync([id], ct);
        if (issue is null)
        {
            return;
        }

        _db.Issues.Remove(issue);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted issue {IssueId}", id);
    }

    /// <inheritdoc/>
    public async Task<Issue> CloseAsync(Guid id, CancellationToken ct = default)
    {
        var issue = await _db.Issues.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Issue {id} not found");

        issue.Status = IssueStatus.Closed;
        issue.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Closed issue {IssueId}", id);
        return issue;
    }
}
