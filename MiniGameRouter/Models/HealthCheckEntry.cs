using Microsoft.Extensions.Caching.Distributed;
using MiniGameRouter.Helper;
using MiniGameRouter.SDK.Models;

namespace MiniGameRouter.Models;

public class HealthCheckEntry(string serviceName, string endPoint)
{
    private const int MaxHistoryCount = 10;

    private readonly byte[] _history = new byte[3];

    public string ServiceName { get; } = serviceName;
    public string EndPoint { get; } = endPoint;
    public ServiceStatus AverageStatus { get; private set; }
    public DateTime LastCheckUtc { get; private set; }
    public Queue<HealthCheckStatus> CheckHistories { get; } = new(MaxHistoryCount);

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
        LastCheckUtc = DateTime.UtcNow;

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

        await cache.SetAsync(ServiceName, AverageStatus, new DistributedCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(5)
        });
    }
}