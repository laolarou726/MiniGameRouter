using MiniGameRouter.Shared.Models.RoutingConfig;

namespace MiniGameRouter.SDK.Interfaces;

public interface IServerConfigurationProvider
{
    MiniGameRouterOptions Options { get; }
}