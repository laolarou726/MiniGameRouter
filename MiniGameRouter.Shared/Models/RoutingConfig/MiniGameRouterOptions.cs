using MiniGameRouter.SDK.Models;

namespace MiniGameRouter.Shared.Models.RoutingConfig;

public class MiniGameRouterOptions
{
    public required string ConnectionString { get; init; }
    public int? HealthCheckConcurrency { get; init; }
    public IReadOnlyDictionary<string, EndPointMappingRequestModel[]>? EndPointMappings { get; init; }
    public IReadOnlyDictionary<string, RoutingMode>? EndPointRoutingConfigs { get; init; }
}