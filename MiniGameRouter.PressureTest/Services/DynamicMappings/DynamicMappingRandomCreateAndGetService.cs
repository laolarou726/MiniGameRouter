using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MiniGameRouter.PressureTest.Providers.RandomOpService;
using MiniGameRouter.SDK.Interfaces;

namespace MiniGameRouter.PressureTest.Services.DynamicMappings;

public sealed class DynamicMappingRandomCreateAndGetService(
    IConfiguration configuration,
    IDynamicRoutingSerivce dynamicRoutingService,
    ILogger<DynamicMappingRandomCreateAndGetService> logger)
    : AbstractRandomDynamicRoutingOpServiceBase(
        configuration.GetValue("PressureTest:RandomDynamicMappingOps:EnableRandomCreateAndGet", false),
        configuration,
        dynamicRoutingService,
        logger)
{
    protected override async Task PerformRandomTask(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Random.Shared.Next(250, 1000), stoppingToken);

            if (Prefixes.TryDequeue(out var prefix)) continue;

            var caseNum = Random.Shared.Next(0, 2);
            string? record;

            switch (caseNum)
            {
                case 0:
                    record = (await DynamicRoutingService.GetMappingAsync(prefix.Item1))?.TargetEndPoint;
                    break;
                case 1:
                    record = await DynamicRoutingService.GetMappingAsync($"{prefix.Item2}{Random.Shared.Next()}");
                    break;
                default:
                    throw new InvalidOperationException("Unknown random endpoint get method!");
            }

            Prefixes.Enqueue(prefix);

            if (record == null)
            {
                Logger.LogError("Failed to get mapping with id [{endPoint}] using getting mode [{mode}].", prefix.Item1, caseNum);
                continue;
            }

            Logger.LogInformation("Get endpoint [{endPoint}] using getting mode [{mode}].", prefix.Item1, caseNum);
        }
    }
}