// <copyright file="AgentRegistryTests.cs" company="Aura">
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
/// Tests for <see cref="AgentRegistry"/>.
/// </summary>
public class AgentRegistryTests
{
    private readonly MockFileSystem _fileSystem;
    private readonly IAgentLoader _agentLoader;
    private readonly ILogger<AgentRegistry> _logger;
    private readonly AgentRegistry _registry;
    private readonly string _agentsDir;

    public AgentRegistryTests()
    {
        // Use cross-platform temp path for mock file system
        _agentsDir = Path.Combine(Path.GetTempPath(), "agents");
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
        var agent1Path = Path.Combine(_agentsDir, "agent1.md");
        var agent2Path = Path.Combine(_agentsDir, "agent2.md");
        _fileSystem.AddDirectory(_agentsDir);
        _fileSystem.AddFile(agent1Path, new MockFileData("# Agent 1"));
        _fileSystem.AddFile(agent2Path, new MockFileData("# Agent 2"));

        var agent1 = CreateMockAgent("agent1", "Agent 1");
        var agent2 = CreateMockAgent("agent2", "Agent 2");

        _agentLoader.LoadAsync(agent1Path).Returns(Task.FromResult<IAgent?>(agent1));
        _agentLoader.LoadAsync(agent2Path).Returns(Task.FromResult<IAgent?>(agent2));

        _registry.AddWatchDirectory(_agentsDir, enableHotReload: false);

        // Act
        await _registry.ReloadAsync();

        // Assert
        _registry.Agents.Should().HaveCount(2);
    }

    [Fact]
    public async Task ReloadAsync_RemovesAgentsForDeletedFiles()
    {
        // Arrange
        var agent1Path = Path.Combine(_agentsDir, "agent1.md");
        _fileSystem.AddDirectory(_agentsDir);
        _fileSystem.AddFile(agent1Path, new MockFileData("# Agent 1"));

        var agent1 = CreateMockAgent("agent1", "Agent 1");
        _agentLoader.LoadAsync(agent1Path).Returns(Task.FromResult<IAgent?>(agent1));

        _registry.AddWatchDirectory(_agentsDir, enableHotReload: false);
        await _registry.ReloadAsync();

        // Now simulate file deletion by removing from the file system
        _fileSystem.RemoveFile(agent1Path);

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

    [Fact]
    public void GetByCapability_ReturnsAgentsWithMatchingCapability()
    {
        // Arrange
        var codingAgent = CreateMockAgent("coding-agent", "Coding Agent", capabilities: [Capabilities.Coding]);
        var chatAgent = CreateMockAgent("chat-agent", "Chat Agent", capabilities: [Capabilities.Chat]);
        var multiAgent = CreateMockAgent("multi-agent", "Multi Agent", capabilities: [Capabilities.Coding, Capabilities.Review]);
        _registry.Register(codingAgent);
        _registry.Register(chatAgent);
        _registry.Register(multiAgent);

        // Act
        var result = _registry.GetByCapability(Capabilities.Coding);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(codingAgent);
        result.Should().Contain(multiAgent);
    }

    [Fact]
    public void GetByCapability_ReturnsSortedByPriority()
    {
        // Arrange (lower priority = more specialized = returned first)
        var specialized = CreateMockAgent("specialized", "Specialized", capabilities: [Capabilities.Coding], priority: 20);
        var generalist = CreateMockAgent("generalist", "Generalist", capabilities: [Capabilities.Coding], priority: 80);
        var domain = CreateMockAgent("domain", "Domain", capabilities: [Capabilities.Coding], priority: 50);
        _registry.Register(generalist);
        _registry.Register(specialized);
        _registry.Register(domain);

        // Act
        var result = _registry.GetByCapability(Capabilities.Coding);

        // Assert
        result.Should().HaveCount(3);
        result[0].Should().Be(specialized);  // Priority 20
        result[1].Should().Be(domain);       // Priority 50
        result[2].Should().Be(generalist);   // Priority 80
    }

    [Fact]
    public void GetByCapability_WithLanguage_FiltersToMatchingOrPolyglot()
    {
        // Arrange
        var csharpAgent = CreateMockAgent("csharp", "C# Agent", capabilities: [Capabilities.Coding], languages: ["csharp"]);
        var polyglot = CreateMockAgent("polyglot", "Polyglot", capabilities: [Capabilities.Coding], languages: []); // Empty = polyglot
        var pythonAgent = CreateMockAgent("python", "Python Agent", capabilities: [Capabilities.Coding], languages: ["python"]);
        _registry.Register(csharpAgent);
        _registry.Register(polyglot);
        _registry.Register(pythonAgent);

        // Act
        var result = _registry.GetByCapability(Capabilities.Coding, "csharp");

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(csharpAgent);
        result.Should().Contain(polyglot);
        result.Should().NotContain(pythonAgent);
    }

    [Fact]
    public void GetBestForCapability_ReturnsLowestPriorityAgent()
    {
        // Arrange
        var specialized = CreateMockAgent("specialized", "Specialized", capabilities: [Capabilities.Coding], priority: 20);
        var generalist = CreateMockAgent("generalist", "Generalist", capabilities: [Capabilities.Coding], priority: 80);
        _registry.Register(generalist);
        _registry.Register(specialized);

        // Act
        var result = _registry.GetBestForCapability(Capabilities.Coding);

        // Assert
        result.Should().Be(specialized);
    }

    [Fact]
    public void GetBestForCapability_ReturnsNullWhenNoMatch()
    {
        // Arrange
        var chatAgent = CreateMockAgent("chat", "Chat", capabilities: [Capabilities.Chat]);
        _registry.Register(chatAgent);

        // Act
        var result = _registry.GetBestForCapability(Capabilities.Coding);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetByCapability_IsCaseInsensitive()
    {
        // Arrange
        var agent = CreateMockAgent("agent", "Agent", capabilities: ["CODING"]);
        _registry.Register(agent);

        // Act
        var result = _registry.GetByCapability("coding");

        // Assert
        result.Should().HaveCount(1);
    }

    private static IAgent CreateMockAgent(string id, string name, IReadOnlyList<string>? tags = null, IReadOnlyList<string>? capabilities = null, int priority = 50, IReadOnlyList<string>? languages = null)
    {
        var metadata = new AgentMetadata(
            name,
            "Test description",
            Capabilities: capabilities ?? [],
            Priority: priority,
            Languages: languages,
            Tags: tags);
        var agent = Substitute.For<IAgent>();
        agent.AgentId.Returns(id);
        agent.Metadata.Returns(metadata);
        agent.ExecuteAsync(Arg.Any<AgentContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(AgentOutput.FromText("test")));
        return agent;
    }
}
