using System.Collections.Concurrent;
using MiniGameRouter.Interfaces;
using MiniGameRouter.Models.Router;
using MiniGameRouter.Shared.Models;

namespace MiniGameRouter.Services.RoutingServices;

public class NodeWeightedRouteService : IRoutingService
{
    private readonly ConcurrentDictionary<string, WeightedRouter<EndPointRecord>> _mappings = [];

    public EndPointRecord? Get(string serviceName, string key = null!)
    {
        if (!_mappings.TryGetValue(serviceName, out var router))
            return null;

        return router.Get();
    }

    public void AddNode(EndPointRecord record)
    {
        if (_mappings.TryGetValue(record.ServiceName, out var router))
        {
            router.Add(record);
            return;
        }

        var newRouter = new WeightedRouter<EndPointRecord>();
        newRouter.Add(record);

        _mappings.TryAdd(record.ServiceName, newRouter);
    }

    public void AddNodes(IEnumerable<EndPointRecord> records)
    {
        foreach (var record in records)
            AddNode(record);
    }

    public bool RemoveNode(EndPointRecord record)
    {
        if (!_mappings.TryGetValue(record.ServiceName, out var router))
            return false;

        router.Remove(record);

        return true;
    }

    public bool RemoveMapping(string serviceName = null!)
    {
        var result = _mappings.TryRemove(serviceName, out var removed);

        if (result)
            removed!.Dispose();

        return result;
    }

    public EndPointRecord? GetRandom(string serviceName)
    {
        if (!_mappings.TryGetValue(serviceName, out var router))
            return null;

        return router.GetRandom();
    }
}