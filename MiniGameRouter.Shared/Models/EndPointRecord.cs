using MiniGameRouter.Shared.Interfaces;

namespace MiniGameRouter.Shared.Models;

public record EndPointRecord(
    long RecordId,
    string ServiceName,
    string EndPoint,
    uint Weight,
    int Timeout,
    bool IsValid) : IWeightedEntity, IValidate;