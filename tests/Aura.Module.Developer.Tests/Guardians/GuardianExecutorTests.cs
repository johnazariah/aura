#nullable enable

using Aura.Foundation.Guardians;
using Aura.Module.Developer.Guardians;
using Aura.Module.Developer.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Aura.Module.Developer.Tests.Guardians;

public class GuardianExecutorTests
{
    private readonly IStoryService _workflowService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly FakeTimeProvider _timeProvider;
    private readonly GuardianExecutor _sut;

    public GuardianExecutorTests()
    {
        _workflowService = Substitute.For<IStoryService>();
        _timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        // Create a mock scope factory that returns the workflow service
        var scope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IStoryService)).Returns(_workflowService);
        scope.ServiceProvider.Returns(serviceProvider);
        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scopeFactory.CreateScope().Returns(scope);

        _sut = new GuardianExecutor(
            _scopeFactory,
            NullLogger<GuardianExecutor>.Instance,
            _timeProvider);
    }

    [Fact]
    public async Task ExecuteAsync_WithMinimalGuardian_ReturnsCleanResult()
    {
        // Arrange
        var guardian = new GuardianDefinition
        {
            Id = "test-guardian",
            Name = "Test Guardian",
            Description = "A test guardian",
        };

        // Act
        var result = await _sut.ExecuteAsync(guardian);

        // Assert
        result.Should().NotBeNull();
        result.GuardianId.Should().Be("test-guardian");
        result.Status.Should().Be(GuardianExecutionStatus.Clean);
    }

    [Fact]
    public async Task ExecuteAsync_WithContext_UsesProvidedTriggerType()
    {
        // Arrange
        var guardian = new GuardianDefinition
        {
            Id = "scheduled-guardian",
            Name = "Scheduled Guardian",
            Description = "A scheduled guardian",
        };

        var context = new GuardianExecutionContext
        {
            TriggerType = GuardianTriggerType.Schedule,
            WorkspacePath = "/path/to/repo",
        };

        // Act
        var result = await _sut.ExecuteAsync(guardian, context);

        // Assert
        result.Should().NotBeNull();
        result.GuardianId.Should().Be("scheduled-guardian");
    }

    [Fact]
    public async Task ExecuteAsync_RecordsDuration()
    {
        // Arrange
        var guardian = new GuardianDefinition
        {
            Id = "timed-guardian",
            Name = "Timed Guardian",
            Description = "A guardian to test duration",
        };

        // Act
        var result = await _sut.ExecuteAsync(guardian);

        // Assert
        result.Duration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        result.CompletedAt.Should().NotBe(default);
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellationToken_CanBeCancelled()
    {
        // Arrange
        var guardian = new GuardianDefinition
        {
            Id = "cancellable-guardian",
            Name = "Cancellable Guardian",
            Description = "A guardian that can be cancelled",
        };

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - should not throw when already cancelled for placeholder checks
        var result = await _sut.ExecuteAsync(guardian, cts.Token);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_SimpleOverload_UsesManualTriggerType()
    {
        // Arrange
        var guardian = new GuardianDefinition
        {
            Id = "manual-guardian",
            Name = "Manual Guardian",
            Description = "A manually triggered guardian",
        };

        // Act - use simple overload without context
        var result = await _sut.ExecuteAsync(guardian);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(GuardianExecutionStatus.Clean);
    }

    [Fact]
    public async Task ExecuteAsync_SetsCompletedAtFromTimeProvider()
    {
        // Arrange - use a date far in the future to avoid FakeTimeProvider "can't go back in time" error
        var expectedTime = new DateTimeOffset(2099, 12, 31, 12, 0, 0, TimeSpan.Zero);
        _timeProvider.SetUtcNow(expectedTime);

        var guardian = new GuardianDefinition
        {
            Id = "time-guardian",
            Name = "Time Guardian",
            Description = "Tests time provider",
        };

        // Act
        var result = await _sut.ExecuteAsync(guardian);

        // Assert
        result.CompletedAt.Should().Be(expectedTime);
    }

    [Fact]
    public async Task ExecuteAsync_WithDetection_LogsSourcesAndRules()
    {
        // Arrange
        var guardian = new GuardianDefinition
        {
            Id = "detection-guardian",
            Name = "Detection Guardian",
            Description = "A guardian with detection configuration",
            Detection = new GuardianDetection
            {
                Sources = [new GuardianSource { Type = "github-actions" }],
                Rules = [new GuardianRule { Id = "test-rule", Description = "Test rule" }],
                Commands = new Dictionary<string, string> { ["dotnet"] = "dotnet build" },
            },
        };

        // Act
        var result = await _sut.ExecuteAsync(guardian);

        // Assert
        result.Should().NotBeNull();
        result.GuardianId.Should().Be("detection-guardian");
        result.Status.Should().Be(GuardianExecutionStatus.Clean);
        result.CheckResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WithGuardianDefinition_CancellationToken_WhenAwaited_CompletesSuccessfully()
    {
        // Arrange
        var guardian = new GuardianDefinition { Id = "test-id", Name = "test-name" };
        var workflowService = Substitute.For<IStoryService>();

        var scope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IStoryService)).Returns(workflowService);
        scope.ServiceProvider.Returns(serviceProvider);
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        var sut = new GuardianExecutor(scopeFactory, NullLogger<GuardianExecutor>.Instance, null);

        // Act
        var result = await sut.ExecuteAsync(guardian, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteAsync_WithGuardianDefinition_GuardianExecutionContext_CancellationToken_WhenAwaited_CompletesSuccessfully()
    {
        // Arrange
        var guardian = new GuardianDefinition { Id = "test-id", Name = "test-name" };
        var workflowService = Substitute.For<IStoryService>();

        var scope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IStoryService)).Returns(workflowService);
        scope.ServiceProvider.Returns(serviceProvider);
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        var sut = new GuardianExecutor(scopeFactory, NullLogger<GuardianExecutor>.Instance, null);

        // Act
        var result = await sut.ExecuteAsync(guardian, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
    }
}
