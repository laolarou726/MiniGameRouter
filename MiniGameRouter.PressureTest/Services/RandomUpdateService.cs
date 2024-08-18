using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MiniGameRouter.PressureTest.Providers;
using MiniGameRouter.SDK.Interfaces;

namespace MiniGameRouter.PressureTest.Services;

public sealed class RandomUpdateService(
    IConfiguration configuration,
    IEndPointService endPointService,
    ILogger<RandomUpdateService> logger)
    : AbstractRandomOpServiceBase(
        configuration.GetValue("PressureTest:RandomEndPointOps:EnableRandomUpdate", false),
        configuration,
        endPointService,
        logger)
{
    protected override async Task PerformRandomTask(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Random.Shared.Next(250, 1000), stoppingToken);

            if (!EndPoints.TryDequeue(out var endPoint)) continue;

            var newEndPoint = RandomServiceProvider.GetRandomEndPoint();
            var succeeded = await EndPointService.EditEndPointAsync(
                endPoint.Item1,
                newEndPoint);

            if (!succeeded)
            {
                Logger.LogError("Failed to update endpoint: {endPoint}", endPoint);
                continue;
            }

            Logger.LogInformation("Updated endpoint with id [{endPoint}]", endPoint.Item1);

            EndPoints.Enqueue((endPoint.Item1, newEndPoint));
        }
    }
}