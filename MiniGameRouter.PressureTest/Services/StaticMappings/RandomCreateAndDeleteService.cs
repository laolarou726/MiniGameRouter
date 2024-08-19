using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MiniGameRouter.PressureTest.Providers;
using MiniGameRouter.PressureTest.Providers.RandomOpService;
using MiniGameRouter.SDK.Interfaces;

namespace MiniGameRouter.PressureTest.Services.StaticMappings;

public sealed class RandomCreateAndDeleteService(
    IConfiguration configuration,
    IEndPointService endPointService,
    ILogger<RandomCreateAndDeleteService> logger)
    : AbstractRandomOpServiceBase(
        configuration.GetValue("PressureTest:RandomEndPointOps:EnableRandomCreateAndDelete", false),
        configuration,
        endPointService,
        logger)
{
    private long _remainCount;

    private async Task AddNewAsync()
    {
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

    protected override async Task PerformRandomTask(CancellationToken stoppingToken)
    {
        _remainCount = ParallelCount;

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Random.Shared.Next(250, 1000), stoppingToken);

            if (Random.Shared.Next(0, 2) == 0 || Interlocked.Read(ref _remainCount) > ParallelCount / 2)
            {
                if (!EndPoints.TryDequeue(out var endPoint)) continue;

                var succeeded = await EndPointService.DeleteEndPointAsync(endPoint.Item1);

                if (!succeeded)
                {
                    Logger.LogError("Failed to delete endpoint: {endPoint}", endPoint);
                    continue;
                }

                var remain = Interlocked.Decrement(ref _remainCount);

                Logger.LogInformation(
                    "Deleted endpoint with id [{endPoint}]. RemainCount={remainCount}",
                    endPoint.Item1,
                    remain);
            }

            await AddNewAsync();

            var incrementedRemain = Interlocked.Increment(ref _remainCount);

            Logger.LogInformation("Created new endpoint. RemainCount={remainCount}", incrementedRemain);
        }
    }
}