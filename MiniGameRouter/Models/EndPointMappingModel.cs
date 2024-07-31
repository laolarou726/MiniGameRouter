using System.ComponentModel.DataAnnotations;
using MiniGameRouter.Interfaces;

namespace MiniGameRouter.Models;

public class EndPointMappingRequestModel
{
    public string? ServiceName { get; set; }
    
    public uint? Weight { get; set; }
    public string? TargetEndPoint { get; set; }
    
    public int TimeoutInMilliseconds { get; set; } = TimeSpan.FromMinutes(5).Milliseconds;
}

public record EndPointRecord(
    Guid Id,
    string ServiceName,
    string EndPoint,
    uint Weight,
    int Timeout,
    bool IsValid) : IWeightedEntity, IValidate;

public class EndPointMappingModel
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    
    [MaxLength(256)]
    public required string ServiceName { get; set; }
    
    public uint? Weight { get; set; }

    public required string TargetEndPoint { get; set; }
    
    public int TimeoutInMilliseconds { get; set; } = TimeSpan.FromMinutes(5).Milliseconds;
    
    public bool IsValid { get; set; }
}