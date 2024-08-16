using Hive.Both.General.Dispatchers;
using Hive.Codec.Abstractions;
using Hive.Codec.MemoryPack;
using Hive.Codec.Shared;
using Hive.Network.Abstractions.Session;
using Hive.Network.Tcp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiniGameRouter.SDK.Helpers;
using MiniGameRouter.SDK.Interfaces;
using MiniGameRouter.SDK.Managers;
using MiniGameRouter.SDK.Providers;
using MiniGameRouter.SDK.Services;
using MiniGameRouter.Shared.Models.RoutingConfig;

namespace MiniGameRouter.SDK;

public static class RegisterFactory
{
    public static IServiceCollection RegisterMiniGameRouter(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<ServiceHealthManager>();
        services.AddSingleton<ExtraEndPointManager>();
        
        services.AddSingleton<ISessionHashIdentityProvider, SessionHashIdentityProvider>();
        services.AddSingleton<IServerConfigurationProvider, ServerConfigurationProvider>();

        services.AddHostedService<ServiceRegistrationManager>();
        services.AddHostedService(sC => sC.GetRequiredService<ServiceHealthManager>());
        services.AddHostedService(sC => sC.GetRequiredService<ExtraEndPointManager>());

        return services
            .AddApiClients(configuration)
            .AddHiveEssentials()
            .UseTcpStack();
    }

    public static IServiceCollection AddApiClients(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration.GetSection("MiniGameRouter").Get<MiniGameRouterOptions>();

        ArgumentNullException.ThrowIfNull(options);

        services
            .AddHttpClient<IEndPointService, EndPointService>(client =>
            {
                client.BaseAddress = new Uri(options.ConnectionString);
            })
            .AddPolicyHandler(HttpPolicyHelper.GetRetryPolicy());

        services
            .AddHttpClient<IHealthCheckService, HealthCheckService>(client =>
            {
                client.BaseAddress = new Uri(options.ConnectionString);
            })
            .AddPolicyHandler(HttpPolicyHelper.GetRetryPolicy());

        services
            .AddHttpClient<IVersionService, VersionService>(client =>
            {
                client.BaseAddress = new Uri(options.ConnectionString);
            })
            .AddPolicyHandler(HttpPolicyHelper.GetRetryPolicy());

        return services;
    }

    public static IServiceCollection AddHiveEssentials(this IServiceCollection services)
    {
        services.AddSingleton<ICustomCodecProvider, DefaultCustomCodecProvider>();
        services.AddSingleton<IPacketIdMapper, DefaultPacketIdMapper>();
        services.AddSingleton<IPacketCodec, MemoryPackPacketCodec>();
        services.AddSingleton<IDispatcher, DefaultDispatcher>();

        return services;
    }

    public static IServiceCollection UseTcpStack(this IServiceCollection services)
    {
        services.AddSingleton<IAcceptor<TcpSession>, TcpAcceptor>();
        services.AddSingleton<IConnector<TcpSession>, TcpConnector>();

        return services;
    }
}