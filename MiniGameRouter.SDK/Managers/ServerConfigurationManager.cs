using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MiniGameRouter.Shared.Models.RoutingConfig;

namespace MiniGameRouter.SDK.Managers;

public class ServerConfigurationManager
{
    public MiniGameRouterOptions Options { get; init; }

    public ServerConfigurationManager(
        IConfiguration configuration,
        ILogger<ServerConfigurationManager> logger)
    {
        var options = configuration.GetSection("MiniGameRouter").Get<MiniGameRouterOptions>();
        
        ArgumentNullException.ThrowIfNull(options);
        
        logger.LogInformation("Server configuration manager initialized.");
        
        Options = options;
    }
}