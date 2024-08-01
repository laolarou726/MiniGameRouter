using System.Collections.Concurrent;
using MiniGameRouter.Interfaces;
using MiniGameRouter.Models.Router;
using MiniGameRouter.Shared.Models;

namespace MiniGameRouter.Services.RoutingServices;

public class NodeHashRouteService : IRoutingService
{
    private readonly ConcurrentDictionary<string, ConsistentHash<EndPointRecord>> _mappings = new();

    public EndPointRecord? Get(string serviceName, string key)
    {
        return _mappings.TryGetValue(serviceName, out var hash)
            ? hash.GetNode(key)
            : null;
    }

    public void AddNode(EndPointRecord record)
    {
        if (_mappings.TryGetValue(record.ServiceName, out var hash))
        {
            hash.Add(record);
            return;
        }

        var newHash = new ConsistentHash<EndPointRecord>();
        newHash.Init([record]);

        _mappings.TryAdd(record.ServiceName, newHash);
    }

    public void AddNodes(IEnumerable<EndPointRecord> records)
    {
        var record = records.First();

        if (_mappings.TryGetValue(record.ServiceName, out var hash))
        {
            foreach (var endPointRecord in records)
                hash.Add(endPointRecord);
            return;
        }

        var newHash = new ConsistentHash<EndPointRecord>();
        newHash.Init(records);

        _mappings.TryAdd(record.ServiceName, newHash);
    }

    public bool RemoveNode(EndPointRecord record)
    {
        if (!_mappings.TryGetValue(record.ServiceName, out var hash))
            return false;

        hash.Remove(record);

        return true;
    }

    public bool RemoveMapping(string serviceName)
    {
        var result = _mappings.TryRemove(serviceName, out var removed);

        if (result)
            removed!.Dispose();

        return result;
    }
}