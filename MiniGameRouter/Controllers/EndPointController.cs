using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using MiniGameRouter.Helper;
using MiniGameRouter.Interfaces;
using MiniGameRouter.Models;
using MiniGameRouter.Models.DB;
using MiniGameRouter.SDK.Models;
using MiniGameRouter.Services.RoutingServices;
using MiniGameRouter.Shared.Models;
using OnceMi.AspNetCore.IdGenerator;
using Prometheus;
using HealthCheckService = MiniGameRouter.Services.HealthCheckService;

namespace MiniGameRouter.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class EndPointController : Controller
{
    public static readonly Gauge EndPointCounter = Metrics.CreateGauge(
        "minigame_router_endpoints_total",
        "Total number of endpoints");

    private static readonly Histogram EndPointGetDuration = Metrics.CreateHistogram(
        "minigame_router_endpoints_get_duration",
        "Duration of getting endpoints",
        new HistogramConfiguration
        {
            LabelNames = ["mode"]
        });

    private static readonly Histogram EndPointCreateDuration = Metrics.CreateHistogram(
        "minigame_router_endpoints_create_duration",
        "Duration of creating endpoints");

    private static readonly Histogram EndPointEditDuration = Metrics.CreateHistogram(
        "minigame_router_endpoints_edit_duration",
        "Duration of editing endpoints");

    private static readonly Histogram EndPointDeleteDuration = Metrics.CreateHistogram(
        "minigame_router_endpoints_delete_duration",
        "Duration of deleting endpoints");

    private readonly IDistributedCache _cache;
    private readonly IIdGeneratorService _idGeneratorService;
    private readonly IHostApplicationLifetime _applicationLifetime;

    private readonly EndPointMappingContext _endPointMappingContext;
    private readonly bool _enforceHealthy;
    private readonly NodeHashRouteService _hashRouteService;
    private readonly HealthCheckService _healthCheckService;
    private readonly ILogger _logger;
    private readonly NodeWeightedRouteService _weightedRouteService;

    public EndPointController(
        IConfiguration configuration,
        EndPointMappingContext endPointMappingContext,
        HealthCheckService healthCheckService,
        NodeWeightedRouteService weightedRouteService,
        NodeHashRouteService hashRouteService,
        IDistributedCache cache,
        IIdGeneratorService idGeneratorService,
        IHostApplicationLifetime applicationLifetime,
        ILogger<EndPointController> logger)
    {
        _enforceHealthy = configuration.GetValue<bool>("Routing:EnforceHealthy");

        _endPointMappingContext = endPointMappingContext;
        _healthCheckService = healthCheckService;
        _weightedRouteService = weightedRouteService;
        _hashRouteService = hashRouteService;
        _cache = cache;
        _idGeneratorService = idGeneratorService;
        _applicationLifetime = applicationLifetime;
        _logger = logger;
    }

    private async Task<EndPointRecord?> PrefetchCacheAsync(
        string serviceName,
        IRoutingService service,
        Func<IRoutingService, EndPointRecord?> getter)
    {
        var services = await _endPointMappingContext.EndPoints
            .AsNoTrackingWithIdentityResolution()
            .Where(e => e.ServiceName.ToLower() == serviceName.ToLower())
            .Select(e => new EndPointRecord(
                e.RecordId,
                e.ServiceName,
                e.TargetEndPoint,
                e.Weight ?? 0,
                e.TimeoutInMilliseconds,
                e.IsValid))
            .OrderBy(e => e.EndPoint)
            .ToListAsync();
        service.AddNodes(services);

        return getter(service);
    }

    private async Task<IActionResult> CheckHealthStatusAndReturn(EndPointRecord endPointRecord)
    {
        if (!_enforceHealthy)
            return Ok(endPointRecord);

        var healthCheckKey = HealthCheckService.GetServiceName(endPointRecord);
        var healthStatus = await _cache.GetAsync<ServiceStatus?>(healthCheckKey);

        if (healthStatus == null) return BadRequest("no healthy upstream");
        if (healthStatus == ServiceStatus.Red) return BadRequest("no healthy upstream");

        return Ok(endPointRecord);
    }

    [HttpGet("list/{count:int}")]
    public async Task<IActionResult> ListAsync([FromRoute] int count)
    {
        var services = await _endPointMappingContext.EndPoints
            .AsNoTrackingWithIdentityResolution()
            .Select(e => new EndPointRecord(
                e.RecordId,
                e.ServiceName,
                e.TargetEndPoint,
                e.Weight ?? 0,
                e.TimeoutInMilliseconds,
                e.IsValid))
            .OrderBy(e => e.EndPoint)
            .Take(count)
            .ToListAsync();

        return Ok(services);
    }

    [HttpGet("list/all")]
    public async Task<IActionResult> ListAllAsync()
    {
        var services = await _endPointMappingContext.EndPoints
            .AsNoTrackingWithIdentityResolution()
            .Select(e => new EndPointRecord(
                e.RecordId,
                e.ServiceName,
                e.TargetEndPoint,
                e.Weight ?? 0,
                e.TimeoutInMilliseconds,
                e.IsValid))
            .OrderBy(e => e.EndPoint)
            .ToListAsync();

        return Ok(services);
    }

    [HttpGet("get/{id:long}")]
    public async Task<IActionResult> GetAsync([FromRoute] long id)
    {
        using (EndPointGetDuration.WithLabels("id").NewTimer())
        {
            var idStr = id.ToString();
            var cached = await _cache.GetAsync<EndPointRecord>(idStr);

            if (cached is not null)
                return await CheckHealthStatusAndReturn(cached);

            var found = await _endPointMappingContext.EndPoints
                .AsNoTrackingWithIdentityResolution()
                .Where(e => e.RecordId == id)
                .FirstOrDefaultAsync();

            if (found == null) return NotFound();

            var record = new EndPointRecord(
                found.RecordId,
                found.ServiceName,
                found.TargetEndPoint,
                found.Weight ?? 0,
                found.TimeoutInMilliseconds,
                found.IsValid);

            await _cache.SetAsync(idStr, record, new DistributedCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(30)
            });

            _logger.LogInformation(
                "Client [{Address}] got mapping using ID [{RecordId}]",
                Request.HttpContext.Connection.RemoteIpAddress,
                idStr);

            return await CheckHealthStatusAndReturn(record);
        }
    }

    [HttpGet("get/{serviceName}")]
    public async Task<IActionResult> GetByServiceAsync(
        [FromRoute] string serviceName,
        [FromQuery] string? mode)
    {
        mode ??= "random";

        if (!mode.ResolveRoute(out var modePair))
            return BadRequest("Invalid mode");

        var modePairVal = modePair.Value;

        if (modePairVal.ModeStr == "Random")
            using (EndPointGetDuration.WithLabels(modePairVal.ModeStr).NewTimer())
            {
                var rand = _weightedRouteService.GetRandom(serviceName);

                if (rand == null)
                {
                    var fetched = await PrefetchCacheAsync(
                        serviceName,
                        _weightedRouteService,
                        s => ((NodeWeightedRouteService)s).GetRandom(serviceName));

                    if (fetched == null) return NotFound();

                    return await CheckHealthStatusAndReturn(fetched);
                }

                _logger.LogInformation(
                    "Client [{Address}] got random mapping to [{EndPoint}] using service [{Service}]",
                    Request.HttpContext.Connection.RemoteIpAddress,
                    rand.EndPoint,
                    serviceName);

                return await CheckHealthStatusAndReturn(rand);
            }

        if (modePairVal.ModeStr == "Weighted")
            using (EndPointGetDuration.WithLabels(modePairVal.ModeStr).NewTimer())
            {
                var weighted = _weightedRouteService.Get(serviceName);

                if (weighted == null)
                {
                    var fetched = await PrefetchCacheAsync(
                        serviceName,
                        _weightedRouteService,
                        s => s.Get(serviceName, null!));

                    if (fetched == null) return NotFound();

                    return await CheckHealthStatusAndReturn(fetched);
                }

                _logger.LogInformation(
                    "Client [{Address}] got weighted mapping to [{EndPoint}] using service [{Service}]",
                    Request.HttpContext.Connection.RemoteIpAddress,
                    weighted.EndPoint,
                    serviceName);

                return await CheckHealthStatusAndReturn(weighted);
            }

        using (EndPointGetDuration.WithLabels(modePairVal.ModeStr).NewTimer())
        {
            if (string.IsNullOrEmpty(modePairVal.HashKey))
                return BadRequest("Invalid hash key");

            var hashed = _hashRouteService.Get(serviceName, modePairVal.HashKey);

            if (hashed == null)
            {
                var fetched = await PrefetchCacheAsync(
                    serviceName,
                    _hashRouteService,
                    s => s.Get(serviceName, modePairVal.HashKey));

                if (fetched == null) return NotFound();

                hashed = fetched;
            }

            _logger.LogInformation(
                "Client [{Address}] got hashed mapping to [{EndPoint}] using service [{Service}]",
                Request.HttpContext.Connection.RemoteIpAddress,
                hashed.EndPoint,
                serviceName);

            return await CheckHealthStatusAndReturn(hashed);
        }
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateAsync(
        [FromBody] EndPointMappingRequestModel model)
    {
        using (EndPointCreateDuration.NewTimer())
        {
            if (string.IsNullOrEmpty(model.ServiceName) ||
                string.IsNullOrEmpty(model.TargetEndPoint))
                return BadRequest("Invalid model");

            var anyExists = await _endPointMappingContext.EndPoints
                .AsNoTrackingWithIdentityResolution()
                .AnyAsync(e => e.ServiceName.ToLower() == model.ServiceName.ToLower() &&
                               e.TargetEndPoint == model.TargetEndPoint);

            if (anyExists) return BadRequest("Mapping already exists");

            var record = new EndPointMappingModel
            {
                RecordId = _idGeneratorService.CreateId(),
                ServiceName = model.ServiceName,
                Weight = model.Weight,
                TargetEndPoint = model.TargetEndPoint,
                TimeoutInMilliseconds = model.TimeoutInMilliseconds,
                CreateTimeUtc = DateTime.UtcNow,
                IsValid = true
            };

            var healthCheckServiceName = HealthCheckService.GetServiceName(record);

            await _cache.SetStringAsync(
                $"REG_{healthCheckServiceName}",
                "1",
                new DistributedCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromDays(1)
                });

            await _endPointMappingContext.EndPoints.AddAsync(record);
            await _endPointMappingContext.SaveChangesAsync();

            _logger.LogInformation(
                "Client [{Address}] created mapping for service [{Service}]",
                Request.HttpContext.Connection.RemoteIpAddress,
                model.ServiceName);

            EndPointCounter.Inc();

            var result = new EndPointRecord(
                record.RecordId,
                record.ServiceName,
                record.TargetEndPoint,
                record.Weight ?? 1,
                record.TimeoutInMilliseconds,
                record.IsValid);

            return Ok(result);
        }
    }

    [HttpPut("edit/{id:long}")]
    public async Task<IActionResult> EditAsync(
        [FromBody] EndPointMappingRequestModel model,
        [FromRoute] long id)
    {
        using (EndPointEditDuration.NewTimer())
        {
            var found = await _endPointMappingContext.EndPoints
                .Where(e => e.RecordId == id)
                .FirstOrDefaultAsync();

            if (found == null) return NotFound();

            if (!string.IsNullOrEmpty(model.ServiceName))
                found.ServiceName = model.ServiceName;
            if (!string.IsNullOrEmpty(model.TargetEndPoint))
                found.TargetEndPoint = model.TargetEndPoint;
            found.Weight = model.Weight;
            found.TimeoutInMilliseconds = model.TimeoutInMilliseconds;

            var record = new EndPointRecord(
                found.RecordId,
                found.ServiceName,
                found.TargetEndPoint,
                found.Weight ?? 0,
                found.TimeoutInMilliseconds,
                found.IsValid);

            _hashRouteService.RemoveNode(record);
            _weightedRouteService.RemoveNode(record);
            await _healthCheckService.RemoveEntryAsync(
                HealthCheckService.GetServiceName(record),
                _applicationLifetime.ApplicationStopping);

            var cacheKey = HealthCheckService.GetServiceName(found);
            await _cache.RemoveAsync(cacheKey);

            _endPointMappingContext.EndPoints.Update(found);
            await _endPointMappingContext.SaveChangesAsync();

            _logger.LogInformation(
                "Client [{Addr}] edited mapping for service [{Service}]",
                Request.HttpContext.Connection.RemoteIpAddress,
                model.ServiceName);

            return Ok();
        }
    }

    [HttpDelete("delete/{id:long}")]
    public async Task<IActionResult> DeleteAsync(
        [FromRoute] long id)
    {
        using (EndPointDeleteDuration.NewTimer())
        {
            var found = await _endPointMappingContext.EndPoints
                .AsNoTrackingWithIdentityResolution()
                .Where(e => e.RecordId == id)
                .FirstOrDefaultAsync();

            if (found == null) return NotFound();

            var record = new EndPointRecord(
                found.RecordId,
                found.ServiceName,
                found.TargetEndPoint,
                found.Weight ?? 0,
                found.TimeoutInMilliseconds,
                found.IsValid);

            _hashRouteService.RemoveNode(record);
            _weightedRouteService.RemoveNode(record);

            var healthCheckServiceName = HealthCheckService.GetServiceName(record);
            var idStr = id.ToString();

            await _cache.RemoveAsync(idStr);
            await _cache.RemoveAsync(healthCheckServiceName);
            await _cache.RemoveAsync($"REG_{healthCheckServiceName}");

            await _healthCheckService.RemoveEntryAsync(
                healthCheckServiceName,
                _applicationLifetime.ApplicationStopping);

            _endPointMappingContext.EndPoints.Remove(found);
            await _endPointMappingContext.SaveChangesAsync();

            _logger.LogInformation(
                "Client [{Addr}] deleted mapping for service [{Service}]",
                Request.HttpContext.Connection.RemoteIpAddress,
                found.ServiceName);

            EndPointCounter.Dec();

            return Ok();
        }
    }
}