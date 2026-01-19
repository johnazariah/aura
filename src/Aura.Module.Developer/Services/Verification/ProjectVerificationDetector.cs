// <copyright file="ProjectVerificationDetector.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

using System.IO.Abstractions;
using Microsoft.Extensions.Logging;

namespace Aura.Module.Developer.Services.Verification;

/// <summary>
/// Detects project types and their verification requirements by scanning for project files.
/// </summary>
public sealed class ProjectVerificationDetector : IProjectVerificationDetector
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<ProjectVerificationDetector> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectVerificationDetector"/> class.
    /// </summary>
    public ProjectVerificationDetector(
        IFileSystem fileSystem,
        ILogger<ProjectVerificationDetector> logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<DetectedProject>> DetectProjectsAsync(
        string directoryPath,
        CancellationToken ct = default)
    {
        var projects = new List<DetectedProject>();

        if (!_fileSystem.Directory.Exists(directoryPath))
        {
            _logger.LogWarning("Directory not found for verification detection: {Path}", directoryPath);
            return Task.FromResult<IReadOnlyList<DetectedProject>>(projects);
        }

        // Detect .NET projects
        projects.AddRange(DetectDotNetProjects(directoryPath));

        // Detect Node.js/npm projects
        projects.AddRange(DetectNpmProjects(directoryPath));

        // Detect Rust/Cargo projects
        projects.AddRange(DetectCargoProjects(directoryPath));

        // Detect Go projects
        projects.AddRange(DetectGoProjects(directoryPath));

        // Detect Python projects
        projects.AddRange(DetectPythonProjects(directoryPath));

        _logger.LogInformation(
            "Detected {ProjectCount} projects in {Path}: {Types}",
            projects.Count,
            directoryPath,
            string.Join(", ", projects.Select(p => $"{p.ProjectType}:{p.ProjectName}")));

        return Task.FromResult<IReadOnlyList<DetectedProject>>(projects);
    }

    private IEnumerable<DetectedProject> DetectDotNetProjects(string directoryPath)
    {
        // Look for .sln files first (solution-level verification)
        var slnFiles = _fileSystem.Directory.GetFiles(directoryPath, "*.sln", SearchOption.TopDirectoryOnly);
        foreach (var slnFile in slnFiles)
        {
            var projectName = _fileSystem.Path.GetFileNameWithoutExtension(slnFile);
            yield return new DetectedProject
            {
                ProjectType = "dotnet",
                ProjectPath = slnFile,
                ProjectName = projectName,
                VerificationSteps =
                [
                    new VerificationStep
                    {
                        StepType = "build",
                        Command = "dotnet",
                        Arguments = ["build", "--no-restore", "-warnaserror"],
                        WorkingDirectory = directoryPath,
                        Description = $"Build solution {projectName}",
                        TimeoutSeconds = 300,
                    },
                    new VerificationStep
                    {
                        StepType = "format",
                        Command = "dotnet",
                        Arguments = ["format", "--verify-no-changes", "--no-restore"],
                        WorkingDirectory = directoryPath,
                        Description = $"Verify formatting for {projectName}",
                        Required = false, // Format issues are warnings
                        TimeoutSeconds = 120,
                    },
                ],
            };
        }

        // If no solution found, look for individual .csproj files
        if (!slnFiles.Any())
        {
            var csprojFiles = _fileSystem.Directory.GetFiles(directoryPath, "*.csproj", SearchOption.AllDirectories)
                .Where(f => !f.Contains("bin") && !f.Contains("obj"))
                .Take(10); // Limit to avoid scanning too many projects

            foreach (var csprojFile in csprojFiles)
            {
                var projectName = _fileSystem.Path.GetFileNameWithoutExtension(csprojFile);
                var projectDir = _fileSystem.Path.GetDirectoryName(csprojFile) ?? directoryPath;
                yield return new DetectedProject
                {
                    ProjectType = "dotnet",
                    ProjectPath = csprojFile,
                    ProjectName = projectName,
                    VerificationSteps =
                    [
                        new VerificationStep
                        {
                            StepType = "build",
                            Command = "dotnet",
                            Arguments = ["build", csprojFile, "--no-restore"],
                            WorkingDirectory = projectDir,
                            Description = $"Build project {projectName}",
                            TimeoutSeconds = 180,
                        },
                    ],
                };
            }
        }
    }

    private IEnumerable<DetectedProject> DetectNpmProjects(string directoryPath)
    {
        var packageJsonFiles = _fileSystem.Directory.GetFiles(directoryPath, "package.json", SearchOption.AllDirectories)
            .Where(f => !f.Contains("node_modules"))
            .Take(5); // Limit for performance

        foreach (var packageJson in packageJsonFiles)
        {
            var projectDir = _fileSystem.Path.GetDirectoryName(packageJson) ?? directoryPath;
            var projectName = _fileSystem.Path.GetFileName(projectDir);
            var steps = new List<VerificationStep>();

            // Check for lock files to determine package manager
            var hasYarnLock = _fileSystem.File.Exists(_fileSystem.Path.Combine(projectDir, "yarn.lock"));
            var hasPnpmLock = _fileSystem.File.Exists(_fileSystem.Path.Combine(projectDir, "pnpm-lock.yaml"));
            var packageManager = hasPnpmLock ? "pnpm" : hasYarnLock ? "yarn" : "npm";

            // Try to read package.json for scripts
            try
            {
                var content = _fileSystem.File.ReadAllText(packageJson);
                if (content.Contains("\"build\""))
                {
                    steps.Add(new VerificationStep
                    {
                        StepType = "build",
                        Command = packageManager,
                        Arguments = ["run", "build"],
                        WorkingDirectory = projectDir,
                        Description = $"Build {projectName}",
                        TimeoutSeconds = 300,
                    });
                }

                if (content.Contains("\"lint\""))
                {
                    steps.Add(new VerificationStep
                    {
                        StepType = "lint",
                        Command = packageManager,
                        Arguments = ["run", "lint"],
                        WorkingDirectory = projectDir,
                        Description = $"Lint {projectName}",
                        Required = false,
                        TimeoutSeconds = 120,
                    });
                }

                if (content.Contains("\"typecheck\"") || content.Contains("\"type-check\""))
                {
                    var scriptName = content.Contains("\"typecheck\"") ? "typecheck" : "type-check";
                    steps.Add(new VerificationStep
                    {
                        StepType = "typecheck",
                        Command = packageManager,
                        Arguments = ["run", scriptName],
                        WorkingDirectory = projectDir,
                        Description = $"Type check {projectName}",
                        TimeoutSeconds = 180,
                    });
                }
            }
            catch
            {
                // If we can't read the package.json, skip script detection
            }

            if (steps.Count > 0)
            {
                yield return new DetectedProject
                {
                    ProjectType = "npm",
                    ProjectPath = packageJson,
                    ProjectName = projectName,
                    VerificationSteps = steps,
                };
            }
        }
    }

    private IEnumerable<DetectedProject> DetectCargoProjects(string directoryPath)
    {
        var cargoFiles = _fileSystem.Directory.GetFiles(directoryPath, "Cargo.toml", SearchOption.AllDirectories)
            .Where(f => !f.Contains("target"))
            .Take(5);

        foreach (var cargoToml in cargoFiles)
        {
            var projectDir = _fileSystem.Path.GetDirectoryName(cargoToml) ?? directoryPath;
            var projectName = _fileSystem.Path.GetFileName(projectDir);

            yield return new DetectedProject
            {
                ProjectType = "cargo",
                ProjectPath = cargoToml,
                ProjectName = projectName,
                VerificationSteps =
                [
                    new VerificationStep
                    {
                        StepType = "build",
                        Command = "cargo",
                        Arguments = ["check"],
                        WorkingDirectory = projectDir,
                        Description = $"Check {projectName}",
                        TimeoutSeconds = 300,
                    },
                    new VerificationStep
                    {
                        StepType = "format",
                        Command = "cargo",
                        Arguments = ["fmt", "--check"],
                        WorkingDirectory = projectDir,
                        Description = $"Verify formatting for {projectName}",
                        Required = false,
                        TimeoutSeconds = 60,
                    },
                    new VerificationStep
                    {
                        StepType = "lint",
                        Command = "cargo",
                        Arguments = ["clippy", "--", "-D", "warnings"],
                        WorkingDirectory = projectDir,
                        Description = $"Lint {projectName}",
                        Required = false,
                        TimeoutSeconds = 180,
                    },
                ],
            };
        }
    }

    private IEnumerable<DetectedProject> DetectGoProjects(string directoryPath)
    {
        var goModFiles = _fileSystem.Directory.GetFiles(directoryPath, "go.mod", SearchOption.AllDirectories)
            .Take(5);

        foreach (var goMod in goModFiles)
        {
            var projectDir = _fileSystem.Path.GetDirectoryName(goMod) ?? directoryPath;
            var projectName = _fileSystem.Path.GetFileName(projectDir);

            yield return new DetectedProject
            {
                ProjectType = "go",
                ProjectPath = goMod,
                ProjectName = projectName,
                VerificationSteps =
                [
                    new VerificationStep
                    {
                        StepType = "build",
                        Command = "go",
                        Arguments = ["build", "./..."],
                        WorkingDirectory = projectDir,
                        Description = $"Build {projectName}",
                        TimeoutSeconds = 180,
                    },
                    new VerificationStep
                    {
                        StepType = "format",
                        Command = "gofmt",
                        Arguments = ["-l", "-d", "."],
                        WorkingDirectory = projectDir,
                        Description = $"Check formatting for {projectName}",
                        Required = false,
                        TimeoutSeconds = 60,
                    },
                    new VerificationStep
                    {
                        StepType = "vet",
                        Command = "go",
                        Arguments = ["vet", "./..."],
                        WorkingDirectory = projectDir,
                        Description = $"Vet {projectName}",
                        TimeoutSeconds = 120,
                    },
                ],
            };
        }
    }

    private IEnumerable<DetectedProject> DetectPythonProjects(string directoryPath)
    {
        // Look for pyproject.toml, setup.py, or requirements.txt
        var pyprojectFiles = _fileSystem.Directory.GetFiles(directoryPath, "pyproject.toml", SearchOption.AllDirectories)
            .Where(f => !f.Contains(".venv") && !f.Contains("venv"))
            .Take(3);

        foreach (var pyproject in pyprojectFiles)
        {
            var projectDir = _fileSystem.Path.GetDirectoryName(pyproject) ?? directoryPath;
            var projectName = _fileSystem.Path.GetFileName(projectDir);
            var steps = new List<VerificationStep>();

            // Check for ruff (preferred modern linter)
            var hasRuff = _fileSystem.File.ReadAllText(pyproject).Contains("ruff");

            if (hasRuff)
            {
                steps.Add(new VerificationStep
                {
                    StepType = "lint",
                    Command = "ruff",
                    Arguments = ["check", "."],
                    WorkingDirectory = projectDir,
                    Description = $"Lint {projectName} with ruff",
                    TimeoutSeconds = 60,
                });
                steps.Add(new VerificationStep
                {
                    StepType = "format",
                    Command = "ruff",
                    Arguments = ["format", "--check", "."],
                    WorkingDirectory = projectDir,
                    Description = $"Check formatting for {projectName}",
                    Required = false,
                    TimeoutSeconds = 60,
                });
            }

            // Check for mypy
            if (_fileSystem.File.ReadAllText(pyproject).Contains("mypy"))
            {
                steps.Add(new VerificationStep
                {
                    StepType = "typecheck",
                    Command = "mypy",
                    Arguments = ["."],
                    WorkingDirectory = projectDir,
                    Description = $"Type check {projectName}",
                    Required = false,
                    TimeoutSeconds = 180,
                });
            }

            if (steps.Count > 0)
            {
                yield return new DetectedProject
                {
                    ProjectType = "python",
                    ProjectPath = pyproject,
                    ProjectName = projectName,
                    VerificationSteps = steps,
                };
            }
        }
    }
}
