using Microsoft.EntityFrameworkCore;

namespace MiniGameRouter.Models.DB;

public class DynamicRoutingMappingContext(DbContextOptions<DynamicRoutingMappingContext> options) : DbContext(options)
{
    public DbSet<DynamicRoutingMappingModel> DynamicRoutingMappings { get; init; }
}