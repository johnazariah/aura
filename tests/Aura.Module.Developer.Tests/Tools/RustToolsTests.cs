// <copyright file="RustToolsTests.cs" company="Aura">
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
/// Tests for Rust shell-based tools.
/// </summary>
public class RustToolsTests
{
    private readonly IProcessRunner _processRunner = Substitute.For<IProcessRunner>();
    private readonly IToolRegistry _registry = Substitute.For<IToolRegistry>();

    public RustToolsTests()
    {
        RustTools.RegisterRustTools(_registry, _processRunner, NullLogger.Instance);
    }

    [Fact]
    public void RegisterRustTools_ShouldRegisterAllRustTools()
    {
        // Verify all expected tools were registered
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t => t.ToolId == "rust.build"));
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t => t.ToolId == "rust.test"));
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t => t.ToolId == "rust.check"));
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t => t.ToolId == "rust.clippy"));
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t => t.ToolId == "rust.fmt"));
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t => t.ToolId == "rust.run"));
    }

    [Fact]
    public void RegisterRustTools_ShouldRegisterExactlySixTools()
    {
        _registry.Received(6).RegisterTool(Arg.Any<ToolDefinition>());
    }

    [Fact]
    public void AllTools_ShouldHaveDescriptions()
    {
        _registry.Received(6).RegisterTool(Arg.Is<ToolDefinition>(t => !string.IsNullOrEmpty(t.Description)));
    }

    [Fact]
    public void AllTools_ShouldHaveRustCategory()
    {
        _registry.Received(6).RegisterTool(Arg.Is<ToolDefinition>(t => t.Categories.Contains("rust")));
    }

    [Fact]
    public void BuildTool_ShouldNotRequireConfirmation()
    {
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t =>
            t.ToolId == "rust.build" && t.RequiresConfirmation == false));
    }

    [Fact]
    public void CheckTool_ShouldNotRequireConfirmation()
    {
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t =>
            t.ToolId == "rust.check" && t.RequiresConfirmation == false));
    }

    [Fact]
    public void FmtTool_ShouldRequireConfirmation()
    {
        // Formatting modifies files, so should require confirmation
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t =>
            t.ToolId == "rust.fmt" && t.RequiresConfirmation == true));
    }

    [Fact]
    public void RunTool_ShouldRequireConfirmation()
    {
        // Running arbitrary code should require confirmation
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t =>
            t.ToolId == "rust.run" && t.RequiresConfirmation == true));
    }

    [Fact]
    public void ClippyTool_ShouldHaveLintingCategory()
    {
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t =>
            t.ToolId == "rust.clippy" && t.Categories.Contains("linting")));
    }

    [Fact]
    public void TestTool_ShouldHaveTestingCategory()
    {
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t =>
            t.ToolId == "rust.test" && t.Categories.Contains("testing")));
    }

    [Fact]
    public void BuildTool_ShouldHaveCompilationCategory()
    {
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t =>
            t.ToolId == "rust.build" && t.Categories.Contains("compilation")));
    }

    [Fact]
    public void FmtTool_ShouldHaveFormattingCategory()
    {
        _registry.Received(1).RegisterTool(Arg.Is<ToolDefinition>(t =>
            t.ToolId == "rust.fmt" && t.Categories.Contains("formatting")));
    }
}
