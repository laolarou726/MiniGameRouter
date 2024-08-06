using MiniGameRouter.Shared.Models.RoutingConfig;

namespace MiniGameRouter.SDK.Interfaces;

public interface IServerConfigurationManager
{
    MiniGameRouterOptions Options { get; }
}