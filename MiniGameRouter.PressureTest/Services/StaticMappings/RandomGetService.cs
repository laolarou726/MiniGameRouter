using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MiniGameRouter.PressureTest.Providers;
using MiniGameRouter.PressureTest.Providers.RandomOpService;
using MiniGameRouter.SDK.Interfaces;
using MiniGameRouter.SDK.Models;
using MiniGameRouter.Shared.Models;

namespace MiniGameRouter.PressureTest.Services.StaticMappings;

public class RandomGetService(
    IConfiguration configuration,
    IEndPointService endPointService,
    ILogger<RandomGetService> logger)
    : AbstractRandomOpServiceBase(
        configuration.GetValue("PressureTest:RandomEndPointOps:EnableRandomGet", false),
        configuration,
        endPointService,
        logger)
{
    protected override async Task PrepareAsync(CancellationToken stoppingToken)
    {
        var subInstanceCount = Configuration.GetValue("PressureTest:RandomEndPointOps:GetSubInstanceCount", 10);

        for (var i = 0; i < ParallelCount; i++)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                Logger.LogInformation("Cancellation requested, stopping service prep...");
                return;
            }

            for (var subInstance = 0; subInstance < subInstanceCount; subInstance++)
            {
                var serviceName = $"Random_Service_For_Get_Test_{i}";
                var endPoint = RandomServiceProvider.GetRandomEndPoint($"SUB_INSTANCE_{subInstance}");
                var id = await EndPointService.CreateEndPointAsync(
                    serviceName,
                    endPoint.TargetEndPoint!,
                    (uint)Random.Shared.Next(1, 10),
                    endPoint.TimeoutInMilliseconds);

                if (!id.HasValue)
                {
                    Logger.LogError("Failed to create endpoint: {endPoint}", endPoint);
                    throw new InvalidOperationException();
                }

                endPoint.ServiceName = serviceName;

                EndPoints.Enqueue((id.Value, endPoint));
            }
        }
    }

    protected override async Task PerformRandomTask(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Random.Shared.Next(250, 1000), stoppingToken);

            if (!EndPoints.TryDequeue(out var endPoint)) continue;

            var caseNum = Random.Shared.Next(0, 4);
            EndPointRecord? record;

            switch (caseNum)
            {
                case 0:
                    record = await EndPointService.GetEndPointAsync(endPoint.Item1);
                    break;
                case 1:
                    record = await EndPointService.GetEndPointAsync(endPoint.Item2.ServiceName!, RoutingMode.Random);
                    break;
                case 2:
                    record = await EndPointService.GetEndPointAsync(endPoint.Item2.ServiceName!, RoutingMode.Weighted);
                    break;
                case 3:
                    record = await EndPointService.GetEndPointAsync(endPoint.Item2.ServiceName!, RoutingMode.Hashed, Guid.NewGuid().ToString("N"));
                    break;
                default:
                    throw new InvalidOperationException("Unknown random endpoint get method!");
            }

            EndPoints.Enqueue(endPoint);

            if (record == null)
            {
                Logger.LogError("Failed to get endpoint with id [{endPoint}] using getting mode [{mode}].", endPoint.Item1, caseNum);
                continue;
            }

            Logger.LogInformation("Get endpoint [{endPoint}] using getting mode [{mode}].", record.RecordId, caseNum);
        }
    }
}