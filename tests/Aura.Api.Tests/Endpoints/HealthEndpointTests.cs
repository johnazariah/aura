// <copyright file="HealthEndpointTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Tests.Endpoints;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

/// <summary>
/// Integration tests for health check endpoints.
/// </summary>
public class HealthEndpointTests : IClassFixture<AuraApiFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public HealthEndpointTests(AuraApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<HealthResponse>(JsonOptions);
        result.Should().NotBeNull();
        result!.Healthy.Should().BeTrue();
        result.Status.Should().Be("healthy");
    }

    [Fact]
    public async Task HealthDb_ReturnsDbStatus()
    {
        // Act
        var response = await _client.GetAsync("/health/db");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<HealthResponse>(JsonOptions);
        result.Should().NotBeNull();
        // In-memory database should be healthy
        result!.Healthy.Should().BeTrue();
    }

    /// <summary>
    /// Response model for health endpoints.
    /// </summary>
    private record HealthResponse(
        string? Status,
        bool Healthy,
        string? Details,
        DateTime Timestamp);
}
