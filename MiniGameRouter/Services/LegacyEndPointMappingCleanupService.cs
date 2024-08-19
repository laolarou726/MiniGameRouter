using Microsoft.EntityFrameworkCore;
using MiniGameRouter.Models.DB;
using MiniGameRouter.SDK.Models;
using MiniGameRouter.Shared.Models;

namespace MiniGameRouter.Services;

public class LegacyEndPointMappingCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;

    public LegacyEndPointMappingCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<LegacyEndPointMappingCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var healthCheckService = scope.ServiceProvider.GetRequiredService<HealthCheckService>();
        var context = scope.ServiceProvider.GetRequiredService<EndPointMappingContext>();

        _logger.LogInformation("Starting cleanup of legacy end point mappings");
        _logger.LogInformation("Cleanup service has the delay of 5 minutes to start, waiting for the health manager to warm up...");

        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Cleaning up legacy end point mappings");

            var now = DateTime.UtcNow.AddMinutes(-10);
            var mappings = await context.EndPoints
                .AsNoTrackingWithIdentityResolution()
                .Where(e => e.CreateTimeUtc < now)
                .ToListAsync(stoppingToken);

            foreach (var mapping in mappings)
            {
                var entry = new HealthCheckRequestModel
                {
                    EndPoint = mapping.TargetEndPoint,
                    ServiceName = mapping.ServiceName,
                    Status = mapping.IsValid ? ServiceStatus.Green : ServiceStatus.Red
                };

                if (healthCheckService.TryGetStatus(entry, out _))
                    continue;

                _logger.LogInformation(
                    "Removing legacy end point mapping [{endPoint}] of service [{serviceName}]",
                    entry.EndPoint,
                    entry.ServiceName);

                context.EndPoints.Remove(mapping);
                await context.SaveChangesAsync(stoppingToken);
            }

            _logger.LogInformation("Legacy end point mappings cleaned up");

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}