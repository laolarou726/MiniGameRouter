using MiniGameRouter.SDK.Models;
using MiniGameRouter.Shared.Models;

namespace MiniGameRouter.SDK.Interfaces;

public interface IEndPointService
{
    Task<EndPointRecord?> GetEndPointAsync(long serviceId);

    Task<EndPointRecord?> GetEndPointAsync(
        string serviceName,
        RoutingMode? routingMode = null,
        string? hashKey = null);

    Task<long?> CreateEndPointAsync(
        string serviceName,
        string endPoint,
        uint weight = 1,
        int timeoutInMilliseconds = 30000,
        bool addToExtraManager = true);

    Task<bool> EditEndPointAsync(
        long id,
        EndPointMappingRequestModel reqModel);

    Task<bool> DeleteEndPointAsync(long id);
}