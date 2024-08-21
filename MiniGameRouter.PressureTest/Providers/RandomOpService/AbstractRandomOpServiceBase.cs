using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MiniGameRouter.Shared.Models;
using System.Collections.Concurrent;
using MiniGameRouter.SDK.Interfaces;

namespace MiniGameRouter.PressureTest.Providers.RandomOpService;

public abstract class AbstractRandomOpServiceBase(
    bool enableRandomOp,
    IConfiguration configuration,
    IEndPointService endPointService,
    ILogger logger)
    : AbstractOpServiceBase(
        enableRandomOp,
        configuration.GetValue("PressureTest:RandomEndPointOps:ParallelCount", 10),
        configuration,
        logger)
{
    protected readonly ConcurrentQueue<(long, EndPointMappingRequestModel)> EndPoints = [];
    protected readonly IEndPointService EndPointService = endPointService;

    protected override async Task PrepareAsync(CancellationToken stoppingToken)
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!EnableRandomOp)
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