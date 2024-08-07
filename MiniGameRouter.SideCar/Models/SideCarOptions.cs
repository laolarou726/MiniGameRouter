namespace MiniGameRouter.SideCar.Models;

public class SideCarOptions
{
    public required string ServiceTag { get; init; }
    public required int DestMaxConnectionTimeout { get; init; }
    public required ListenEndPointModel Listen { get; init; }
    public required string DestinationAddr { get; init; }
    public required int DestinationPort { get; init; }
}