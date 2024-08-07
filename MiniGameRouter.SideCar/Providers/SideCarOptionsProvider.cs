using MiniGameRouter.SideCar.Interfaces;
using MiniGameRouter.SideCar.Models;

namespace MiniGameRouter.SideCar.Providers;

public class SideCarOptionsProvider : ISideCarOptionsProvider
{
    public SideCarOptions Options { get; }

    public SideCarOptionsProvider(
        IConfiguration configuration,
        ILogger<SideCarOptionsProvider> logger)
    {
        var options = configuration.GetSection("SideCar").Get<SideCarOptions>();
        
        ArgumentNullException.ThrowIfNull(options);
        
        logger.LogInformation("SideCar startup options received, service tag [{tag}]", options.ServiceTag);
        logger.LogInformation("Preparing to start server on [{address}:{port}]", options.Listen.Address, options.Listen.Port);
        
        Options = options;
    }
}