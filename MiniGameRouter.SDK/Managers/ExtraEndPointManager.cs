using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MiniGameRouter.SDK.Interfaces;

namespace MiniGameRouter.SDK.Managers;

public class ExtraEndPointManager : IHostedService
{
    private readonly ConcurrentDictionary<Guid, byte> _endPoints = [];

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger _logger;

    public ExtraEndPointManager(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ExtraEndPointManager> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public void AddEndPoint(Guid id)
    {
        _endPoints.TryAdd(id, 0);
    }

    public void RemoveEndPoint(Guid id)
    {
        _endPoints.TryRemove(id, out _);
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ExtraEndPointManager stopping, waiting for 5 sec to fully collect...");

        await Task.Delay(5000, CancellationToken.None);

        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var endPointService = scope.ServiceProvider.GetRequiredService<IEndPointService>();

        foreach (var id in _endPoints.Keys)
        {
            var succeeded = await endPointService.DeleteEndPointAsync(id);

            if (!succeeded)
            {
                _logger.LogError("Failed to remove endpoint with id [{id}]", id);
                continue;
            }

            _logger.LogInformation("Extra endpoint with id [{id}] deleted.", id);
        }

        _logger.LogInformation("ExtraEndPointManager stopped.");
    }
}