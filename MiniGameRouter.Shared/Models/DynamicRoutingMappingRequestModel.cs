namespace MiniGameRouter.Shared.Models;

public class DynamicRoutingMappingRequestModel
{
    public required string MatchPrefix { get; set; }

    public required string TargetEndPoint { get; set; }
}