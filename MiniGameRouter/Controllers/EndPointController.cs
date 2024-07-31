using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MiniGameRouter.Helper;
using MiniGameRouter.Interfaces;
using MiniGameRouter.Models;
using MiniGameRouter.Models.DB;
using MiniGameRouter.Services.RoutingServices;
using HealthCheckService = MiniGameRouter.Services.HealthCheckService;

namespace MiniGameRouter.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class EndPointController : Controller
{
    private readonly EndPointMappingContext _endPointMappingContext;
    private readonly HealthCheckService _healthCheckService;
    private readonly NodeWeightedRouteService _weightedRouteService;
    private readonly NodeHashRouteService _hashRouteService;
    private readonly IDistributedCache _cache;
    private readonly ILogger _logger;
    
    public EndPointController(
        EndPointMappingContext endPointMappingContext,
        HealthCheckService healthCheckService,
        NodeWeightedRouteService weightedRouteService,
        NodeHashRouteService hashRouteService,
        IDistributedCache cache,
        ILogger<EndPointController> logger)
    {
        _endPointMappingContext = endPointMappingContext;
        _healthCheckService = healthCheckService;
        _weightedRouteService = weightedRouteService;
        _hashRouteService = hashRouteService;
        _cache = cache;
        _logger = logger;
    }

    private async Task<EndPointRecord?> PrefetchCacheAsync(
        string serviceName,
        IRoutingService service,
        Func<IRoutingService, EndPointRecord?> getter)
    {
        var services = await _endPointMappingContext.EndPoints
            .Where(e => e.ServiceName.ToLower() == serviceName.ToLower())
            .Select(e => new EndPointRecord(
                e.Id,
                e.ServiceName,
                e.TargetEndPoint,
                e.Weight ?? 0,
                e.TimeoutInMilliseconds,
                e.IsValid))
            .ToListAsync();
        service.AddNodes(services);

        return getter(service);
    }

    private async Task<IActionResult> CheckHealthStatusAndReturn(EndPointRecord endPointRecord)
    {
        var healthCheckKey = HealthCheckService.GetServiceName(endPointRecord);
        var healthStatus = await _cache.GetAsync<ServiceStatus?>(healthCheckKey);

        if (healthStatus == null) return BadRequest("no healthy upstream");
        if (healthStatus == ServiceStatus.Red) return BadRequest("no healthy upstream");
        
        return Ok(endPointRecord);
    }
    
    [HttpGet("get/{id:guid}")]
    public async Task<IActionResult> GetAsync(
        [FromRoute] Guid id,
        [FromQuery] string? mode)
    {
        var idStr = id.ToString("N");
        var cached = await _cache.GetAsync<EndPointRecord>(idStr);
        
        if (cached is not null)
            return await CheckHealthStatusAndReturn(cached);

        var found = await _endPointMappingContext.EndPoints.FindAsync([id]);

        if (found == null) return NotFound();

        var record = new EndPointRecord(
            found.Id,
            found.ServiceName,
            found.TargetEndPoint,
            found.Weight ?? 0,
            found.TimeoutInMilliseconds,
            found.IsValid);

        await _cache.SetAsync(idStr, record);
        
        _logger.LogInformation(
            "Client [{Addr}] got mapping using ID [{Id}]",
            Request.HttpContext.Connection.RemoteIpAddress,
            idStr);

        return await CheckHealthStatusAndReturn(record);
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
        {
            var rand = _weightedRouteService.GetRandom(serviceName);

            if (rand == null)
            {
                var fetched = await PrefetchCacheAsync(
                    serviceName,
                    _weightedRouteService,
                    s => ((NodeWeightedRouteService) s).GetRandom(serviceName));

                if (fetched == null) return NotFound();

                return await CheckHealthStatusAndReturn(fetched);
            }
            
            _logger.LogInformation(
                "Client [{Addr}] got random mapping to [{EndPoint}] using service [{Service}]",
                Request.HttpContext.Connection.RemoteIpAddress,
                rand.EndPoint,
                serviceName);

            return await CheckHealthStatusAndReturn(rand);
        }
        
        if (modePairVal.ModeStr == "Weighted")
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
                "Client [{Addr}] got weighted mapping to [{EndPoint}] using service [{Service}]",
                Request.HttpContext.Connection.RemoteIpAddress,
                weighted.EndPoint,
                serviceName);

            return await CheckHealthStatusAndReturn(weighted);
        }
        
        if (string.IsNullOrEmpty(modePairVal.HashKey))
            return BadRequest("Invalid hash key");

        var hashed = _hashRouteService.Get(serviceName, modePairVal.HashKey);

        if (hashed == null)
        {
            var fetched = await PrefetchCacheAsync(
                serviceName,
                _hashRouteService,
                s => s.Get(serviceName, null!));

            if (fetched == null) return NotFound();

            hashed = fetched;
        }
        
        _logger.LogInformation(
            "Client [{Addr}] got hashed mapping to [{EndPoint}] using service [{Service}]",
            Request.HttpContext.Connection.RemoteIpAddress,
            hashed.EndPoint,
            serviceName);

        return await CheckHealthStatusAndReturn(hashed);
    }
    
    [HttpPost("create")]
    public async Task<IActionResult> CreateAsync(
        [FromBody] EndPointMappingRequestModel model)
    {
        if (string.IsNullOrEmpty(model.ServiceName) ||
            string.IsNullOrEmpty(model.TargetEndPoint))
            return BadRequest("Invalid model");
        
        var anyExists = await _endPointMappingContext.EndPoints
            .AnyAsync(e => e.ServiceName.ToLower() == model.ServiceName.ToLower() &&
                           e.TargetEndPoint == model.TargetEndPoint);
        
        if (anyExists) return BadRequest("Mapping already exists");
        
        var record = new EndPointMappingModel
        {
            ServiceName = model.ServiceName,
            Weight = model.Weight,
            TargetEndPoint = model.TargetEndPoint,
            TimeoutInMilliseconds = model.TimeoutInMilliseconds,
            IsValid = true
        };
        
        await _endPointMappingContext.EndPoints.AddAsync(record);
        await _endPointMappingContext.SaveChangesAsync();
        
        return Ok(record.Id);
    }
    
    [HttpPut("edit/{id:guid}")]
    public async Task<IActionResult> EditAsync(
        [FromBody] EndPointMappingRequestModel model,
        [FromRoute] Guid id)
    {
        var found = await _endPointMappingContext.EndPoints.FindAsync([id]);

        if (found == null) return NotFound();

        found.ServiceName = model.ServiceName;
        found.Weight = model.Weight;
        found.TargetEndPoint = model.TargetEndPoint;
        found.TimeoutInMilliseconds = model.TimeoutInMilliseconds;
        
        var record = new EndPointRecord(
            found.Id,
            found.ServiceName,
            found.TargetEndPoint,
            found.Weight ?? 0,
            found.TimeoutInMilliseconds,
            found.IsValid);

        _hashRouteService.RemoveNode(record);
        _weightedRouteService.RemoveNode(record);
        _healthCheckService.RemoveEntry(HealthCheckService.GetServiceName(record));
        
        var cacheKey = HealthCheckService.GetServiceName(found);
        await _cache.RemoveAsync(cacheKey);

        _endPointMappingContext.EndPoints.Update(found);
        await _endPointMappingContext.SaveChangesAsync();
        
        return Ok();
    }
    
    [HttpDelete("delete/{id:guid}")]
    public async Task<IActionResult> DeleteAsync(
        [FromRoute] Guid id)
    {
        var found = await _endPointMappingContext.EndPoints.FindAsync([id]);

        if (found == null) return NotFound();

        var record = new EndPointRecord(
            found.Id,
            found.ServiceName,
            found.TargetEndPoint,
            found.Weight ?? 0,
            found.TimeoutInMilliseconds,
            found.IsValid);

        _hashRouteService.RemoveNode(record);
        _weightedRouteService.RemoveNode(record);
        _healthCheckService.RemoveEntry(HealthCheckService.GetServiceName(record));

        var cacheKey = HealthCheckService.GetServiceName(found);
        await _cache.RemoveAsync(cacheKey);

        _endPointMappingContext.EndPoints.Remove(found);
        await _endPointMappingContext.SaveChangesAsync();
        
        return Ok();
    }
}