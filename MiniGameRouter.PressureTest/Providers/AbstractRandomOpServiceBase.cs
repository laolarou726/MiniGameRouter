using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MiniGameRouter.Shared.Models;
using System.Collections.Concurrent;
using MiniGameRouter.SDK.Interfaces;

namespace MiniGameRouter.PressureTest.Providers;

public abstract class AbstractRandomOpServiceBase : BackgroundService
{
    private readonly bool _enableRandomOp;

    protected readonly int ParallelCount;
    protected readonly ConcurrentQueue<(Guid, EndPointMappingRequestModel)> EndPoints = [];
    protected readonly IEndPointService EndPointService;
    protected readonly ILogger Logger;

    protected AbstractRandomOpServiceBase(
        bool enableRandomOp,
        IConfiguration configuration,
        IEndPointService endPointService,
        ILogger logger)
    {
        _enableRandomOp = enableRandomOp;
        ParallelCount = configuration.GetValue("PressureTest:RandomEndPointOps:ParallelCount", 10);

        EndPointService = endPointService;
        Logger = logger;
    }

    protected virtual async Task PrepareAsync(CancellationToken stoppingToken)
    {
        for (var i = 0; i < ParallelCount; i++)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                Logger.LogInformation("Cancellation requested, stopping service prep...");
                return;
            }

            var endPoint = RandomServiceProvider.GetRandomEndPoint();
            var id = await EndPointService.CreateEndPointAsync(
                endPoint.ServiceName!,
                endPoint.TargetEndPoint!,
                1,
                endPoint.TimeoutInMilliseconds);

            if (!id.HasValue)
            {
                Logger.LogError("Failed to create endpoint: {endPoint}", endPoint);
                throw new InvalidOperationException();
            }

            EndPoints.Enqueue((id.Value, endPoint));
        }
    }

    protected abstract Task PerformRandomTask(CancellationToken stoppingToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enableRandomOp)
        {
            Logger.LogInformation("RandomUpdateService is disabled.");
            return;
        }

        await PrepareAsync(stoppingToken);

        var tasks = new Task[ParallelCount];

        for (var i = 0; i < ParallelCount; i++)
        {
            tasks[i] = PerformRandomTask(stoppingToken);
        }

        await Task.WhenAll(tasks);
    }
}