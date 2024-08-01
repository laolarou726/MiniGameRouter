using MiniGameRouter.Shared.Models;

namespace MiniGameRouter.Interfaces;

public interface IRoutingService
{
    EndPointRecord? Get(string serviceName, string key);

    void AddNode(EndPointRecord record);

    void AddNodes(IEnumerable<EndPointRecord> records);

    bool RemoveNode(EndPointRecord record);

    bool RemoveMapping(string serviceName);
}