using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Distributed;
using MiniGameRouter.Helper;
using MiniGameRouter.SDK.Models;

namespace MiniGameRouter.Models;

public class HealthCheckEntry(string serviceName, string endPoint)
{
    private const int MaxHistoryCount = 10;

    private long _greenCount;
    private long _yellowCount;
    private long _redCount;

    public string ServiceName { get; } = serviceName;
    public string EndPoint { get; } = endPoint;
    public ServiceStatus AverageStatus { get; private set; }
    public DateTime LastCheckUtc { get; private set; }
    public ConcurrentQueue<HealthCheckStatus> CheckHistories { get; } = [];

    private void ComputeAverageStatus()
    {
        double total = Interlocked.Read(ref _redCount) +
                       Interlocked.Read(ref _yellowCount) * 2 +
                       Interlocked.Read(ref _greenCount) * 3;
        var average = total / MaxHistoryCount;

        AverageStatus = average switch
        {
            < 1 => ServiceStatus.Red,
            < 2 => ServiceStatus.Yellow,
            _ => ServiceStatus.Green
        };
    }

    private void AddCheck(HealthCheckStatus status)
    {
        LastCheckUtc = status.CheckTime;
        CheckHistories.Enqueue(status);

        if (CheckHistories.Count > MaxHistoryCount)
        {
            if (!CheckHistories.TryDequeue(out var oldest))
                return;

            switch (oldest.Status)
            {
                case ServiceStatus.Green:
                    Interlocked.Decrement(ref _greenCount);
                    break;
                case ServiceStatus.Yellow:
                    Interlocked.Decrement(ref _yellowCount);
                    break;
                case ServiceStatus.Red:
                    Interlocked.Decrement(ref _redCount);
                    break;
            }

            ComputeAverageStatus();
        }

        switch (status.Status)
        {
            case ServiceStatus.Green:
                Interlocked.Increment(ref _greenCount);
                break;
            case ServiceStatus.Yellow:
                Interlocked.Increment(ref _yellowCount);
                break;
            case ServiceStatus.Red:
                Interlocked.Increment(ref _redCount);
                break;
        }
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