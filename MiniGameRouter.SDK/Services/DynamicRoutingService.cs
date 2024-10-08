﻿using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MiniGameRouter.SDK.Interfaces;
using MiniGameRouter.Shared.Models;
using System.Net;
using System.Net.Http.Json;
using MiniGameRouter.SDK.Managers;

namespace MiniGameRouter.SDK.Services;

public class DynamicRoutingService : IDynamicRoutingService
{
    private readonly DynamicMappingManager _dynamicMappingManager;
    private readonly HttpClient _httpClient;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly ILogger _logger;

    public DynamicRoutingService(
        DynamicMappingManager dynamicMappingManager,
        HttpClient httpClient,
        IHostApplicationLifetime hostApplicationLifetime,
        ILogger<DynamicRoutingService> logger)
    {
        _dynamicMappingManager = dynamicMappingManager;
        _httpClient = httpClient;
        _hostApplicationLifetime = hostApplicationLifetime;
        _logger = logger;
    }

    public async Task<DynamicRoutingRecord?> GetMappingAsync(long id)
    {
        var url = $"/DynamicRouting/get/{id}";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        using var res = await _httpClient.SendAsync(req, _hostApplicationLifetime.ApplicationStopping);

        if (res is { IsSuccessStatusCode: false, StatusCode: HttpStatusCode.NotFound })
        {
            _logger.LogWarning("Mapping with id [{id}] not found.", id);
            return null;
        }

        res.EnsureSuccessStatusCode();

        return await res.Content.ReadFromJsonAsync<DynamicRoutingRecord>();
    }

    public async Task<string?> GetMappingAsync(string rawString)
    {
        var url = $"/DynamicRouting/match/{Uri.EscapeDataString(rawString)}";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        using var res = await _httpClient.SendAsync(req, _hostApplicationLifetime.ApplicationStopping);

        if (res is { IsSuccessStatusCode: false, StatusCode: HttpStatusCode.NotFound })
        {
            _logger.LogWarning("Mapping {ServiceName} not found.", rawString);
            return null;
        }

        res.EnsureSuccessStatusCode();

        return await res.Content.ReadAsStringAsync();
    }

    public async Task<long?> CreateMappingAsync(string matchPrefix, string targetEndPoint)
    {
        const string url = "/DynamicRouting/create";

        var reqModel = new DynamicRoutingMappingRequestModel
        {
            MatchPrefix = matchPrefix,
            TargetEndPoint = targetEndPoint
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Content = JsonContent.Create(reqModel);

        using var res = await _httpClient.SendAsync(req, _hostApplicationLifetime.ApplicationStopping);

        if (res is { IsSuccessStatusCode: false, StatusCode: HttpStatusCode.BadRequest })
        {
            _logger.LogError(
                "Failed to create mapping [{prefix} -> {endPoint}]. Maybe is because a same mapping is already exists.",
                reqModel.MatchPrefix,
                reqModel.TargetEndPoint);

            return null;
        }

        res.EnsureSuccessStatusCode();

        var createdRecord = await res.Content.ReadFromJsonAsync<long>();

        if (createdRecord == default)
        {
            _logger.LogError("Failed to create mapping [{prefix} -> {endPoint}].",
                reqModel.MatchPrefix,
                reqModel.TargetEndPoint);

            return null;
        }

        _dynamicMappingManager.AddEndPoint(createdRecord);

        _logger.LogInformation("Mapping [{prefix} -> {endPoint}] with id [{createdRecord}] created.",
            reqModel.MatchPrefix,
            reqModel.TargetEndPoint,
            createdRecord);

        return createdRecord;
    }

    public async Task<bool> DeleteMappingAsync(long id)
    {
        var url = $"/DynamicRouting/delete/{id}";

        using var req = new HttpRequestMessage(HttpMethod.Delete, url);
        using var res = await _httpClient.SendAsync(req);

        if (res is { IsSuccessStatusCode: false, StatusCode: HttpStatusCode.NotFound })
        {
            _logger.LogWarning("Maping with id [{id}] not found.", id);
            return false;
        }

        res.EnsureSuccessStatusCode();

        _dynamicMappingManager.RemoveEndPoint(id);

        return true;
    }
}