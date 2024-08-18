using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace MiniGameRouter.Helper;

public static class RedisHelper
{
    public static async Task SetAsync<T>(this IDistributedCache cache, string key, T value, DistributedCacheEntryOptions? options = null)
    {
        options ??= new DistributedCacheEntryOptions();

        var json = JsonSerializer.Serialize(value);
        await cache.SetStringAsync(key, json, options);
    }

    public static async Task<T?> GetAsync<T>(this IDistributedCache cache, string key)
    {
        var json = await cache.GetStringAsync(key);

        return json is null
            ? default
            : JsonSerializer.Deserialize<T>(json);
    }
}