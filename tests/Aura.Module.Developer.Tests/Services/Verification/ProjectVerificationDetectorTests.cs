// <copyright file="ProjectVerificationDetectorTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Tests.Services.Verification;

using System.IO.Abstractions.TestingHelpers;
using Aura.Module.Developer.Services.Verification;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class ProjectVerificationDetectorTests
{
    private readonly MockFileSystem _fileSystem = new();
    private readonly ProjectVerificationDetector _detector;

    public ProjectVerificationDetectorTests()
    {
        _detector = new ProjectVerificationDetector(
            _fileSystem,
            NullLogger<ProjectVerificationDetector>.Instance);
    }

    [Fact]
    public async Task DetectProjectsAsync_EmptyDirectory_ReturnsEmpty()
    {
        // Arrange
        _fileSystem.AddDirectory(@"C:\project");

        // Act
        var projects = await _detector.DetectProjectsAsync(@"C:\project");

        // Assert
        Assert.Empty(projects);
    }

    [Fact]
    public async Task DetectProjectsAsync_NonexistentDirectory_ReturnsEmpty()
    {
        // Act
        var projects = await _detector.DetectProjectsAsync(@"C:\nonexistent");

        // Assert
        Assert.Empty(projects);
    }

    [Fact]
    public async Task DetectProjectsAsync_DotNetSolution_DetectsBuildAndFormat()
    {
        // Arrange
        _fileSystem.AddDirectory(@"C:\project");
        _fileSystem.AddFile(@"C:\project\MyApp.sln", new MockFileData(""));

        // Act
        var projects = await _detector.DetectProjectsAsync(@"C:\project");

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
        _fileSystem.AddDirectory(@"C:\project\src\MyLib");
        _fileSystem.AddFile(@"C:\project\src\MyLib\MyLib.csproj", new MockFileData(""));

        // Act
        var projects = await _detector.DetectProjectsAsync(@"C:\project");

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
        _fileSystem.AddDirectory(@"C:\project");
        _fileSystem.AddFile(@"C:\project\package.json", new MockFileData("""
        {
            "name": "my-app",
            "scripts": {
                "build": "tsc",
                "lint": "eslint ."
            }
        }
        """));

        // Act
        var projects = await _detector.DetectProjectsAsync(@"C:\project");

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
        _fileSystem.AddDirectory(@"C:\project");
        _fileSystem.AddFile(@"C:\project\package.json", new MockFileData("""
        {
            "scripts": { "build": "tsc" }
        }
        """));
        _fileSystem.AddFile(@"C:\project\yarn.lock", new MockFileData(""));

        // Act
        var projects = await _detector.DetectProjectsAsync(@"C:\project");

        // Assert
        var project = Assert.Single(projects);
        var buildStep = project.VerificationSteps.First(s => s.StepType == "build");
        Assert.Equal("yarn", buildStep.Command);
    }

    [Fact]
    public async Task DetectProjectsAsync_Cargo_DetectsCheckFormatClippy()
    {
        // Arrange
        _fileSystem.AddDirectory(@"C:\project");
        _fileSystem.AddFile(@"C:\project\Cargo.toml", new MockFileData(""));

        // Act
        var projects = await _detector.DetectProjectsAsync(@"C:\project");

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
        _fileSystem.AddDirectory(@"C:\project");
        _fileSystem.AddFile(@"C:\project\go.mod", new MockFileData("module example.com/myapp"));

        // Act
        var projects = await _detector.DetectProjectsAsync(@"C:\project");

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
        _fileSystem.AddDirectory(@"C:\project");
        _fileSystem.AddFile(@"C:\project\pyproject.toml", new MockFileData("""
        [tool.ruff]
        line-length = 88
        """));

        // Act
        var projects = await _detector.DetectProjectsAsync(@"C:\project");

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
        _fileSystem.AddDirectory(@"C:\project");
        _fileSystem.AddFile(@"C:\project\MyApp.sln", new MockFileData(""));
        _fileSystem.AddDirectory(@"C:\project\frontend");
        _fileSystem.AddFile(@"C:\project\frontend\package.json", new MockFileData("""
        { "scripts": { "build": "vite build" } }
        """));

        // Act
        var projects = await _detector.DetectProjectsAsync(@"C:\project");

        // Assert
        Assert.Equal(2, projects.Count);
        Assert.Contains(projects, p => p.ProjectType == "dotnet");
        Assert.Contains(projects, p => p.ProjectType == "npm");
    }

    [Fact]
    public async Task DetectProjectsAsync_SkipsNodeModules()
    {
        // Arrange
        _fileSystem.AddDirectory(@"C:\project");
        _fileSystem.AddFile(@"C:\project\package.json", new MockFileData("""
        { "scripts": { "build": "tsc" } }
        """));
        _fileSystem.AddDirectory(@"C:\project\node_modules\some-dep");
        _fileSystem.AddFile(@"C:\project\node_modules\some-dep\package.json", new MockFileData("""
        { "scripts": { "build": "tsc" } }
        """));

        // Act
        var projects = await _detector.DetectProjectsAsync(@"C:\project");

        // Assert
        Assert.Single(projects);
    }

    [Fact]
    public async Task DetectedProject_VerificationStep_HasCorrectDefaults()
    {
        // Arrange
        _fileSystem.AddDirectory(@"C:\project");
        _fileSystem.AddFile(@"C:\project\MyApp.sln", new MockFileData(""));

        // Act
        var projects = await _detector.DetectProjectsAsync(@"C:\project");

        // Assert
        var buildStep = projects[0].VerificationSteps.First(s => s.StepType == "build");
        Assert.True(buildStep.Required);
        Assert.True(buildStep.TimeoutSeconds > 0);
        Assert.NotEmpty(buildStep.WorkingDirectory);
    }
}
