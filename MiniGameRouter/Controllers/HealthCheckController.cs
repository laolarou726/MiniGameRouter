using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using MiniGameRouter.Models;
using MiniGameRouter.Models.DB;
using MiniGameRouter.SDK.Models;
using MiniGameRouter.Services;
using MiniGameRouter.Shared.Models;

namespace MiniGameRouter.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class HealthCheckController : Controller
{
    private readonly EndPointMappingContext _endPointMappingContext;
    private readonly HealthCheckService _healthCheckService;
    private readonly IDistributedCache _cache;
    
    public HealthCheckController(
        EndPointMappingContext endPointMappingContext,
        HealthCheckService healthCheckService,
        IDistributedCache cache)
    {
        _endPointMappingContext = endPointMappingContext;
        _healthCheckService = healthCheckService;
        _cache = cache;
    }
    
    [HttpPut("report")]
    public async Task<IActionResult> ReportAsync(
        [FromBody] HealthCheckRequestModel model)
    {
        if (model.Status is not ServiceStatus.Green and not ServiceStatus.Yellow and not ServiceStatus.Red)
            return BadRequest("Invalid status");

        if (!_healthCheckService.TryGetStatus(model, out _))
        {
            var hasService = await _endPointMappingContext.EndPoints
                .AnyAsync(e => e.ServiceName.ToLower() == model.ServiceName.ToLower() &&
                               e.TargetEndPoint == model.EndPoint);
            
            if (!hasService)
                return BadRequest("Service not found");
        }
        
        var statusHistory = new HealthCheckStatus
        {
            CheckTime = DateTime.UtcNow,
            Status = model.Status
        };
        
        await _healthCheckService.AddCheckAsync(model, statusHistory, _cache);

        return Ok();
    }
}