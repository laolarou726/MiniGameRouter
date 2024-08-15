using System.ComponentModel.DataAnnotations;

namespace MiniGameRouter.Models;

public class EndPointMappingModel
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(256)] public required string ServiceName { get; set; }

    [MaxLength(256)] public required string TargetEndPoint { get; set; }

    public uint? Weight { get; set; }

    public int TimeoutInMilliseconds { get; set; } = TimeSpan.FromMinutes(5).Milliseconds;

    public required DateTime CreateTimeUtc { get; set; }

    public bool IsValid { get; set; }
}