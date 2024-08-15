using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MiniGameRouter.SDK.Interfaces;
using MiniGameRouter.SDK.Models;
using MiniGameRouter.Shared.Models;

namespace MiniGameRouter.SDK.Services;

public class HealthCheckService : IHealthCheckService
{
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    public HealthCheckService(
        HttpClient httpClient,
        IHostApplicationLifetime hostApplicationLifetime,
        ILogger<HealthCheckService> logger)
    {
        _httpClient = httpClient;
        _hostApplicationLifetime = hostApplicationLifetime;
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

        using var req = new HttpRequestMessage(HttpMethod.Put, url);
        req.Content = JsonContent.Create(reqModel);

        using var res = await _httpClient.SendAsync(req, _hostApplicationLifetime.ApplicationStopped);

        if (res is { IsSuccessStatusCode: false, StatusCode: HttpStatusCode.NotFound })
            _logger.LogWarning(
                "HealthCheck endpoint not found for [{service}] at [{endPoint}].",
                serviceName,
                endPoint);

        return res.IsSuccessStatusCode;
    }
}