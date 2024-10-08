using System.Runtime;
using Microsoft.EntityFrameworkCore;
using MiniGameRouter.Models.DB;
using MiniGameRouter.Services;
using MiniGameRouter.Services.RoutingServices;
using OnceMi.AspNetCore.IdGenerator;
using Prometheus;
using Prometheus.DotNetRuntime;
using Prometheus.SystemMetrics;
using Serilog;
using Yitter.IdGenerator;

const string redisInstanceName = "MiniGameRouter:";
const string mongoDatabaseName = "MiniGameRouter";

var builder = WebApplication.CreateBuilder(args);

// Setup logger
// For entry logging

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.WithProperty("SourceContext", "Main")
    .MinimumLevel.Debug()
    .CreateLogger();

builder.Host.UseSerilog((hostingContext, loggerConfiguration) =>
    loggerConfiguration.ReadFrom.Configuration(hostingContext.Configuration));

// Setup RecordId Generator

builder.Services.AddIdGenerator(options =>
{
    options.AppId = builder.Configuration.GetValue<ushort>("NodeId");
    options.GeneratorOptions = new IdGeneratorOptions
    {
        SeqBitLength = 10
    };
});

// Setup Prometheus

DotNetRuntimeStatsBuilder.Default().StartCollecting();
builder.Services.AddSystemMetrics();
builder.Services.UseHttpClientMetrics();

// Add DB contexts

var connectionStr = builder.Configuration.GetConnectionString("DefaultMongoConnection") ??
                    "mongodb://localhost:27017";

builder.Services.AddDbContext<EndPointMappingContext>(options => options.UseMongoDB(connectionStr, mongoDatabaseName));
builder.Services.AddDbContext<DynamicRoutingMappingContext>(options => options.UseMongoDB(connectionStr, mongoDatabaseName));

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("RedisCache");
    options.InstanceName = redisInstanceName;
});

// Add services to the container.

builder.Configuration.AddEnvironmentVariables();
builder.Services.AddHealthChecks();
builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<NodeHashRouteService>();
builder.Services.AddSingleton<NodeWeightedRouteService>();
builder.Services.AddSingleton<HealthCheckService>();
builder.Services.AddSingleton<DynamicRoutingService>();
builder.Services.AddSingleton<DynamicRoutingPrefixMatchService>();

builder.Services.AddHostedService(sP => sP.GetRequiredService<DynamicRoutingPrefixMatchService>());
builder.Services.AddHostedService(sP => sP.GetRequiredService<HealthCheckService>());
builder.Services.AddHostedService<LegacyEndPointMappingCleanupService>();


Log.Information("Starting MiniGameRouter...");
Log.Information("[GC] IsServer={0} LatencyMode={1} LargeObjectHeapCompactionMode={2}",
    GCSettings.IsServerGC,
    GCSettings.LatencyMode,
    GCSettings.LargeObjectHeapCompactionMode);

var app = builder.Build();

#if DEBUG
app.UseSerilogRequestLogging();
#endif

app.UseCors(corsBuilder => corsBuilder
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

app.MapHealthChecks("/ready");
app.MapHealthChecks("/app-health/minigamerouter/readyz");

// Configure the Prometheus scraping endpoint
app.UseRouting();
app.UseAuthorization();
#pragma warning disable ASP0014
app.UseEndpoints(endPoints => endPoints.MapMetrics());
#pragma warning restore ASP0014
app.UseMetricServer();

app.UseHttpMetrics();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();