using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniGameRouter.Models.DB;
using MiniGameRouter.Services;
using MiniGameRouter.Shared.Models;
using Prometheus;

namespace MiniGameRouter.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class DynamicRoutingController : Controller
{
    private static readonly Gauge RegisteredMatchCounter = Metrics.CreateGauge(
        "minigame_router_dynamic_routing_match_total",
        "Total number of dynamic routing matches");

    private static readonly Counter MatchMissedCounter = Metrics.CreateCounter(
        "minigame_router_dynamic_routing_match_missed_total",
        "Total number of dynamic routing matching miss");

    private static readonly Histogram DynamicRoutingGetDuration = Metrics.CreateHistogram(
        "minigame_router_dynamic_routing_get_duration",
        "Duration of getting dynamic routing",
        new HistogramConfiguration
        {
            LabelNames = ["method"]
        });

    private static readonly Histogram DynamicRoutingCreateDuration = Metrics.CreateHistogram(
        "minigame_router_dynamic_routing_create_duration",
        "Duration of creating dynamic routing");

    private static readonly Histogram DynamicRoutingDeleteDuration = Metrics.CreateHistogram(
        "minigame_router_dynamic_routing_delete_duration",
        "Duration of delete dynamic routing");

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

    [HttpGet("get/{id:long}")]
    public async Task<IActionResult> GetRoutingAsync(
        [FromRoute] long id)
    {
        using (DynamicRoutingGetDuration.WithLabels("by_id").NewTimer())
        {
            var record = await _dynamicRoutingMappingContext.DynamicRoutingMappings
                .AsNoTrackingWithIdentityResolution()
                .Where(r => r.RecordId == id)
                .FirstOrDefaultAsync();

            if (record == null)
            {
                _logger.LogWarning("Mapping with id [{id}] not found.", id);

                MatchMissedCounter.Inc();
                return NotFound();
            }

            var result = new DynamicRoutingRecord(
                record.RecordId,
                record.MatchPrefix,
                record.TargetEndPoint);

            return Ok(result);
        }
    }

    [HttpGet("match/{rawStr}")]
    public IActionResult GetRoutingAsync(
        [FromRoute] string rawStr)
    {
        using (DynamicRoutingGetDuration.WithLabels("by_raw_string").NewTimer())
        {
            var match = _dynamicRoutingService.TryGetMatch(rawStr);

            if (match == null)
            {
                _logger.LogWarning("Not mapping found using prefix [{rawStr}]", rawStr);

                MatchMissedCounter.Inc();
                return NotFound();
            }

            return Ok(match);
        }
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateRoutingAsync(
        [FromBody] DynamicRoutingMappingRequestModel model)
    {
        using (DynamicRoutingCreateDuration.NewTimer())
        {
            var id = await _dynamicRoutingService.TryAddMappingToDbAsync(model);

            if (!id.HasValue) return BadRequest();

            RegisteredMatchCounter.Inc();

            return Ok(id.Value);
        }
    }

    [HttpDelete("delete/{id:long}")]
    public async Task<IActionResult> RemoveRoutingAsync(
        [FromRoute] long id)
    {
        using (DynamicRoutingDeleteDuration.NewTimer())
        {
            var result = await _dynamicRoutingService.TryRemoveMappingAsync(id);

            if (!result) return NotFound();

            RegisteredMatchCounter.Dec();

            return Ok();
        }
    }
}