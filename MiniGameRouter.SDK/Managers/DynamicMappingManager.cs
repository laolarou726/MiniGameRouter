using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MiniGameRouter.SDK.Interfaces;
using System.Collections.Concurrent;

namespace MiniGameRouter.SDK.Managers;

public class DynamicMappingManager : IHostedService
{
    private readonly ConcurrentDictionary<long, byte> _mappings = [];

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger _logger;

    public DynamicMappingManager(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<DynamicMappingManager> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public void AddEndPoint(long id)
    {
        _mappings.TryAdd(id, 0);
    }

    public void RemoveEndPoint(long id)
    {
        _mappings.TryRemove(id, out _);
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DynamicMappingManager stopping, waiting for 5 sec to fully collect...");

        await Task.Delay(5000, CancellationToken.None);

        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var dynamicRoutingService = scope.ServiceProvider.GetRequiredService<IDynamicRoutingService>();

        foreach (var id in _mappings.Keys)
        {
            var succeeded = await dynamicRoutingService.DeleteMappingAsync(id);

            if (!succeeded)
            {
                _logger.LogError("Failed to remove mapping with id [{id}]", id);
                continue;
            }

            _logger.LogInformation("Mapping with id [{id}] deleted.", id);
        }

        _logger.LogInformation("DynamicMappingManager stopped.");
    }
}