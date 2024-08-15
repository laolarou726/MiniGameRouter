using System.Runtime;
using Microsoft.EntityFrameworkCore;
using MiniGameRouter.Models.DB;
using MiniGameRouter.Services;
using MiniGameRouter.Services.RoutingServices;
using Prometheus;
using Prometheus.DotNetRuntime;
using Prometheus.SystemMetrics;
using Serilog;

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

// Setup Prometheus

DotNetRuntimeStatsBuilder.Default().StartCollecting();
builder.Services.AddSystemMetrics();
builder.Services.UseHttpClientMetrics();

// Add DB contexts

var connectionStr = builder.Configuration.GetConnectionString("DefaultMongoConnection") ??
                    "mongodb://localhost:27017";

builder.Services.AddDbContext<EndPointMappingContext>(options => options.UseMongoDB(connectionStr, mongoDatabaseName));

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

builder.Services.AddHostedService(sP => sP.GetRequiredService<HealthCheckService>());

Log.Information("Starting MiniGameRouter...");
Log.Information("[GC] IsServer={0} LatencyMode={1} LargeObjectHeapCompactionMode={2}",
    GCSettings.IsServerGC,
    GCSettings.LatencyMode,
    GCSettings.LargeObjectHeapCompactionMode);

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseCors(corsBuilder => corsBuilder
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

app.MapHealthChecks("/ready");
app.MapHealthChecks("/app-health/minigamerouter/readyz");

// Configure the Prometheus scraping endpoint
app.UseRouting();
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

app.UseAuthorization();

app.MapControllers();

app.Run();