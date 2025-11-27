// <copyright file="CodingAgentIntegrationTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Integration.Tests.Agents;

using System.Net.Http.Json;
using Aura.Integration.Tests.Fixtures;

/// <summary>
/// Integration tests for coding agent with real Ollama LLM.
/// </summary>
[Collection("Ollama")]
[Trait("Category", "Integration")]
public sealed class CodingAgentIntegrationTests : IClassFixture<IntegrationApiFactory>
{
    private readonly IntegrationApiFactory _factory;
    private readonly OllamaFixture _ollama;
    private readonly HttpClient _client;

    public CodingAgentIntegrationTests(IntegrationApiFactory factory, OllamaFixture ollama)
    {
        _factory = factory;
        _ollama = ollama;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CodingAgent_CSharpFunction_ReturnsValidCode()
    {
        SkipIfNoOllama();
        SkipIfNoModel("qwen2.5-coder");

        // Arrange
        var request = new
        {
            prompt = "Write a C# method that reverses a string. Just the method, no class wrapper."
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/agents/integration-coding-agent/execute", request);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ExecuteResponse>();
        result.Should().NotBeNull();
        result!.Content.Should().NotBeNullOrEmpty();

        // Should contain C# code elements
        var code = result.Content;
        code.Should().ContainAny("public", "private", "static", "string");
        code.Should().ContainAny("Reverse", "reverse", "char", "ToCharArray", "StringBuilder");
    }

    [Fact]
    public async Task CodingAgent_PythonFunction_ReturnsValidCode()
    {
        SkipIfNoOllama();
        SkipIfNoModel("qwen2.5-coder");

        // Arrange
        var request = new
        {
            prompt = "Write a Python function that calculates the factorial of a number. Just the function definition."
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/agents/integration-coding-agent/execute", request);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ExecuteResponse>();
        result.Should().NotBeNull();
        result!.Content.Should().NotBeNullOrEmpty();

        // Should contain Python code elements
        var code = result.Content;
        code.Should().Contain("def ");
        code.Should().ContainAny("factorial", "return", "if", "else");
    }

    [Fact]
    public async Task CodingAgent_TypeScriptFunction_ReturnsValidCode()
    {
        SkipIfNoOllama();
        SkipIfNoModel("qwen2.5-coder");

        // Arrange
        var request = new
        {
            prompt = "Write a TypeScript function that checks if a string is a palindrome. Include type annotations."
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/agents/integration-coding-agent/execute", request);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ExecuteResponse>();
        result.Should().NotBeNull();
        result!.Content.Should().NotBeNullOrEmpty();

        // Should contain TypeScript code elements
        var code = result.Content;
        code.Should().ContainAny("function", "const", "=>"); // Function declaration
        code.Should().ContainAny(": string", ": boolean", "string", "boolean"); // Type annotations
    }

    [Fact]
    public async Task CodingAgent_ErrorHandling_IncludesTryCatch()
    {
        SkipIfNoOllama();
        SkipIfNoModel("qwen2.5-coder");

        // Arrange
        var request = new
        {
            prompt = "Write a C# method that reads a file and returns its contents. Include proper error handling with try-catch."
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/agents/integration-coding-agent/execute", request);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ExecuteResponse>();
        result.Should().NotBeNull();
        result!.Content.Should().NotBeNullOrEmpty();

        // Should contain error handling elements
        var code = result.Content;
        code.Should().ContainAny("try", "catch", "exception", "Exception");
        code.Should().ContainAny("File.", "ReadAllText", "StreamReader");
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
