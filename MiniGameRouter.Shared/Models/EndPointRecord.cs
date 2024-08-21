using MiniGameRouter.Shared.Interfaces;

namespace MiniGameRouter.Shared.Models;

public record EndPointRecord(
    long Id,
    string ServiceName,
    string EndPoint,
    uint Weight,
    int Timeout,
    bool IsValid) : IWeightedEntity, IValidate;