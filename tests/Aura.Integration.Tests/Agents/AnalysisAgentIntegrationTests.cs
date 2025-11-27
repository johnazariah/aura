// <copyright file="AnalysisAgentIntegrationTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Integration.Tests.Agents;

using System.Net.Http.Json;
using Aura.Integration.Tests.Fixtures;

/// <summary>
/// Integration tests for analysis/digestion agent with real Ollama LLM.
/// </summary>
[Collection("Ollama")]
[Trait("Category", "Integration")]
public sealed class AnalysisAgentIntegrationTests : IClassFixture<IntegrationApiFactory>
{
    private readonly IntegrationApiFactory _factory;
    private readonly OllamaFixture _ollama;
    private readonly HttpClient _client;

    public AnalysisAgentIntegrationTests(IntegrationApiFactory factory, OllamaFixture ollama)
    {
        _factory = factory;
        _ollama = ollama;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AnalysisAgent_TextSummarization_ProducesShorterOutput()
    {
        SkipIfNoOllama();
        SkipIfNoModel("llama3");

        // Arrange - A long-ish text to summarize
        var longText = """
            The software development lifecycle (SDLC) is a process used by software development teams 
            to design, develop, and test high-quality software. The SDLC provides a structured approach 
            to software development, breaking down the process into distinct phases including planning, 
            analysis, design, development, testing, deployment, and maintenance. Each phase has its own 
            deliverables and activities that must be completed before moving to the next phase. 
            The planning phase involves defining project goals and requirements. The analysis phase 
            focuses on understanding what the software should do. The design phase creates the architecture 
            and detailed design specifications. Development is where actual coding takes place. Testing 
            verifies the software works correctly. Deployment releases the software to users. Finally, 
            maintenance ensures ongoing support and updates.
            """;

        var request = new
        {
            prompt = $"Summarize this text in 2-3 sentences:\n\n{longText}"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/agents/integration-analysis-agent/execute", request);

        // Debug: If not OK, read the error message
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"API returned {response.StatusCode}: {errorContent}");
        }

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ExecuteResponse>();
        result.Should().NotBeNull();
        result!.Content.Should().NotBeNullOrEmpty();

        // Summary should be shorter than the original
        result.Content.Length.Should().BeLessThan(longText.Length);

        // Should contain key concepts
        result.Content.ToLowerInvariant().Should().ContainAny(
            "software", "sdlc", "development", "phases", "lifecycle");
    }

    [Fact]
    public async Task AnalysisAgent_IssueDigestion_ExtractsKeyPoints()
    {
        SkipIfNoOllama();
        SkipIfNoModel("llama3");

        // Arrange - Simulated GitHub issue
        var issueText = """
            ## Bug: Application crashes when opening large files

            **Environment:**
            - OS: Windows 11
            - App Version: 2.3.1
            - RAM: 16GB

            **Steps to Reproduce:**
            1. Open the application
            2. Click File > Open
            3. Select a file larger than 500MB
            4. Application freezes then crashes

            **Expected Behavior:**
            The file should open, possibly with a progress indicator for large files.

            **Actual Behavior:**
            The application becomes unresponsive and crashes with an OutOfMemoryException.

            **Additional Context:**
            This started happening after the 2.3.0 update. The previous version handled large files fine.
            Logs show memory usage spiking to 100% before the crash.
            """;

        var request = new
        {
            prompt = $"Analyze this bug report and identify: 1) The core problem, 2) The likely cause, 3) Suggested fix approach:\n\n{issueText}"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/agents/integration-analysis-agent/execute", request);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ExecuteResponse>();
        result.Should().NotBeNull();
        result!.Content.Should().NotBeNullOrEmpty();

        // Should identify key elements
        var analysis = result.Content.ToLowerInvariant();
        analysis.Should().ContainAny("memory", "crash", "outofmemory", "large file");
        analysis.Should().ContainAny("500mb", "file", "loading", "streaming", "chunk");
    }

    [Fact]
    public async Task AnalysisAgent_ProConsList_StructuredOutput()
    {
        SkipIfNoOllama();
        SkipIfNoModel("llama3");

        // Arrange
        var request = new
        {
            prompt = "List 3 pros and 3 cons of using microservices architecture vs monolithic architecture."
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/agents/integration-analysis-agent/execute", request);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ExecuteResponse>();
        result.Should().NotBeNull();
        result!.Content.Should().NotBeNullOrEmpty();

        // Should mention both architectures
        var analysis = result.Content.ToLowerInvariant();
        analysis.Should().ContainAny("microservice", "micro-service", "distributed");
        analysis.Should().ContainAny("monolith", "monolithic", "single");

        // Should have some structure indicators (lists, pros, cons)
        analysis.Should().ContainAny("pro", "con", "advantage", "disadvantage", "benefit", "drawback", "-", "1.", "•");
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
