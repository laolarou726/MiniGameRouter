using Hive.Both.General.Dispatchers;
using Hive.Codec.Abstractions;
using Hive.Codec.MemoryPack;
using Hive.Codec.Shared;
using Hive.Network.Abstractions.Session;
using Hive.Network.Tcp;
using Microsoft.Extensions.DependencyInjection;
using MiniGameRouter.SDK.Helpers;
using MiniGameRouter.SDK.Interfaces;
using MiniGameRouter.SDK.Managers;
using MiniGameRouter.SDK.Models;
using MiniGameRouter.SDK.Services;

namespace MiniGameRouter.SDK;

public static class RegisterFactory
{
    public static IServiceCollection RegisterMiniGameRouter(
        this IServiceCollection services,
        MiniGameRouterOptions options)
    {
        services.AddSingleton<ServiceHealthManager>();
        services.AddHostedService(sC => sC.GetRequiredService<ServiceHealthManager>());

        return services
            .AddApiClients(options)
            .AddHiveEssentials()
            .UseTcpStack();
    }

    private static IServiceCollection AddApiClients(
        this IServiceCollection services,
        MiniGameRouterOptions options)
    {
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

    private static IServiceCollection AddHiveEssentials(this IServiceCollection services)
    {
        services.AddSingleton<ICustomCodecProvider, DefaultCustomCodecProvider>();
        services.AddSingleton<IPacketIdMapper, DefaultPacketIdMapper>();
        services.AddSingleton<IPacketCodec, MemoryPackPacketCodec>();
        services.AddSingleton<IDispatcher, DefaultDispatcher>();

        return services;
    }

    private static IServiceCollection UseTcpStack(this IServiceCollection services)
    {
        services.AddSingleton<IAcceptor<TcpSession>, TcpAcceptor>();
        services.AddSingleton<IConnector<TcpSession>, TcpConnector>();

        return services;
    }
}