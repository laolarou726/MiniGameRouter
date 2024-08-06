using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MiniGameRouter.SDK.Interfaces;
using MiniGameRouter.Shared.Models.RoutingConfig;

namespace MiniGameRouter.SDK.Providers;

public class ServerConfigurationProvider : IServerConfigurationProvider
{
    public MiniGameRouterOptions Options { get; init; }

    public ServerConfigurationProvider(
        IConfiguration configuration,
        ILogger<ServerConfigurationProvider> logger)
    {
        var options = configuration.GetSection("MiniGameRouter").Get<MiniGameRouterOptions>();
        
        ArgumentNullException.ThrowIfNull(options);
        
        logger.LogInformation("Server configuration manager initialized.");
        
        Options = options;
    }
}