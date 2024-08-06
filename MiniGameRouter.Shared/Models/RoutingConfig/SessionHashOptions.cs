namespace MiniGameRouter.Shared.Models.RoutingConfig;

public class SessionHashOptions
{
    public required string GeneralSessionHash { get; init; }
    
    public IReadOnlyDictionary<string, string>? ServiceHashes { get; init; }
}