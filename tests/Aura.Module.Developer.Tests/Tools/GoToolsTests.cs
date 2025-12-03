// <copyright file="GoToolsTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Tests.Tools;

using Aura.Foundation.Shell;
using Aura.Foundation.Tools;
using Aura.Module.Developer.Tools;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

/// <summary>
/// Tests for Go shell-based tools.
/// </summary>
public class GoToolsTests
{
    private readonly IProcessRunner _processRunner = Substitute.For<IProcessRunner>();
    private readonly IToolRegistry _registry = Substitute.For<IToolRegistry>();

    public GoToolsTests()
    {
        GoTools.RegisterGoTools(_registry, _processRunner, NullLogger.Instance);
    }

    [Fact]
    public void RegisterGoTools_ShouldRegisterAllGoTools()
    {
        // Verify all expected tools were registered
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t => t.ToolId == "go.build"));
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t => t.ToolId == "go.test"));
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t => t.ToolId == "go.vet"));
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t => t.ToolId == "go.fmt"));
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t => t.ToolId == "go.mod_tidy"));
    }

    [Fact]
    public void RegisterGoTools_ShouldRegisterExactlyFiveTools()
    {
        _registry.Received(5).RegisterTool(Arg.Any<ToolDefinition>());
    }

    [Fact]
    public void AllTools_ShouldHaveDescriptions()
    {
        _registry.Received(5).RegisterTool(Arg.Is<ToolDefinition>(t => !string.IsNullOrEmpty(t.Description)));
    }

    [Fact]
    public void AllTools_ShouldHaveGoCategory()
    {
        _registry.Received(5).RegisterTool(Arg.Is<ToolDefinition>(t => t.Categories.Contains("go")));
    }

    [Fact]
    public void BuildTool_ShouldNotRequireConfirmation()
    {
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t =>
            t.ToolId == "go.build" && t.RequiresConfirmation == false));
    }

    [Fact]
    public void FmtTool_ShouldRequireConfirmation()
    {
        // Formatting modifies files, so should require confirmation
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t =>
            t.ToolId == "go.fmt" && t.RequiresConfirmation == true));
    }

    [Fact]
    public void VetTool_ShouldHaveLintingCategory()
    {
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t =>
            t.ToolId == "go.vet" && t.Categories.Contains("linting")));
    }

    [Fact]
    public void TestTool_ShouldHaveTestingCategory()
    {
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t =>
            t.ToolId == "go.test" && t.Categories.Contains("testing")));
    }

    [Fact]
    public void ModTidyTool_ShouldHaveDependenciesCategory()
    {
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t =>
            t.ToolId == "go.mod_tidy" && t.Categories.Contains("dependencies")));
    }
}
