using Microsoft.EntityFrameworkCore;

namespace MiniGameRouter.Models.DB;

public class EndPointMappingContext(DbContextOptions<EndPointMappingContext> options) : DbContext(options)
{
    public DbSet<EndPointMappingModel> EndPoints { get; init; }
}