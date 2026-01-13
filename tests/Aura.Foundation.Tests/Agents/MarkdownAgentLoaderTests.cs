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
    public void Parse_ExtractsToolNamesWithDots()
    {
        // Arrange - dotted tool names like file.read, shell.exec
        var content = """
            # Test Agent

            ## Metadata

            - **Name**: Test Agent
            - **Description**: Test

            ## Tools Available

            - **file.read(path)**: Read a file from the workspace
            - **file.write(path, content)**: Write content to a file
            - **shell.exec(command)**: Execute a shell command

            ## System Prompt

            You are a test agent.
            """;

        // Act
        var definition = _loader.Parse("test-agent", content);

        // Assert
        definition.Should().NotBeNull();
        definition!.Tools.Should().HaveCount(3);
        definition.Tools.Should().Contain("file.read");
        definition.Tools.Should().Contain("file.write");
        definition.Tools.Should().Contain("shell.exec");
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
        definition.Priority.Should().Be(AgentDefinition.DefaultPriority);
        definition.Capabilities.Should().BeEmpty();
        definition.Languages.Should().BeEmpty();
        definition.Tags.Should().BeEmpty();
        definition.Tools.Should().BeEmpty();
    }

    [Fact]
    public void Parse_ExtractsPriorityFromMetadata()
    {
        // Arrange
        var content = """
            # Specialized Agent

            ## Metadata

            - **Name**: Specialized Agent
            - **Description**: A specialized agent
            - **Priority**: 20

            ## System Prompt

            You are an agent.
            """;

        // Act
        var definition = _loader.Parse("specialized-agent", content);

        // Assert
        definition.Should().NotBeNull();
        definition!.Priority.Should().Be(20);
    }

    [Fact]
    public void Parse_ExtractsLanguagesSection()
    {
        // Arrange
        var content = """
            # C# Agent

            ## Metadata

            - **Name**: C# Agent
            - **Description**: A C# specialist

            ## Capabilities

            - coding

            ## Languages

            - csharp
            - fsharp

            ## System Prompt

            You are an agent.
            """;

        // Act
        var definition = _loader.Parse("csharp-agent", content);

        // Assert
        definition.Should().NotBeNull();
        definition!.Languages.Should().HaveCount(2);
        definition.Languages.Should().Contain("csharp");
        definition.Languages.Should().Contain("fsharp");
    }

    [Fact]
    public void Parse_ExtractsTagsSection()
    {
        // Arrange
        var content = """
            # Tagged Agent

            ## Metadata

            - **Name**: Tagged Agent
            - **Description**: An agent with tags

            ## Tags

            - user-tag-1
            - user-tag-2

            ## System Prompt

            You are an agent.
            """;

        // Act
        var definition = _loader.Parse("tagged-agent", content);

        // Assert
        definition.Should().NotBeNull();
        definition!.Tags.Should().HaveCount(2);
        definition.Tags.Should().Contain("user-tag-1");
        definition.Tags.Should().Contain("user-tag-2");
    }

    [Fact]
    public void Parse_DistinguishesCapabilitiesFromTags()
    {
        // Arrange
        var content = """
            # Full Agent

            ## Metadata

            - **Name**: Full Agent
            - **Description**: An agent with both capabilities and tags
            - **Priority**: 30

            ## Capabilities

            - coding
            - review

            ## Languages

            - csharp

            ## Tags

            - my-custom-tag
            - another-tag

            ## System Prompt

            You are an agent.
            """;

        // Act
        var definition = _loader.Parse("full-agent", content);

        // Assert
        definition.Should().NotBeNull();
        definition!.Capabilities.Should().BeEquivalentTo(["coding", "review"]);
        definition.Languages.Should().BeEquivalentTo(["csharp"]);
        definition.Tags.Should().BeEquivalentTo(["my-custom-tag", "another-tag"]);
        definition.Priority.Should().Be(30);
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
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "agents", "nonexistent.md");
        var agent = await _loader.LoadAsync(nonExistentPath);

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

        var agentPath = Path.Combine(Path.GetTempPath(), "agents", "test-agent.md");
        _fileSystem.AddFile(agentPath, new MockFileData(content));

        // Act
        var agent = await _loader.LoadAsync(agentPath);

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
    public void Parse_ExtractsIngesterCapabilities()
    {
        // Arrange
        var content = """
            # Generic Ingester

            ## Metadata

            - **Name**: Generic Ingester
            - **Description**: Ingests any file type
            - **Priority**: 20

            ## Capabilities

            - ingest:*

            ## Tags

            - ingester
            - polyglot

            ## System Prompt

            Parse the code and extract chunks.
            """;

        // Act
        var definition = _loader.Parse("generic-ingester", content);

        // Assert
        definition.Should().NotBeNull();
        definition!.Capabilities.Should().Contain("ingest:*");
        definition.Priority.Should().Be(20);
        definition.Tags.Should().Contain("ingester");
    }

    [Fact]
    public void Parse_ExtractsSpecificIngesterCapabilities()
    {
        // Arrange
        var content = """
            # Python Ingester

            ## Metadata

            - **Name**: Python Ingester
            - **Description**: Ingests Python files
            - **Priority**: 10

            ## Capabilities

            - ingest:py
            - ingest:pyw

            ## Languages

            - python

            ## System Prompt

            Parse Python code.
            """;

        // Act
        var definition = _loader.Parse("python-ingester", content);

        // Assert
        definition.Should().NotBeNull();
        definition!.Capabilities.Should().BeEquivalentTo(["ingest:py", "ingest:pyw"]);
        definition.Languages.Should().Contain("python");
        definition.Priority.Should().Be(10);
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
            Priority: 50,
            Languages: ["csharp"],
            Tags: ["tag1"],
            Tools: ["validate_code"]);

        // Act
        var metadata = definition.ToMetadata();

        // Assert
        metadata.Name.Should().Be("Test Agent");
        metadata.Description.Should().Be("Test description");
        metadata.Provider.Should().Be("ollama");
        metadata.Model.Should().Be("llama3:8b");
        metadata.Temperature.Should().Be(0.8);
        metadata.Capabilities.Should().BeEquivalentTo(["coding", "testing"]);
        metadata.Priority.Should().Be(50);
        metadata.Languages.Should().BeEquivalentTo(["csharp"]);
        metadata.Tags.Should().BeEquivalentTo(["tag1"]);
        metadata.Tools.Should().BeEquivalentTo(["validate_code"]);
    }
}
