// <copyright file="GuardianScheduler.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Guardians;

using Aura.Foundation.Guardians;
using Cronos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Background service that schedules guardian checks based on cron triggers.
/// </summary>
public sealed class GuardianScheduler : BackgroundService
{
    private readonly IGuardianRegistry _registry;
    private readonly IGuardianExecutor _executor;
    private readonly ILogger<GuardianScheduler> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, (CronExpression Cron, DateTimeOffset NextRun)> _schedules = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="GuardianScheduler"/> class.
    /// </summary>
    /// <param name="registry">Guardian registry.</param>
    /// <param name="executor">Guardian executor.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="timeProvider">Time provider for testability.</param>
    public GuardianScheduler(
        IGuardianRegistry registry,
        IGuardianExecutor executor,
        ILogger<GuardianScheduler> logger,
        TimeProvider? timeProvider = null)
    {
        _registry = registry;
        _executor = executor;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;

        _registry.GuardiansChanged += OnGuardiansChanged;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Guardian scheduler starting");

        // Initial schedule build
        RebuildSchedules();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = _timeProvider.GetUtcNow();
                var guardiansToRun = GetGuardiansDueForExecution(now);

                foreach (var guardianId in guardiansToRun)
                {
                    var guardian = _registry.GetGuardian(guardianId);
                    if (guardian is null)
                    {
                        continue;
                    }

                    _logger.LogInformation("Executing scheduled guardian: {GuardianId}", guardianId);

                    try
                    {
                        await _executor.ExecuteAsync(guardian, stoppingToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Guardian execution failed: {GuardianId}", guardianId);
                    }

                    // Update next run time
                    UpdateNextRun(guardianId, now);
                }

                // Sleep until next check (every minute)
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in guardian scheduler loop");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);
            }
        }

        _logger.LogInformation("Guardian scheduler stopped");
    }

    private void OnGuardiansChanged(object? sender, GuardianRegistryChangedEventArgs e)
    {
        _logger.LogDebug("Guardian registry changed, rebuilding schedules");
        RebuildSchedules();
    }

    private void RebuildSchedules()
    {
        _schedules.Clear();

        foreach (var guardian in _registry.Guardians)
        {
            foreach (var trigger in guardian.Triggers)
            {
                if (trigger.Type == GuardianTriggerType.Schedule && !string.IsNullOrEmpty(trigger.Cron))
                {
                    try
                    {
                        var cron = CronExpression.Parse(trigger.Cron);
                        var nextRun = cron.GetNextOccurrence(_timeProvider.GetUtcNow().UtcDateTime);

                        if (nextRun.HasValue)
                        {
                            _schedules[guardian.Id] = (cron, new DateTimeOffset(nextRun.Value, TimeSpan.Zero));
                            _logger.LogDebug(
                                "Scheduled guardian {GuardianId} next run: {NextRun}",
                                guardian.Id,
                                nextRun.Value);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Invalid cron expression for guardian {GuardianId}: {Cron}", guardian.Id, trigger.Cron);
                    }
                }
            }
        }

        _logger.LogInformation("Built schedules for {Count} guardians", _schedules.Count);
    }

    private List<string> GetGuardiansDueForExecution(DateTimeOffset now)
    {
        return _schedules
            .Where(kvp => kvp.Value.NextRun <= now)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    private void UpdateNextRun(string guardianId, DateTimeOffset fromTime)
    {
        if (_schedules.TryGetValue(guardianId, out var schedule))
        {
            var nextRun = schedule.Cron.GetNextOccurrence(fromTime.UtcDateTime);
            if (nextRun.HasValue)
            {
                _schedules[guardianId] = (schedule.Cron, new DateTimeOffset(nextRun.Value, TimeSpan.Zero));
                _logger.LogDebug("Guardian {GuardianId} next run: {NextRun}", guardianId, nextRun.Value);
            }
            else
            {
                _schedules.Remove(guardianId);
            }
        }
    }
}
