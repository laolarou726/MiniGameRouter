using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MiniGameRouter.SDK.Interfaces;

namespace MiniGameRouter.PressureTest.Providers.RandomOpService;

public abstract class AbstractRandomDynamicRoutingOpServiceBase(
    bool enableRandomOp,
    IConfiguration configuration,
    IDynamicRoutingSerivce dynamicRoutingService,
    ILogger logger)
    : AbstractOpServiceBase(
        enableRandomOp,
        configuration.GetValue("PressureTest:RandomDynamicMappingOps:ParallelCount", 10),
        configuration,
        logger)
{
    protected readonly IDynamicRoutingSerivce DynamicRoutingService = dynamicRoutingService;
    protected readonly ConcurrentQueue<(Guid, string)> Prefixes = [];

    protected override async Task PrepareAsync(CancellationToken stoppingToken)
    {
        var mappingCount = configuration.GetValue("PressureTest:RandomDynamicMappingOps:MappingCount", 200);

        for (var i = 0; i < mappingCount; i++)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                Logger.LogInformation("Cancellation requested, stopping service prep...");
                return;
            }

            var prefix = Guid.NewGuid().ToString("N");
            var endPoint = Guid.NewGuid().ToString("N");
            var id = await DynamicRoutingService.CreateMappingAsync(prefix, endPoint);

            if (!id.HasValue)
            {
                Logger.LogError("Failed to create dynamic mapping: [{prefix} -> {endPoint}]", prefix, endPoint);
                throw new InvalidOperationException();
            }

            Prefixes.Enqueue((id.Value, prefix));
        }
    }
}