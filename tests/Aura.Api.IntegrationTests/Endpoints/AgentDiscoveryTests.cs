// <copyright file="AgentDiscoveryTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.IntegrationTests.Endpoints;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aura.Api.IntegrationTests.Infrastructure;
using FluentAssertions;
using Xunit;

/// <summary>
/// Integration tests for agent discovery endpoints.
/// Tests validate the API loads agents from the actual agents/ directory.
/// </summary>
[Trait("Category", "Integration")]
public class AgentDiscoveryTests(AuraApiFactory factory) : IntegrationTestBase(factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public async Task GetAgents_ReturnsAllLoadedAgents()
    {
        // Act
        var response = await Client.GetAsync("/api/agents");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var agents = await response.Content.ReadFromJsonAsync<AgentResponse[]>(JsonOptions);
        agents.Should().NotBeNull();

        // We have 8 agent files in agents/: build-fixer, business-analyst, chat,
        // code-review, coding, documentation, echo, issue-enricher
        agents!.Length.Should().BeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public async Task GetAgents_IncludesCapabilitiesAndPriority()
    {
        // Act
        var response = await Client.GetAsync("/api/agents");
        var agents = await response.Content.ReadFromJsonAsync<AgentResponse[]>(JsonOptions);

        // Assert
        agents.Should().NotBeNull();

        // chat-agent has priority 80, capability "chat"
        var chatAgent = agents!.FirstOrDefault(a => a.Capabilities.Contains("chat"));
        chatAgent.Should().NotBeNull();
        chatAgent!.Priority.Should().Be(80);

        // coding-agent has priority 70, capability "coding", no languages (polyglot)
        var codingAgent = agents.FirstOrDefault(a =>
            a.Capabilities.Contains("coding") && a.Languages.Length == 0);
        codingAgent.Should().NotBeNull();
        codingAgent!.Priority.Should().Be(70);
    }

    [Fact]
    public async Task GetAgents_WithCapabilityFilter_ReturnsMatchingAgents()
    {
        // Act
        var response = await Client.GetAsync("/api/agents?capability=chat");
        var agents = await response.Content.ReadFromJsonAsync<AgentResponse[]>(JsonOptions);

        // Assert
        agents.Should().NotBeNull();
        agents!.Should().AllSatisfy(a => a.Capabilities.Should().Contain("chat"));
    }

    [Fact]
    public async Task GetAgents_WithCapabilityFilter_SortedByPriority()
    {
        // Act - analysis capability has business-analyst-agent
        var response = await Client.GetAsync("/api/agents?capability=analysis");
        var agents = await response.Content.ReadFromJsonAsync<AgentResponse[]>(JsonOptions);

        // Assert
        agents.Should().NotBeNull();
        agents!.Length.Should().BeGreaterThanOrEqualTo(1);

        // Should be sorted by priority (lowest first)
        for (var i = 1; i < agents.Length; i++)
        {
            agents[i].Priority.Should().BeGreaterThanOrEqualTo(agents[i - 1].Priority);
        }
    }

    [Fact]
    public async Task GetAgents_WithLanguageFilter_ReturnsPolyglotForUnknownLanguage()
    {
        // Act - COBOL isn't explicitly supported, polyglot agent should match
        var response = await Client.GetAsync("/api/agents?capability=coding&language=cobol");
        var agents = await response.Content.ReadFromJsonAsync<AgentResponse[]>(JsonOptions);

        // Assert
        agents.Should().NotBeNull();
        agents!.Length.Should().BeGreaterThanOrEqualTo(1);

        // The coding-agent is polyglot (no languages specified)
        agents[0].Languages.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBestAgent_ReturnsSingleBestMatch()
    {
        // Act
        var response = await Client.GetAsync("/api/agents/best?capability=chat");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var agent = await response.Content.ReadFromJsonAsync<AgentResponse>(JsonOptions);
        agent.Should().NotBeNull();
        agent!.Capabilities.Should().Contain("chat");
        agent.Id.Should().Be("chat-agent");
    }

    [Fact]
    public async Task GetBestAgent_ForCoding_ReturnsHighestPriorityCodingAgent()
    {
        // Act
        var response = await Client.GetAsync("/api/agents/best?capability=coding");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var agent = await response.Content.ReadFromJsonAsync<AgentResponse>(JsonOptions);
        agent.Should().NotBeNull();
        agent!.Capabilities.Should().Contain("coding");

        // Should return one of the specialist agents (priority 10) rather than the fallback (priority 70)
        agent.Priority.Should().Be(10);
    }

    [Fact]
    public async Task GetBestAgent_ForUnknownLanguage_ReturnsPolyglot()
    {
        // Act - COBOL isn't explicitly supported, should fall back to polyglot
        var response = await Client.GetAsync("/api/agents/best?capability=coding&language=cobol");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var agent = await response.Content.ReadFromJsonAsync<AgentResponse>(JsonOptions);
        agent.Should().NotBeNull();
        agent!.Languages.Should().BeEmpty(); // Polyglot has no specific languages
    }

    [Fact]
    public async Task GetBestAgent_ForNonexistentCapability_Returns404()
    {
        // Act
        var response = await Client.GetAsync("/api/agents/best?capability=nonexistent");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAgent_ById_ReturnsAgentDetails()
    {
        // Act - use a known agent ID
        var response = await Client.GetAsync("/api/agents/chat-agent");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var agent = await response.Content.ReadFromJsonAsync<AgentResponse>(JsonOptions);
        agent.Should().NotBeNull();
        agent!.Id.Should().Be("chat-agent");
        // Name defaults to AgentId when not specified in metadata
        agent.Name.Should().Be("chat-agent");
        agent.Capabilities.Should().Contain("chat");
    }

    [Fact]
    public async Task GetAgent_ById_Unknown_Returns404()
    {
        // Act
        var response = await Client.GetAsync("/api/agents/nonexistent-agent");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Response model for agent endpoints.
    /// </summary>
    private record AgentResponse(
        string Id,
        string Name,
        string Description,
        string[] Capabilities,
        int Priority,
        string[] Languages,
        string Provider,
        string Model,
        string[] Tags);
}
