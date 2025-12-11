// <copyright file="OllamaProviderTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Tests.Llm;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aura.Foundation.Agents;
using Aura.Foundation.Llm;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

public class OllamaProviderTests
{
    private readonly OllamaOptions _options;

    public OllamaProviderTests()
    {
        _options = new OllamaOptions
        {
            BaseUrl = "http://localhost:11434",
            TimeoutSeconds = 30,
            DefaultModel = "test-model",
            DefaultEmbeddingModel = "test-embedding-model"
        };
    }

    [Fact]
    public void ProviderId_ReturnsOllama()
    {
        // Arrange
        var sut = CreateProvider();

        // Assert
        sut.ProviderId.Should().Be("ollama");
    }

    [Fact]
    public async Task GenerateAsync_SuccessfulResponse_ReturnsResponse()
    {
        // Arrange
        var responseContent = new
        {
            model = "test-model",
            response = "Generated text response",
            done = true,
            prompt_eval_count = 10,
            eval_count = 20
        };

        var handler = CreateMockHandler(HttpStatusCode.OK, responseContent);
        var sut = CreateProvider(handler);

        // Act
        var result = await sut.GenerateAsync("test-model", "Hello");

        // Assert
        result.Content.Should().Be("Generated text response");
        result.Model.Should().Be("test-model");
        result.TokensUsed.Should().Be(30); // 10 + 20
        result.FinishReason.Should().Be("stop");
    }

    [Fact]
    public async Task GenerateAsync_HttpError_ThrowsLlmException()
    {
        // Arrange
        var handler = CreateMockHandler(HttpStatusCode.InternalServerError, new { error = "Server error" });
        var sut = CreateProvider(handler);

        // Act & Assert
        var act = async () => await sut.GenerateAsync("test-model", "Hello");
        await act.Should().ThrowAsync<LlmException>()
            .Where(e => e.Code == LlmErrorCode.GenerationFailed);
    }

    [Fact]
    public async Task GenerateAsync_ConnectionError_ThrowsLlmException()
    {
        // Arrange
        var handler = new FailingHttpHandler(new HttpRequestException("Connection refused"));
        var sut = CreateProvider(handler);

        // Act & Assert
        var act = async () => await sut.GenerateAsync("test-model", "Hello");
        await act.Should().ThrowAsync<LlmException>()
            .Where(e => e.Code == LlmErrorCode.Unavailable);
    }

    [Fact]
    public async Task ChatAsync_SuccessfulResponse_ReturnsResponse()
    {
        // Arrange
        var responseContent = new
        {
            model = "test-model",
            message = new { role = "assistant", content = "Chat response" },
            done = true,
            prompt_eval_count = 5,
            eval_count = 15
        };

        var handler = CreateMockHandler(HttpStatusCode.OK, responseContent);
        var sut = CreateProvider(handler);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello!")
        };

        // Act
        var result = await sut.ChatAsync("test-model", messages);

        // Assert
        result.Content.Should().Be("Chat response");
        result.TokensUsed.Should().Be(20); // 5 + 15
    }

    [Fact]
    public async Task ListModelsAsync_SuccessfulResponse_ReturnsModels()
    {
        // Arrange
        var responseContent = new
        {
            models = new[]
            {
                new { name = "llama3:8b", size = 4_000_000_000L },
                new { name = "codellama:7b", size = 3_500_000_000L }
            }
        };

        var handler = CreateMockHandler(HttpStatusCode.OK, responseContent);
        var sut = CreateProvider(handler);

        // Act
        var result = await sut.ListModelsAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("llama3:8b");
        result[1].Name.Should().Be("codellama:7b");
    }

    [Fact]
    public async Task ListModelsAsync_ConnectionError_ReturnsEmptyList()
    {
        // Arrange
        var handler = new FailingHttpHandler(new HttpRequestException("Connection refused"));
        var sut = CreateProvider(handler);

        // Act
        var result = await sut.ListModelsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task IsModelAvailableAsync_ModelExists_ReturnsTrue()
    {
        // Arrange
        var responseContent = new
        {
            models = new[] { new { name = "llama3:8b" } }
        };

        var handler = CreateMockHandler(HttpStatusCode.OK, responseContent);
        var sut = CreateProvider(handler);

        // Act
        var result = await sut.IsModelAvailableAsync("llama3:8b");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsModelAvailableAsync_ModelNotExists_ReturnsFalse()
    {
        // Arrange
        var responseContent = new
        {
            models = new[] { new { name = "llama3:8b" } }
        };

        var handler = CreateMockHandler(HttpStatusCode.OK, responseContent);
        var sut = CreateProvider(handler);

        // Act
        var result = await sut.IsModelAvailableAsync("non-existent-model");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsHealthyAsync_ServerResponds_ReturnsTrue()
    {
        // Arrange
        var handler = CreateMockHandler(HttpStatusCode.OK, new { models = Array.Empty<object>() });
        var sut = CreateProvider(handler);

        // Act
        var result = await sut.IsHealthyAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsHealthyAsync_ServerError_ReturnsFalse()
    {
        // Arrange
        var handler = new FailingHttpHandler(new HttpRequestException("Connection refused"));
        var sut = CreateProvider(handler);

        // Act
        var result = await sut.IsHealthyAsync();

        // Assert
        result.Should().BeFalse();
    }

    private OllamaProvider CreateProvider(HttpMessageHandler? handler = null)
    {
        handler ??= CreateMockHandler(HttpStatusCode.OK, new { });
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(_options.BaseUrl)
        };

        return new OllamaProvider(
            httpClient,
            Options.Create(_options),
            NullLogger<OllamaProvider>.Instance);
    }

    private static MockHttpHandler CreateMockHandler(HttpStatusCode statusCode, object content)
    {
        return new MockHttpHandler(statusCode, content);
    }

    private class MockHttpHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly object _content;

        public MockHttpHandler(HttpStatusCode statusCode, object content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = JsonContent.Create(_content, options: new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                })
            };

            return Task.FromResult(response);
        }
    }

    private class FailingHttpHandler : HttpMessageHandler
    {
        private readonly Exception _exception;

        public FailingHttpHandler(Exception exception)
        {
            _exception = exception;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            throw _exception;
        }
    }
}
