namespace MiniGameRouter.Models;

public class HealthCheckRequestModel
{
    public string ServiceName { get; set; }
    public string EndPoint { get; set; }
    public ServiceStatus Status { get; set; }
}

public class HealthCheckStatus
{
    public ServiceStatus Status { get; set; }
    
    public DateTime CheckTime { get; set; }
}