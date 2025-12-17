// <copyright file="CodeGraphEnricherTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Tests.Rag;

using Aura.Foundation.Data.Entities;
using Aura.Foundation.Rag;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

public class CodeGraphEnricherTests
{
    private readonly ICodeGraphService _mockGraphService;
    private readonly CodeGraphEnricher _enricher;

    public CodeGraphEnricherTests()
    {
        _mockGraphService = Substitute.For<ICodeGraphService>();
        _enricher = new CodeGraphEnricher(
            _mockGraphService,
            NullLogger<CodeGraphEnricher>.Instance);
    }

    [Fact]
    public async Task EnrichAsync_WithNoSymbols_ReturnsEmptyResult()
    {
        // Arrange
        var prompt = "what is the weather today?"; // No PascalCase symbols

        // Act
        var result = await _enricher.EnrichAsync(prompt);

        // Assert
        result.FormattedContext.Should().BeEmpty();
        result.Nodes.Should().BeEmpty();
        result.Edges.Should().BeEmpty();
    }

    [Fact]
    public async Task EnrichAsync_WithInterfaceName_QueriesImplementations()
    {
        // Arrange
        var prompt = "How is IWorkflowService implemented?";
        var interfaceNode = CreateNode("IWorkflowService", CodeNodeType.Interface);
        var implNode = CreateNode("WorkflowService", CodeNodeType.Class);

        _mockGraphService
            .FindNodesAsync("IWorkflowService", null, null, Arg.Any<CancellationToken>())
            .Returns(new[] { interfaceNode });

        _mockGraphService
            .FindImplementationsAsync("IWorkflowService", null, Arg.Any<CancellationToken>())
            .Returns(new[] { implNode });

        // Act
        var result = await _enricher.EnrichAsync(prompt);

        // Assert
        result.Nodes.Should().Contain(n => n.Name == "IWorkflowService");
        result.Nodes.Should().Contain(n => n.Name == "WorkflowService");
        result.FormattedContext.Should().Contain("IWorkflowService");
        result.FormattedContext.Should().Contain("WorkflowService");
    }

    [Fact]
    public async Task EnrichAsync_WithClassName_QueriesTypeMembers()
    {
        // Arrange
        var prompt = "What methods does WorkflowExecutor have?";
        var classNode = CreateNode("WorkflowExecutor", CodeNodeType.Class);
        var method1 = CreateNode("ExecuteAsync", CodeNodeType.Method);
        var method2 = CreateNode("ValidateStep", CodeNodeType.Method);

        _mockGraphService
            .FindNodesAsync("WorkflowExecutor", null, null, Arg.Any<CancellationToken>())
            .Returns(new[] { classNode });

        _mockGraphService
            .GetTypeMembersAsync("WorkflowExecutor", null, Arg.Any<CancellationToken>())
            .Returns(new[] { method1, method2 });

        // Act
        var result = await _enricher.EnrichAsync(prompt);

        // Assert
        result.Nodes.Should().Contain(n => n.Name == "WorkflowExecutor");
        result.Nodes.Should().Contain(n => n.Name == "ExecuteAsync");
        result.Nodes.Should().Contain(n => n.Name == "ValidateStep");
    }

    [Fact]
    public async Task EnrichAsync_ExtractsMultipleSymbols()
    {
        // Arrange
        var prompt = "How does WorkflowService use GitService?";

        _mockGraphService
            .FindNodesAsync(Arg.Any<string>(), null, null, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<CodeNode>());

        // Act
        await _enricher.EnrichAsync(prompt);

        // Assert - should query both symbols
        await _mockGraphService.Received(1)
            .FindNodesAsync("WorkflowService", null, null, Arg.Any<CancellationToken>());
        await _mockGraphService.Received(1)
            .FindNodesAsync("GitService", null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnrichAsync_GracefullyHandlesQueryErrors()
    {
        // Arrange
        var prompt = "Tell me about WorkflowService";

        _mockGraphService
            .FindNodesAsync("WorkflowService", null, null, Arg.Any<CancellationToken>())
            .Throws(new Exception("Database error"));

        // Act - should not throw
        var result = await _enricher.EnrichAsync(prompt);

        // Assert - returns empty result instead of throwing
        result.Nodes.Should().BeEmpty();
    }

    private static CodeNode CreateNode(string name, CodeNodeType nodeType) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            NodeType = nodeType,
            FilePath = $"src/{name}.cs",
            LineNumber = 10,
        };
}
