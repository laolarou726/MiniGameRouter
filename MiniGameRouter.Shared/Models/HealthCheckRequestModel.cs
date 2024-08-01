using MiniGameRouter.SDK.Models;

namespace MiniGameRouter.Shared.Models;

public class HealthCheckRequestModel
{
    public required string ServiceName { get; init; }
    public required string EndPoint { get; init; }
    public required ServiceStatus Status { get; init; }
}