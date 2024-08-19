using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MiniGameRouter.PressureTest.Providers.RandomOpService;

public abstract class AbstractOpServiceBase : BackgroundService
{
    protected readonly bool EnableRandomOp;
    protected readonly int ParallelCount;
    protected readonly IConfiguration Configuration;
    protected readonly ILogger Logger;

    protected AbstractOpServiceBase(
        bool enableRandomOp,
        int parallelCount,
        IConfiguration configuration,
        ILogger logger)
    {
        EnableRandomOp = enableRandomOp;
        ParallelCount = parallelCount;

        Configuration = configuration;
        Logger = logger;
    }

    protected abstract Task PrepareAsync(CancellationToken stoppingToken);

    protected abstract Task PerformRandomTask(CancellationToken stoppingToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!EnableRandomOp)
        {
            Logger.LogInformation("RandomOpService is disabled.");
            return;
        }

        await PrepareAsync(stoppingToken);

        var tasks = new Task[ParallelCount];

        for (var i = 0; i < ParallelCount; i++)
        {
            tasks[i] = PerformRandomTask(stoppingToken);
        }

        await Task.WhenAll(tasks);
    }
}