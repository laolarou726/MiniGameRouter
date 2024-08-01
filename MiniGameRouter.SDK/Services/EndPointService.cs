using System.Collections.Specialized;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hive.Network.Shared;
using Microsoft.Extensions.Logging;
using MiniGameRouter.SDK.Interfaces;
using MiniGameRouter.SDK.Managers;
using MiniGameRouter.SDK.Models;
using MiniGameRouter.Shared.Models;

namespace MiniGameRouter.SDK.Services;

public class EndPointService : IEndPointService
{
    private readonly ServiceHealthManager _healthManager;
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    public EndPointService(
        ServiceHealthManager healthManager,
        HttpClient httpClient,
        ILogger<EndPointService> logger)
    {
        _healthManager = healthManager;
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<EndPointRecord?> GetEndPointAsync(Guid serviceId)
    {
        return GetEndPointAsync(serviceId.ToString("N"));
    }

    public async Task<EndPointRecord?> GetEndPointAsync(
        string serviceName,
        RoutingModel? routingMode = null,
        string? hashKey = null)
    {
        const string url = "/EndPoint/get/";

        if (routingMode == RoutingModel.Hashed && string.IsNullOrEmpty(hashKey))
            throw new Exception("Hash key is required for Hashed routing mode.");

        var uriBuilder = new UriBuilder($"{url}{Uri.EscapeDataString(serviceName)}");
        var query = new NameValueCollection();

        if (routingMode != null)
            query["mode"] = routingMode switch
            {
                RoutingModel.Random => "random",
                RoutingModel.Weighted => "weighted",
                RoutingModel.Hashed => $"hash;{Uri.EscapeDataString(hashKey!)}",
                _ => throw new ArgumentOutOfRangeException(nameof(routingMode), routingMode, null)
            };

        uriBuilder.Query = query.ToString();

        var reqUri = uriBuilder.Uri;
        using var req = new HttpRequestMessage(HttpMethod.Get, reqUri);
        using var res = await _httpClient.SendAsync(req);

        if (res is { IsSuccessStatusCode: false, StatusCode: HttpStatusCode.NotFound })
        {
            _logger.LogWarning("Service {ServiceName} not found.", serviceName);
            return null;
        }

        res.EnsureSuccessStatusCode();

        var resModel = await res.Content.ReadFromJsonAsync<EndPointRecord>();

        return resModel;
    }

    public async Task<Guid?> CreateEndPointAsync(
        string serviceName,
        string endPoint,
        uint weight = 1,
        int timeoutInMilliseconds = 30000)
    {
        const string url = "/EndPoint/create";

        var reqModel = new EndPointMappingRequestModel
        {
            ServiceName = serviceName,
            TargetEndPoint = endPoint,
            TimeoutInMilliseconds = timeoutInMilliseconds,
            Weight = weight
        };

        await using var stream = RecycleMemoryStreamManagerHolder.Shared.GetStream();
        await JsonSerializer.SerializeAsync(stream, reqModel);

        stream.Seek(0, SeekOrigin.Begin);

        using var req = new HttpRequestMessage(HttpMethod.Put, url);
        req.Content = new StreamContent(stream);

        using var res = await _httpClient.SendAsync(req);

        if (res is { IsSuccessStatusCode: false, StatusCode: HttpStatusCode.BadRequest })
        {
            var error = await res.Content.ReadAsStringAsync();

            _logger.LogError(
                "Failed to create endpoint [{endPoint}]. Error: {error}",
                reqModel,
                error);

            return null;
        }

        res.EnsureSuccessStatusCode();

        var createdId = await res.Content.ReadFromJsonAsync<Guid>();

        return createdId;
    }

    public async Task<bool> EditEndPointAsync(
        Guid id,
        EndPointMappingRequestModel reqModel)
    {
        var uri = $"/EndPoint/edit/{id:N}";

        await using var stream = RecycleMemoryStreamManagerHolder.Shared.GetStream();
        await JsonSerializer.SerializeAsync(stream, reqModel);

        stream.Seek(0, SeekOrigin.Begin);

        using var req = new HttpRequestMessage(HttpMethod.Put, uri);
        req.Content = new StreamContent(stream);

        using var res = await _httpClient.SendAsync(req);

        if (res is { IsSuccessStatusCode: false, StatusCode: HttpStatusCode.NotFound })
        {
            _logger.LogWarning("EndPoint {Id} not found.", id);
            return false;
        }

        res.EnsureSuccessStatusCode();

        return true;
    }

    public async Task<bool> DeleteEndPointAsync(Guid id)
    {
        var uri = $"/EndPoint/delete/{id:N}";

        using var req = new HttpRequestMessage(HttpMethod.Delete, uri);
        using var res = await _httpClient.SendAsync(req);

        if (res is { IsSuccessStatusCode: false, StatusCode: HttpStatusCode.NotFound })
        {
            _logger.LogWarning("EndPoint {Id} not found.", id);
            return false;
        }

        res.EnsureSuccessStatusCode();

        return true;
    }
}