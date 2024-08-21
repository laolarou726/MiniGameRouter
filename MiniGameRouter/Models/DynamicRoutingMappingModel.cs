using System.ComponentModel.DataAnnotations;

namespace MiniGameRouter.Models;

public class DynamicRoutingMappingModel
{
    public required long Id { get; set; }

    [MaxLength(256)] public required string MatchPrefix { get; set; }

    [MaxLength(256)] public required string TargetEndPoint { get; set; }
}