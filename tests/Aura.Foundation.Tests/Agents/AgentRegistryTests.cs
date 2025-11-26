// <copyright file="AgentRegistryTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Tests.Agents;

using System.IO.Abstractions.TestingHelpers;
using Aura.Foundation.Agents;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

/// <summary>
/// Tests for <see cref="AgentRegistry"/>.
/// </summary>
public class AgentRegistryTests
{
    private readonly MockFileSystem _fileSystem;
    private readonly IAgentLoader _agentLoader;
    private readonly ILogger<AgentRegistry> _logger;
    private readonly AgentRegistry _registry;

    public AgentRegistryTests()
    {
        _fileSystem = new MockFileSystem();
        _agentLoader = Substitute.For<IAgentLoader>();
        _logger = Substitute.For<ILogger<AgentRegistry>>();
        _registry = new AgentRegistry(_agentLoader, _fileSystem, _logger);
    }

    [Fact]
    public void Register_AddsAgentToRegistry()
    {
        // Arrange
        var agent = CreateMockAgent("test-agent", "Test Agent");

        // Act
        _registry.Register(agent);

        // Assert
        _registry.Agents.Should().HaveCount(1);
        _registry.GetAgent("test-agent").Should().Be(agent);
    }

    [Fact]
    public void Register_UpdatesExistingAgent()
    {
        // Arrange
        var agent1 = CreateMockAgent("test-agent", "Test Agent v1");
        var agent2 = CreateMockAgent("test-agent", "Test Agent v2");

        // Act
        _registry.Register(agent1);
        _registry.Register(agent2);

        // Assert
        _registry.Agents.Should().HaveCount(1);
        _registry.GetAgent("test-agent")!.Metadata.Name.Should().Be("Test Agent v2");
    }

    [Fact]
    public void GetAgent_ReturnsNullForUnknownAgent()
    {
        // Act
        var result = _registry.GetAgent("unknown");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void TryGetAgent_ReturnsTrueForExistingAgent()
    {
        // Arrange
        var agent = CreateMockAgent("test-agent", "Test Agent");
        _registry.Register(agent);

        // Act
        var found = _registry.TryGetAgent("test-agent", out var result);

        // Assert
        found.Should().BeTrue();
        result.Should().Be(agent);
    }

    [Fact]
    public void TryGetAgent_ReturnsFalseForUnknownAgent()
    {
        // Act
        var found = _registry.TryGetAgent("unknown", out var result);

        // Assert
        found.Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void Unregister_RemovesAgent()
    {
        // Arrange
        var agent = CreateMockAgent("test-agent", "Test Agent");
        _registry.Register(agent);

        // Act
        var removed = _registry.Unregister("test-agent");

        // Assert
        removed.Should().BeTrue();
        _registry.Agents.Should().BeEmpty();
    }

    [Fact]
    public void Unregister_ReturnsFalseForUnknownAgent()
    {
        // Act
        var removed = _registry.Unregister("unknown");

        // Assert
        removed.Should().BeFalse();
    }

    [Fact]
    public void GetAgentsByTags_ReturnsMatchingAgents()
    {
        // Arrange
        var agent1 = CreateMockAgent("agent1", "Agent 1", tags: ["coding", "python"]);
        var agent2 = CreateMockAgent("agent2", "Agent 2", tags: ["testing"]);
        var agent3 = CreateMockAgent("agent3", "Agent 3", tags: ["coding", "csharp"]);

        _registry.Register(agent1);
        _registry.Register(agent2);
        _registry.Register(agent3);

        // Act
        var codingAgents = _registry.GetAgentsByTags("coding");

        // Assert
        codingAgents.Should().HaveCount(2);
        codingAgents.Should().Contain(a => a.AgentId == "agent1");
        codingAgents.Should().Contain(a => a.AgentId == "agent3");
    }

    [Fact]
    public void GetAgentsByTags_ReturnsEmptyForNoMatch()
    {
        // Arrange
        var agent = CreateMockAgent("test-agent", "Test Agent", tags: ["coding"]);
        _registry.Register(agent);

        // Act
        var result = _registry.GetAgentsByTags("unknown-tag");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetAgentsByTags_ReturnsEmptyForEmptyTags()
    {
        // Arrange
        var agent = CreateMockAgent("test-agent", "Test Agent", tags: ["coding"]);
        _registry.Register(agent);

        // Act
        var result = _registry.GetAgentsByTags();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Register_RaisesAgentsChangedEvent()
    {
        // Arrange
        var agent = CreateMockAgent("test-agent", "Test Agent");
        AgentRegistryChangedEventArgs? eventArgs = null;
        _registry.AgentsChanged += (_, e) => eventArgs = e;

        // Act
        _registry.Register(agent);

        // Assert
        eventArgs.Should().NotBeNull();
        eventArgs!.ChangeType.Should().Be(AgentChangeType.Added);
        eventArgs.AgentId.Should().Be("test-agent");
        eventArgs.Agent.Should().Be(agent);
    }

    [Fact]
    public void Unregister_RaisesAgentsChangedEvent()
    {
        // Arrange
        var agent = CreateMockAgent("test-agent", "Test Agent");
        _registry.Register(agent);

        AgentRegistryChangedEventArgs? eventArgs = null;
        _registry.AgentsChanged += (_, e) => eventArgs = e;

        // Act
        _registry.Unregister("test-agent");

        // Assert
        eventArgs.Should().NotBeNull();
        eventArgs!.ChangeType.Should().Be(AgentChangeType.Removed);
        eventArgs.AgentId.Should().Be("test-agent");
    }

    [Fact]
    public async Task ReloadAsync_LoadsAgentsFromDirectory()
    {
        // Arrange
        const string directory = @"C:\agents";
        _fileSystem.AddDirectory(directory);
        _fileSystem.AddFile(@"C:\agents\agent1.md", new MockFileData("# Agent 1"));
        _fileSystem.AddFile(@"C:\agents\agent2.md", new MockFileData("# Agent 2"));

        var agent1 = CreateMockAgent("agent1", "Agent 1");
        var agent2 = CreateMockAgent("agent2", "Agent 2");

        _agentLoader.LoadAsync(@"C:\agents\agent1.md").Returns(Task.FromResult<IAgent?>(agent1));
        _agentLoader.LoadAsync(@"C:\agents\agent2.md").Returns(Task.FromResult<IAgent?>(agent2));

        _registry.AddWatchDirectory(directory, enableHotReload: false);

        // Act
        await _registry.ReloadAsync();

        // Assert
        _registry.Agents.Should().HaveCount(2);
    }

    [Fact]
    public async Task ReloadAsync_RemovesAgentsForDeletedFiles()
    {
        // Arrange
        const string directory = @"C:\agents";
        _fileSystem.AddDirectory(directory);
        _fileSystem.AddFile(@"C:\agents\agent1.md", new MockFileData("# Agent 1"));

        var agent1 = CreateMockAgent("agent1", "Agent 1");
        _agentLoader.LoadAsync(@"C:\agents\agent1.md").Returns(Task.FromResult<IAgent?>(agent1));

        _registry.AddWatchDirectory(directory, enableHotReload: false);
        await _registry.ReloadAsync();

        // Now simulate file deletion by removing from the file system
        _fileSystem.RemoveFile(@"C:\agents\agent1.md");

        // Act
        await _registry.ReloadAsync();

        // Assert
        _registry.Agents.Should().BeEmpty();
    }

    [Fact]
    public void GetAgent_IsCaseInsensitive()
    {
        // Arrange
        var agent = CreateMockAgent("Test-Agent", "Test Agent");
        _registry.Register(agent);

        // Act & Assert
        _registry.GetAgent("test-agent").Should().Be(agent);
        _registry.GetAgent("TEST-AGENT").Should().Be(agent);
        _registry.GetAgent("Test-Agent").Should().Be(agent);
    }

    private static IAgent CreateMockAgent(string id, string name, IReadOnlyList<string>? tags = null)
    {
        var metadata = new AgentMetadata(name, "Test description", Tags: tags);
        var agent = Substitute.For<IAgent>();
        agent.AgentId.Returns(id);
        agent.Metadata.Returns(metadata);
        agent.ExecuteAsync(Arg.Any<AgentContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success<AgentOutput, AgentError>(AgentOutput.FromText("test"))));
        return agent;
    }
}
