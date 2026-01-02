// <copyright file="AgentExecutionTests.cs" company="Aura">
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
/// Integration tests for agent execution endpoints.
/// Tests validate agent execution through the API using the stub LLM provider.
/// </summary>
[Trait("Category", "Integration")]
public class AgentExecutionTests(AuraApiFactory factory) : IntegrationTestBase(factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public async Task ExecuteAgent_WithValidPrompt_ReturnsResponse()
    {
        // Arrange - use the echo-agent which is designed for testing
        var request = new { Prompt = "Hello, test agent!" };

        // Act
        var response = await Client.PostAsJsonAsync("/api/agents/echo-agent/execute", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ExecuteResponse>(JsonOptions);
        result.Should().NotBeNull();
        result!.Content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteAgent_ChatAgent_ReturnsResponse()
    {
        // Arrange
        var request = new { Prompt = "What can you help me with?" };

        // Act
        var response = await Client.PostAsJsonAsync("/api/agents/chat-agent/execute", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ExecuteResponse>(JsonOptions);
        result.Should().NotBeNull();
        result!.Content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteAgent_UnknownAgent_Returns404()
    {
        // Arrange
        var request = new { Prompt = "Hello" };

        // Act
        var response = await Client.PostAsJsonAsync("/api/agents/nonexistent-agent/execute", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ExecuteAgent_StubProvider_ReturnsStubResponse()
    {
        // Arrange - echo-agent uses stub provider which returns predictable responses
        var request = new { Prompt = "What is 2+2?" };

        // Act
        var response = await Client.PostAsJsonAsync("/api/agents/echo-agent/execute", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ExecuteResponse>(JsonOptions);
        result.Should().NotBeNull();

        // Stub provider returns a canned response containing "Stub"
        result!.Content.Should().Contain("Stub");
    }

    /// <summary>
    /// Response model for execute endpoint.
    /// </summary>
    private record ExecuteResponse(
        string Content,
        int TokensUsed,
        Dictionary<string, string>? Artifacts);
}
