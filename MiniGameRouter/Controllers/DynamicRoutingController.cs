using Microsoft.AspNetCore.Mvc;
using MiniGameRouter.Models.DB;
using MiniGameRouter.Shared.Models;

namespace MiniGameRouter.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class DynamicRoutingController : Controller
{
    private readonly DynamicRoutingMappingContext _dynamicRoutingMappingContext;
    private readonly ILogger _logger;

    public DynamicRoutingController(
        DynamicRoutingMappingContext dynamicRoutingMappingContext,
        ILogger<DynamicRoutingController> logger)
    {
        _dynamicRoutingMappingContext = dynamicRoutingMappingContext;
        _logger = logger;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetRoutingAsync(
        [FromRoute] Guid id)
    {
        return Ok();
    }

    [HttpGet("{rawStr}")]
    public async Task<IActionResult> GetRoutingAsync(
        [FromRoute] string rawStr)
    {
        return Ok();
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateRoutingAsync(
        [FromBody] DynamicRoutingMappingRequestModel model)
    {
        return Ok();
    }

    [HttpDelete("delete/{id:guid}")]
    public async Task<IActionResult> RemoveRoutingAsync(
        [FromRoute] Guid id)
    {
        return Ok();
    }
}