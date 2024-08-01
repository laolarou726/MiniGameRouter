namespace MiniGameRouter.Shared.Models;

public class EndPointMappingRequestModel
{
    public string? ServiceName { get; set; }
    public uint? Weight { get; set; }
    public string? TargetEndPoint { get; set; }
    public int TimeoutInMilliseconds { get; set; } = TimeSpan.FromMinutes(5).Milliseconds;
}