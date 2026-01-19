// <copyright file="IProjectVerificationDetector.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services.Verification;

/// <summary>
/// Detects project types and their verification requirements.
/// </summary>
public interface IProjectVerificationDetector
{
    /// <summary>
    /// Detects all projects in a directory and their verification requirements.
    /// </summary>
    /// <param name="directoryPath">The directory to scan.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of detected projects with their verification requirements.</returns>
    Task<IReadOnlyList<DetectedProject>> DetectProjectsAsync(
        string directoryPath,
        CancellationToken ct = default);
}

/// <summary>
/// Represents a detected project with its verification requirements.
/// </summary>
public sealed record DetectedProject
{
    /// <summary>Gets the project type (e.g., "dotnet", "npm", "cargo", "go", "python").</summary>
    public required string ProjectType { get; init; }

    /// <summary>Gets the project path (e.g., path to .csproj, package.json, Cargo.toml).</summary>
    public required string ProjectPath { get; init; }

    /// <summary>Gets the project name.</summary>
    public required string ProjectName { get; init; }

    /// <summary>Gets the verification steps available for this project.</summary>
    public required IReadOnlyList<VerificationStep> VerificationSteps { get; init; }
}

/// <summary>
/// Represents a single verification step that can be run.
/// </summary>
public sealed record VerificationStep
{
    /// <summary>Gets the step type (e.g., "build", "format", "lint", "test").</summary>
    public required string StepType { get; init; }

    /// <summary>Gets the command to run.</summary>
    public required string Command { get; init; }

    /// <summary>Gets the arguments to pass to the command.</summary>
    public required IReadOnlyList<string> Arguments { get; init; }

    /// <summary>Gets the working directory for the command.</summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>Gets whether this step is required (blocking) or optional (warning only).</summary>
    public bool Required { get; init; } = true;

    /// <summary>Gets the timeout in seconds for this step.</summary>
    public int TimeoutSeconds { get; init; } = 120;

    /// <summary>Gets a human-readable description of this step.</summary>
    public string? Description { get; init; }
}
