using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using MiniGameRouter.Models;
using MiniGameRouter.Models.DB;
using MiniGameRouter.SDK.Models;
using MiniGameRouter.Services;
using MiniGameRouter.Shared.Models;
using Prometheus;

namespace MiniGameRouter.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class HealthCheckController : Controller
{
    private readonly IDistributedCache _cache;
    private readonly EndPointMappingContext _endPointMappingContext;
    private readonly HealthCheckService _healthCheckService;
    private readonly ILogger _logger;

    private static readonly Histogram HealthCheckDuration = Metrics.CreateHistogram(
        "minigame_router_health_check_duration",
        "Duration of health check");

    public HealthCheckController(
        EndPointMappingContext endPointMappingContext,
        HealthCheckService healthCheckService,
        IDistributedCache cache,
        ILogger<HealthCheckController> logger)
    {
        _endPointMappingContext = endPointMappingContext;
        _healthCheckService = healthCheckService;
        _cache = cache;
        _logger = logger;
    }

    [HttpGet("status")]
    public IActionResult Status()
    {
        return Ok(_healthCheckService.GetEntries());
    }

    [HttpPut("report")]
    public async Task<IActionResult> ReportAsync(
        [FromBody] HealthCheckRequestModel model)
    {
        if (model.Status is not ServiceStatus.Green and not ServiceStatus.Yellow and not ServiceStatus.Red)
            return BadRequest("Invalid status");

        using (HealthCheckDuration.NewTimer())
        {
            if (!_healthCheckService.TryGetStatus(model, out _))
            {
                var healthCheckServiceName = HealthCheckService.GetServiceName(model);
                var hasServiceCache = await _cache.GetStringAsync($"REG_{healthCheckServiceName}");

                if (string.IsNullOrEmpty(hasServiceCache) || hasServiceCache != "1")
                {
                    var hasService = await _endPointMappingContext.EndPoints
                        .AsNoTrackingWithIdentityResolution()
                        .AnyAsync(e => e.ServiceName.ToLower() == model.ServiceName.ToLower() &&
                                       e.TargetEndPoint == model.EndPoint);

                    if (!hasService)
                        return NotFound("Service not found");
                }
            }

            var statusHistory = new HealthCheckStatus
            {
                CheckTime = DateTime.UtcNow,
                Status = model.Status
            };

            await _healthCheckService.AddCheckAsync(model, statusHistory, _cache);
        }

        _logger.LogInformation(
            "Service [{service}] reported its status as [{status}] at [{endPoint}].",
            model.ServiceName,
            model.Status,
            model.EndPoint);

        return Ok();
    }
}