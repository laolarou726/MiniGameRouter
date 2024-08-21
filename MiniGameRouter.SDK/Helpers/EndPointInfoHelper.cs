using Microsoft.Extensions.DependencyInjection;
using MiniGameRouter.SDK.Interfaces;
using MiniGameRouter.SDK.Models;
using MiniGameRouter.SDK.Services;
using MiniGameRouter.Shared.Models;

namespace MiniGameRouter.SDK.Helpers;

public class EndPointInfoHelper
{
    private readonly bool _isId;
    private readonly string _serviceName;
    private readonly long _id;

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private string? _hashKey;
    private RoutingMode? _routingMode;

    public EndPointInfoHelper(
        string serviceName,
        IServiceScopeFactory serviceScopeFactory)
    {
        _serviceName = serviceName;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public EndPointInfoHelper(
        long serviceId,
        IServiceScopeFactory serviceScopeFactory)
    {
        _isId = true;
        _id = serviceId;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public EndPointInfoHelper WithRoutingMode(RoutingMode routingMode)
    {
        _routingMode = routingMode;
        return this;
    }

    public EndPointInfoHelper WithHashKey(string hashKey)
    {
        _hashKey = hashKey;
        return this;
    }

    public async Task<EndPointRecord?> GetAsync()
    {
        if (_isId && (_routingMode != null || !string.IsNullOrEmpty(_hashKey)))
            throw new Exception("Routing mode and hash key are not supported for GUID service ID.");

        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var endPointService = scope.ServiceProvider.GetRequiredService<EndPointService>();

        if (_isId)
            return await endPointService.GetEndPointAsync(_id);

        var serverConfigProvider = scope.ServiceProvider.GetRequiredService<IServerConfigurationProvider>();
        var sessionHashIdProvider = scope.ServiceProvider.GetRequiredService<ISessionHashIdentityProvider>();
        var routingMode = RoutingMode.Random;

        if (_routingMode == null)
            routingMode = serverConfigProvider.Options
                .EndPointRoutingConfigs
                ?.GetValueOrDefault(_serviceName) ?? RoutingMode.Random;

        var hashId = _hashKey;

        if (routingMode == RoutingMode.Hashed &&
            string.IsNullOrEmpty(hashId))
        {
            var definedHash = sessionHashIdProvider
                .ServiceHashes
                ?.GetValueOrDefault(_serviceName);

            if (string.IsNullOrEmpty(definedHash))
                throw new Exception("Hash key is required for hashed routing mode.");
        }

        return routingMode switch
        {
            RoutingMode.Random => await endPointService.GetEndPointAsync(_serviceName, RoutingMode.Random),
            RoutingMode.Hashed => await endPointService.GetEndPointAsync(_serviceName, RoutingMode.Hashed, hashId),
            RoutingMode.Weighted => await endPointService.GetEndPointAsync(_serviceName, RoutingMode.Weighted),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}