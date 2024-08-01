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
    
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger _logger;
    
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
        var endPointService = scope.ServiceProvider.GetRequiredService<IHealthCheckService>();
        
        foreach (var endPoint in _endPoints.Values)
        {
            if (ct.IsCancellationRequested)
                break;
            
            var health = await endPointService.ReportHealthAsync(endPoint.ServiceName, endPoint.EndPoint, status);
            
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
            await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(9, 14)), stoppingToken);
        }
    }
}