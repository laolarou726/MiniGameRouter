using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MiniGameRouter.SDK.Interfaces;
using MiniGameRouter.SDK.Models;
using MiniGameRouter.Shared.Models;

namespace MiniGameRouter.SDK.Managers;

public class ServiceHealthManager : BackgroundService
{
    private readonly ConcurrentDictionary<Guid, EndPointRecord> _endPoints = [];
    private readonly Channel<EndPointRecord> _checkChannel = Channel.CreateUnbounded<EndPointRecord>();
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
            var canRead = await _checkChannel.Reader.WaitToReadAsync(ct);

            if (!canRead) break;

            var endPoint = await _checkChannel.Reader.ReadAsync(ct);

            if (!_endPoints.ContainsKey(endPoint.Id))
            {
                _logger.LogWarning(
                    "EndPoint with id [{id}] not found. Remaining count {count}.",
                    endPoint.Id,
                    _checkChannel.Reader.Count);

                await Task.Delay(100, ct);
                continue;
            }

            if (ct.IsCancellationRequested)
                break;

            var health = await healthCheckService.ReportHealthAsync(endPoint.ServiceName, endPoint.EndPoint, status);

            var canWrite = await _checkChannel.Writer.WaitToWriteAsync(ct);

            if (!canWrite) break;

            await _checkChannel.Writer.WriteAsync(endPoint, ct);

            if (!health)
            {
                _logger.LogWarning(
                    "HealthCheck for [{service}] at [{endPoint}] failed.",
                    endPoint.ServiceName,
                    endPoint.EndPoint);

                continue;
            }

            _logger.LogInformation(
                "HealthCheck for [{service}] at [{endPoint}] is [{status}], remaining count [{count}].",
                endPoint.ServiceName,
                endPoint.EndPoint,
                health ? "Success" : "Failed",
                _checkChannel.Reader.Count);

            await Task.Delay(TimeSpan.FromSeconds(Random.Shared.NextDouble() * 10d / Math.Ceiling((double)_endPoints.Count / maxConcurrency)), ct);
        }
    }

    public async Task AddOrUpdateEndPointAsync(EndPointRecord record, CancellationToken ct)
    {
        _endPoints.AddOrUpdate(record.Id, record, (_, _) => record);

        var canWrite = await _checkChannel.Writer.WaitToWriteAsync(ct);

        if (!canWrite)
        {
            _logger.LogWarning("Failed to write to check channel.");
            return;
        }

        await _checkChannel.Writer.WriteAsync(record, ct);
    }

    public void RemoveEndPoint(Guid id)
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
        _checkChannel.Writer.Complete();

        _logger.LogInformation("ServiceHealthManager stopped.");

        return base.StopAsync(cancellationToken);
    }
}