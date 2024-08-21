namespace MiniGameRouter.Shared.Models;

public record DynamicRoutingRecord(long RecordId, string MatchPrefix, string TargetEndPoint);