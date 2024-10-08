using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;

namespace MiniGameRouter.Models;

public class EndPointMappingModel
{
    public ObjectId Id { get; set; } = ObjectId.GenerateNewId();

    public required long RecordId { get; set; }

    [MaxLength(256)] public required string ServiceName { get; set; }

    [MaxLength(256)] public required string TargetEndPoint { get; set; }

    public uint? Weight { get; set; }

    public int TimeoutInMilliseconds { get; set; } = TimeSpan.FromMinutes(5).Milliseconds;

    public required DateTime CreateTimeUtc { get; set; }

    public bool IsValid { get; set; }
}