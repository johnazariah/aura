using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Anvil.Cli.Adapters;
using Anvil.Cli.Exceptions;
using Anvil.Cli.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Anvil.Cli.Tests.Adapters;

public class AuraClientTests
{
    private readonly MockHttpMessageHandler _handler;
    private readonly AuraClient _sut;

    public AuraClientTests()
    {
        _handler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(_handler)
        {
            BaseAddress = new Uri("http://localhost:5300")
        };
        _sut = new AuraClient(httpClient, NullLogger<AuraClient>.Instance);
    }

    [Fact]
    public async Task HealthCheckAsync_WhenHealthy_ReturnsTrue()
    {
        // Arrange
        _handler.SetResponse(HttpStatusCode.OK, new { status = "Healthy" });

        // Act
        var result = await _sut.HealthCheckAsync();

        // Assert
        result.Should().BeTrue();
        _handler.LastRequestUri.Should().Be("/health");
    }

    [Fact]
    public async Task HealthCheckAsync_WhenUnhealthy_ReturnsFalse()
    {
        // Arrange
        _handler.SetResponse(HttpStatusCode.ServiceUnavailable, new { status = "Unhealthy" });

        // Act
        var result = await _sut.HealthCheckAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HealthCheckAsync_WhenConnectionRefused_ThrowsUnavailable()
    {
        // Arrange
        _handler.SetException(new HttpRequestException("Connection refused"));

        // Act
        var act = () => _sut.HealthCheckAsync();

        // Assert
        await act.Should().ThrowAsync<AuraUnavailableException>();
    }

    [Fact]
    public async Task CreateStoryAsync_ReturnsStory()
    {
        // Arrange
        var storyId = Guid.NewGuid();
        _handler.SetResponse(HttpStatusCode.OK, new StoryResponse
        {
            Id = storyId,
            Title = "Test Story",
            Description = "Test description",
            Status = "Created"
        });

        var request = new CreateStoryRequest
        {
            Title = "Test Story",
            Description = "Test description",
            RepositoryPath = "c:/repos/test"
        };

        // Act
        var result = await _sut.CreateStoryAsync(request);

        // Assert
        result.Id.Should().Be(storyId);
        result.Title.Should().Be("Test Story");
        result.Status.Should().Be("Created");
        _handler.LastRequestUri.Should().Be("/api/developer/stories");
        _handler.LastMethod.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task GetStoryAsync_WhenNotFound_Throws()
    {
        // Arrange
        var storyId = Guid.NewGuid();
        _handler.SetResponse(HttpStatusCode.NotFound, new { error = "Not found" });

        // Act
        var act = () => _sut.GetStoryAsync(storyId);

        // Assert
        await act.Should().ThrowAsync<StoryNotFoundException>()
            .Where(ex => ex.StoryId == storyId);
    }

    [Fact]
    public async Task GetStoryAsync_WhenFound_ReturnsStory()
    {
        // Arrange
        var storyId = Guid.NewGuid();
        _handler.SetResponse(HttpStatusCode.OK, new StoryResponse
        {
            Id = storyId,
            Title = "Found Story",
            Status = "Completed"
        });

        // Act
        var result = await _sut.GetStoryAsync(storyId);

        // Assert
        result.Id.Should().Be(storyId);
        result.Title.Should().Be("Found Story");
        _handler.LastRequestUri.Should().Be($"/api/developer/stories/{storyId}");
    }

    [Fact]
    public async Task AnalyzeStoryAsync_CallsCorrectEndpoint()
    {
        // Arrange
        var storyId = Guid.NewGuid();
        _handler.SetResponse(HttpStatusCode.OK, new StoryResponse
        {
            Id = storyId,
            Title = "Story",
            Status = "Analyzing"
        });

        // Act
        var result = await _sut.AnalyzeStoryAsync(storyId);

        // Assert
        result.Status.Should().Be("Analyzing");
        _handler.LastRequestUri.Should().Be($"/api/developer/stories/{storyId}/analyze");
        _handler.LastMethod.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task PlanStoryAsync_CallsCorrectEndpoint()
    {
        // Arrange
        var storyId = Guid.NewGuid();
        _handler.SetResponse(HttpStatusCode.OK, new StoryResponse
        {
            Id = storyId,
            Title = "Story",
            Status = "Planning"
        });

        // Act
        var result = await _sut.PlanStoryAsync(storyId);

        // Assert
        result.Status.Should().Be("Planning");
        _handler.LastRequestUri.Should().Be($"/api/developer/stories/{storyId}/plan");
        _handler.LastMethod.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task RunStoryAsync_CallsCorrectEndpoint()
    {
        // Arrange
        var storyId = Guid.NewGuid();
        _handler.SetResponse(HttpStatusCode.OK, new { storyId, status = "Running" });

        // Act
        await _sut.RunStoryAsync(storyId);

        // Assert
        _handler.LastRequestUri.Should().Be($"/api/developer/stories/{storyId}/run");
        _handler.LastMethod.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task DeleteStoryAsync_CallsCorrectEndpoint()
    {
        // Arrange
        var storyId = Guid.NewGuid();
        _handler.SetResponse(HttpStatusCode.NoContent, null);

        // Act
        await _sut.DeleteStoryAsync(storyId);

        // Assert
        _handler.LastRequestUri.Should().Be($"/api/developer/stories/{storyId}");
        _handler.LastMethod.Should().Be(HttpMethod.Delete);
    }

    [Fact]
    public async Task ApiCall_With500Error_ThrowsAuraApiException()
    {
        // Arrange
        var storyId = Guid.NewGuid();
        _handler.SetResponse(HttpStatusCode.InternalServerError, new { error = "Internal error" });

        // Act
        var act = () => _sut.GetStoryAsync(storyId);

        // Assert
        await act.Should().ThrowAsync<AuraApiException>()
            .Where(ex => ex.StatusCode == 500);
    }

    /// <summary>
    /// Mock HTTP message handler for testing HTTP client.
    /// </summary>
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private HttpStatusCode _statusCode = HttpStatusCode.OK;
        private object? _responseContent;
        private Exception? _exception;

        public string? LastRequestUri { get; private set; }
        public HttpMethod? LastMethod { get; private set; }
        public string? LastRequestBody { get; private set; }

        public void SetResponse(HttpStatusCode statusCode, object? content)
        {
            _statusCode = statusCode;
            _responseContent = content;
            _exception = null;
        }

        public void SetException(Exception exception)
        {
            _exception = exception;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri?.PathAndQuery;
            LastMethod = request.Method;

            if (request.Content is not null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            if (_exception is not null)
            {
                throw _exception;
            }

            var response = new HttpResponseMessage(_statusCode);
            if (_responseContent is not null)
            {
                response.Content = JsonContent.Create(_responseContent, options: new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            }

            return response;
        }
    }
}
