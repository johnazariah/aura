// <copyright file="PythonToolsTests.cs" company="Aura">
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
/// Tests for Python shell-based tools.
/// </summary>
public class PythonToolsTests
{
    private readonly IProcessRunner _processRunner = Substitute.For<IProcessRunner>();
    private readonly IToolRegistry _registry = Substitute.For<IToolRegistry>();

    public PythonToolsTests()
    {
        PythonTools.RegisterPythonTools(_registry, _processRunner, NullLogger.Instance);
    }

    [Fact]
    public void RegisterPythonTools_ShouldRegisterAllPythonTools()
    {
        // Verify all expected tools were registered
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t => t.ToolId == "python.run_script"));
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t => t.ToolId == "python.run_tests"));
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t => t.ToolId == "python.lint"));
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t => t.ToolId == "python.format"));
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t => t.ToolId == "python.type_check"));
    }

    [Fact]
    public void RegisterPythonTools_ShouldRegisterExactlyFiveTools()
    {
        _registry.Received(5).RegisterTool(Arg.Any<ToolDefinition>());
    }

    [Fact]
    public void AllTools_ShouldHaveDescriptions()
    {
        _registry.Received(5).RegisterTool(Arg.Is<ToolDefinition>(t => !string.IsNullOrEmpty(t.Description)));
    }

    [Fact]
    public void AllTools_ShouldHavePythonCategory()
    {
        _registry.Received(5).RegisterTool(Arg.Is<ToolDefinition>(t => t.Categories.Contains("python")));
    }

    [Fact]
    public void RunScriptTool_ShouldRequireConfirmation()
    {
        // Running arbitrary scripts can be dangerous, so requires confirmation
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t =>
            t.ToolId == "python.run_script" && t.RequiresConfirmation == true));
    }

    [Fact]
    public void FormatTool_ShouldRequireConfirmation()
    {
        // Formatting modifies files, so should require confirmation
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t =>
            t.ToolId == "python.format" && t.RequiresConfirmation == true));
    }

    [Fact]
    public void LintTool_ShouldHaveLintingCategory()
    {
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t =>
            t.ToolId == "python.lint" && t.Categories.Contains("linting")));
    }

    [Fact]
    public void TestTool_ShouldHaveTestingCategory()
    {
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t =>
            t.ToolId == "python.run_tests" && t.Categories.Contains("testing")));
    }

    [Fact]
    public void TypeCheckTool_ShouldHavePythonCategory()
    {
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t =>
            t.ToolId == "python.type_check" && t.Categories.Contains("python")));
    }
}
