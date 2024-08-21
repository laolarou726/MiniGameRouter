using MiniGameRouter.Shared.Models;

namespace MiniGameRouter.SDK.Interfaces;

public interface IDynamicRoutingService
{
    Task<DynamicRoutingRecord?> GetMappingAsync(long id);

    Task<string?> GetMappingAsync(string rawString);

    Task<long?> CreateMappingAsync(string matchPrefix, string targetEndPoint);

    Task<bool> DeleteMappingAsync(long id);
}