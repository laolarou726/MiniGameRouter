using Microsoft.EntityFrameworkCore;
using MiniGameRouter.Models;
using MiniGameRouter.Models.DB;
using MiniGameRouter.Shared.Models;

namespace MiniGameRouter.Services;

public class DynamicRoutingService
{
    private readonly DynamicRoutingPrefixMatchService _prefixMatchService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger _logger;

    public DynamicRoutingService(
        DynamicRoutingPrefixMatchService prefixMatchService,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<DynamicRoutingService> logger)
    {
        _prefixMatchService = prefixMatchService;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public string? TryGetMatch(string rawStr)
    {
        var match = _prefixMatchService.TryGetMatch(rawStr);

        return match?.TargetEndPoint;
    }

    public async Task<Guid?> TryAddMappingToDbAsync(DynamicRoutingMappingRequestModel model)
    {
        if (string.IsNullOrEmpty(model.MatchPrefix) ||
            string.IsNullOrEmpty(model.TargetEndPoint)) return null;

        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DynamicRoutingMappingContext>();

        var anyExist = await context.DynamicRoutingMappings
            .AnyAsync(m => m.MatchPrefix == model.MatchPrefix);

        if (anyExist)
        {
            _logger.LogWarning("A mapping with same prefix [{prefix}] has already been added.", model.MatchPrefix);

            return null;
        }

        _prefixMatchService.AddMatch(model.MatchPrefix, model.TargetEndPoint);

        var mapping = new DynamicRoutingMappingModel
        {
            MatchPrefix = model.MatchPrefix,
            TargetEndPoint = model.TargetEndPoint
        };

        await context.DynamicRoutingMappings.AddAsync(mapping);
        await context.SaveChangesAsync();

        return mapping.Id;
    }

    public async Task<bool> TryRemoveMappingAsync(Guid id)
    {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DynamicRoutingMappingContext>();

        var record = await context.DynamicRoutingMappings.FindAsync(id);

        if (record == null) return false;

        _prefixMatchService.TryRemoveMatch(record.MatchPrefix);

        context.DynamicRoutingMappings.Remove(record);
        await context.SaveChangesAsync();

        return true;
    }
}