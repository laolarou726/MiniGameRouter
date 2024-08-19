namespace MiniGameRouter.Shared.Models;

public record DynamicRoutingRecord(Guid Id, string MatchPrefix, string TargetEndPoint);