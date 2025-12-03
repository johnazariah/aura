// <copyright file="FSharpToolsTests.cs" company="Aura">
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
/// Tests for F# shell-based tools.
/// </summary>
public class FSharpToolsTests
{
    private readonly IProcessRunner _processRunner = Substitute.For<IProcessRunner>();
    private readonly IToolRegistry _registry = Substitute.For<IToolRegistry>();

    public FSharpToolsTests()
    {
        FSharpTools.RegisterFSharpTools(_registry, _processRunner, NullLogger.Instance);
    }

    [Fact]
    public void RegisterFSharpTools_ShouldRegisterAllFSharpTools()
    {
        // Verify all expected tools were registered
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t => t.ToolId == "fsharp.check_project"));
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t => t.ToolId == "fsharp.build"));
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t => t.ToolId == "fsharp.format"));
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t => t.ToolId == "fsharp.test"));
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t => t.ToolId == "fsharp.fsi"));
    }

    [Fact]
    public void RegisterFSharpTools_ShouldRegisterExactlyFiveTools()
    {
        // Should have registered exactly 5 tools
        _registry.Received(5).RegisterTool(Arg.Any<ToolDefinition>());
    }

    [Fact]
    public void AllTools_ShouldHaveDescriptions()
    {
        // Verify all tools have descriptions
        _registry.Received(5).RegisterTool(Arg.Is<ToolDefinition>(t => !string.IsNullOrEmpty(t.Description)));
    }

    [Fact]
    public void AllTools_ShouldHaveFSharpCategory()
    {
        // Verify all tools have the fsharp category
        _registry.Received(5).RegisterTool(Arg.Is<ToolDefinition>(t => t.Categories.Contains("fsharp")));
    }

    [Fact]
    public void CheckProjectTool_ShouldNotRequireConfirmation()
    {
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t =>
            t.ToolId == "fsharp.check_project" && t.RequiresConfirmation == false));
    }

    [Fact]
    public void FormatTool_ShouldRequireConfirmation()
    {
        // Formatting modifies files, so should require confirmation
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t =>
            t.ToolId == "fsharp.format" && t.RequiresConfirmation == true));
    }

    [Fact]
    public void BuildTool_ShouldHaveCompilationCategory()
    {
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t =>
            t.ToolId == "fsharp.build" && t.Categories.Contains("compilation")));
    }

    [Fact]
    public void TestTool_ShouldHaveTestingCategory()
    {
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t =>
            t.ToolId == "fsharp.test" && t.Categories.Contains("testing")));
    }

    [Fact]
    public void FsiTool_ShouldHaveReplCategory()
    {
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t =>
            t.ToolId == "fsharp.fsi" && t.Categories.Contains("repl")));
    }
}
