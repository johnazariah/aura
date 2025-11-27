// <copyright file="AgentDiscoveryTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Tests.Endpoints;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

/// <summary>
/// Integration tests for agent discovery endpoints.
/// </summary>
public class AgentDiscoveryTests : IClassFixture<AuraApiFactory>
{
    private readonly HttpClient _client;
    private readonly AuraApiFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public AgentDiscoveryTests(AuraApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAgents_ReturnsAllLoadedAgents()
    {
        // Act
        var response = await _client.GetAsync("/api/agents");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var agents = await response.Content.ReadFromJsonAsync<AgentResponse[]>(JsonOptions);
        agents.Should().NotBeNull();
        agents!.Length.Should().BeGreaterThanOrEqualTo(4); // At least our 4 test agents
    }

    [Fact]
    public async Task GetAgents_IncludesCapabilitiesAndPriority()
    {
        // Act
        var response = await _client.GetAsync("/api/agents");
        var agents = await response.Content.ReadFromJsonAsync<AgentResponse[]>(JsonOptions);

        // Assert
        agents.Should().NotBeNull();

        var chatAgent = agents!.FirstOrDefault(a => a.Capabilities.Contains("chat"));
        chatAgent.Should().NotBeNull();
        chatAgent!.Priority.Should().Be(80);

        var codingAgent = agents.FirstOrDefault(a =>
            a.Capabilities.Contains("coding") && a.Languages.Length == 0);
        codingAgent.Should().NotBeNull();
        codingAgent!.Priority.Should().Be(70);
    }

    [Fact]
    public async Task GetAgents_WithCapabilityFilter_ReturnsMatchingAgents()
    {
        // Act
        var response = await _client.GetAsync("/api/agents?capability=coding");
        var agents = await response.Content.ReadFromJsonAsync<AgentResponse[]>(JsonOptions);

        // Assert
        agents.Should().NotBeNull();
        agents!.Should().AllSatisfy(a => a.Capabilities.Should().Contain("coding"));
    }

    [Fact]
    public async Task GetAgents_WithCapabilityFilter_SortedByPriority()
    {
        // Act
        var response = await _client.GetAsync("/api/agents?capability=coding");
        var agents = await response.Content.ReadFromJsonAsync<AgentResponse[]>(JsonOptions);

        // Assert
        agents.Should().NotBeNull();
        agents!.Length.Should().BeGreaterThanOrEqualTo(2);

        // Should be sorted by priority (lowest first)
        for (var i = 1; i < agents.Length; i++)
        {
            agents[i].Priority.Should().BeGreaterThanOrEqualTo(agents[i - 1].Priority);
        }
    }

    [Fact]
    public async Task GetAgents_WithLanguageFilter_ReturnsCSharpSpecialistFirst()
    {
        // Act
        var response = await _client.GetAsync("/api/agents?capability=coding&language=csharp");
        var agents = await response.Content.ReadFromJsonAsync<AgentResponse[]>(JsonOptions);

        // Assert
        agents.Should().NotBeNull();
        agents!.Length.Should().BeGreaterThanOrEqualTo(2);

        // C# specialist (priority 30) should be first
        agents[0].Languages.Should().Contain("csharp");
        agents[0].Priority.Should().Be(30);
    }

    [Fact]
    public async Task GetBestAgent_ReturnsSingleBestMatch()
    {
        // Act
        var response = await _client.GetAsync("/api/agents/best?capability=chat");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var agent = await response.Content.ReadFromJsonAsync<AgentResponse>(JsonOptions);
        agent.Should().NotBeNull();
        agent!.Capabilities.Should().Contain("chat");
    }

    [Fact]
    public async Task GetBestAgent_ForCSharp_ReturnsCSharpSpecialist()
    {
        // Act
        var response = await _client.GetAsync("/api/agents/best?capability=coding&language=csharp");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var agent = await response.Content.ReadFromJsonAsync<AgentResponse>(JsonOptions);
        agent.Should().NotBeNull();
        agent!.Priority.Should().Be(30); // C# specialist priority
        agent.Languages.Should().Contain("csharp");
    }

    [Fact]
    public async Task GetBestAgent_ForUnknownLanguage_ReturnsPolyglot()
    {
        // Act - Rust isn't explicitly supported, should fall back to polyglot
        var response = await _client.GetAsync("/api/agents/best?capability=coding&language=rust");

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
        var response = await _client.GetAsync("/api/agents/best?capability=nonexistent");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAgent_ById_ReturnsAgentDetails()
    {
        // Arrange - first get the list to find an ID
        var listResponse = await _client.GetAsync("/api/agents");
        var agents = await listResponse.Content.ReadFromJsonAsync<AgentResponse[]>(JsonOptions);
        var firstAgent = agents!.First();

        // Act
        var response = await _client.GetAsync($"/api/agents/{firstAgent.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var agent = await response.Content.ReadFromJsonAsync<AgentResponse>(JsonOptions);
        agent.Should().NotBeNull();
        agent!.Id.Should().Be(firstAgent.Id);
    }

    [Fact]
    public async Task GetAgent_ById_Unknown_Returns404()
    {
        // Act
        var response = await _client.GetAsync("/api/agents/nonexistent-agent");

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
