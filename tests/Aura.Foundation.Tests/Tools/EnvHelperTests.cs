using Aura.Foundation.Tools;
using Xunit;

namespace Aura.Foundation.Tests.Tools;

public class EnvHelperTests : IDisposable
{
    private const string TestKey = "AURA_TEST_ENV_VAR";
    private readonly string? _originalValue;

    public EnvHelperTests()
    {
        // Save original value to restore later
        _originalValue = Environment.GetEnvironmentVariable(TestKey);
    }

    public void Dispose()
    {
        // Restore original value
        if (_originalValue is null)
        {
            Environment.SetEnvironmentVariable(TestKey, null);
        }
        else
        {
            Environment.SetEnvironmentVariable(TestKey, _originalValue);
        }
    }

    [Fact]
    public void GetOrDefault_ReturnsValue_WhenEnvironmentVariableIsSet()
    {
        // Arrange
        const string expectedValue = "test-value";
        Environment.SetEnvironmentVariable(TestKey, expectedValue);

        // Act
        var result = EnvHelper.GetOrDefault(TestKey, "default-value");

        // Assert
        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public void GetOrDefault_ReturnsDefaultValue_WhenEnvironmentVariableIsNotSet()
    {
        // Arrange
        const string defaultValue = "default-value";
        Environment.SetEnvironmentVariable(TestKey, null);

        // Act
        var result = EnvHelper.GetOrDefault(TestKey, defaultValue);

        // Assert
        Assert.Equal(defaultValue, result);
    }

    [Fact]
    public void GetOrDefault_ReturnsDefaultValue_WhenEnvironmentVariableIsEmpty()
    {
        // Arrange
        const string defaultValue = "default-value";
        Environment.SetEnvironmentVariable(TestKey, string.Empty);

        // Act
        var result = EnvHelper.GetOrDefault(TestKey, defaultValue);

        // Assert
        Assert.Equal(defaultValue, result);
    }

    [Fact]
    public void RequireEnv_ReturnsValue_WhenEnvironmentVariableIsSet()
    {
        // Arrange
        const string expectedValue = "required-value";
        Environment.SetEnvironmentVariable(TestKey, expectedValue);

        // Act
        var result = EnvHelper.RequireEnv(TestKey);

        // Assert
        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public void RequireEnv_ThrowsInvalidOperationException_WhenEnvironmentVariableIsNotSet()
    {
        // Arrange
        Environment.SetEnvironmentVariable(TestKey, null);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => EnvHelper.RequireEnv(TestKey));
        Assert.Contains(TestKey, exception.Message);
        Assert.Contains("Required environment variable", exception.Message);
    }

    [Fact]
    public void RequireEnv_ThrowsInvalidOperationException_WhenEnvironmentVariableIsEmpty()
    {
        // Arrange
        Environment.SetEnvironmentVariable(TestKey, string.Empty);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => EnvHelper.RequireEnv(TestKey));
        Assert.Contains(TestKey, exception.Message);
        Assert.Contains("Required environment variable", exception.Message);
    }

    [Fact]
    public void RequireEnv_ThrowsInvalidOperationException_WhenEnvironmentVariableIsWhitespace()
    {
        // Arrange
        Environment.SetEnvironmentVariable(TestKey, "   ");

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => EnvHelper.RequireEnv(TestKey));
        Assert.Contains(TestKey, exception.Message);
        Assert.Contains("Required environment variable", exception.Message);
    }
}
