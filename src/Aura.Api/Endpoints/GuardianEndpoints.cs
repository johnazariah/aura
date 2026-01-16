// <copyright file="GuardianEndpoints.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Endpoints;

using Aura.Foundation.Guardians;

/// <summary>
/// Guardian endpoints for monitoring and managing guardians.
/// </summary>
public static class GuardianEndpoints
{
    /// <summary>
    /// Maps all guardian endpoints to the application.
    /// </summary>
    public static WebApplication MapGuardianEndpoints(this WebApplication app)
    {
        app.MapGet("/api/guardians", ListGuardians);
        app.MapGet("/api/guardians/{id}", GetGuardian);
        app.MapPost("/api/guardians/{id}/run", RunGuardian);
        app.MapPost("/api/guardians/reload", ReloadGuardians);

        return app;
    }

    private static IResult ListGuardians(IGuardianRegistry registry)
    {
        var guardians = registry.Guardians.Select(g => new GuardianSummaryResponse
        {
            Id = g.Id,
            Name = g.Name,
            Description = g.Description,
            Version = g.Version,
            TriggerCount = g.Triggers.Count,
        }).ToList();

        return Results.Ok(guardians);
    }

    private static IResult GetGuardian(string id, IGuardianRegistry registry)
    {
        var guardian = registry.GetGuardian(id);
        if (guardian is null)
        {
            return Results.NotFound(new { error = $"Guardian '{id}' not found" });
        }

        return Results.Ok(new GuardianDetailResponse
        {
            Id = guardian.Id,
            Name = guardian.Name,
            Description = guardian.Description,
            Version = guardian.Version,
            Triggers = guardian.Triggers.Select(t => new GuardianTriggerResponse
            {
                Type = t.Type.ToString(),
                Cron = t.Cron,
                Patterns = t.Patterns,
            }).ToList(),
            Detection = guardian.Detection is not null ? new GuardianDetectionResponse
            {
                SourceCount = guardian.Detection.Sources?.Count ?? 0,
                RuleCount = guardian.Detection.Rules?.Count ?? 0,
                CommandCount = guardian.Detection.Commands?.Count ?? 0,
            } : null,
            Workflow = guardian.Workflow is not null ? new GuardianWorkflowResponse
            {
                Title = guardian.Workflow.Title,
                Priority = guardian.Workflow.Priority,
                Mode = guardian.Workflow.Mode,
                SuggestedCapability = guardian.Workflow.SuggestedCapability,
            } : null,
        });
    }

    private static async Task<IResult> RunGuardian(
        string id,
        IGuardianRegistry registry,
        IGuardianExecutor executor,
        CancellationToken ct)
    {
        var guardian = registry.GetGuardian(id);
        if (guardian is null)
        {
            return Results.NotFound(new { error = $"Guardian '{id}' not found" });
        }

        var result = await executor.ExecuteAsync(guardian, ct);

        return Results.Ok(new GuardianExecutionResponse
        {
            GuardianId = result.GuardianId,
            Status = result.Status.ToString(),
            ViolationsFound = result.CheckResult?.Violations.Count ?? 0,
            WorkflowsCreated = result.CreatedWorkflowIds.Count,
            CreatedWorkflowIds = result.CreatedWorkflowIds,
            Duration = result.Duration,
            ErrorMessage = result.ErrorMessage,
        });
    }

    private static async Task<IResult> ReloadGuardians(IGuardianRegistry registry)
    {
        await registry.ReloadAsync();
        return Results.Ok(new { message = "Guardians reloaded", count = registry.Guardians.Count });
    }
}

// Response DTOs

/// <summary>Guardian summary for list view.</summary>
public record GuardianSummaryResponse
{
    /// <summary>Gets the guardian ID.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the guardian name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the version.</summary>
    public int Version { get; init; }

    /// <summary>Gets the number of triggers.</summary>
    public int TriggerCount { get; init; }
}

/// <summary>Guardian detail response.</summary>
public record GuardianDetailResponse
{
    /// <summary>Gets the guardian ID.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the guardian name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the version.</summary>
    public int Version { get; init; }

    /// <summary>Gets the triggers.</summary>
    public required IReadOnlyList<GuardianTriggerResponse> Triggers { get; init; }

    /// <summary>Gets the detection configuration.</summary>
    public GuardianDetectionResponse? Detection { get; init; }

    /// <summary>Gets the workflow template.</summary>
    public GuardianWorkflowResponse? Workflow { get; init; }
}

/// <summary>Guardian trigger response.</summary>
public record GuardianTriggerResponse
{
    /// <summary>Gets the trigger type.</summary>
    public required string Type { get; init; }

    /// <summary>Gets the cron expression.</summary>
    public string? Cron { get; init; }

    /// <summary>Gets the file patterns.</summary>
    public IReadOnlyList<string>? Patterns { get; init; }
}

/// <summary>Guardian detection response.</summary>
public record GuardianDetectionResponse
{
    /// <summary>Gets the source count.</summary>
    public int SourceCount { get; init; }

    /// <summary>Gets the rule count.</summary>
    public int RuleCount { get; init; }

    /// <summary>Gets the command count.</summary>
    public int CommandCount { get; init; }
}

/// <summary>Guardian workflow template response.</summary>
public record GuardianWorkflowResponse
{
    /// <summary>Gets the workflow title template.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the priority.</summary>
    public string Priority { get; init; } = "medium";

    /// <summary>Gets the mode.</summary>
    public string Mode { get; init; } = "structured";

    /// <summary>Gets the suggested capability.</summary>
    public string? SuggestedCapability { get; init; }
}

/// <summary>Guardian execution response.</summary>
public record GuardianExecutionResponse
{
    /// <summary>Gets the guardian ID.</summary>
    public required string GuardianId { get; init; }

    /// <summary>Gets the execution status.</summary>
    public required string Status { get; init; }

    /// <summary>Gets the number of violations found.</summary>
    public int ViolationsFound { get; init; }

    /// <summary>Gets the number of workflows created.</summary>
    public int WorkflowsCreated { get; init; }

    /// <summary>Gets the IDs of created workflows.</summary>
    public IReadOnlyList<Guid> CreatedWorkflowIds { get; init; } = [];

    /// <summary>Gets the execution duration.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Gets the error message if any.</summary>
    public string? ErrorMessage { get; init; }
}
