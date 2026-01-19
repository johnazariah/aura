// <copyright file="ProjectVerificationDetectorTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Tests.Services.Verification;

using System.IO.Abstractions.TestingHelpers;
using System.Runtime.InteropServices;
using Aura.Module.Developer.Services.Verification;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class ProjectVerificationDetectorTests
{
    private readonly MockFileSystem _fileSystem;
    private readonly ProjectVerificationDetector _detector;
    private readonly string _testRoot;

    public ProjectVerificationDetectorTests()
    {
        // Use platform-appropriate paths
        _testRoot = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? @"C:\project"
            : "/tmp/project";

        _fileSystem = new MockFileSystem();
        _detector = new ProjectVerificationDetector(
            _fileSystem,
            NullLogger<ProjectVerificationDetector>.Instance);
    }

    private string P(string relativePath) =>
        Path.Combine(_testRoot, relativePath.Replace('\\', Path.DirectorySeparatorChar));

    [Fact]
    public async Task DetectProjectsAsync_EmptyDirectory_ReturnsEmpty()
    {
        // Arrange
        _fileSystem.AddDirectory(_testRoot);

        // Act
        var projects = await _detector.DetectProjectsAsync(_testRoot);

        // Assert
        Assert.Empty(projects);
    }

    [Fact]
    public async Task DetectProjectsAsync_NonexistentDirectory_ReturnsEmpty()
    {
        // Act
        var projects = await _detector.DetectProjectsAsync(P("nonexistent"));

        // Assert
        Assert.Empty(projects);
    }

    [Fact]
    public async Task DetectProjectsAsync_DotNetSolution_DetectsBuildAndFormat()
    {
        // Arrange
        _fileSystem.AddDirectory(_testRoot);
        _fileSystem.AddFile(P("MyApp.sln"), new MockFileData(""));

        // Act
        var projects = await _detector.DetectProjectsAsync(_testRoot);

        // Assert
        var project = Assert.Single(projects);
        Assert.Equal("dotnet", project.ProjectType);
        Assert.Equal("MyApp", project.ProjectName);
        Assert.Equal(2, project.VerificationSteps.Count);
        Assert.Contains(project.VerificationSteps, s => s.StepType == "build");
        Assert.Contains(project.VerificationSteps, s => s.StepType == "format");
    }

    [Fact]
    public async Task DetectProjectsAsync_DotNetCsproj_DetectsBuild()
    {
        // Arrange
        _fileSystem.AddDirectory(P("src/MyLib"));
        _fileSystem.AddFile(P("src/MyLib/MyLib.csproj"), new MockFileData(""));

        // Act
        var projects = await _detector.DetectProjectsAsync(_testRoot);

        // Assert
        var project = Assert.Single(projects);
        Assert.Equal("dotnet", project.ProjectType);
        Assert.Equal("MyLib", project.ProjectName);
        Assert.Single(project.VerificationSteps);
        Assert.Equal("build", project.VerificationSteps[0].StepType);
    }

    [Fact]
    public async Task DetectProjectsAsync_NpmWithBuildAndLint_DetectsBoth()
    {
        // Arrange
        _fileSystem.AddDirectory(_testRoot);
        _fileSystem.AddFile(P("package.json"), new MockFileData("""
        {
            "name": "my-app",
            "scripts": {
                "build": "tsc",
                "lint": "eslint ."
            }
        }
        """));

        // Act
        var projects = await _detector.DetectProjectsAsync(_testRoot);

        // Assert
        var project = Assert.Single(projects);
        Assert.Equal("npm", project.ProjectType);
        Assert.Equal("project", project.ProjectName);
        Assert.Contains(project.VerificationSteps, s => s.StepType == "build");
        Assert.Contains(project.VerificationSteps, s => s.StepType == "lint");
    }

    [Fact]
    public async Task DetectProjectsAsync_NpmWithYarnLock_UsesYarn()
    {
        // Arrange
        _fileSystem.AddDirectory(_testRoot);
        _fileSystem.AddFile(P("package.json"), new MockFileData("""
        {
            "scripts": { "build": "tsc" }
        }
        """));
        _fileSystem.AddFile(P("yarn.lock"), new MockFileData(""));

        // Act
        var projects = await _detector.DetectProjectsAsync(_testRoot);

        // Assert
        var project = Assert.Single(projects);
        var buildStep = project.VerificationSteps.First(s => s.StepType == "build");
        Assert.Equal("yarn", buildStep.Command);
    }

    [Fact]
    public async Task DetectProjectsAsync_Cargo_DetectsCheckFormatClippy()
    {
        // Arrange
        _fileSystem.AddDirectory(_testRoot);
        _fileSystem.AddFile(P("Cargo.toml"), new MockFileData(""));

        // Act
        var projects = await _detector.DetectProjectsAsync(_testRoot);

        // Assert
        var project = Assert.Single(projects);
        Assert.Equal("cargo", project.ProjectType);
        Assert.Equal(3, project.VerificationSteps.Count);
        Assert.Contains(project.VerificationSteps, s => s.StepType == "build" && s.Command == "cargo");
        Assert.Contains(project.VerificationSteps, s => s.StepType == "format");
        Assert.Contains(project.VerificationSteps, s => s.StepType == "lint");
    }

    [Fact]
    public async Task DetectProjectsAsync_GoMod_DetectsBuildVet()
    {
        // Arrange
        _fileSystem.AddDirectory(_testRoot);
        _fileSystem.AddFile(P("go.mod"), new MockFileData("module example.com/myapp"));

        // Act
        var projects = await _detector.DetectProjectsAsync(_testRoot);

        // Assert
        var project = Assert.Single(projects);
        Assert.Equal("go", project.ProjectType);
        Assert.Equal(3, project.VerificationSteps.Count);
        Assert.Contains(project.VerificationSteps, s => s.StepType == "build" && s.Command == "go");
        Assert.Contains(project.VerificationSteps, s => s.StepType == "format");
        Assert.Contains(project.VerificationSteps, s => s.StepType == "vet");
    }

    [Fact]
    public async Task DetectProjectsAsync_PythonWithRuff_DetectsLintFormat()
    {
        // Arrange
        _fileSystem.AddDirectory(_testRoot);
        _fileSystem.AddFile(P("pyproject.toml"), new MockFileData("""
        [tool.ruff]
        line-length = 88
        """));

        // Act
        var projects = await _detector.DetectProjectsAsync(_testRoot);

        // Assert
        var project = Assert.Single(projects);
        Assert.Equal("python", project.ProjectType);
        Assert.Contains(project.VerificationSteps, s => s.StepType == "lint" && s.Command == "ruff");
        Assert.Contains(project.VerificationSteps, s => s.StepType == "format");
    }

    [Fact]
    public async Task DetectProjectsAsync_MultipleProjectTypes_DetectsAll()
    {
        // Arrange
        _fileSystem.AddDirectory(_testRoot);
        _fileSystem.AddFile(P("MyApp.sln"), new MockFileData(""));
        _fileSystem.AddDirectory(P("frontend"));
        _fileSystem.AddFile(P("frontend/package.json"), new MockFileData("""
        { "scripts": { "build": "vite build" } }
        """));

        // Act
        var projects = await _detector.DetectProjectsAsync(_testRoot);

        // Assert
        Assert.Equal(2, projects.Count);
        Assert.Contains(projects, p => p.ProjectType == "dotnet");
        Assert.Contains(projects, p => p.ProjectType == "npm");
    }

    [Fact]
    public async Task DetectProjectsAsync_SkipsNodeModules()
    {
        // Arrange
        _fileSystem.AddDirectory(_testRoot);
        _fileSystem.AddFile(P("package.json"), new MockFileData("""
        { "scripts": { "build": "tsc" } }
        """));
        _fileSystem.AddDirectory(P("node_modules/some-dep"));
        _fileSystem.AddFile(P("node_modules/some-dep/package.json"), new MockFileData("""
        { "scripts": { "build": "tsc" } }
        """));

        // Act
        var projects = await _detector.DetectProjectsAsync(_testRoot);

        // Assert
        Assert.Single(projects);
    }

    [Fact]
    public async Task DetectedProject_VerificationStep_HasCorrectDefaults()
    {
        // Arrange
        _fileSystem.AddDirectory(_testRoot);
        _fileSystem.AddFile(P("MyApp.sln"), new MockFileData(""));

        // Act
        var projects = await _detector.DetectProjectsAsync(_testRoot);

        // Assert
        var buildStep = projects[0].VerificationSteps.First(s => s.StepType == "build");
        Assert.True(buildStep.Required);
        Assert.True(buildStep.TimeoutSeconds > 0);
        Assert.NotEmpty(buildStep.WorkingDirectory);
    }
}
