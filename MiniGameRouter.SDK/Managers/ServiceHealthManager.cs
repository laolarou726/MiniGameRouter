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
    private readonly ConcurrentDictionary<long, EndPointRecord> _endPoints = [];
    private readonly object _locker = new();
    private readonly PriorityQueue<EndPointRecord, DateTime> _checkQueue = new ();
    private readonly ILogger _logger;

    private readonly IServerConfigurationProvider _configurationProvider;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public ServiceHealthManager(
        IServerConfigurationProvider configurationProvider,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ServiceHealthManager> logger)
    {
        _configurationProvider = configurationProvider;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    private async Task UpdateServiceHealthAsync(ServiceStatus status, CancellationToken ct)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var healthCheckService = scope.ServiceProvider.GetRequiredService<IHealthCheckService>();
        var maxConcurrency = _configurationProvider.Options.HealthCheckConcurrency ?? 5;

        while (!ct.IsCancellationRequested)
        {
            EndPointRecord? endPoint;
            bool succeeded;

            lock (_locker)
            {
                succeeded = _checkQueue.TryDequeue(out endPoint, out _);
            }

            if (!succeeded || endPoint == null)
            {
                await Task.Delay(1, ct);
                continue;
            }

            if (!_endPoints.ContainsKey(endPoint.Id))
            {
                _logger.LogWarning(
                    "EndPoint with id [{id}] not found.",
                    endPoint.Id);

                continue;
            }

            if (ct.IsCancellationRequested)
                break;

            var health = await healthCheckService.ReportHealthAsync(endPoint.ServiceName, endPoint.EndPoint, status);

            lock (_locker)
            {
                _checkQueue.Enqueue(endPoint, DateTime.UtcNow);
            }

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

            await Task.Delay(TimeSpan.FromSeconds(Random.Shared.NextDouble() * 10d / Math.Ceiling((double)_endPoints.Count / maxConcurrency)), ct);
        }
    }

    public void AddOrUpdateEndPoint(EndPointRecord record)
    {
        _endPoints.AddOrUpdate(record.Id, record, (_, _) => record);

        lock (_locker)
        {
            _checkQueue.Enqueue(record, default);
        }
    }

    public void RemoveEndPoint(long id)
    {
        _endPoints.TryRemove(id, out _);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ServiceHealthManager started.");

        var tasks = new Task[_configurationProvider.Options.HealthCheckConcurrency ?? 5];

        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = UpdateServiceHealthAsync(ServiceStatus.Green, stoppingToken);
        }

        await Task.WhenAll(tasks).WaitAsync(stoppingToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ServiceHealthManager stopped.");

        return base.StopAsync(cancellationToken);
    }
}