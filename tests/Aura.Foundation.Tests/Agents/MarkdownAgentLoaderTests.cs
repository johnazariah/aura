// <copyright file="MarkdownAgentLoaderTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Tests.Agents;

using System.IO.Abstractions.TestingHelpers;
using Aura.Foundation.Agents;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

/// <summary>
/// Tests for <see cref="MarkdownAgentLoader"/>.
/// </summary>
public class MarkdownAgentLoaderTests
{
    private readonly MockFileSystem _fileSystem;
    private readonly IAgentFactory _agentFactory;
    private readonly ILogger<MarkdownAgentLoader> _logger;
    private readonly MarkdownAgentLoader _loader;

    public MarkdownAgentLoaderTests()
    {
        _fileSystem = new MockFileSystem();
        _agentFactory = Substitute.For<IAgentFactory>();
        _logger = Substitute.For<ILogger<MarkdownAgentLoader>>();
        _loader = new MarkdownAgentLoader(_fileSystem, _agentFactory, _logger);

        // Setup factory to return a mock agent when called
        _agentFactory.CreateAgent(Arg.Any<AgentDefinition>())
            .Returns(callInfo =>
            {
                var def = callInfo.Arg<AgentDefinition>();
                var agent = Substitute.For<IAgent>();
                agent.AgentId.Returns(def.AgentId);
                agent.Metadata.Returns(def.ToMetadata());
                return agent;
            });
    }

    [Fact]
    public void Parse_ExtractsMetadataCorrectly()
    {
        // Arrange
        var content = """
            # Test Agent

            ## Metadata

            - **Type**: Coder
            - **Name**: Test Agent
            - **Version**: 1.0.0
            - **Provider**: ollama
            - **Model**: qwen2.5-coder:7b
            - **Temperature**: 0.5
            - **Description**: A test agent for unit testing.

            ## Capabilities

            - coding
            - testing

            ## System Prompt

            You are a test agent.
            """;

        // Act
        var definition = _loader.Parse("test-agent", content);

        // Assert
        definition.Should().NotBeNull();
        definition!.AgentId.Should().Be("test-agent");
        definition.Name.Should().Be("Test Agent");
        definition.Description.Should().Be("A test agent for unit testing.");
        definition.Provider.Should().Be("ollama");
        definition.Model.Should().Be("qwen2.5-coder:7b");
        definition.Temperature.Should().Be(0.5);
    }

    [Fact]
    public void Parse_ExtractsCapabilities()
    {
        // Arrange
        var content = """
            # Test Agent

            ## Metadata

            - **Name**: Test Agent
            - **Description**: Test

            ## Capabilities

            - coding
            - code-generation
            - python
            - testing

            ## System Prompt

            You are a test agent.
            """;

        // Act
        var definition = _loader.Parse("test-agent", content);

        // Assert
        definition.Should().NotBeNull();
        definition!.Capabilities.Should().HaveCount(4);
        definition.Capabilities.Should().Contain("coding");
        definition.Capabilities.Should().Contain("code-generation");
        definition.Capabilities.Should().Contain("python");
        definition.Capabilities.Should().Contain("testing");
    }

    [Fact]
    public void Parse_ExtractsToolNames()
    {
        // Arrange
        var content = """
            # Test Agent

            ## Metadata

            - **Name**: Test Agent
            - **Description**: Test

            ## Tools Available

            **validate_code(files: string[], language: string)**
            - Validates code files

            **run_tests(testFiles: string[])**
            - Runs test files

            ## System Prompt

            You are a test agent.
            """;

        // Act
        var definition = _loader.Parse("test-agent", content);

        // Assert
        definition.Should().NotBeNull();
        definition!.Tools.Should().HaveCount(2);
        definition.Tools.Should().Contain("validate_code");
        definition.Tools.Should().Contain("run_tests");
    }

    [Fact]
    public void Parse_ExtractsSystemPrompt()
    {
        // Arrange
        var content = """
            # Test Agent

            ## Metadata

            - **Name**: Test Agent
            - **Description**: Test

            ## System Prompt

            You are an expert developer.
            Work Item: {{context.WorkItemTitle}}
            Workspace: {{context.Data.WorkspacePath}}
            """;

        // Act
        var definition = _loader.Parse("test-agent", content);

        // Assert
        definition.Should().NotBeNull();
        definition!.SystemPrompt.Should().Contain("You are an expert developer.");
        definition.SystemPrompt.Should().Contain("{{context.WorkItemTitle}}");
        definition.SystemPrompt.Should().Contain("{{context.Data.WorkspacePath}}");
    }

    [Fact]
    public void Parse_UsesDefaultsForMissingOptionalFields()
    {
        // Arrange
        var content = """
            # Minimal Agent

            ## Metadata

            - **Name**: Minimal Agent
            - **Description**: A minimal agent

            ## System Prompt

            You are an agent.
            """;

        // Act
        var definition = _loader.Parse("minimal-agent", content);

        // Assert
        definition.Should().NotBeNull();
        definition!.Provider.Should().Be(AgentDefinition.DefaultProvider);
        definition.Model.Should().Be(AgentDefinition.DefaultModel);
        definition.Temperature.Should().Be(AgentDefinition.DefaultTemperature);
        definition.Capabilities.Should().BeEmpty();
        definition.Tools.Should().BeEmpty();
    }

    [Fact]
    public void Parse_ReturnsNullForMissingMetadataSection()
    {
        // Arrange
        var content = """
            # Agent Without Metadata

            ## System Prompt

            You are an agent.
            """;

        // Act
        var definition = _loader.Parse("bad-agent", content);

        // Assert
        definition.Should().BeNull();
    }

    [Fact]
    public void Parse_ReturnsNullForMissingSystemPrompt()
    {
        // Arrange
        var content = """
            # Agent Without System Prompt

            ## Metadata

            - **Name**: Bad Agent
            - **Description**: Missing system prompt
            """;

        // Act
        var definition = _loader.Parse("bad-agent", content);

        // Assert
        definition.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_ReturnsNullForNonExistentFile()
    {
        // Act
        var agent = await _loader.LoadAsync(@"C:\agents\nonexistent.md");

        // Assert
        agent.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_LoadsAgentFromFile()
    {
        // Arrange
        var content = """
            # Test Agent

            ## Metadata

            - **Name**: Test Agent
            - **Description**: A test agent

            ## System Prompt

            You are a test agent.
            """;

        _fileSystem.AddFile(@"C:\agents\test-agent.md", new MockFileData(content));

        // Act
        var agent = await _loader.LoadAsync(@"C:\agents\test-agent.md");

        // Assert
        agent.Should().NotBeNull();
        agent!.AgentId.Should().Be("test-agent");
        agent.Metadata.Name.Should().Be("Test Agent");
    }

    [Fact]
    public void Parse_HandlesInvalidTemperature()
    {
        // Arrange
        var content = """
            # Test Agent

            ## Metadata

            - **Name**: Test Agent
            - **Description**: Test
            - **Temperature**: not-a-number

            ## System Prompt

            You are an agent.
            """;

        // Act
        var definition = _loader.Parse("test-agent", content);

        // Assert
        definition.Should().NotBeNull();
        definition!.Temperature.Should().Be(AgentDefinition.DefaultTemperature);
    }

    [Fact]
    public void ToMetadata_ConvertsDefinitionCorrectly()
    {
        // Arrange
        var definition = new AgentDefinition(
            AgentId: "test-agent",
            Name: "Test Agent",
            Description: "Test description",
            Provider: "ollama",
            Model: "llama3:8b",
            Temperature: 0.8,
            SystemPrompt: "You are a test agent.",
            Capabilities: ["coding", "testing"],
            Tools: ["validate_code"]);

        // Act
        var metadata = definition.ToMetadata();

        // Assert
        metadata.Name.Should().Be("Test Agent");
        metadata.Description.Should().Be("Test description");
        metadata.Provider.Should().Be("ollama");
        metadata.Model.Should().Be("llama3:8b");
        metadata.Temperature.Should().Be(0.8);
        metadata.Tags.Should().BeEquivalentTo(["coding", "testing"]);
        metadata.Tools.Should().BeEquivalentTo(["validate_code"]);
    }
}
