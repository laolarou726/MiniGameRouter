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
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger _logger;

    public DynamicRoutingService(
        IDistributedCache cache,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<DynamicRoutingService> logger)
    {
        _cache = cache;
        _serviceScopeFactory = serviceScopeFactory;
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

        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DynamicRoutingMappingContext>();

        var match = await context.DynamicRoutingMappings
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

        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DynamicRoutingMappingContext>();

        var anyExist = await context.DynamicRoutingMappings
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

        await context.DynamicRoutingMappings.AddAsync(mapping);
        await context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> TryRemoveMappingAsync(Guid id)
    {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DynamicRoutingMappingContext>();

        var record = await context.DynamicRoutingMappings.FindAsync(id);

        if (record == null) return false;

        if (_cacheMappings.TryRemove(record.MatchPrefix, out var keys))
        {
            foreach (var key in keys)
            {
                _logger.LogInformation("Removing cache key [{key}] due to mapping has been removed.", key);

                await _cache.RemoveAsync(key);
            }
        }

        context.DynamicRoutingMappings.Remove(record);
        await context.SaveChangesAsync();

        return true;
    }
}