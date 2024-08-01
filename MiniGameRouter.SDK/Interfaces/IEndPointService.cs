using MiniGameRouter.SDK.Models;
using MiniGameRouter.Shared.Models;

namespace MiniGameRouter.SDK.Interfaces;

public interface IEndPointService
{
    Task<EndPointRecord?> GetEndPointAsync(Guid serviceId);

    Task<EndPointRecord?> GetEndPointAsync(
        string serviceName,
        RoutingModel? routingMode = null,
        string? hashKey = null);

    Task<Guid?> CreateEndPointAsync(
        string serviceName,
        string endPoint,
        uint weight = 1,
        int timeoutInMilliseconds = 30000);

    Task<bool> EditEndPointAsync(
        Guid id,
        EndPointMappingRequestModel reqModel);

    Task<bool> DeleteEndPointAsync(Guid id);
}