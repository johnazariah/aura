// <copyright file="Problem.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Problems;

using System.Diagnostics;

/// <summary>
/// Factory methods for creating RFC 7807 problem responses.
/// </summary>
public static class Problem
{
    private const string ContentType = "application/problem+json";

    // =========================================================================
    // Not Found (404)
    // =========================================================================

    /// <summary>Creates a generic not found response.</summary>
    public static IResult NotFound(string resourceType, object id, HttpContext context) =>
        CreateProblem(
            ProblemTypes.NotFound,
            $"{resourceType} Not Found",
            404,
            $"{resourceType} with ID '{id}' does not exist.",
            context);

    /// <summary>Creates a story not found response.</summary>
    public static IResult StoryNotFound(Guid id, HttpContext context) =>
        CreateProblem(
            ProblemTypes.StoryNotFound,
            "Story Not Found",
            404,
            $"Story with ID '{id}' does not exist.",
            context);

    /// <summary>Creates a step not found response.</summary>
    public static IResult StepNotFound(Guid storyId, Guid stepId, HttpContext context) =>
        CreateProblem(
            ProblemTypes.StepNotFound,
            "Step Not Found",
            404,
            $"Step '{stepId}' not found in story '{storyId}'.",
            context);

    /// <summary>Creates an agent not found response.</summary>
    public static IResult AgentNotFound(string agentId, HttpContext context) =>
        CreateProblem(
            ProblemTypes.AgentNotFound,
            "Agent Not Found",
            404,
            $"Agent '{agentId}' not found.",
            context);

    /// <summary>Creates an agent not found for capability response.</summary>
    public static IResult AgentNotFoundForCapability(string capability, string? language, HttpContext context)
    {
        var detail = language is not null
            ? $"No agent found for capability '{capability}' with language '{language}'."
            : $"No agent found for capability '{capability}'.";

        return CreateProblem(
            ProblemTypes.AgentNotFound,
            "Agent Not Found",
            404,
            detail,
            context);
    }

    /// <summary>Creates a story not found by path response.</summary>
    public static IResult StoryNotFoundByPath(string path, HttpContext context) =>
        CreateProblem(
            ProblemTypes.StoryNotFound,
            "Story Not Found",
            404,
            $"No story found for path: {path}",
            context);

    /// <summary>Creates a workspace not found response.</summary>
    public static IResult WorkspaceNotFound(string idOrPath, HttpContext context) =>
        CreateProblem(
            ProblemTypes.WorkspaceNotFound,
            "Workspace Not Found",
            404,
            $"Workspace '{idOrPath}' not found.",
            context);

    // =========================================================================
    // Bad Request (400)
    // =========================================================================

    /// <summary>Creates a validation failed response.</summary>
    public static IResult ValidationFailed(IDictionary<string, string[]> errors, HttpContext context) =>
        Results.Json(
            new ValidationProblemDetails
            {
                Type = ProblemTypes.ValidationFailed,
                Title = "Validation Failed",
                Status = 400,
                Detail = "One or more validation errors occurred.",
                Instance = context.Request.Path,
                TraceId = GetTraceId(context),
                Errors = errors,
            },
            contentType: ContentType,
            statusCode: 400);

    /// <summary>Creates a bad request response for invalid input.</summary>
    public static IResult BadRequest(string detail, HttpContext context, string? type = null) =>
        CreateProblem(
            type ?? ProblemTypes.InvalidRequest,
            "Bad Request",
            400,
            detail,
            context);

    /// <summary>Creates a response for invalid status value.</summary>
    public static IResult InvalidStatus(string status, HttpContext context) =>
        CreateProblem(
            ProblemTypes.InvalidStatus,
            "Invalid Status",
            400,
            $"Invalid status: {status}",
            context);

    /// <summary>Creates a response for missing required field.</summary>
    public static IResult MissingRequiredField(string fieldName, string? hint, HttpContext context)
    {
        var detail = hint is not null
            ? $"{fieldName} is required. {hint}"
            : $"{fieldName} is required.";

        return CreateProblem(
            ProblemTypes.ValidationFailed,
            "Missing Required Field",
            400,
            detail,
            context);
    }

    // =========================================================================
    // Conflict (409)
    // =========================================================================

    /// <summary>Creates an invalid state response.</summary>
    public static IResult InvalidState(string detail, HttpContext context) =>
        CreateProblem(
            ProblemTypes.InvalidState,
            "Invalid State",
            409,
            detail,
            context);

    /// <summary>Creates a response when story is not linked to an issue.</summary>
    public static IResult NotLinkedToIssue(HttpContext context) =>
        CreateProblem(
            ProblemTypes.NotLinkedToIssue,
            "Not Linked to Issue",
            409,
            "Story is not linked to a GitHub issue.",
            context);

    // =========================================================================
    // External Service Errors (502)
    // =========================================================================

    /// <summary>Creates a GitHub API error response.</summary>
    public static IResult GitHubError(string detail, HttpContext context) =>
        CreateProblem(
            ProblemTypes.GitHubError,
            "GitHub API Error",
            502,
            detail,
            context);

    /// <summary>Creates a response when GitHub is not configured.</summary>
    public static IResult GitHubNotConfigured(HttpContext context) =>
        CreateProblem(
            ProblemTypes.GitHubNotConfigured,
            "GitHub Not Configured",
            400,
            "GitHub integration not configured. Set GitHub:Token in appsettings.json.",
            context);

    /// <summary>Creates a Git operation failed response.</summary>
    public static IResult GitOperationFailed(string operation, string detail, HttpContext context) =>
        CreateProblem(
            ProblemTypes.GitOperationFailed,
            $"Git {operation} Failed",
            502,
            detail,
            context);

    /// <summary>Creates an LLM provider error response.</summary>
    public static IResult LlmProviderError(string detail, HttpContext context) =>
        CreateProblem(
            ProblemTypes.LlmProviderError,
            "LLM Provider Error",
            502,
            detail,
            context);

    // =========================================================================
    // Internal Server Error (500)
    // =========================================================================

    /// <summary>Creates an internal server error response.</summary>
    public static IResult InternalError(string detail, HttpContext context) =>
        CreateProblem(
            ProblemTypes.InternalError,
            "Internal Server Error",
            500,
            detail,
            context);

    // =========================================================================
    // Helper Methods
    // =========================================================================

    private static IResult CreateProblem(
        string type,
        string title,
        int status,
        string detail,
        HttpContext context) =>
        Results.Json(
            new ProblemDetails
            {
                Type = type,
                Title = title,
                Status = status,
                Detail = detail,
                Instance = context.Request.Path,
                TraceId = GetTraceId(context),
            },
            contentType: ContentType,
            statusCode: status);

    private static string? GetTraceId(HttpContext context) =>
        Activity.Current?.Id ?? context.TraceIdentifier;
}
