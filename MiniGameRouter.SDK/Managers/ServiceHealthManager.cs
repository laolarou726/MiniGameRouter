using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MiniGameRouter.SDK.Interfaces;
using MiniGameRouter.SDK.Models;
using MiniGameRouter.Shared.Models;

namespace MiniGameRouter.SDK.Managers;

public class ServiceHealthManager : BackgroundService
{
    private readonly ConcurrentDictionary<Guid, EndPointRecord> _endPoints = new();
    private readonly ILogger _logger;

    private readonly IServiceScopeFactory _serviceScopeFactory;

    public ServiceHealthManager(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ServiceHealthManager> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    private async Task UpdateServiceHealthAsync(ServiceStatus status, CancellationToken ct)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var healthCheckService = scope.ServiceProvider.GetRequiredService<IHealthCheckService>();

        foreach (var endPoint in _endPoints.Values)
        {
            if (ct.IsCancellationRequested)
                break;

            var health = await healthCheckService.ReportHealthAsync(endPoint.ServiceName, endPoint.EndPoint, status);

            if (!health)
            {
                _logger.LogWarning(
                    "HealthCheck for [{service}] at [{endPoint}] failed.",
                    endPoint.ServiceName,
                    endPoint.EndPoint);

                continue;
            }

            _logger.LogInformation(
                "HealthCheck for [{service}] at [{endPoint}] is [{status}].",
                endPoint.ServiceName,
                endPoint.EndPoint,
                health ? "Success" : "Failed");
        }
    }

    public void AddOrUpdateEndPoint(EndPointRecord record)
    {
        _endPoints.AddOrUpdate(record.Id, record, (_, _) => record);
    }

    public void RemoveEndPoint(Guid id)
    {
        _endPoints.TryRemove(id, out _);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await UpdateServiceHealthAsync(ServiceStatus.Green, stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(5, 10)), stoppingToken);
        }
    }
}