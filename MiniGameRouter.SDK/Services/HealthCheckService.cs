using System.Net;
using System.Text.Json;
using Hive.Network.Shared;
using Microsoft.Extensions.Logging;
using MiniGameRouter.SDK.Interfaces;
using MiniGameRouter.SDK.Models;
using MiniGameRouter.Shared.Models;

namespace MiniGameRouter.SDK.Services;

public class HealthCheckService : IHealthCheckService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    public HealthCheckService(
        HttpClient httpClient,
        ILogger<HealthCheckService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> ReportHealthAsync(
        string serviceName,
        string endPoint,
        ServiceStatus status)
    {
        const string url = "/HealthCheck/report";

        var reqModel = new HealthCheckRequestModel
        {
            EndPoint = endPoint,
            ServiceName = serviceName,
            Status = status
        };

        await using var stream = RecycleMemoryStreamManagerHolder.Shared.GetStream();
        await JsonSerializer.SerializeAsync(stream, reqModel);

        stream.Seek(0, SeekOrigin.Begin);

        using var req = new HttpRequestMessage(HttpMethod.Put, url);
        req.Content = new StreamContent(stream);

        using var res = await _httpClient.SendAsync(req);

        if (res is { IsSuccessStatusCode: false, StatusCode: HttpStatusCode.NotFound })
            _logger.LogWarning(
                "HealthCheck endpoint not found for [{service}] at [{endPoint}].",
                serviceName,
                endPoint);

        return res.IsSuccessStatusCode;
    }
}