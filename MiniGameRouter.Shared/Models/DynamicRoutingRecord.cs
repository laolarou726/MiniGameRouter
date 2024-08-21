namespace MiniGameRouter.Shared.Models;

public record DynamicRoutingRecord(long Id, string MatchPrefix, string TargetEndPoint);