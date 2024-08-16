using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using MiniGameRouter.Helper;
using MiniGameRouter.Models;
using MiniGameRouter.Models.DB;
using MiniGameRouter.Shared.Models;

namespace MiniGameRouter.Services;

public class DynamicRoutingService
{
    // [prefix : [cache key]]
    private readonly ConcurrentDictionary<string, List<string>> _cacheMappings = [];

    private readonly IDistributedCache _cache;
    private readonly DynamicRoutingMappingContext _dynamicRoutingMappingContext;
    private readonly ILogger _logger;

    public DynamicRoutingService(
        IDistributedCache cache,
        DynamicRoutingMappingContext dynamicRoutingMappingContext,
        ILogger<DynamicRoutingService> logger)
    {
        _cache = cache;
        _dynamicRoutingMappingContext = dynamicRoutingMappingContext;
        _logger = logger;
    }

    private static string GetCacheKey(string rawStr)
    {
        return $"dr_{rawStr.ToLower()}";
    }

    public async Task<string?> TryGetMatchAsync(string rawStr)
    {
        var loweredRawStr = rawStr.ToLower();
        var cacheKey = GetCacheKey(rawStr);

        var cachedMapping = await _cache.GetAsync<string>(cacheKey);
        if (!string.IsNullOrEmpty(cachedMapping))
            return cachedMapping;

        var match = await _dynamicRoutingMappingContext.DynamicRoutingMappings
            .Where(m => loweredRawStr.StartsWith(m.MatchPrefix.ToLower()))
            .OrderBy(m => m.MatchPrefix.Length)
            .LastOrDefaultAsync();

        if (match == null) return null;

        _cacheMappings.AddOrUpdate(match.MatchPrefix, [cacheKey], (_, value) =>
        {
            value.Add(cacheKey);
            return value;
        });

        await _cache.SetAsync(cacheKey, match.TargetEndPoint);

        return match.TargetEndPoint;
    }

    public async Task<bool> TryAddMappingToDbAsync(DynamicRoutingMappingRequestModel model)
    {
        if (string.IsNullOrEmpty(model.MatchPrefix) ||
            string.IsNullOrEmpty(model.TargetEndPoint)) return false;

        var anyExist = await _dynamicRoutingMappingContext.DynamicRoutingMappings
            .AnyAsync(m => m.MatchPrefix == model.MatchPrefix);

        if (anyExist)
        {
            _logger.LogWarning("A mapping with same prefix [{prefix}] has already been added.", model.MatchPrefix);

            return false;
        }

        foreach (var (k, cacheKeys) in _cacheMappings)
        {
            if (model.MatchPrefix.StartsWith(k, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var key in cacheKeys)
                {
                    _logger.LogInformation("Removing cache key [{key}] due to more precise prefix has been added.", key);

                    await _cache.RemoveAsync(key);
                }
            }
        }

        var mapping = new DynamicRoutingMappingModel
        {
            MatchPrefix = model.MatchPrefix,
            TargetEndPoint = model.TargetEndPoint
        };

        await _dynamicRoutingMappingContext.DynamicRoutingMappings.AddAsync(mapping);
        await _dynamicRoutingMappingContext.SaveChangesAsync();

        return true;
    }

    public async Task<bool> TryRemoveMappingAsync(Guid id)
    {
        var record = await _dynamicRoutingMappingContext.DynamicRoutingMappings.FindAsync(id);

        if (record == null) return false;

        if (_cacheMappings.TryRemove(record.MatchPrefix, out var keys))
        {
            foreach (var key in keys)
            {
                _logger.LogInformation("Removing cache key [{key}] due to mapping has been removed.", key);

                await _cache.RemoveAsync(key);
            }
        }

        _dynamicRoutingMappingContext.DynamicRoutingMappings.Remove(record);
        await _dynamicRoutingMappingContext.SaveChangesAsync();

        return true;
    }
}