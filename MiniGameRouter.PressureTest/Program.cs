using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using MiniGameRouter.SDK.Interfaces;
using MiniGameRouter.SDK.Managers;
using MiniGameRouter.SDK.Providers;
using Serilog;
using System.Runtime;
using Microsoft.Extensions.DependencyInjection;
using MiniGameRouter.SDK;
using MiniGameRouter.PressureTest.Providers;
using MiniGameRouter.PressureTest.Services.StaticMappings;
using MiniGameRouter.PressureTest.Services.DynamicMappings;

namespace MiniGameRouter.PressureTest;

internal class Program
{
    static void Main(string[] args)
    {
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

        builder.Services.AddSingleton<ServiceHealthManager>();
        builder.Services.AddSingleton<ExtraEndPointManager>();
        builder.Services.AddSingleton<DynamicMappingManager>();

        builder.Services.AddSingleton<ISessionHashIdentityProvider, SessionHashIdentityProvider>();
        builder.Services.AddSingleton<IServerConfigurationProvider, RandomServerConfigurationProvider>();

        builder.Services.AddHostedService(sC => sC.GetRequiredService<ServiceHealthManager>());
        builder.Services.AddHostedService(sC => sC.GetRequiredService<ExtraEndPointManager>());
        builder.Services.AddHostedService(sC => sC.GetRequiredService<DynamicMappingManager>());
        builder.Services.AddHostedService<ServiceRegistrationManager>();

        builder.Services.AddSingleton<RandomServiceProvider>();
        builder.Services.AddHostedService<RandomCreateAndDeleteService>();
        builder.Services.AddHostedService<RandomUpdateService>();
        builder.Services.AddHostedService<RandomGetService>();
        builder.Services.AddHostedService<DynamicMappingRandomCreateAndGetService>();

        builder.Services
            .AddApiClients(builder.Configuration)
            .AddHiveEssentials()
            .UseTcpStack();

        Log.Information("Starting MiniGameRouter Pressure Tests...");
        Log.Information("[GC] IsServer={0} LatencyMode={1} LargeObjectHeapCompactionMode={2}",
            GCSettings.IsServerGC,
            GCSettings.LatencyMode,
            GCSettings.LargeObjectHeapCompactionMode);

        var host = builder.Build();
        host.Run();
    }
}