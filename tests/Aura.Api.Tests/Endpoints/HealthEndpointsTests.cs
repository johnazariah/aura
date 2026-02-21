// <copyright file="HealthEndpointsTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Tests.Endpoints;

using Aura.Api.Endpoints;
using FluentAssertions;
using Xunit;

public class HealthEndpointsTests
{
    [Fact]
    public void GetHealth_ReturnsCorrectJsonStructure()
    {
        // Arrange
        var serverStartTime = new DateTime(2026, 2, 7, 9, 12, 51, DateTimeKind.Utc);
        var deploymentTag = "v1.3.1-abc1234";

        // Act
        var result = InvokeGetHealth(serverStartTime, deploymentTag);

        // Assert
        result.Should().NotBeNull();
        var status = GetPropertyValue(result, "status");
        var startedAt = GetPropertyValue(result, "startedAt");
        var deployTag = GetPropertyValue(result, "deployTag");

        status.Should().Be("healthy");
        startedAt.Should().Be("2026-02-07T09:12:51Z");
        deployTag.Should().Be("v1.3.1-abc1234");
    }

    [Fact]
    public void GetHealth_WithDifferentServerStartTime_ReturnsCorrectTimestamp()
    {
        // Arrange
        var serverStartTime = new DateTime(2025, 12, 25, 14, 30, 0, DateTimeKind.Utc);
        var deploymentTag = "v2.0.0";

        // Act
        var result = InvokeGetHealth(serverStartTime, deploymentTag);

        // Assert
        var startedAt = GetPropertyValue(result, "startedAt");
        startedAt.Should().Be("2025-12-25T14:30:00Z");
    }

    [Fact]
    public void GetHealth_WithEmptyDeployTag_ReturnsEmptyString()
    {
        // Arrange
        var serverStartTime = DateTime.UtcNow;
        var deploymentTag = string.Empty;

        // Act
        var result = InvokeGetHealth(serverStartTime, deploymentTag);

        // Assert
        var deployTag = GetPropertyValue(result, "deployTag");
        deployTag.Should().Be(string.Empty);
    }

    [Fact]
    public void GetHealth_StatusAlwaysHealthy()
    {
        // Arrange
        var serverStartTime = DateTime.UtcNow;
        var deploymentTag = "test-tag";

        // Act
        var result = InvokeGetHealth(serverStartTime, deploymentTag);

        // Assert
        var status = GetPropertyValue(result, "status");
        status.Should().Be("healthy");
    }

    [Fact]
    public void GetHealth_TimestampFormattedAsISO8601()
    {
        // Arrange
        var serverStartTime = new DateTime(2026, 1, 15, 8, 45, 33, DateTimeKind.Utc);
        var deploymentTag = "v1.0.0";

        // Act
        var result = InvokeGetHealth(serverStartTime, deploymentTag);

        // Assert
        var startedAt = GetPropertyValue(result, "startedAt");
        var timestampString = startedAt.ToString();
        timestampString.Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$");
    }

    private static object InvokeGetHealth(DateTime serverStartTime, string deploymentTag)
    {
        var method = typeof(HealthEndpoints).GetMethod(
            "GetHealth",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        if (method == null)
        {
            throw new InvalidOperationException("GetHealth method not found");
        }

        var result = method.Invoke(null, new object[] { serverStartTime, deploymentTag });
        return result ?? throw new InvalidOperationException("GetHealth returned null");
    }

    private static object GetPropertyValue(object obj, string propertyName)
    {
        var type = obj.GetType();
        var property = type.GetProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException($"Property {propertyName} not found");
        }

        return property.GetValue(obj) ?? throw new InvalidOperationException($"Property {propertyName} is null");
    }
}
