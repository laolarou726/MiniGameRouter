using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Caching.Distributed;
using MiniGameRouter.Models;
using MiniGameRouter.SDK.Models;
using MiniGameRouter.Shared.Models;
using Prometheus;

namespace MiniGameRouter.Services;

public sealed class HealthCheckService : BackgroundService
{
    private static readonly Gauge CurrentHealthyServicesCount = Metrics.CreateGauge(
        "minigame_router_healthy_services_count",
        "Current healthy services count");

    private static readonly Gauge CurrentUnhealthyServicesCount = Metrics.CreateGauge(
        "minigame_router_unhealthy_services_count",
        "Current unhealthy services count");

    private static readonly Gauge ServiceStatusCount = Metrics.CreateGauge(
        "minigame_router_service_status_count",
        "Current service status count",
        new GaugeConfiguration
        {
            LabelNames = ["status"]
        });

    private readonly TimeSpan _checkTimeout = TimeSpan.FromSeconds(20);
    private readonly ConcurrentDictionary<string, HealthCheckEntry> _entries = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;

    public HealthCheckService(
        IServiceScopeFactory scopeFactory,
        ILogger<HealthCheckService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public static string GetServiceName(string serviceName, string endPoint)
    {
        return $"SERVICE_STATUS_{serviceName}_{endPoint}";
    }

    public static string GetServiceName(HealthCheckRequestModel model)
    {
        return GetServiceName(model.ServiceName, model.EndPoint);
    }

    public static string GetServiceName(EndPointRecord record)
    {
        return GetServiceName(record.ServiceName, record.EndPoint);
    }

    public static string GetServiceName(EndPointMappingModel model)
    {
        return GetServiceName(model.ServiceName, model.TargetEndPoint);
    }

    public FrozenDictionary<string, HealthCheckEntry> GetEntries()
    {
        return _entries.ToFrozenDictionary();
    }

    public bool TryGetStatus(
        HealthCheckRequestModel model,
        [NotNullWhen(true)] out HealthCheckEntry? entry)
    {
        return _entries.TryGetValue(GetServiceName(model), out entry);
    }

    public async Task AddCheckAsync(
        HealthCheckRequestModel reqModel,
        HealthCheckStatus status,
        IDistributedCache cache)
    {
        var serviceName = GetServiceName(reqModel);
        var entry = _entries.GetOrAdd(serviceName, _ => new HealthCheckEntry(serviceName, reqModel.EndPoint));
        await entry.AddCheckAsync(status, cache);
    }

    public async Task RemoveEntryAsync(string serviceName, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();

        if (_entries.TryRemove(serviceName, out var entry))
        {
            await cache.RemoveAsync(entry.ServiceName, ct);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();

        while (!stoppingToken.IsCancellationRequested)
        {
            var greenCount = 0;
            var yellowCount = 0;
            var redCount = 0;

            foreach (var entry in _entries.Values)
            {
                switch (entry.AverageStatus)
                {
                    case ServiceStatus.Green:
                        greenCount++;
                        break;
                    case ServiceStatus.Yellow:
                        yellowCount++;
                        break;
                    case ServiceStatus.Red:
                        redCount++;
                        break;
                }

                if (entry.LastCheckUtc.Add(_checkTimeout) >= DateTime.UtcNow) continue;
                if (DateTime.UtcNow - entry.LastCheckUtc >= TimeSpan.FromMinutes(10))
                {
                    _logger.LogWarning(
                        "Service {ServiceName} is not responding for a long time, removing it from health check",
                        entry.ServiceName);

                    await RemoveEntryAsync(entry.ServiceName, stoppingToken);
                    continue;
                }

                var status = new HealthCheckStatus
                {
                    Status = ServiceStatus.Red,
                    CheckTime = entry.LastCheckUtc
                };

                await entry.AddCheckAsync(status, cache);
            }

            CurrentHealthyServicesCount.Set(greenCount + yellowCount);
            CurrentUnhealthyServicesCount.Set(redCount);

            ServiceStatusCount.WithLabels("green").Set(greenCount);
            ServiceStatusCount.WithLabels("yellow").Set(yellowCount);
            ServiceStatusCount.WithLabels("red").Set(redCount);

            await Task.Delay(_checkTimeout, stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("HealthCheckService stopping, waiting for 5 sec to fully collect...");

        await Task.Delay(5000, CancellationToken.None);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();

        foreach (var entry in _entries.Values)
        {
            await cache.RemoveAsync(entry.ServiceName, CancellationToken.None);

            _logger.LogInformation(
                "Service [{service}] status [{status}] removed from cache.",
                entry.ServiceName,
                entry.AverageStatus);
        }

        _logger.LogInformation("HealthCheckService stopped.");
    }
}