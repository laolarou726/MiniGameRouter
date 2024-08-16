using Microsoft.AspNetCore.Mvc;
using MiniGameRouter.Models.DB;
using MiniGameRouter.Services;
using MiniGameRouter.Shared.Models;

namespace MiniGameRouter.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class DynamicRoutingController : Controller
{
    private readonly DynamicRoutingMappingContext _dynamicRoutingMappingContext;
    private readonly DynamicRoutingService _dynamicRoutingService;
    private readonly ILogger _logger;

    public DynamicRoutingController(
        DynamicRoutingMappingContext dynamicRoutingMappingContext,
        DynamicRoutingService dynamicRoutingService,
        ILogger<DynamicRoutingController> logger)
    {
        _dynamicRoutingMappingContext = dynamicRoutingMappingContext;
        _dynamicRoutingService = dynamicRoutingService;
        _logger = logger;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetRoutingAsync(
        [FromRoute] Guid id)
    {
        var record = await _dynamicRoutingMappingContext.DynamicRoutingMappings.FindAsync(id);

        if (record == null) return NotFound();

        return Ok(record);
    }

    [HttpGet("{rawStr}")]
    public async Task<IActionResult> GetRoutingAsync(
        [FromRoute] string rawStr)
    {
        var match = await _dynamicRoutingService.TryGetMatchAsync(rawStr);

        if (match == null) return NotFound();

        return Ok(match);
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateRoutingAsync(
        [FromBody] DynamicRoutingMappingRequestModel model)
    {
        var result = await _dynamicRoutingService.TryAddMappingToDbAsync(model);

        if (!result) return BadRequest();

        return Ok();
    }

    [HttpDelete("delete/{id:guid}")]
    public async Task<IActionResult> RemoveRoutingAsync(
        [FromRoute] Guid id)
    {
        var result = await _dynamicRoutingService.TryRemoveMappingAsync(id);

        if (!result) return NotFound();

        return Ok();
    }
}