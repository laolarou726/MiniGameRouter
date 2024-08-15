using System.ComponentModel.DataAnnotations;

namespace MiniGameRouter.Models;

public class DynamicRoutingMappingModel
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(256)] public required string MatchPrefix { get; set; }

    [MaxLength(256)] public required string TargetEndPoint { get; set; }
}