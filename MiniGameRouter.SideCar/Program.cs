using System.Runtime;
using MiniGameRouter.SDK;
using MiniGameRouter.SideCar;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// Setup logger
// For entry logging

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.WithProperty("SourceContext", "Main")
    .MinimumLevel.Debug()
    .CreateLogger();

builder.Services.AddSerilog(loggerConfiguration =>
    loggerConfiguration.ReadFrom.Configuration(builder.Configuration));

builder.Configuration.AddEnvironmentVariables();

builder.Services.RegisterMiniGameRouter(builder.Configuration);
builder.Services.AddHostedService<Worker>();

Log.Information("Starting MiniGameRouter Sidecar Service...");
Log.Information("[GC] IsServer={0} LatencyMode={1} LargeObjectHeapCompactionMode={2}",
    GCSettings.IsServerGC,
    GCSettings.LatencyMode,
    GCSettings.LargeObjectHeapCompactionMode);

var host = builder.Build();
host.Run();