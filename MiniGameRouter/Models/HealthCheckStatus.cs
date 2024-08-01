using MiniGameRouter.SDK.Models;

namespace MiniGameRouter.Models;

public class HealthCheckStatus
{
    public ServiceStatus Status { get; set; }

    public DateTime CheckTime { get; set; }
}