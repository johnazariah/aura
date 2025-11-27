// <copyright file="AgentExecutionTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Tests.Endpoints;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

/// <summary>
/// Integration tests for agent execution endpoints.
/// </summary>
public class AgentExecutionTests : IClassFixture<AuraApiFactory>
{
    private readonly HttpClient _client;
    private readonly AuraApiFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public AgentExecutionTests(AuraApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ExecuteAgent_WithValidPrompt_ReturnsResponse()
    {
        // Arrange
        var request = new { Prompt = "Hello, test agent!" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/agents/chat-agent/execute", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ExecuteResponse>(JsonOptions);
        result.Should().NotBeNull();
        result!.Content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteAgent_WithWorkspacePath_IncludesInContext()
    {
        // Arrange
        var request = new
        {
            Prompt = "Analyze this workspace",
            WorkspacePath = "/test/workspace",
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/agents/analysis-agent/execute", request);

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
        var response = await _client.PostAsJsonAsync("/api/agents/nonexistent-agent/execute", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ExecuteAgent_StubProvider_ReturnsStubResponse()
    {
        // Arrange - use the stub provider which returns predictable responses
        var request = new { Prompt = "What is 2+2?" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/agents/chat-agent/execute", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ExecuteResponse>(JsonOptions);
        result.Should().NotBeNull();

        // Stub provider returns a canned response
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
