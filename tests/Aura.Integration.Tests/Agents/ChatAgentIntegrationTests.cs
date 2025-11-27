// <copyright file="ChatAgentIntegrationTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Integration.Tests.Agents;

using System.Net.Http.Json;
using Aura.Integration.Tests.Fixtures;

/// <summary>
/// Integration tests for chat agent with real Ollama LLM.
/// </summary>
[Collection("Ollama")]
[Trait("Category", "Integration")]
public sealed class ChatAgentIntegrationTests : IClassFixture<IntegrationApiFactory>
{
    private readonly IntegrationApiFactory _factory;
    private readonly OllamaFixture _ollama;
    private readonly HttpClient _client;

    public ChatAgentIntegrationTests(IntegrationApiFactory factory, OllamaFixture ollama)
    {
        _factory = factory;
        _ollama = ollama;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ChatAgent_SimpleQuestion_ReturnsCoherentResponse()
    {
        // Skip if Ollama not available
        SkipIfNoOllama();
        SkipIfNoModel("llama3");

        // Arrange
        var request = new
        {
            prompt = "What is 2 + 2? Reply with just the number."
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/agents/integration-chat-agent/execute", request);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ExecuteResponse>();
        result.Should().NotBeNull();
        result!.Content.Should().NotBeNullOrEmpty();

        // The response should contain "4" somewhere
        result.Content.Should().Contain("4");
    }

    [Fact]
    public async Task ChatAgent_GreetingQuestion_RespondsNaturally()
    {
        // Skip if Ollama not available
        SkipIfNoOllama();
        SkipIfNoModel("llama3");

        // Arrange
        var request = new
        {
            prompt = "Hello! Who are you?"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/agents/integration-chat-agent/execute", request);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ExecuteResponse>();
        result.Should().NotBeNull();
        result!.Content.Should().NotBeNullOrEmpty();

        // Should have some kind of self-introduction
        result.Content.ToLowerInvariant().Should().ContainAny("assistant", "help", "ai", "i am", "i'm");
    }

    [Fact]
    public async Task ChatAgent_FactualQuestion_ReturnsAccurateInfo()
    {
        // Skip if Ollama not available
        SkipIfNoOllama();
        SkipIfNoModel("llama3");

        // Arrange
        var request = new
        {
            prompt = "What is the capital of France? Reply with just the city name."
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/agents/integration-chat-agent/execute", request);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ExecuteResponse>();
        result.Should().NotBeNull();
        result!.Content.Should().Contain("Paris");
    }

    private void SkipIfNoOllama()
    {
        if (!_ollama.IsAvailable)
        {
            Assert.Skip(_ollama.SkipReason ?? "Ollama not available");
        }
    }

    private void SkipIfNoModel(string modelName)
    {
        if (!_ollama.HasModel(modelName))
        {
            Assert.Skip($"{modelName} model not installed");
        }
    }

    private sealed record ExecuteResponse(string Content, string AgentName, bool Success, string? Error);
}
