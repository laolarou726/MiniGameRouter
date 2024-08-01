using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Caching.Distributed;
using MiniGameRouter.Helper;
using MiniGameRouter.Models;
using MiniGameRouter.SDK.Models;
using MiniGameRouter.Shared.Models;

namespace MiniGameRouter.Services;

public sealed class HealthCheckService : BackgroundService
{
    public class HealthCheckEntry(string serviceName, string endPoint)
    {
        private const int MaxHistoryCount = 10;
        
        private readonly byte[] _history = new byte[3];
        
        public string ServiceName { get; } = serviceName;
        public string EndPoint { get; } = endPoint;
        public ServiceStatus AverageStatus { get; private set; }
        public DateTime LastCheckUtc { get; private set; }
        public Queue<HealthCheckStatus> CheckHistories { get; } = new (MaxHistoryCount);

        private void ComputeAverageStatus()
        {
            double total = _history[0] + _history[1] * 2 + _history[2] * 3;
            var average = total / MaxHistoryCount;
            
            AverageStatus = average switch
            {
                < 1.5 => ServiceStatus.Red,
                < 2.5 => ServiceStatus.Yellow,
                _ => ServiceStatus.Green
            };
        }
        
        private void AddCheck(HealthCheckStatus status)
        {
            CheckHistories.Enqueue(status);
            if (CheckHistories.Count > MaxHistoryCount)
            {
                var oldest = CheckHistories.Dequeue();

                _history[(int)oldest.Status - 1]--;
                ComputeAverageStatus();
            }

            _history[(int)status.Status - 1]++;
        }

        public async Task AddCheckAsync(
            HealthCheckStatus status,
            IDistributedCache cache)
        {
            AddCheck(status);

            if (CheckHistories.Count < MaxHistoryCount) return;

            await cache.SetAsync(ServiceName, AverageStatus);
        }
    }

    private readonly TimeSpan _checkTimeout = TimeSpan.FromSeconds(15);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConcurrentDictionary<string, HealthCheckEntry> _entries = new ();
    
    public HealthCheckService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }
    
    public static string GetServiceName(string serviceName, string endPoint) =>
        $"SERVICE_STATUS_{serviceName}_{endPoint}";
    
    public static string GetServiceName(HealthCheckRequestModel model) =>
        GetServiceName(model.ServiceName, model.EndPoint);
    
    public static string GetServiceName(EndPointRecord record) =>
        GetServiceName(record.ServiceName, record.EndPoint);
    
    public static string GetServiceName(EndPointMappingModel model) =>
        GetServiceName(model.ServiceName, model.TargetEndPoint);
    
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
    
    public bool RemoveEntry(string serviceName) => _entries.TryRemove(serviceName, out _);
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
        
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var entry in _entries.Values)
            {
                if (entry.LastCheckUtc.Add(_checkTimeout) >= DateTime.UtcNow) continue;
                
                var status = new HealthCheckStatus
                {
                    Status = ServiceStatus.Red,
                    CheckTime = DateTime.UtcNow
                };
                
                await entry.AddCheckAsync(status, cache);
            }
            
            await Task.Delay(_checkTimeout, stoppingToken);
        }
    }
}