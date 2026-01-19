// <copyright file="GitHubServiceTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.GitHub.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

public class GitHubServiceTests
{
    private readonly GitHubOptions _options = new() { Token = "test-token" };

    private GitHubService CreateService(HttpMessageHandler? handler = null)
    {
        handler ??= CreateMockHandler(HttpStatusCode.OK, new { });
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.github.com")
        };

        return new GitHubService(
            httpClient,
            Options.Create(_options),
            NullLogger<GitHubService>.Instance);
    }

    private static MockHttpHandler CreateMockHandler(HttpStatusCode statusCode, object content)
    {
        return new MockHttpHandler(statusCode, content);
    }

    private class MockHttpHandler(HttpStatusCode statusCode, object content) : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode = statusCode;
        private readonly object _content = content;

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
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

    [Fact]
    public async Task GetIssueAsync_WithValidParams_ReturnsIssue()
    {
        // Arrange
        var expectedIssue = new GitHubIssue
        {
            Number = 42,
            Title = "Test Issue",
            State = "open",
            HtmlUrl = "https://github.com/owner/repo/issues/42",
            Body = "Issue body",
            CreatedAt = DateTimeOffset.UtcNow
        };
        var handler = CreateMockHandler(HttpStatusCode.OK, expectedIssue);
        var sut = CreateService(handler);

        // Act
        var result = await sut.GetIssueAsync("owner", "repo", 42);

        // Assert
        result.Should().NotBeNull();
        result.Number.Should().Be(42);
        result.Title.Should().Be("Test Issue");
        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/repos/owner/repo/issues/42");
    }

    [Fact]
    public void ParseIssueUrl_WithValidGitHubUrl_ReturnsOwnerRepoAndNumber()
    {
        // Arrange
        var url = "https://github.com/owner/repo/issues/42";
        var sut = CreateService();

        // Act
        var result = sut.ParseIssueUrl(url);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Owner.Should().Be("owner");
        result.Value.Repo.Should().Be("repo");
        result.Value.Number.Should().Be(42);
    }

    [Fact]
    public async Task PostCommentAsync_WithValidParams_PostsComment()
    {
        // Arrange
        var handler = CreateMockHandler(HttpStatusCode.OK, new { });
        var sut = CreateService(handler);
        var comment = "Test comment body";

        // Act
        await sut.PostCommentAsync("owner", "repo", 42, comment);

        // Assert
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/repos/owner/repo/issues/42/comments");
        var requestBody = await handler.LastRequest.Content!.ReadAsStringAsync();
        requestBody.Should().Contain(comment);
    }

    [Fact]
    public async Task CloseIssueAsync_WithValidParams_ClosesIssue()
    {
        // Arrange
        var handler = CreateMockHandler(HttpStatusCode.OK, new { });
        var sut = CreateService(handler);

        // Act
        await sut.CloseIssueAsync("owner", "repo", 42);

        // Assert
        handler.LastRequest!.Method.Should().Be(HttpMethod.Patch);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/repos/owner/repo/issues/42");
        var requestBody = await handler.LastRequest.Content!.ReadAsStringAsync();
        requestBody.Should().Contain("\"state\":\"closed\"");
    }

    [Fact]
    public async Task ListWorkflowsAsync_WithValidParams_ReturnsWorkflows()
    {
        // Arrange
        var workflows = new
        {
            workflows = new[]
            {
                new { id = 1L, name = "CI", path = ".github/workflows/ci.yml", state = "active" },
                new { id = 2L, name = "Build", path = ".github/workflows/build.yml", state = "active" }
            }
        };
        var handler = CreateMockHandler(HttpStatusCode.OK, workflows);
        var sut = CreateService(handler);

        // Act
        var result = await sut.ListWorkflowsAsync("owner", "repo");

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/repos/owner/repo/actions/workflows");
    }

    [Fact]
    public async Task ListWorkflowRunsAsync_WithWorkflowId_ReturnsRuns()
    {
        // Arrange
        var runs = new
        {
            workflow_runs = new[]
            {
                new { id = 100L, status = "completed", head_sha = "abc123", @event = "push" }
            }
        };
        var handler = CreateMockHandler(HttpStatusCode.OK, runs);
        var sut = CreateService(handler);

        // Act
        var result = await sut.ListWorkflowRunsAsync("owner", "repo", "build.yml");

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Contain("/actions/workflows/build.yml/runs");
    }

    [Fact]
    public async Task GetWorkflowRunAsync_WithValidId_ReturnsRun()
    {
        // Arrange
        var run = new { id = 100L, status = "completed", conclusion = "success", head_sha = "abc123", @event = "push" };
        var handler = CreateMockHandler(HttpStatusCode.OK, run);
        var sut = CreateService(handler);

        // Act
        var result = await sut.GetWorkflowRunAsync("owner", "repo", 100);

        // Assert
        result.Should().NotBeNull();
        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/repos/owner/repo/actions/runs/100");
    }

    [Fact]
    public async Task ListJobsAsync_WithValidRunId_ReturnsJobs()
    {
        // Arrange
        var jobs = new { jobs = new[] { new { id = 200, name = "build", status = "completed" } } };
        var handler = CreateMockHandler(HttpStatusCode.OK, jobs);
        var sut = CreateService(handler);

        // Act
        var result = await sut.ListJobsAsync("owner", "repo", 100);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/repos/owner/repo/actions/runs/100/jobs");
    }

    [Fact]
    public async Task TriggerWorkflowAsync_WithValidParams_TriggersWorkflow()
    {
        // Arrange
        var handler = CreateMockHandler(HttpStatusCode.OK, new { });
        var sut = CreateService(handler);

        // Act
        await sut.TriggerWorkflowAsync("owner", "repo", "build.yml", "main");

        // Assert
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Contain("/actions/workflows/build.yml/dispatches");
        var requestBody = await handler.LastRequest.Content!.ReadAsStringAsync();
        requestBody.Should().Contain("\"ref\":\"main\"");
    }

    [Fact]
    public async Task RerunWorkflowAsync_WithValidRunId_RerunsWorkflow()
    {
        // Arrange
        var handler = CreateMockHandler(HttpStatusCode.OK, new { });
        var sut = CreateService(handler);

        // Act
        await sut.RerunWorkflowAsync("owner", "repo", 100);

        // Assert
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/repos/owner/repo/actions/runs/100/rerun");
    }

    [Fact]
    public async Task CancelWorkflowRunAsync_WithValidRunId_CancelsRun()
    {
        // Arrange
        var handler = CreateMockHandler(HttpStatusCode.OK, new { });
        var sut = CreateService(handler);

        // Act
        await sut.CancelWorkflowRunAsync("owner", "repo", 100);

        // Assert
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/repos/owner/repo/actions/runs/100/cancel");
    }

    [Fact]
    public void ParseIssueUrl_WithInvalidUrl_ReturnsNull()
    {
        // Arrange
        var invalidUrl = "https://example.com/not/github";
        var sut = CreateService();

        // Act
        var result = sut.ParseIssueUrl(invalidUrl);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetIssueAsync_WithNonExistentIssue_ThrowsHttpRequestException()
    {
        // Arrange
        var handler = CreateMockHandler(HttpStatusCode.NotFound, new { message = "Not Found" });
        var sut = CreateService(handler);

        // Act
        var act = async () => await sut.GetIssueAsync("owner", "repo", 99999);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public void ParseIssueUrl_WithRealWorldUrl_ExtractsCorrectParts()
    {
        // Arrange
        var sut = CreateService();

        // Act
        var result = sut.ParseIssueUrl("https://github.com/microsoft/vscode/issues/1234");

        // Assert
        result.Should().NotBeNull();
        result!.Value.Owner.Should().Be("microsoft");
        result.Value.Repo.Should().Be("vscode");
        result.Value.Number.Should().Be(1234);
    }

    [Fact]
    public void ParseIssueUrl_WithPullRequestUrl_ReturnsNull()
    {
        // Arrange
        var sut = CreateService();

        // Act
        var result = sut.ParseIssueUrl("https://github.com/owner/repo/pull/123");

        // Assert - PR URLs should not match issue pattern
        result.Should().BeNull();
    }

    [Fact]
    public async Task ListWorkflowsAsync_WithUnauthorized_ThrowsHttpRequestException()
    {
        // Arrange
        var handler = CreateMockHandler(HttpStatusCode.Unauthorized, new { message = "Bad credentials" });
        var sut = CreateService(handler);

        // Act
        var act = async () => await sut.ListWorkflowsAsync("owner", "repo");

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task TriggerWorkflowAsync_WithInputs_IncludesInputsInRequest()
    {
        // Arrange
        var handler = CreateMockHandler(HttpStatusCode.OK, new { });
        var sut = CreateService(handler);

        // Act
        await sut.TriggerWorkflowAsync("owner", "repo", "deploy.yml", "develop", new Dictionary<string, string>
        {
            ["environment"] = "staging",
            ["version"] = "1.0.0"
        });

        // Assert
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        var requestBody = await handler.LastRequest.Content!.ReadAsStringAsync();
        requestBody.Should().Contain("\"environment\":\"staging\"");
        requestBody.Should().Contain("\"version\":\"1.0.0\"");
    }

    [Fact]
    public async Task ListJobsAsync_WithMultipleJobs_ReturnsAllJobs()
    {
        // Arrange
        var jobs = new
        {
            jobs = new object[]
            {
                new { id = 200L, name = "build", status = "completed", conclusion = "success" },
                new { id = 201L, name = "test", status = "completed", conclusion = "success" },
                new { id = 202L, name = "deploy", status = "queued", conclusion = "pending" }
            }
        };
        var handler = CreateMockHandler(HttpStatusCode.OK, jobs);
        var sut = CreateService(handler);

        // Act
        var result = await sut.ListJobsAsync("owner", "repo", 100);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task ListWorkflowRunsAsync_WithNoRuns_ReturnsEmptyList()
    {
        // Arrange
        var runs = new { workflow_runs = Array.Empty<object>() };
        var handler = CreateMockHandler(HttpStatusCode.OK, runs);
        var sut = CreateService(handler);

        // Act
        var result = await sut.ListWorkflowRunsAsync("owner", "repo", null);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetWorkflowRunLogsAsync_WithValidRun_ReturnsFormattedLogs()
    {
        // Arrange
        var jobs = new
        {
            jobs = new object[]
            {
        new
        {
            id = 200L,
            run_id = 100L,
            name = "build",
            status = "completed",
            conclusion = "success",
            steps = new object[]
            {
                new { number = 1, name = "Checkout", status = "completed", conclusion = "success" },
                new { number = 2, name = "Build", status = "completed", conclusion = "success" }
            }
        }
            }
        };
        var handler = CreateMockHandler(HttpStatusCode.OK, jobs);
        var sut = CreateService(handler);

        // Act
        var result = await sut.GetWorkflowRunLogsAsync("owner", "repo", 100);

        // Assert
        result.Should().Contain("Workflow Run 100");
        result.Should().Contain("Job: build");
        result.Should().Contain("Checkout");
        result.Should().Contain("Build");
    }

    [Fact]
    public void IsConfigured_WithValidToken_ReturnsTrue()
    {
        // Arrange
        var sut = CreateService();

        // Act
        var isConfigured = sut.IsConfigured;

        // Assert
        isConfigured.Should().BeTrue();
    }

    [Fact]
    public void IsConfigured_WithEmptyToken_ReturnsFalse()
    {
        // Arrange
        var emptyOptions = new GitHubOptions { Token = string.Empty };
        var httpClient = new HttpClient(CreateMockHandler(HttpStatusCode.OK, new { }))
        {
            BaseAddress = new Uri("https://api.github.com")
        };
        var sut = new GitHubService(httpClient, Options.Create(emptyOptions), NullLogger<GitHubService>.Instance);

        // Act
        var isConfigured = sut.IsConfigured;

        // Assert
        isConfigured.Should().BeFalse();
    }
}
