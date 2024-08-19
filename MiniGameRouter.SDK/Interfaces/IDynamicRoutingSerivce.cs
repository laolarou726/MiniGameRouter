using MiniGameRouter.Shared.Models;

namespace MiniGameRouter.SDK.Interfaces;

public interface IDynamicRoutingSerivce
{
    Task<DynamicRoutingRecord?> GetMappingAsync(Guid id);

    Task<string?> GetMappingAsync(string rawString);

    Task<Guid?> CreateMappingAsync(string matchPrefix, string targetEndPoint);

    Task<bool> DeleteMappingAsync(Guid id);
}