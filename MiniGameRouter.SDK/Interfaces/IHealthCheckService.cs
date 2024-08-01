using MiniGameRouter.SDK.Models;

namespace MiniGameRouter.SDK.Interfaces;

public interface IHealthCheckService
{
    Task<bool> ReportHealthAsync(
        string serviceName,
        string endPoint,
        ServiceStatus status);
}