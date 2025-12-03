// <copyright file="TypeScriptToolsTests.cs" company="Aura">
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
/// Tests for TypeScript shell-based tools.
/// </summary>
public class TypeScriptToolsTests
{
    private readonly IProcessRunner _processRunner = Substitute.For<IProcessRunner>();
    private readonly IToolRegistry _registry = Substitute.For<IToolRegistry>();

    public TypeScriptToolsTests()
    {
        TypeScriptTools.RegisterTypeScriptTools(_registry, _processRunner, NullLogger.Instance);
    }

    [Fact]
    public void RegisterTypeScriptTools_ShouldRegisterAllTypeScriptTools()
    {
        // Verify all expected tools were registered
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t => t.ToolId == "typescript.compile"));
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t => t.ToolId == "typescript.type_check"));
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t => t.ToolId == "typescript.run_tests"));
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t => t.ToolId == "typescript.lint"));
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t => t.ToolId == "typescript.format"));
    }

    [Fact]
    public void RegisterTypeScriptTools_ShouldRegisterExactlyFiveTools()
    {
        _registry.Received(5).RegisterTool(Arg.Any<ToolDefinition>());
    }

    [Fact]
    public void AllTools_ShouldHaveDescriptions()
    {
        _registry.Received(5).RegisterTool(Arg.Is<ToolDefinition>(t => !string.IsNullOrEmpty(t.Description)));
    }

    [Fact]
    public void AllTools_ShouldHaveTypeScriptCategory()
    {
        _registry.Received(5).RegisterTool(Arg.Is<ToolDefinition>(t => t.Categories.Contains("typescript")));
    }

    [Fact]
    public void CompileTool_ShouldNotRequireConfirmation()
    {
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t =>
            t.ToolId == "typescript.compile" && t.RequiresConfirmation == false));
    }

    [Fact]
    public void FormatTool_ShouldRequireConfirmation()
    {
        // Formatting modifies files, so should require confirmation
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t =>
            t.ToolId == "typescript.format" && t.RequiresConfirmation == true));
    }

    [Fact]
    public void LintTool_ShouldHaveLintingCategory()
    {
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t =>
            t.ToolId == "typescript.lint" && t.Categories.Contains("linting")));
    }

    [Fact]
    public void TestTool_ShouldHaveTestingCategory()
    {
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t =>
            t.ToolId == "typescript.run_tests" && t.Categories.Contains("testing")));
    }

    [Fact]
    public void TypeCheckTool_ShouldHaveTypeScriptCategory()
    {
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t =>
            t.ToolId == "typescript.type_check" && t.Categories.Contains("typescript")));
    }
}
